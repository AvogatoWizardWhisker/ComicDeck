using System;
using System.Collections.Generic;
using System.Text;
using ComicDeck.Core.Archives;
using ComicDeck.Core.Utilities;
using Microsoft.UI.Xaml;

namespace ComicDeck.Services
{
    /// <summary>
    /// Keeps the currnt page and its neighbours decoded, and loads the neighbours in the background.
    /// One cache per open comic. UI thread only: it has no locking. Dispose it before the archive
    /// </summary>
    public sealed class PageCache : IDisposable
    {
        private sealed record Entry(Task<LoadedPage> Load, CancellationTokenSource Cts, DecodeRequest Request);

        private readonly IComicArchive _archive;
        private readonly PageImageLoader _loader;
        private readonly int _radius;
        private readonly Dictionary<int, Entry> _entries = new();
        private bool _disposed;

        public PageCache(IComicArchive archive, PageImageLoader loader, int radius = 2)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(radius);
            _archive = archive;
            _loader = loader;
            _radius = radius;
        }

        /// <summary>
        /// Returns the page, and starts loading the pages around it. Cancelling the token stops waiting,
        /// but the load itself continues while the page is still inside the cached window.
        /// </summary>
        public Task<LoadedPage> GetAsync(int index, DecodeRequest request, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if ((uint)index >= (uint)_archive.Pages.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            // the current page goes first, so it is first in line for the archive
            var current = GetOrStart(index, request);

            Trim(index);
            for(var distance = 1; distance <= _radius; distance++)
            {
                Prefetch(index + distance, request); // forward first: it's likelier next move
                Prefetch(index - distance, request);
            }

            return current.Load.WaitAsync(cancellationToken);
        }

        private void Prefetch(int index, DecodeRequest request)
        {
            if ((uint)index >= (uint)_archive.Pages.Count) return;

            // a neighbour that already failed is not retried on every page turn.
            // It is retried if the reader actually lands on it.
            if (_entries.TryGetValue(index, out var existing) && existing.Request == request) return;

            GetOrStart(index, request);
        }   
              
        private Entry GetOrStart(int index, DecodeRequest request)
        {
            if (_entries.TryGetValue(index, out var existing))
            {
                var usable = existing.Request == request
                    && !existing.Load.IsFaulted
                    && !existing.Load.IsCanceled;
                if (usable) return existing;

                Discard(existing);
            }

            var cts = new CancellationTokenSource();
            var load = _loader.LoadAsync(_archive, index, request, cts.Token);

            // Nobody awaits a prefetch, so observe its failure here rather than leave it unobserved.
            _ = load.ContinueWith(
                static t => { _ = t.Exception; },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            var entry = new Entry(load, cts, request);
            _entries[index] = entry;
            return entry;
        }

        private void Trim(int center)
        {
            foreach (var key in _entries.Keys.Where(k => Math.Abs(k - center) > _radius).ToList())
            {
                Discard(_entries[key]);
                _entries.Remove(key);
            }
        }

        private static void Discard(Entry entry)
        {
            entry.Cts.Cancel();
            entry.Cts.Dispose();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var entry in _entries.Values)
                Discard(entry);
            _entries.Clear();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using ComicDeck.Core.Archives;
using ComicDeck.Core.Library;
using ComicDeck.Core.Models;
using ComicDeck.Core.Utilities;
using ComicDeck.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace ComicDeck.ViewModels
{
    public sealed partial class ReaderViewModel : ObservableObject, IDisposable
    {
        private static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(1.5);

        private readonly IComicArchiveFactory _archiveFactory;
        private readonly PageImageLoader _loader;
        private readonly IComicRepository _comics;
        private readonly TimeProvider _time;
        private readonly SemaphoreSlim _saveGate = new(1, 1);

        private IComicArchive? _archive;
        private PageCache? _cache;
        private DecodeRequest _request;
        private int _loadVersion;

        private ReadingProgress? _progress; // null when the comic isn't in the library
        private bool _dirty;
        private CancellationTokenSource? _saveDelay;

        public ReaderViewModel(IComicArchiveFactory archiveFactory, PageImageLoader loader, IComicRepository comics, TimeProvider time)
        {
            _archiveFactory = archiveFactory;
            _loader = loader;
            _comics = comics;
            _time = time;
            Title = string.Empty;
        }

        [ObservableProperty]
        public partial string Title { get; set; }

        [ObservableProperty]
        public partial ImageSource? CurrentImage {get; set;}

        [ObservableProperty]
        public partial bool IsLoading { get; set;}

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PageText))]
        public partial int CurrentIndex { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PageText))]
        public partial int PageCount { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasError))]
        public partial string? ErrorMessage { get; set; }

        public bool HasError => ErrorMessage is not null;
        public string PageText => PageCount == 0 ? string.Empty : $"{CurrentIndex + 1} / {PageCount}";

        public async Task OpenAsync(string path, int? ComicId, DecodeRequest request)
        {
            Dispose();
            _request = request;
            ErrorMessage = null;
            Title = Path.GetFileNameWithoutExtension(path);

            try
            {
                _archive = await Task.Run(() => _archiveFactory.Open(path));
            }
            catch (ArchiveException ex)
            {
                ErrorMessage = ex.Message;
                return;
            }

            _cache = new PageCache(_archive, _loader);
            PageCount = _archive.Pages.Count;

            var start = 0;
            if (ComicId is int id)
            {
                _progress = await LoadProgressAsync(id);
                if (_progress.Status == ReadStatus.InProgress)
                    start = Math.Clamp(_progress.CurrentPage, 0, PageCount - 1);
            }

            await ShowPageAsync(start);
        }

        private async Task<ReadingProgress> LoadProgressAsync(int comicId)
        {
            try
            {
                return await _comics.GetProgressAsync(comicId) ?? new ReadingProgress { ComicId = comicId };
            }
            catch (Exception)
            {
                // a database problem must not stop the comic from opening
                return new ReadingProgress { ComicId = comicId };
            }
        }

        public Task NextAsync() => ShowPageAsync(CurrentIndex + 1);
        public Task PreviousAsync() => ShowPageAsync(CurrentIndex - 1);
        public Task FirstAsync() => ShowPageAsync(0);
        public Task LastAsync() => ShowPageAsync(PageCount - 1);

        public async Task UpdateViewportAsync(DecodeRequest request)
        {
            if (request == _request) return;
            _request = request;
            if (_cache is not null)
                await ShowPageAsync(CurrentIndex, track: false); // a resize isn't reading progress
        }

        private async Task ShowPageAsync(int index, bool track = true)
        {
            if (_cache is null || PageCount == 0) return;

            index = Math.Clamp(index, 0, PageCount - 1);
            CurrentIndex = index;
            if (track) RecordProgress(index);

            var version = ++_loadVersion;
            IsLoading = true;
            ErrorMessage = null;

            try
            {
                var page = await _cache.GetAsync(index, _request);
                if (version != _loadVersion) return;

                CurrentImage = page.Image;
            }
            catch (OperationCanceledException)
            {   }
            catch (Exception ex) when (ex is ArchiveException or PageLoadException)
            {
                if (version != _loadVersion) return;
                CurrentImage = null;
                ErrorMessage = ex.Message;
            }
            finally
            {
                if (version == _loadVersion) IsLoading = false;
            }
        }

        // progress saving

        private void RecordProgress(int index)
        {
            if (_progress is null) return;

            _progress.UpdatePage(index, PageCount, _time.GetUtcNow().UtcDateTime);
            _dirty = true;

            _saveDelay?.Cancel();
            var cts = _saveDelay = new CancellationTokenSource();
            _ = SaveAfterDelayAsync(cts.Token);
        }

        private async Task SaveAfterDelayAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(SaveDelay, token);
                await SaveNowAsync();
            }
            catch (OperationCanceledException)
            {
                // another page turn restarted the timer, or the reader closed( which saves itself)
            }
        }

        private async Task SaveNowAsync()
        {
            var progress = _progress;
            if (progress is null || !_dirty) return;

            await _saveGate.WaitAsync();
            try
            {
                if (!_dirty) return;
                _dirty = false;

                //save a copy, so the live object can keep changing while the write runs.
                var snapshot = new ReadingProgress
                {
                    Id = progress.Id,
                    ComicId = progress.ComicId,
                    CurrentPage = progress.CurrentPage,
                    Status = progress.Status,
                    LastReadUtc = progress.LastReadUtc,
                    CompletedUtc = progress.CompletedUtc

                };

                await _comics.SaveProgressAsync(snapshot);
                progress.Id = snapshot.Id; // so the next save updates instead of inserting
            }
            catch (Exception)
            {
                _dirty = true; // saving is best effort; the next page turn tries again
            }
            finally
            {
                _saveGate.Release();
            }
        }

        /// <summary>Saves any pending progress, then releases everything. Call this when leaving the reader</summary>
        public async Task CloseAsync()
        {
            _saveDelay?.Cancel();
            await SaveNowAsync();
            Dispose();
        }

        public void Dispose()
        {
            _loadVersion++;
            _saveDelay?.Cancel();
            _saveDelay = null;
            _progress = null;
            _dirty = false;

            _cache?.Dispose();      // the cache first,
            _cache = null;
            _archive?.Dispose();    // then the archive
            _archive = null;
            CurrentImage = null;
            PageCount = 0;
            CurrentIndex = 0;
        }


    }
}

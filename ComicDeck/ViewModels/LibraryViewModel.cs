using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using ComicDeck.Core.Library;
using ComicDeck.Core.Models;
using ComicDeck.Core.Utilities;
using ComicDeck.Services;
using Microsoft.UI.Xaml.Media.Animation;

namespace ComicDeck.ViewModels
{
    public sealed partial class LibraryViewModel : ObservableObject
    {
        private static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(250);

        private readonly IComicRepository _comics;
        private readonly ILibraryFolderRepository _folders;
        private readonly ILibraryScanner _scanner;
        private readonly CoverThumbnailService _thumbnails;
        private readonly TimeProvider _time;

        private IReadOnlyList<ComicListItem> _all = Array.Empty<ComicListItem>();
        private LibraryQuery _query = new();
        private CancellationTokenSource? _searchDelay;
        private int _applyVersion;

        public LibraryViewModel(
            IComicRepository comics,
            ILibraryFolderRepository folders,
            ILibraryScanner scanner,
            CoverThumbnailService thumbnails,
            TimeProvider time)
        {
            _comics = comics;
            _folders = folders;
            _scanner = scanner;
            _thumbnails = thumbnails;
            _time = time;

            Items = Array.Empty<ComicItemViewModel>();
            Status = string.Empty;
            ScanMaximum = 1;
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowEmptyHint))]
        [NotifyPropertyChangedFor(nameof(ShowNoMatches))]
        public partial IReadOnlyList<ComicItemViewModel> Items { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsIdle))]
        [NotifyPropertyChangedFor(nameof(ShowEmptyHint))]
        public partial bool IsScanning { get; set; }

        [ObservableProperty]
        public partial string Status { get; set; }

        [ObservableProperty]
        public partial double ScanMaximum { get; set; }

        [ObservableProperty]
        public partial double ScanValue { get; set; }

        public bool IsIdle => !IsScanning;
        public bool ShowEmptyHint => _all.Count == 0 && !IsScanning;
        public bool ShowNoMatches => _all.Count > 0 && Items.Count == 0;

        // loading, searching and sorting

        public async Task LoadAsync()
        {
            _all = await _comics.GetLibraryItemsAsync();
            await ApplyQueryAsync();
        }

        /// <summary> Waits for a pause in typing, so each keystroke doesn't re-run the query.</summary>
        public async Task SetSearchAsync(string text)
        {
            _searchDelay?.Cancel();
            var cts = _searchDelay = new CancellationTokenSource();

            try
            {
                await Task.Delay(SearchDelay, cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;   // newer keystroke
            }

            await UpdateQueryAsync(_query with { Search = text });
        }

        public Task SetSortAsync(LibrarySort sort) => UpdateQueryAsync(_query with { Sort = sort });
        public Task SetReverseAsync(bool reverse) => UpdateQueryAsync(_query with { Reverse = reverse });
        public Task SetFilterAsync(LibraryFilter filter) => UpdateQueryAsync(_query with { Filter = filter });

        private Task UpdateQueryAsync(LibraryQuery query)
        {
            if (query == _query) return Task.CompletedTask;
            _query = query;
            return ApplyQueryAsync();
        }

        private async Task ApplyQueryAsync()
        {
            var version = ++_applyVersion;
            var rows = _all;
            var query = _query;

            // Sorting and building up to 10,000 view models doesn't belong on the UI thread.
            var built = await Task.Run(() => LibraryQuery.Apply(rows, query)
                .Select(r => new ComicItemViewModel(r, _thumbnails))
                .ToList());

            if (version != _applyVersion) return;   // a newer query is already running

            Items = built;
            Status = _all.Count == 0 ? string.Empty
                   : query.IsFiltered ? $"{built.Count} of {_all.Count} comics"
                   : $"{_all.Count} comics";
        }

        // folders and scanning

        public async Task AddFolderAsync(string path)
        {
            var folder = await _folders.AddFolderAsync(path);
            await ScanAsync(new[] { folder });
        }

        public async Task RescanAsync()
        {
            var folders = await _folders.GetFoldersAsync();
            if (folders.Count == 0)
            {
                Status = "Add a folder first.";
                return;
            }
            await ScanAsync(folders);
        }

        private async Task ScanAsync(IReadOnlyList<LibraryFolder> folders)
        {
            if (IsScanning) return;
            IsScanning = true;

            int added = 0, updated = 0, removed = 0, failed = 0;
            var unavailable = new List<string>();

            // Created here, on the UI thread, so the callbacks arrive on the UI thread too.
            var progress = new Progress<ScanProgress>(p =>
            {
                ScanMaximum = Math.Max(p.Total, 1);
                ScanValue = p.Processed;
                Status = $"Scanning {p.Processed} / {p.Total}";
            });

            try
            {
                foreach (var folder in folders)
                {
                    var folderPath = folder.Path;
                    try
                    {
                        var result = await Task.Run(() => _scanner.ScanAsync(folderPath, progress));
                        added += result.Added;
                        updated += result.Updated;
                        removed += result.Removed;
                        failed += result.Failures.Count;
                    }
                    catch (DirectoryNotFoundException)
                    {
                        unavailable.Add(folderPath);   // e.g. an unplugged drive: its comics are kept
                    }
                }
            }
            finally
            {
                IsScanning = false;
            }

            await LoadAsync();

            var summary = $"Added {added}, updated {updated}, removed {removed}.";
            if (failed > 0) summary += $" {failed} file(s) couldn't be read.";
            if (unavailable.Count > 0) summary += $" {unavailable.Count} folder(s) unavailable.";
            Status = summary;
        }

        // read state

        public async Task SetReadAsync(ComicItemViewModel comic, bool read)
        {
            var progress = await _comics.GetProgressAsync(comic.Id)
                           ?? new ReadingProgress { ComicId = comic.Id };
            var now = _time.GetUtcNow().UtcDateTime;

            if (read) progress.MarkCompleted(comic.PageCount, now);
            else progress.MarkUnread();

            await _comics.SaveProgressAsync(progress);
            comic.ApplyProgress(progress.Status, progress.CurrentPage);
        }

        public async Task ToggleFavoriteAsync(ComicItemViewModel comic)
        {
            var favorite = !comic.IsFavorite;
            await _comics.SetFavoriteAsync(comic.Id, favorite);
            comic.ApplyFavorite(favorite);
        }
    }
}

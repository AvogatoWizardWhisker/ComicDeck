using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using ComicDeck.Core.Library;
using ComicDeck.Core.Models;
using ComicDeck.Core.Utilities;
using ComicDeck.Services;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Controls;

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
            Tiles = Array.Empty<AlphaTile>();
            Groups = Array.Empty<ComicGroup>();
            Status = string.Empty;
            ScanMaximum = 1;
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowEmptyHint))]
        [NotifyPropertyChangedFor(nameof(ShowNoMatches))]
        [NotifyPropertyChangedFor(nameof(HasLibrary))]
        public partial IReadOnlyList<ComicItemViewModel> Items { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsIdle))]
        [NotifyPropertyChangedFor(nameof(ShowEmptyHint))]
        [NotifyPropertyChangedFor(nameof(HasLibrary))]
        public partial bool IsScanning { get; set; }

        [ObservableProperty]
        public partial string Status { get; set; }

        [ObservableProperty]
        public partial double ScanMaximum { get; set; }

        [ObservableProperty]
        public partial double ScanValue { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsFlat))]
        public partial bool IsGrouped { get; set; }

        [ObservableProperty]
        public partial IReadOnlyList<AlphaTile> Tiles { get; set; }

        [ObservableProperty]
        public partial IReadOnlyList<ComicGroup> Groups { get; set; }

        public bool IsFlat => !IsGrouped;

        /// <summary> The jump-list tile for the group that contains this comic.</summary>
        public AlphaTile? TileFor(ComicItemViewModel comic)
        {
            var key = Groups.FirstOrDefault(g => g.Contains(comic))?.Key;
            return key is null ? null : Tiles.FirstOrDefault(t => t.Key == key);
        }

        public bool IsIdle => !IsScanning;
        public bool ShowEmptyHint => _all.Count == 0 && !IsScanning;
        public bool HasLibrary => !ShowEmptyHint;
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
            var source = _all;
            var query = _query;

            // Sorting and building up to 10,000 view models doesn't belong on the UI thread.
            var built = await Task.Run(() =>
            {
                var rows = LibraryQuery.Apply(source, query);
                var items = rows.Select(r => new ComicItemViewModel(r, _thumbnails)).ToList();

                if (!LibraryGrouping.IsAlphabetical(query.Sort))
                    return (Items: items, Groups: new List<ComicGroup>(), Tiles: new List<AlphaTile>(), Grouped: false);

                var pairs = rows.Zip(items, (row, vm) => (Row: row, Vm: vm));
                var groups = LibraryGrouping
                    .Group(pairs, p => LibraryGrouping.KeyOf(p.Row, query.Sort), query.Reverse)
                    .Select(g => new ComicGroup(g.Key, g.Items.Select(p => p.Vm)))
                    .ToList();

                var tiles = LibraryGrouping.AllKeys
                    .Select(k => new AlphaTile(k, groups.FirstOrDefault(g => g.Key == k)?.FirstOrDefault()))
                    .ToList();

                return (Items: items, Groups: groups, Tiles: tiles, Grouped: true);
            });

            if (version != _applyVersion) return;   // a newer query is already running

            Items = built.Items;
            Tiles = built.Tiles;
            IsGrouped = built.Grouped;
            Groups = built.Groups;   // last, because the page rebinds when this changes

            Status = _all.Count == 0 ? string.Empty
                   : query.IsFiltered ? $"{built.Items.Count} of {_all.Count} comics"
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

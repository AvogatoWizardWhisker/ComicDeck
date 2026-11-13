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
        private readonly IComicRepository _comics;
        private readonly ILibraryFolderRepository _folders;
        private readonly ILibraryScanner _scanner;
        private readonly CoverThumbnailService _thumbnails;

        public LibraryViewModel(IComicRepository comics, ILibraryFolderRepository folders, ILibraryScanner scanner, CoverThumbnailService thumbnails)
        {
            _comics = comics;
            _folders = folders;
            _scanner = scanner;
            _thumbnails = thumbnails;
        }

        [ObservableProperty, NotifyPropertyChangedFor(nameof(ShowEmptyHint))]
        public partial IReadOnlyList<ComicItemViewModel> Items { get; set; } = Array.Empty<ComicItemViewModel>();

        [ObservableProperty, NotifyPropertyChangedFor(nameof(IsIdle), nameof(ShowEmptyHint))]
        public partial bool IsScanning { get; set; }

        [ObservableProperty] public partial string Status { get; set; } = string.Empty;
        [ObservableProperty] public partial double ScanMaximum { get; set; } = 1;
        [ObservableProperty] public partial double ScanValue { get; set; }

        public bool IsIdle => !IsScanning;
        public bool ShowEmptyHint => Items.Count == 0 && !IsScanning;

        public async Task LoadAsync()
        {
            var rows = await _comics.GetLibraryItemsAsync();

            // Sorting and building 10,000 view models doesn't belong in the UI thread
            Items = await Task.Run(() => rows
            .OrderBy(r => r.SeriesName ?? r.DisplayTitle, NaturalStringComparer.Instance)
            .ThenBy(r => r.Number ?? string.Empty, NaturalStringComparer.Instance)
            .ThenBy(r => r.DisplayTitle, NaturalStringComparer.Instance)
            .Select(r => new ComicItemViewModel(r, _thumbnails))
            .ToList());

            Status = Items.Count == 0 ? string.Empty : $"{Items.Count} comics";
        }

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
                Status = "Add a folder first";
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

            // Create here, on the UI thread, so the callbacks arrive on the UI thread too.
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
                    try
                    {
                        var result = await Task.Run(() => _scanner.ScanAsync(folder.Path, progress));
                        added += result.Added;
                        updated += result.Updated;
                        removed += result.Removed;
                        failed += result.Failures.Count;
                    }
                    catch (DirectoryNotFoundException)
                    {
                        unavailable.Add(folder.Path); // e.g an unplugged drive: its comics are kept
                    }
                }
            }
            finally
            {
                IsScanning = false;
            }

            await LoadAsync();

            var summary = $"Added {added}, updated {updated}, removed {removed}.";
            if (failed > 0) summary += $"{failed} file(s) couldn't be read.";
            if (unavailable.Count > 0) summary += $"{unavailable.Count} folder(s) unavailable";
            Status = summary;
        }

    }
}

using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using ComicDeck.Core.Library;
using ComicDeck.Core.Models;
using ComicDeck.Services;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace ComicDeck.ViewModels
{
    /// <summary> One card in the library grid</summary>
    public sealed partial class ComicItemViewModel : ObservableObject
    {
        private readonly ComicListItem _item;
        private readonly CoverThumbnailService _thumbnails;
        private bool _coverRequested;

        public ComicItemViewModel(ComicListItem item, CoverThumbnailService thumbnails)
        {
            _item = item;
            _thumbnails = thumbnails;
        }

        public string FilePath => _item.FilePath;
        public string Title => _item.DisplayTitle;

                public string Subtitle =>
            _item.SeriesName is null ? string.Empty
            : !string.IsNullOrEmpty(_item.Number) ? $"{_item.SeriesName} #{_item.Number}"
            : _item.Volume is int v ? $"{_item.SeriesName} · Vol. {v}"
            : _item.SeriesName;
        /*public string Subtitle =>
            $"{_item.SeriesName} | N={_item.Number ?? "-"} | V={_item.Volume?.ToString() ?? "-"}";*/

        public bool ShowProgress => _item.Status == ReadStatus.InProgress && _item.PageCount > 1;

        public double ProgressPercent => _item.PageCount > 1 ? 100.0 * _item.CurrentPage / (_item.PageCount - 1) : 0;

        [ObservableProperty]
        public partial ImageSource? Cover { get; set; }

        public async Task LoadCoverAsync()
        {
            if (_coverRequested) return;
            _coverRequested = true;

            var fileName = await _thumbnails.GetOrCreateAsync(_item);
            if (fileName is null || !_coverRequested) return;   // failed, or the card was recycled menwhile

            Cover = new BitmapImage
            {
                DecodePixelHeight = 360,
                UriSource = new Uri($"ms-appdata:///local/covers/{fileName}")
            };
        }

        /// <summary> Called when the card scrolls away and its container is reused</summary>
        public void UnloadCover()
        {
            _coverRequested = false;
            Cover = null;
        }

        public int Id => _item.Id;
        public int PageCount => _item.PageCount;
        public bool IsCompleted => _item.Status == ReadStatus.Completed;

        /// <summary>Updates the card after the user marks it read or unread</summary>
        public void ApplyProgress(ReadStatus status, int currentPage)
        {
            _item.Status = status;
            _item.CurrentPage = currentPage;
            OnPropertyChanged(nameof(ShowProgress));
            OnPropertyChanged(nameof(ProgressPercent));
            OnPropertyChanged(nameof(IsCompleted));
        }

        public bool IsFavorite => _item.IsFavorite;
        public string FavoriteMenuText => _item.IsFavorite ? "Remove from favorites" : "Add to favorites";

        public void ApplyFavorite(bool isFavorite)
        {
            _item.IsFavorite = isFavorite;
            OnPropertyChanged(nameof(isFavorite));
            OnPropertyChanged(nameof(FavoriteMenuText));
        }

        public string ToolTipText
        {
            get
            {
                var lines = new List<string> { Title };
                if (!string.IsNullOrEmpty(Subtitle)) lines.Add(Subtitle);
                lines.Add(Path.GetFileName(_item.FilePath));
                return string.Join(Environment.NewLine, lines);
            }
        }

    }
}

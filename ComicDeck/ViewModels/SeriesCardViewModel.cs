using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using ComicDeck.Core.Library;
using ComicDeck.Services;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace ComicDeck.ViewModels
{
    public sealed partial class SeriesCardViewModel : ObservableObject
    {
        private readonly SeriesSummary _summary;
        private readonly CoverThumbnailService _thumbnails;
        private bool _coverRequested;

        public SeriesCardViewModel(SeriesSummary summary, CoverThumbnailService thumbnails)
        {
            _summary = summary;
            _thumbnails = thumbnails;
        }

        public string Name => _summary.Name;

        public string Summary
        {
            get
            {
                var count = _summary.Count == 1 ? "1 issue" : $"{_summary.Count} issues";
                var left = _summary.Count - _summary.Completed;
                return left == 0 ? $"{count} . finished" : $"{count} . {left} left";
            }
        }

        [ObservableProperty]
        public partial ImageSource? Cover { get; set; }

        public async Task LoadCoverAsync()
        {
            if (_coverRequested) return;
            _coverRequested = true;

            await Task.Yield(); // let the card finish the binding before the cover is set

            var fileName = await _thumbnails.GetOrCreateAsync(_summary.Cover);
            if (fileName is null || !_coverRequested) return;

            Cover = new BitmapImage
            {
                DecodePixelHeight = 360,
                UriSource = new Uri($"ms-appdata:///local/covers/{fileName}")
            };
        }

        public void UnloadCover()
        {
            _coverRequested = false;
            Cover = null;
        }
    }

}

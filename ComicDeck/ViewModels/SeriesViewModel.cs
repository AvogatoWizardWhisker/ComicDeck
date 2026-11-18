using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using ComicDeck.Core.Library;
using ComicDeck.Services;

namespace ComicDeck.ViewModels
{
    public sealed partial class SeriesViewModel : ObservableObject
    {
        private readonly IComicRepository _comics;
        private readonly CoverThumbnailService _thumbnails;

        public SeriesViewModel(IComicRepository comics, CoverThumbnailService thumbnails)
        {
            _comics = comics;
            _thumbnails = thumbnails;
            Items = Array.Empty<SeriesCardViewModel>();
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowEmptyHint))]
        public partial IReadOnlyList<SeriesCardViewModel> Items { get; set; }

        public bool ShowEmptyHint => Items.Count == 0;

        public async Task LoadAsync()
        {
            var rows = await _comics.GetLibraryItemsAsync();
            Items = await Task.Run(() => SeriesGrouping.Build(rows)
            .Select(s => new SeriesCardViewModel(s, _thumbnails))
            .ToList());
        }
    }
}

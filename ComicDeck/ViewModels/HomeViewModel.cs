using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using ComicDeck.Core.Library;
using ComicDeck.Services;

namespace ComicDeck.ViewModels
{
    public sealed partial class HomeViewModel : ObservableObject
    {
        private const int ShelfSize = 128;

        private readonly IComicRepository _comics;
        private readonly CoverThumbnailService _thumbnails;

        public HomeViewModel(IComicRepository comics, CoverThumbnailService thumbnails)
        {
            _comics = comics;
            _thumbnails = thumbnails;
            ContinueReading = Array.Empty<ComicItemViewModel>();
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasContinueReading))]
        [NotifyPropertyChangedFor(nameof(ShowEmptyHint))]
        public partial IReadOnlyList<ComicItemViewModel> ContinueReading { get; set; }

        public bool HasContinueReading => ContinueReading.Count > 0;
        public bool ShowEmptyHint => ContinueReading.Count == 0;

        public async Task LoadAsync()
        {
            var rows = await _comics.GetContinueReadingAsync(ShelfSize);
            ContinueReading = rows.Select(r => new ComicItemViewModel(r, _thumbnails)).ToList();
        }

        public async Task ToggleFavoriteAsync(ComicItemViewModel comic)
        {
            var favorite = !comic.IsFavorite;
            await _comics.SetFavoriteAsync(comic.Id, favorite);
            comic.ApplyFavorite(favorite);
        }
    }
}

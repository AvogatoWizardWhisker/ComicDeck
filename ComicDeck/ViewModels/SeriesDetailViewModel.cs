using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using ComicDeck.Core.Library;
using ComicDeck.Core.Models;
using ComicDeck.Services;
using Microsoft.UI.Xaml.Controls;

namespace ComicDeck.ViewModels
{
    public sealed partial class SeriesDetailViewModel : ObservableObject
    {
        private readonly IComicRepository _comics;
        private readonly CoverThumbnailService _thumbnails;
        private ComicListItem? _next;

        public SeriesDetailViewModel(IComicRepository comics, CoverThumbnailService thumbnails)
        {
            _comics = comics;
            _thumbnails = thumbnails;
            Title = string.Empty;
            Subtitle = string.Empty;
            ContinueText = string.Empty;
            Issues = Array.Empty<ComicItemViewModel>();
        }

        [ObservableProperty] public partial string Title { get; set; }
        [ObservableProperty] public partial string Subtitle { get; set; }
        [ObservableProperty] public partial string ContinueText { get; set; }
        [ObservableProperty] public partial IReadOnlyList<ComicItemViewModel> Issues { get; set; }

        public bool HasContinue => _next is not null;
        public ReaderRequest? ContinurRequest => _next is null ? null : new ReaderRequest(_next.FilePath, _next.Id);

        public async Task LoadAsync(string seriesName)
        {
            var rows = await _comics.GetLibraryItemsAsync();
            var (issues, summary) = await Task.Run(() =>
            {
                var list = SeriesGrouping.IssuesOf(rows, seriesName);
                return (list, SeriesGrouping.Build(list).FirstOrDefault());
            });

            Title = summary?.Name ?? seriesName;
            Issues = issues.Select(i => new ComicItemViewModel(i, _thumbnails)).ToList();
            _next = summary?.Next;

            if (summary is null)
            {
                Subtitle = string.Empty;
                ContinueText = string.Empty;
            }
            else
            {
                Subtitle = $"{summary.Count} issue(s) . {summary.Completed} read";
                ContinueText = summary.Completed == summary.Count ? "Read again" 
                    : summary.Next.Status == ReadStatus.InProgress ? "Continue reading" 
                    : summary.Completed == 0 && summary.InProgress == 0 ? "Start reading" 
                    : "Read next issue";
            }

            OnPropertyChanged(nameof(HasContinue));
        }
    }
}

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
    public sealed partial class SeriesDetailViewModel : ObservableObject
    {
        private readonly IComicRepository _comics;
        private readonly CoverThumbnailService _thumbnails;
        private ComicListItem? _next;

        private readonly TimeProvider _time;

        public SeriesDetailViewModel(IComicRepository comics, CoverThumbnailService thumbnails, TimeProvider time)
        {
            _comics = comics;
            _thumbnails = thumbnails;
            _time = time;
            Title = string.Empty;
            Subtitle = string.Empty;
            ContinueText = string.Empty;
            Issues = Array.Empty<ComicItemViewModel>();
            Summary = string.Empty; // no summary anywhere: fall back to facts we already have, so there is never an empty gap
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Crumbs))]
        public partial string Title { get; set; }
        [ObservableProperty] public partial string Subtitle { get; set; }
        [ObservableProperty] public partial string ContinueText { get; set; }
        [ObservableProperty] public partial IReadOnlyList<ComicItemViewModel> Issues { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSummary))]
        public partial string Summary { get; set; }

        [ObservableProperty] public partial ImageSource? Cover { get; set; }

        public bool HasSummary => !string.IsNullOrWhiteSpace(Summary);

        public IReadOnlyList<string> Crumbs => new[] { "Series", Title };

        public bool HasContinue => _next is not null;
        public ReaderRequest? ContinueRequest => _next is null ? null : new ReaderRequest(_next.FilePath, _next.Id);

        public async Task LoadAsync(string seriesName)
        {
            Cover = null; // clear the previous series' cover while the new one loads

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

            var summaries = await _comics.GetSummariesAsync(issues.Select(i => i.Id).ToList());
            var text = summary is null ? null : SeriesGrouping.PickSummary(issues, summary.Next, summaries);

            

            if (summary is not null)
            {
                var file = await _thumbnails.GetOrCreateAsync(summary.Cover);
                if (file is not null)
                    Cover = new BitmapImage
                    {
                        DecodePixelHeight = 360,
                        UriSource = new Uri($"ms-appdata:///local/covers/{file}")
                    };
            }

            OnPropertyChanged(nameof(HasContinue));
        }

        public async Task SetReadAsync(ComicItemViewModel comic, bool read)
        {
            var progress = await _comics.GetProgressAsync(comic.Id) ?? new ReadingProgress { ComicId = comic.Id };
            var now = _time.GetUtcNow().UtcDateTime;

            if (read) progress.MarkCompleted(comic.PageCount, now);
            else progress.MarkUnread();

            await _comics.SaveProgressAsync(progress);
            comic.ApplyProgress(progress.Status, progress.CurrentPage);
        }

        ///<summary> marks every issue in the series read or unread</summary>
        public async Task SetAllReadAsync(bool read)
        {
            foreach (var comic in Issues)
                await SetReadAsync(comic, read);

            // the subtitle and button depend on the counts, so reload them
            await LoadAsync(Title);
        }
    }
}

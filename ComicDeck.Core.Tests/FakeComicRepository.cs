using System;
using System.Collections.Generic;
using System.Text;
using ComicDeck.Core.Library;
using ComicDeck.Core.Models;

namespace ComicDeck.Core.Tests;

internal sealed class FakeComicRepository : IComicRepository
{
    private int _nextComicId = 1;
    private int _nextSeriesId = 1;
    private int _nextProgressId = 1;

    public List<Comic> Comics { get; } = new();
    public List<Series> SeriesList { get; } = new();
    public List<ReadingProgress> ProgressRows { get; } = new();

    public Task<IReadOnlyList<Comic>> GetComicsInFolderAsync(string folderPath, CancellationToken ct = default)
    {
        var prefix = Path.EndsInDirectorySeparator(folderPath)
            ? folderPath
            : folderPath + Path.DirectorySeparatorChar;

        IReadOnlyList<Comic> result = Comics
            .Where(c => c.FilePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Task.FromResult(result);
    }

    public Task SaveComicAsync(Comic comic, CancellationToken ct = default)
    {
        if (comic.Id == 0)
        {
            comic.Id = _nextComicId++;
            Comics.Add(comic);
        }
        else
        {
            var index = Comics.FindIndex(c => c.Id == comic.Id);
            Comics[index] = comic;
        }
        return Task.CompletedTask;
    }

    public Task RemoveComicsAsync(IReadOnlyCollection<int> comicIds, CancellationToken ct = default)
    {
        Comics.RemoveAll(c => comicIds.Contains(c.Id));
        ProgressRows.RemoveAll(p => comicIds.Contains(p.ComicId));
        return Task.CompletedTask;
    }

    public Task<Series> GetOrCreateSeriesAsync(string name, string? publisher, int? year, CancellationToken ct = default)
    {
        var series = SeriesList.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (series is null)
        {
            series = new Series
            {
                Id = _nextSeriesId++,
                Name = name,
                Publisher = publisher,
                Year = year,
                AddedUtc = DateTime.UtcNow
            };
            SeriesList.Add(series);
        }
        return Task.FromResult(series);
    }

    public Task<ReadingProgress?> GetProgressAsync(int comicId, CancellationToken ct = default) =>
        Task.FromResult(ProgressRows.FirstOrDefault(p => p.ComicId == comicId));

    public Task SaveProgressAsync(ReadingProgress progress, CancellationToken ct = default)
    {
        if (progress.Id == 0)
        {
            progress.Id = _nextProgressId++;
            ProgressRows.Add(progress);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ComicListItem>> GetLibraryItemsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<ComicListItem> items = Comics.Select(c =>
        {
            var series = SeriesList.FirstOrDefault(s => s.Id == c.SeriesId);
            var progress = ProgressRows.FirstOrDefault(p => p.ComicId == c.Id);
            return new ComicListItem
            {
                Id = c.Id,
                FilePath = c.FilePath,
                Title = c.Title,
                SeriesName = series?.Name,
                Number = c.Number,
                PageCount = c.PageCount,
                FileSize = c.FileSize,
                FileModifiedUtc = c.FileModifiedUtc,
                IsFavorite = c.IsFavorite,
                CurrentPage = progress?.CurrentPage ?? 0,
                Status = progress?.Status ?? ReadStatus.Unread
            };
        }).ToList();
        return Task.FromResult(items);
    }
}
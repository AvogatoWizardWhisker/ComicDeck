using System;
using System.Collections.Generic;
using System.Text;
using ComicDeck.Core.Models;

namespace ComicDeck.Core.Library
{
    public interface IComicRepository
    {
        /// <summary>
        /// All comics whose path lies under <paramref name="folderPath"/>. Implementation must match on a directory
        /// boundary (so "D:\Comics" does not match "D:\Comics2\x.cbz") and ignore case.
        /// </summary>
        Task<IReadOnlyList<Comic>> GetComicsInFolderAsync(string folderPath, CancellationToken cancellationToken = default);

        /// <summary>Inserts the comic <c>Id == 0</c> and assigns the new Id, otherwise updates it.</summary>
        Task SaveComicAsync(Comic comic, CancellationToken cancellationToken = default);

        /// <summary>Removes the comics and their reading progress</summary>
        Task RemoveComicsAsync(IReadOnlyCollection<int> comicIds, CancellationToken cancellationToken = default);

        /// <summary>Finds a series by names (while ignoring cases) or creates it</summary>
        Task<Series> GetOrCreateSeriesAsync(string name, string? publisher, int? year, CancellationToken cancellationToken = default);

        Task<ReadingProgress?> GetProgressAsync(int comicId, CancellationToken cancellationToken = default);

        /// <summary>Inserts the progress when <c>Id == 0</c>, otherwise updates it.</summary>
        Task SaveProgressAsync(ReadingProgress progress, CancellationToken cancellationToken = default);
    }
}

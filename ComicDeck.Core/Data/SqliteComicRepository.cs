using System;
using System.Collections.Generic;
using System.Text;
using ComicDeck.Core.Library;
using ComicDeck.Core.Models;
using SQLite;

namespace ComicDeck.Core.Data
{
    public sealed class SqliteComicRepository : IComicRepository
    {
        private const int DeleteChunkSize = 500; // stays far below SQlite's parameter limits

        private readonly SQLiteAsyncConnection _db;

        public SqliteComicRepository(ComicDatabase database) => _db = database.Connection;

        public async Task<IReadOnlyList<Comic>> GetComicsInFolderAsync(
            string folderPath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // the trailing separator is what stops "D:\Comics" matching "D:\Comics2\x.cbz"
            var prefix = Path.EndsInDirectorySeparator(folderPath)
            ? folderPath
            : folderPath + Path.DirectorySeparatorChar;

            // sqlite's substr counts code points, not UTF-15 ubitsn si ciunt runes
            var length = prefix.EnumerateRunes().Count();

            return await _db.QueryAsync<Comic>(
                "SELECT * FROM Comic WHERE substr(FilePath, 1, ?) = ? COLLATE NOCASE", length, prefix);
        }

        public async Task SaveComicAsync(Comic comic, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (comic.Id == 0)
                await _db.InsertAsync(comic); //sqlite-net write the new Id back onto the object
            else
                await _db.UpdateAsync(comic);
        }

        public async Task RemoveComicsAsync(
            IReadOnlyCollection<int> comicIds, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (comicIds.Count == 0) return;

            await _db.RunInTransactionAsync(conn =>
            {
                foreach (var chunk in comicIds.Chunk(DeleteChunkSize))
                {
                    // int only so inlining them is safe and avoids building parameter lists
                    var ids = string.Join(',', chunk);
                    conn.Execute($"DELETE FROM ReadingProgress WHERE ComicId IN ({ids})");
                    conn.Execute($"DELETE FROM Comic WHERE Id IN ({ids})");
                }
            });
        }

        public async Task<Series> GetOrCreateSeriesAsync(
            string name, string? publisher, int? year, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            name = name.Trim();

            Series? result = null;
            await _db.RunInTransactionAsync(conn =>
            {
                result = conn
                .Query<Series>("SELECT * FROM Series WHERE Name = ? COLLATE NOCASE LIMIT 1", name)
                .FirstOrDefault();

                if (result is null)
                {
                    result = new Series
                    {
                        Name = name,
                        Publisher = publisher,
                        Year = year,
                        AddedUtc = DateTime.UtcNow
                    };
                    conn.Insert(result);
                }
            });

            return result!;
        }

        public async Task<ReadingProgress?> GetProgressAsync(
            int comicId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await _db.FindAsync<ReadingProgress>(p => p.ComicId == comicId);
        }

        public async Task SaveProgressAsync(
            ReadingProgress progress, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (progress.Id == 0)
                await _db.InsertAsync(progress);
            else
                await _db.UpdateAsync(progress);
        }

        public async Task<IReadOnlyList<ComicListItem>> GetLibraryItemsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return await _db.QueryAsync<ComicListItem>(
                """
                SELECT c.Id, c.FilePath, c.Title, c.Volume, s.Name AS SeriesName, c.Number, c.PageCount, c.FileSize, c.FileModifiedUtc, c.IsFavorite, c.AddedUtc,
                COALESCE(p.CurrentPage, 0) AS CurrentPage,
                COALESCE(p.Status, 0) AS Status, p.LastReadUtc AS LastReadUtc
                From Comic c
                LEFT JOIN Series s ON s.Id = c.SeriesId
                LEFT JOIN ReadingProgress p ON p.ComicId = c.Id
                """);
        }

        public async Task<IReadOnlyList<ComicListItem>> GetContinueReadingAsync(int limit, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Status 1 = ReadStatus.InProgress
            return await _db.QueryAsync<ComicListItem>(
                """
                SELECT c.Id, c.FilePath, c.Title, s.Name AS SeriesName, c.Number, c.PageCount,
                       c.FileSize, c.FileModifiedUtc, c.IsFavorite,
                       p.CurrentPage AS CurrentPage, p.Status AS Status
                FROM Comic c
                JOIN ReadingProgress p ON p.ComicId = c.Id
                LEFT JOIN Series s ON s.Id = c.SeriesId
                WHERE p.Status = 1
                ORDER BY p.LastReadUtc DESC
                LIMIT ?
                """, limit);
        }

        public async Task SetFavoriteAsync(int comicId, bool isFavorite, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _db.ExecuteAsync("UPDATE Comic SET IsFavorite = ? WHERE Id = ?", isFavorite ? 1 : 0, comicId);
        }

        public async Task<int> FindComicIdAsync(string filePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await _db.ExecuteScalarAsync<int>(
                "SELECT Id FROM Comic WHERE FilePath = ? COLLATE NOCASE LIMIT 1", filePath);
        }

        public async Task<IReadOnlyDictionary<int, string>> GetSummariesAsync(
            IReadOnlyCollection<int> comicIds, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (comicIds.Count == 0) return new Dictionary<int, string>();

            var ids = string.Join(',', comicIds); // integers only, safe to inline
            var rows = await _db.QueryAsync<Comic>(
                $"SELECT Id, Summary FROM Comic WHERE Id IN ({ids}) AND Summary IS NOT NULL AND Summary <> ''");

            return rows.ToDictionary(c => c.Id, c => c.Summary!);
        }
    }
}

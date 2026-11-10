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
    }
}

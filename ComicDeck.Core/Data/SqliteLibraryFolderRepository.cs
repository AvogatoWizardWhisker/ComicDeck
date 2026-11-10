using System;
using System.Collections.Generic;
using System.Text;
using ComicDeck.Core.Library;
using ComicDeck.Core.Models;
using SQLite;

namespace ComicDeck.Core.Data
{
    public sealed class SqliteLibraryFolderRepository : ILibraryFolderRepository
    {
        private readonly SQLiteAsyncConnection _db;
        public SqliteLibraryFolderRepository(ComicDatabase database) => _db = database.Connection;

        public async Task<IReadOnlyList<LibraryFolder>> GetFoldersAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await _db.QueryAsync<LibraryFolder>("SELECT * FROM LibraryFolder ORDER BY Path COLLATE NOCASE");
        }

        public async Task<LibraryFolder> AddFolderAsync(string path, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            path = Normalize(path);

            LibraryFolder? result = null;
            await _db.RunInTransactionAsync(conn =>
            {
                result = conn.Query<LibraryFolder>("SELECT * FROM LibraryFolder WHERE Path = ? COLLATE NOCASE LIMIT 1", path)
                .FirstOrDefault();

                if (result is null)
                {
                    result = new LibraryFolder { Path = path, AddedUtc = DateTime.UtcNow };
                    conn.Insert(result);
                }
            });

            return result!;
        }

        public async Task RemoveFolderAsync (int folderId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _db.DeleteAsync<LibraryFolder>(folderId);
        }

        ///<summary>Ful path without a trailing seperator, so "D:\Comics" and D:\Comics\" are one folder</summary>
        private static string Normalize(string path)
        {
            var full = Path.GetFullPath(path);
            return Path.EndsInDirectorySeparator(full) && full != Path.GetPathRoot(full)
                ? full[..^1]
                : full;
        }
    }
}

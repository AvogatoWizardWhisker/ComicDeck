using System;
using System.Collections.Generic;
using System.Text;
using ComicDeck.Core.Models;
using SQLite;

namespace ComicDeck.Core.Data
{
    /// <summary>
    /// Owns the single SQLite connection and create the schema
    /// </summary>
    public sealed class ComicDatabase : IAsyncDisposable
    {
        static ComicDatabase()
        {
            // safe to call more than once. It can be removed if the sqlite-net version isn't needed.
            // SQLitePCL.Batteries_V2.Init();
        }

        public SQLiteAsyncConnection Connection { get; }

        private ComicDatabase(SQLiteAsyncConnection connection) => Connection = connection;

        public static async Task<ComicDatabase> OpenAsync(string databasePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var directory = Path.GetDirectoryName(Path.GetFullPath(databasePath));
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var connection = new SQLiteAsyncConnection(
                databasePath,
                SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);

            try
            {
                // journal_mode returns a row, so it must be read as a scalar
                await connection.ExecuteScalarAsync<string>("PRAGMA journal_mode=WAL");
                await connection.ExecuteAsync("PRAGMA synchronous=NORMAL");

                const CreateFlags flags = CreateFlags.ImplicitPK | CreateFlags.AutoIncPK;
                await connection.CreateTableAsync<Comic>(flags);
                await connection.CreateTableAsync<Series>(flags);
                await connection.CreateTableAsync<ReadingProgress>(flags);
                await connection.CreateTableAsync<LibraryFolder>(flags);

                await connection.ExecuteAsync(
               "CREATE UNIQUE INDEX IF NOT EXISTS ux_comic_path ON Comic(FilePath COLLATE NOCASE)");
                await connection.ExecuteAsync(
                    "CREATE INDEX IF NOT EXISTS ix_comic_series ON Comic(SeriesId)");
                await connection.ExecuteAsync(
                    "CREATE UNIQUE INDEX IF NOT EXISTS ux_series_name ON Series(Name COLLATE NOCASE)");
                await connection.ExecuteAsync(
                    "CREATE UNIQUE INDEX IF NOT EXISTS ux_progress_comic ON ReadingProgress(ComicId)");
                await connection.ExecuteAsync(
                    "CREATE UNIQUE INDEX IF NOT EXISTS ux_folder_path ON LibraryFolder(Path COLLATE NOCASE)");
            }
            catch
            {
                await connection.CloseAsync();
                throw;
            }

            return new ComicDatabase(connection);
        }

        public async ValueTask DisposeAsync() => await Connection.CloseAsync();
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Compression;
using ComicDeck.Core.Archives;
using ComicDeck.Core.Library;
using ComicDeck.Core.Metadata;
using ComicDeck.Core.Models;

namespace ComicDeck.Core.Tests
{
    public sealed class LibraryScannerTests : IDisposable
    {
        private readonly string _root = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ComicDeckTests", Guid.NewGuid().ToString("N"))).FullName;

        private readonly FakeComicRepository _repo = new();
        private readonly LibraryScanner _scanner;

        public LibraryScannerTests()
        {
            _scanner = new LibraryScanner(_repo, new ComicArchiveFactory(), new MetadataService());
        }

        public void Dispose() => Directory.Delete(_root, recursive: true);

        private string CreateCbz(string relativePath, int pages = 2)
        {
            var path = Path.Combine(_root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (File.Exists(path)) File.Delete(path);

            using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
            for (var i = 1; i <= pages; i++)
            {
                using var writer = new StreamWriter(zip.CreateEntry($"p{i}.jpg").Open());
                writer.Write($"page {i}");
            }
            return path;
        }

        private sealed class SyncProgress<T>(Action<T> handler) : IProgress<T>
        {
            public void Report(T value) => handler(value);
        }

        [Fact]
        public async Task Adds_comics_from_subfolders_and_ignores_other_files()
        {
            CreateCbz("Batman 001.cbz");
            CreateCbz(Path.Combine("Indie", "Saga 002.cbz"));
            File.WriteAllText(Path.Combine(_root, "notes.txt"), "ignore me");

            var result = await _scanner.ScanAsync(_root);

            Assert.Equal(2, result.Added);
            Assert.Empty(result.Failures);
            Assert.Equal(2, _repo.Comics.Count);
            Assert.Equal(new[] { "Batman", "Saga" }, _repo.SeriesList.Select(s => s.Name).Order());

            var batman = _repo.Comics.Single(c => c.Number == "1");
            Assert.Equal(2, batman.PageCount);
            Assert.Equal(ArchiveFormat.Zip, batman.Format);
            Assert.NotNull(batman.SeriesId);
        }

        [Fact]
        public async Task Second_scan_skips_unchanged_files()
        {
            CreateCbz("Batman 001.cbz");
            CreateCbz("Batman 002.cbz");

            await _scanner.ScanAsync(_root);
            var result = await _scanner.ScanAsync(_root);

            Assert.Equal(0, result.Added);
            Assert.Equal(0, result.Updated);
            Assert.Equal(2, result.Unchanged);
        }

        [Fact]
        public async Task Changed_file_is_updated_and_keeps_user_stale()
        {
            CreateCbz("Batman 001.cbz", pages: 2);
            await _scanner.ScanAsync(_root);

            var comic = _repo.Comics.Single();
            var id = comic.Id;
            comic.IsFavorite = true;

            CreateCbz("Batman 001.cbz", pages: 3);
            var result = await _scanner.ScanAsync(_root);

            Assert.Equal(1, result.Updated);
            Assert.Equal(0, result.Added);
            Assert.Single(_repo.Comics);
            Assert.Equal(id, _repo.Comics[0].Id);
            Assert.Equal(3, _repo.Comics[0].PageCount);
            Assert.True(_repo.Comics[0].IsFavorite);
        }

        [Fact]
        public async Task Deleted_file_is_removed_from_the_library()
        {
            var path = CreateCbz("Batman 001.cbz");
            CreateCbz("Batman 002.cbz");
            await _scanner.ScanAsync(_root);

            File.Delete(path);
            var result = await _scanner.ScanAsync(_root);

            Assert.Equal(1, result.Removed);
            Assert.Single(_repo.Comics);
        }

        [Fact]
        public async Task Bad_file_is_reported_but_does_not_stop_the_scan()
        {
            File.WriteAllText(Path.Combine(_root, "bad.cbz"), "not a zip");
            CreateCbz("Good 001.cbz");

            var result = await _scanner.ScanAsync(_root);

            Assert.Equal(1, result.Added);
            var failure = Assert.Single(result.Failures);
            Assert.EndsWith("bad.cbz", failure.FilePath);
        }

        [Fact]
        public async Task Missing_folder_throws_and_removes_nothing()
        {
            CreateCbz("Batman 001.cbz");
            await _scanner.ScanAsync(_root);

            await Assert.ThrowsAsync<DirectoryNotFoundException>(
                () => _scanner.ScanAsync(Path.Combine(_root, "nope")));

            Assert.Single(_repo.Comics);
        }

        [Fact]
        public async Task Reports_progress_for_every_file()
        {
            CreateCbz("Batman 001.cbz");
            CreateCbz("Batman 002.cbz");
            var reports = new List<ScanProgress>();

            await _scanner.ScanAsync(_root, new SyncProgress<ScanProgress>(reports.Add));

            Assert.Equal(2, reports.Count);
            Assert.Equal(2, reports[^1].Processed);
            Assert.Equal(2, reports[^1].Total);
        }

        [Fact]
        public async Task Cancelled_token_stops_the_scan()
        {
            CreateCbz("Batman 001.cbz");
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => _scanner.ScanAsync(_root, null, cts.Token));

        }
    }
}

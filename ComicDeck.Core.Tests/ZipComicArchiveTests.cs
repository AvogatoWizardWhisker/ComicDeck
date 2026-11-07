using System;
using System.Collections.Generic;
using System.Text;
using ComicDeck.Core.Archives;
using System.IO.Compression;

namespace ComicDeck.Core.Tests
{
    public sealed class ZipComicArchiveTests : IDisposable
    {
        private readonly string _dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ComicDeckTests", Guid.NewGuid().ToString("N"))).FullName;

        public void Dispose() => Directory.Delete(_dir, recursive:true);

        private string CreateZip(params (string name, string content)[] entries)
        {
            var path = Path.Combine(_dir, $"{Guid.NewGuid():N}.cbz");
            using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
            foreach (var (name, content) in entries)
            {
                var entry = zip.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(content);
            }
            return path;
        }

        [Fact]
        public void Pages_are_filtered_and_naturally_sorted()
        {
            var path = CreateZip(
            ("page10.jpg", "10"), ("page2.jpg", "2"), ("page1.jpg", "1"),
            ("__MACOSX/page1.jpg", "junk"), ("Thumbs.db", "junk"),
            (".DS_Store", "junk"), ("ComicInfo.xml", "<ComicInfo/>"));

            using var archive = ZipComicArchive.Open(path);

            Assert.Equal(new[] { "page1.jpg", "page2.jpg", "page10.jpg" }, archive.Pages.Select(p => p.EntryName));
        }

        [Fact]
        public async Task OpenPageAsync_returns_the_right_bytes()
        {
            var path = CreateZip(("a1.jpg", "first"), ("a2.jpg", "second"));
            using var archive = ZipComicArchive.Open(path);

            await using var stream = await archive.OpenPageAsync(1, TestContext.Current.CancellationToken);
            using var reader = new StreamReader(stream);

            Assert.Equal("second", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task OpenEntryAsync_finds_ComicInfo_and_returns_null_when_missing()
        {
            var path = CreateZip(
                ("p1.jpg", "x"), ("ComicInfo.xml", "<ComicInfo/>"));
            using var archive = ZipComicArchive.Open(path);

            Assert.NotNull(await archive.OpenEntryAsync("ComicInfo.xml", TestContext.Current.CancellationToken));
            Assert.Null(await archive.OpenEntryAsync("missing.xml", TestContext.Current.CancellationToken));
        }

        [Fact]
        public void Corrupt_file_throws_ArchiveException()
        {
            var path = Path.Combine(_dir, "bad.cbz");
            File.WriteAllText(path, "not a zip file");

            var ex = Assert.Throws<ArchiveException>(() => ZipComicArchive.Open(path));
            Assert.Equal(ArchiveErrorKind.Corrupt, ex.Kind);
        }

        [Fact]
        public void Archive_without_images_throws_Empty()
        {
            var path = CreateZip(("readme.txt", "hello"));
            var ex = Assert.Throws<ArchiveException>(() => ZipComicArchive.Open(path));
            Assert.Equal(ArchiveErrorKind.Empty, ex.Kind);
        }

        [Fact]
        public void Missing_file_throws_NotFound()
        {
            var ex = Assert.Throws<ArchiveException>(() => ZipComicArchive.Open(Path.Combine(_dir, "nope.cbz")));
            Assert.Equal(ArchiveErrorKind.NotFound, ex.Kind);
        }
    }
    
}

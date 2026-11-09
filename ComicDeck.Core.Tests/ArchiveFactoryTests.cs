using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Compression;
using ComicDeck.Core.Archives;
using ComicDeck.Core.Models;

namespace ComicDeck.Core.Tests
{
    public sealed class ArchiveFactoryTests : IDisposable
    {
        private readonly string _dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ComicDeckTest", Guid.NewGuid().ToString("N"))).FullName;

        public void Dispose() => Directory.Delete(_dir, recursive: true);

        [Theory]
        [InlineData(new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x00, 0x00 }, ArchiveFormat.Zip)]
        [InlineData(new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x00 }, ArchiveFormat.Rar)]
        [InlineData(new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x01, 0x00 }, ArchiveFormat.Rar)]
        [InlineData(new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C, 0x00, 0x04 }, ArchiveFormat.SevenZip)]
        [InlineData(new byte[] { 0x68, 0x65, 0x6C, 0x6C, 0x6F }, ArchiveFormat.Unknown)]
        [InlineData(new byte[0], ArchiveFormat.Unknown)]
        public void Detects_format_header_bytes(byte[] header, ArchiveFormat expected)
        {
            Assert.Equal(expected, ArchiveFormatDetector.Detect(header));
        }

        [Fact]
        public void Misnamed_zip_still_opens_as_zip()
        {
            var zipPath = Path.Combine(_dir, "real.cbz");
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                using var writer = new StreamWriter(zip.CreateEntry("p1.jpg").Open());
                writer.Write("x");
            }

            var misnamed = Path.Combine(_dir, "actually-a-zip.cbr");
            File.Move(zipPath, misnamed);

            using var archive = new ComicArchiveFactory().Open(misnamed);

            Assert.IsType<ZipComicArchive>(archive);
        }

        [Fact]
        public void Truncated_rar_throws_ArchiveException()
        {
            var path = Path.Combine(_dir, "a.cbr");
            File.WriteAllBytes(path, new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x00, 0x00 });

            Assert.Throws<ArchiveException>(() => new ComicArchiveFactory().Open(path));
        }

        [Fact]
        public void Missing_file_throws_NotFound()
        {
            var ex = Assert.Throws<ArchiveException>(
                () => new ComicArchiveFactory().Open(Path.Combine(_dir, "nope.cbz")));

            Assert.Equal(ArchiveErrorKind.NotFound, ex.Kind);
        }
    }
}

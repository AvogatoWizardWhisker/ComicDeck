using System;
using System.Collections.Generic;
using System.Text;
using ComicDeck.Core.Archives;
using ComicDeck.Core.Metadata;
using ComicDeck.Core.Utilities;

namespace ComicDeck.Core.Tests
{
    public class SharpCompressComicArchiveTests
    {
        private static string Fixture(string name) =>
            Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

        // Adjust the bool to match which of the fixtures contain a ComicInfo.xml.
        // RAR4 has no fixture: WinRAR 7.x can't create one. If a real RAR4 file ever fails, i'll add it here
        public static TheoryData<string, bool> Fixtures => new()
        {
            { "sample.cbr", true },
            { "sample.cb7", true },
            { "sample-solid.cbr", true}
        };

        [Theory, MemberData(nameof(Fixtures))]
        public void Pages_are_found_naturally_sorted(string fixture, bool _)
        {
            using var archive = new ComicArchiveFactory().Open(Fixture(fixture));

            Assert.IsType<SharpCompressComicArchive>(archive);
            Assert.NotEmpty(archive.Pages);

            var names = archive.Pages.Select(p => p.EntryName).ToList();
            Assert.Equal(names.OrderBy(n => n, NaturalStringComparer.Instance), names);
            Assert.DoesNotContain(names, n => n.Contains("ComicInfo", StringComparison.OrdinalIgnoreCase));
        }

        [Theory, MemberData(nameof(Fixtures))]
        public async Task Every_page_can_be_read(string fixture, bool _)
        {
            using var archive = new ComicArchiveFactory().Open(Fixture(fixture));

            foreach (var page in archive.Pages)
            {
                await using var stream = await archive.OpenPageAsync(page.Index);
                Assert.True(stream.CanSeek);
                Assert.True(stream.Length > 0, $"{page.EntryName} came back empty");
            }
        }

        [Theory, MemberData(nameof(Fixtures))]
        public async Task Pages_can_be_read_in_any_order(string fixture, bool _)
        {
            var factory = new ComicArchiveFactory();

            var forward = new List<byte[]>();
            using (var archive = factory.Open(Fixture(fixture)))
                for (var i = 0; i < archive.Pages.Count; i++)
                    forward.Add(await ReadAll(archive, i));

            using var second = factory.Open(Fixture(fixture));
                for (var i = second.Pages.Count - 1; i >= 0; i--) // last page first: the solid-archive case
                    Assert.Equal(forward[i], await ReadAll(second, i));
        }

        [Theory, MemberData(nameof(Fixtures))]
        public async Task ComicInfo_is_found_only_when_present(string fixture, bool hasComicInfo)
        {
            using var archive = new ComicArchiveFactory().Open(Fixture(fixture));

            await using var stream = await archive.OpenEntryAsync("ComicInfo.xml");

            Assert.Equal(hasComicInfo, stream is not null);
        }

        [Fact]
        public async Task Metadata_service_reads_ComicInfo_from_rar()
        {
            using var archive = new ComicArchiveFactory().Open(Fixture("sample.cbr"));

            var m = await new MetadataService().ReadAsync(archive);

            Assert.Equal("Fixture Series", m.Series); // from the xml content suggested
            Assert.Equal(archive.Pages.Count, m.PageCount);
        }

        [Fact]
        public async Task Disposing_a_solid_archive_removes_its_temp_cache()
        {
            var cacheRoot = Path.Combine(Path.GetTempPath(), "ComicDeck", "cache");
            var before = Directory.Exists(cacheRoot) ? Directory.GetDirectories(cacheRoot).Length : 0;

            using (var archive = new ComicArchiveFactory().Open(Fixture("sample-solid.cbr")))
            {
                await using var stream = await archive.OpenPageAsync(archive.Pages.Count - 1);
            }
                

            var after = Directory.Exists(cacheRoot) ? Directory.GetDirectories(cacheRoot).Length : 0;
            Assert.Equal(before, after);
        }

        private static async Task<byte[]> ReadAll(IComicArchive archive, int index)
        {
            await using var stream = await archive.OpenPageAsync(index);
            using var copy = new MemoryStream();
            await stream.CopyToAsync(copy);
            return copy.ToArray();
        }

    }
}

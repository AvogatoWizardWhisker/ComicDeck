using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Compression;
using ComicDeck.Core.Metadata;
using ComicDeck.Core.Archives;

namespace ComicDeck.Core.Tests;

public sealed class MetadataServiceTests : IDisposable
{
    private readonly string _dir = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "ComicDeckTests", Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string CreateZip(string fileName, params (string name, string content)[] entries)
    {
        var path = Path.Combine(_dir, fileName);
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
    public async Task ComicInfo_wins_and_file_name_fills_the_gaps()
    {
        var path = CreateZip("Batman 012 (2016).cbz",
            ("p1.jpg", "x"), ("p2.jpg", "x"),
            ("ComicInfo.xml", "<ComicInfo><Series>Detective</Series><Number>3</Number><PageCount>99</PageCount></ComicInfo>"));

        using var archive = ZipComicArchive.Open(path);
        var m = await new MetadataService().ReadAsync(archive, TestContext.Current.CancellationToken);

        Assert.Equal("Detective", m.Series);   // from XML, not the file name
        Assert.Equal("3", m.Number);
        Assert.Equal(2016, m.Year);            // XML has no year, so the file name supplies it
        Assert.Equal(2, m.PageCount);          // the archive wins over the XML's 99
    }

    [Fact]
    public async Task Falls_back_to_file_name_without_ComicInfo()
    {
        var path = CreateZip("Saga 007 (2012).cbz", ("p1.jpg", "x"));

        using var archive = ZipComicArchive.Open(path);
        var m = await new MetadataService().ReadAsync(archive, TestContext.Current.CancellationToken);

        Assert.Equal("Saga", m.Series);
        Assert.Equal("7", m.Number);
        Assert.Equal(2012, m.Year);
    }

    [Fact]
    public async Task Broken_xml_falls_back_to_file_name()
    {
        var path = CreateZip("Saga 007.cbz", ("p1.jpg", "x"), ("ComicInfo.xml", "<ComicInfo><oops"));

        using var archive = ZipComicArchive.Open(path);
        var m = await new MetadataService().ReadAsync(archive, TestContext.Current.CancellationToken);

        Assert.Equal("Saga", m.Series);
    }
}

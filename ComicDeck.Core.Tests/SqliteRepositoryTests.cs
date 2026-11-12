using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Compression;
using ComicDeck.Core.Archives;
using ComicDeck.Core.Data;
using ComicDeck.Core.Library;
using ComicDeck.Core.Metadata;
using ComicDeck.Core.Models;

namespace ComicDeck.Core.Tests;

public sealed class SqliteRepositoryTests : IAsyncLifetime
{
    private readonly string _dir = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "ComicDeckTests", Guid.NewGuid().ToString("N"))).FullName;

    private ComicDatabase _db = null!;
    private SqliteComicRepository _repo = null!;
    private SqliteLibraryFolderRepository _folders = null!;

    public async ValueTask InitializeAsync()
    {
        _db = await ComicDatabase.OpenAsync(Path.Combine(_dir, "test.db"));
        _repo = new SqliteComicRepository(_db);
        _folders = new SqliteLibraryFolderRepository(_db);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        Directory.Delete(_dir, recursive: true);
    }

    private static Comic NewComic(string path) => new()
    {
        FilePath = path,
        FileSize = 1234,
        FileModifiedUtc = new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc),
        Format = ArchiveFormat.Zip,
        PageCount = 20,
        AddedUtc = new DateTime(2026, 11, 6, 8, 0, 0, DateTimeKind.Utc)
    };

    [Fact]
    public async Task Saved_comic_gets_an_id_and_round_trips()
    {
        var comic = NewComic(@"D:\Comics\Batman 001.cbz");
        comic.Title = "Court of Owls";
        comic.Year = 2016;
        comic.ReadingDirection = ReadingDirection.RightToLeft;
        comic.IsFavorite = true;

        await _repo.SaveComicAsync(comic, TestContext.Current.CancellationToken);
        Assert.NotEqual(0, comic.Id);

        var loaded = Assert.Single(await _repo.GetComicsInFolderAsync(@"D:\Comics", TestContext.Current.CancellationToken));
        Assert.Equal(comic.Id, loaded.Id);
        Assert.Equal("Court of Owls", loaded.Title);
        Assert.Equal(2016, loaded.Year);
        Assert.Equal(ReadingDirection.RightToLeft, loaded.ReadingDirection);
        Assert.True(loaded.IsFavorite);
        Assert.Equal(ArchiveFormat.Zip, loaded.Format);
        Assert.Equal(comic.FileModifiedUtc, loaded.FileModifiedUtc);   // ticks keep full precision
        Assert.Null(loaded.Number);
        Assert.Null(loaded.SeriesId);
    }

    [Fact]
    public async Task Saving_an_existing_comic_updates_instead_of_inserting()
    {
        var comic = NewComic(@"D:\Comics\a.cbz");
        await _repo.SaveComicAsync(comic, TestContext.Current.CancellationToken);

        comic.PageCount = 99;
        await _repo.SaveComicAsync(comic, TestContext.Current.CancellationToken);

        var loaded = Assert.Single(await _repo.GetComicsInFolderAsync(@"D:\Comics", TestContext.Current.CancellationToken));
        Assert.Equal(99, loaded.PageCount);
    }

    [Fact]
    public async Task Folder_query_respects_directory_boundary_and_ignores_case()
    {
        await _repo.SaveComicAsync(NewComic(@"D:\Comics\a.cbz"), TestContext.Current.CancellationToken);
        await _repo.SaveComicAsync(NewComic(@"D:\Comics\Sub\b.cbz"), TestContext.Current.CancellationToken);
        await _repo.SaveComicAsync(NewComic(@"D:\Comics2\c.cbz"), TestContext.Current.CancellationToken);

        var result = await _repo.GetComicsInFolderAsync(@"d:\comics", TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, c => c.FilePath.Contains("Comics2"));
    }

    [Fact]
    public async Task Folder_query_handles_like_wildcards_in_paths()
    {
        await _repo.SaveComicAsync(NewComic(@"D:\100%_Comics\a.cbz"), TestContext.Current.CancellationToken);
        await _repo.SaveComicAsync(NewComic(@"D:\100x\b.cbz"), TestContext.Current.CancellationToken);

        var result = await _repo.GetComicsInFolderAsync(@"D:\100%_Comics", TestContext.Current.CancellationToken);

        Assert.Single(result);
    }

    [Fact]
    public async Task Series_is_created_once_regardless_of_case()
    {
        var first = await _repo.GetOrCreateSeriesAsync("Batman", "DC", 2016, TestContext.Current.CancellationToken);
        var second = await _repo.GetOrCreateSeriesAsync("BATMAN", null, null, TestContext.Current.CancellationToken);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("Batman", second.Name);
    }

    [Fact]
    public async Task Removing_comics_also_removes_their_progress()
    {
        var keep = NewComic(@"D:\Comics\keep.cbz");
        var drop = NewComic(@"D:\Comics\drop.cbz");
        await _repo.SaveComicAsync(keep, TestContext.Current.CancellationToken);
        await _repo.SaveComicAsync(drop, TestContext.Current.CancellationToken);
        await _repo.SaveProgressAsync(new ReadingProgress { ComicId = keep.Id, CurrentPage = 3 }, TestContext.Current.CancellationToken);
        await _repo.SaveProgressAsync(new ReadingProgress { ComicId = drop.Id, CurrentPage = 5 }, TestContext.Current.CancellationToken);

        await _repo.RemoveComicsAsync(new[] { drop.Id }, TestContext.Current.CancellationToken);

        Assert.Single(await _repo.GetComicsInFolderAsync(@"D:\Comics", TestContext.Current.CancellationToken));
        Assert.NotNull(await _repo.GetProgressAsync(keep.Id, TestContext.Current.CancellationToken));
        Assert.Null(await _repo.GetProgressAsync(drop.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Removing_more_ids_than_one_chunk_works()
    {
        var ids = new List<int>();
        for (var i = 0; i < 1200; i++)
        {
            var c = NewComic($@"D:\Comics\{i}.cbz");
            await _repo.SaveComicAsync(c, TestContext.Current.CancellationToken);
            ids.Add(c.Id);
        }

        await _repo.RemoveComicsAsync(ids, TestContext.Current.CancellationToken);

        Assert.Empty(await _repo.GetComicsInFolderAsync(@"D:\Comics", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Progress_is_inserted_then_updated()
    {
        var comic = NewComic(@"D:\Comics\a.cbz");
        await _repo.SaveComicAsync(comic, TestContext.Current.CancellationToken);

        var progress = new ReadingProgress { ComicId = comic.Id };
        progress.UpdatePage(5, 20, DateTime.UtcNow);
        await _repo.SaveProgressAsync(progress, TestContext.Current.CancellationToken);
        Assert.NotEqual(0, progress.Id);

        progress.UpdatePage(19, 20, DateTime.UtcNow);
        await _repo.SaveProgressAsync(progress, TestContext.Current.CancellationToken);

        var loaded = await _repo.GetProgressAsync(comic.Id, TestContext.Current.CancellationToken);
        Assert.Equal(19, loaded!.CurrentPage);
        Assert.Equal(ReadStatus.Completed, loaded.Status);
    }

    [Fact]
    public async Task Adding_the_same_folder_twice_returns_the_same_entry()
    {
        var first = await _folders.AddFolderAsync(@"D:\Comics", TestContext.Current.CancellationToken);
        var second = await _folders.AddFolderAsync(@"d:\comics\", TestContext.Current.CancellationToken);

        Assert.Equal(first.Id, second.Id);
        Assert.Single(await _folders.GetFoldersAsync(TestContext.Current.CancellationToken));

        await _folders.RemoveFolderAsync(first.Id, TestContext.Current.CancellationToken);
        Assert.Empty(await _folders.GetFoldersAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Scanner_works_against_the_real_database()
    {
        var root = Path.Combine(_dir, "library");
        Directory.CreateDirectory(root);
        foreach (var name in new[] { "Batman 001.cbz", "Batman 002.cbz" })
        {
            using var zip = ZipFile.Open(Path.Combine(root, name), ZipArchiveMode.Create);
            using var writer = new StreamWriter(zip.CreateEntry("p1.jpg").Open());
            writer.Write("x");
        }

        var scanner = new LibraryScanner(_repo, new ComicArchiveFactory(), new MetadataService());

        var first = await scanner.ScanAsync(root, cancellationToken: TestContext.Current.CancellationToken);
        var second = await scanner.ScanAsync(root, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, first.Added);
        Assert.Equal(2, second.Unchanged);   // proves timestamps survive the database round trip
        Assert.Equal(0, second.Updated);
    }

    [Fact]
    public async Task Library_items_joins_series_and_progress()
    {
        var series = await _repo.GetOrCreateSeriesAsync("Batman", "DC", 2016, TestContext.Current.CancellationToken);
        var withSeries = NewComic(@"D:\Comics\Batman 001.cbz");
        withSeries.SeriesId = series.Id;
        withSeries.Number = "1";
        var standalone = NewComic(@"D:\Comics\Standalone.cbz");
        standalone.Title = "Watchmen";
        await _repo.SaveComicAsync(withSeries, TestContext.Current.CancellationToken);
        await _repo.SaveComicAsync(standalone, TestContext.Current.CancellationToken);

        var progress = new ReadingProgress { ComicId = withSeries.Id };
        progress.UpdatePage(5, 20, DateTime.UtcNow);
        await _repo.SaveProgressAsync(progress, TestContext.Current.CancellationToken);

        var items = await _repo.GetLibraryItemsAsync(TestContext.Current.CancellationToken);

        var a = items.Single(i => i.Id == withSeries.Id);
        Assert.Equal("Batman", a.SeriesName);
        Assert.Equal("1", a.Number);
        Assert.Equal(5, a.CurrentPage);
        Assert.Equal(ReadStatus.InProgress, a.Status);
        Assert.Equal("Batman 001", a.DisplayTitle);

        var b = items.Single(i => i.Id == standalone.Id);
        Assert.Null(b.SeriesName);
        Assert.Equal(ReadStatus.Unread, b.Status);
        Assert.Equal(0, b.CurrentPage);
        Assert.Equal("Watchmen", b.DisplayTitle);
    }

}


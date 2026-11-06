using System;
using System.Collections.Generic;
using System.Text;
using ComicDeck.Core.Models;

namespace ComicDeck.Core.Tests;

public class ReadingProgressTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Middle_page_marks_InProgress()
    {
        var p = new ReadingProgress();
        p.UpdatePage(5, 20, Now);

        Assert.Equal(ReadStatus.InProgress, p.Status);
        Assert.Equal(5, p.CurrentPage);
        Assert.Equal(Now, p.LastReadUtc);
    }

    [Fact]
    public void First_page_alone_stays_Unread()
    {
        var p = new ReadingProgress();
        p.UpdatePage(0, 20, Now);

        Assert.Equal(ReadStatus.Unread, p.Status);
    }

    [Fact]
    public void Last_page_marks_Completed_and_sets_date_once()
    {
        var p = new ReadingProgress();
        p.UpdatePage(19, 20, Now);
        var later = Now.AddHours(1);
        p.UpdatePage(19, 20, later);

        Assert.Equal(ReadStatus.Completed, p.Status);
        Assert.Equal(Now, p.CompletedUtc);   // not overwritten by the second update
    }

    [Fact]
    public void Page_index_is_clamped()
    {
        var p = new ReadingProgress();
        p.UpdatePage(999, 20, Now);
        Assert.Equal(19, p.CurrentPage);

        p.UpdatePage(-5, 20, Now);
        Assert.Equal(0, p.CurrentPage);
    }

    [Fact]
    public void MarkUnread_resets_everything()
    {
        var p = new ReadingProgress();
        p.UpdatePage(19, 20, Now);
        p.MarkUnread();

        Assert.Equal(ReadStatus.Unread, p.Status);
        Assert.Equal(0, p.CurrentPage);
        Assert.Null(p.LastReadUtc);
        Assert.Null(p.CompletedUtc);
    }

    [Theory]
    [InlineData(0, 11, 0.0)]
    [InlineData(5, 11, 0.5)]
    [InlineData(10, 11, 1.0)]
    public void Fraction_is_proportional(int page, int count, double expected)
    {
        var p = new ReadingProgress { CurrentPage = page };
        Assert.Equal(expected, p.GetFraction(count), precision: 5);
    }

    [Fact]
    public void DisplayTitle_falls_back_to_file_name()
    {
        var comic = new Comic { FilePath = @"C:\Comics\Batman 012 (2016).cbz" };
        Assert.Equal("Batman 012 (2016)", comic.DisplayTitle);

        comic.Title = "The Court of Owls";
        Assert.Equal("The Court of Owls", comic.DisplayTitle);
    }
}
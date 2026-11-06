using System;
using System.Collections.Generic;
using System.Text;
using ComicDeck.Core.Metadata;
using ComicDeck.Core.Models;

namespace ComicDeck.Core.Tests;

public class ComicInfoParserTests
{
    private const string Sample = """
        <?xml version="1.0"?>
        <ComicInfo xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
          <Title>The Court of Owls</Title>
          <Series>Batman</Series>
          <Number>12</Number>
          <Count>-1</Count>
          <Volume>2</Volume>
          <Year>2016</Year>
          <Month>13</Month>
          <Writer>Scott Snyder</Writer>
          <Manga>YesAndRightToLeft</Manga>
          <PageCount>22</PageCount>
        </ComicInfo>
        """;

    [Fact]
    public void Parses_common_fields()
    {
        var m = ComicInfoParser.Parse(Sample)!;

        Assert.Equal("The Court of Owls", m.Title);
        Assert.Equal("Batman", m.Series);
        Assert.Equal("12", m.Number);
        Assert.Equal(2, m.Volume);
        Assert.Equal(2016, m.Year);
        Assert.Equal("Scott Snyder", m.Writer);
        Assert.Equal(22, m.PageCount);
    }

    [Fact]
    public void Unknown_and_out_of_range_values_become_null()
    {
        var m = ComicInfoParser.Parse(Sample)!;

        Assert.Null(m.Count);   // -1 means unknown
        Assert.Null(m.Month);   // 13 is not a month
    }

    [Fact]
    public void Manga_flag_maps_to_reading_direction()
    {
        Assert.Equal(ReadingDirection.RightToLeft, ComicInfoParser.Parse(Sample)!.ReadingDirection);
        Assert.Equal(ReadingDirection.LeftToRight,
            ComicInfoParser.Parse("<ComicInfo><Manga>No</Manga></ComicInfo>")!.ReadingDirection);
        Assert.Null(ComicInfoParser.Parse("<ComicInfo><Manga>Yes</Manga></ComicInfo>")!.ReadingDirection);
    }

    [Fact]
    public void Element_names_are_case_insensitive()
    {
        var m = ComicInfoParser.Parse("<ComicInfo><series>Saga</series></ComicInfo>")!;
        Assert.Equal("Saga", m.Series);
    }

    [Theory]
    [InlineData("this is not xml")]
    [InlineData("<Other><Title>x</Title></Other>")]
    public void Unusable_input_returns_null(string xml)
    {
        Assert.Null(ComicInfoParser.Parse(xml));
    }

    [Fact]
    public void Dtd_is_rejected_instead_of_expanded()
    {
        const string xml = """
            <?xml version="1.0"?>
            <!DOCTYPE ComicInfo [<!ENTITY x "boom">]>
            <ComicInfo><Title>&x;</Title></ComicInfo>
            """;

        Assert.Null(ComicInfoParser.Parse(xml));
    }
}

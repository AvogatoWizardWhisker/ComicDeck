using System;
using System.Collections.Generic;
using System.Text;
using ComicDeck.Core.Metadata;

namespace ComicDeck.Core.Tests
{
    public class FileNameParserTests
    {
        [Theory]
        //     file name                          series         number  year  volume title
        [InlineData("Batman 012 (2016).cbz", "Batman", "12", 2016, null, null)]
        [InlineData("Batman v2 012 (2016) (Digital).cbz", "Batman", "12", 2016, 2, null)]
        [InlineData("Batman #5 - The Court of Owls.cbr", "Batman", "5", null, null, "The Court of Owls")]
        [InlineData("Batman - 012 - Zero Year.cbz", "Batman", "12", null, null, "Zero Year")]
        [InlineData("Saga Vol. 3.cbz", "Saga", null, null, 3, null)]
        [InlineData("Spider-Man 12.cbz", "Spider-Man", "12", null, null, null)]
        [InlineData("X_Men_007_(of_12).cbz", "X Men", "7", null, null, null)]
        [InlineData("Batman 12.5.cbz", "Batman", "12.5", null, null, null)]
        [InlineData("2000 AD 1234.cbz", "2000 AD", "1234", null, null, null)]
        [InlineData("Batman Annual 01 [Group].cbz", "Batman Annual", "1", null, null, null)]
        public void Parses_issue_style_names(
            string fileName, string? series, string? number, int? year, int? volume, string? title)
        {
            var m = FileNameParser.Parse(fileName);

            Assert.Equal(series, m.Series);
            Assert.Equal(number, m.Number);
            Assert.Equal(year, m.Year);
            Assert.Equal(volume, m.Volume);
            Assert.Equal(title, m.Title);
        }

        [Fact]
        public void Standalone_name_becomes_the_title()
        {
            var m = FileNameParser.Parse(@"C:\Comics\Watchmen.cbz");

            Assert.Equal("Watchmen", m.Title);
            Assert.Null(m.Series);
            Assert.Null(m.Number);
        }

        [Fact]
        public void Of_group_sets_count()
        {
            Assert.Equal(12, FileNameParser.Parse("X_Men_007_(of_12).cbz").Count);
        }

        [Fact]
        public void Empty_name_gives_empty_metadata()
        {
            Assert.False(FileNameParser.Parse("").HasAnyData);
        }
    }
}

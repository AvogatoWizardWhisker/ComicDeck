using System;
using System.Collections.Generic;
using System.Text;
using ComicDeck.Core.Library;

namespace ComicDeck.Core.Tests
{
    public class LibraryGroupingTests
    {
        [Theory]
        [InlineData("Batman", "B")]
        [InlineData("batman", "B")]
        [InlineData("École", "E")]
        [InlineData("'Tis the season", "T")]
        [InlineData("3 Body Problem", "#")]
        [InlineData("名探偵コナン", "#")]
        [InlineData("", "#")]
        [InlineData(null, "#")]
        public void Picks_the_group_letter(string? text, string expected)
        {
            Assert.Equal(expected, LibraryGrouping.LetterOf(text));
        }

        [Fact]
        public void Series_sort_uses_the_series_name_and_title_sort_uses_the_title()
        {
            var item = new ComicListItem { FilePath = @"D:\x\Zed.cbz", Title = "Zed", SeriesName = "Alpha" };

            Assert.Equal("A", LibraryGrouping.KeyOf(item, LibrarySort.Series));
            Assert.Equal("Z", LibraryGrouping.KeyOf(item, LibrarySort.Title));
        }

        [Fact]
        public void Groups_come_out_in_key_order_and_items_keep_their_order()
        {
            var words = new[] { "Beta", "9Lives", "Alpha", "Bravo", "Avatar" };

            var groups = LibraryGrouping.Group(words, LibraryGrouping.LetterOf, reverse: false);

            Assert.Equal(new[] { "#", "A", "B" }, groups.Select(g => g.Key));
            Assert.Equal(new[] { "Alpha", "Avatar" }, groups[1].Items);
            Assert.Equal(new[] { "Beta", "Bravo" }, groups[2].Items);
        }

        [Fact]
        public void Reverse_flips_the_group_order()
        {
            var groups = LibraryGrouping.Group(new[] { "Bravo", "Beta", "Alpha", "9Lives" }, LibraryGrouping.LetterOf, reverse: true);

            Assert.Equal(new[] { "B", "A", "#" }, groups.Select(g => g.Key));
            Assert.Equal(new[] { "Bravo", "Beta" }, groups[0].Items); // incoming order is kept
        }

        [Fact]
        public void The_jump_list_has_27_kzys_starting_with_hash()
        {
            Assert.Equal(27, LibraryGrouping.AllKeys.Count);
            Assert.Equal("#", LibraryGrouping.AllKeys[0]);
            Assert.Equal("Z", LibraryGrouping.AllKeys[^1]);
        }
    }
}

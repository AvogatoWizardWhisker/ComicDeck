using System;
using System.Collections.Generic;
using System.Text;
using ComicDeck.Core.Library;
using ComicDeck.Core.Models;

namespace ComicDeck.Core.Tests
{
    public class LibraryQueryTests
    {
        private static ComicListItem Item(int id, string? series, string? number, string? title = null, ReadStatus status = ReadStatus.Unread, bool fav = false, int addedDay = 1, int? readDay = null) => new()
        {
            Id = id,
            FilePath = $@"D:\Comics\file{id}.cbz",
            SeriesName = series,
            Number = number,
            Title = title,
            Status = status,
            IsFavorite = fav,
            AddedUtc = new DateTime(2026, 11, addedDay, 0, 0, 0, DateTimeKind.Utc),
            LastReadUtc = readDay is int d ? new DateTime(2026, 11, d, 0, 0, 0, DateTimeKind.Utc) : null
        };

        private static readonly ComicListItem[] Library =
        {
            Item(1, "Batman", "10"),
            Item(2, "Batman", "2", status: ReadStatus.InProgress, readDay: 5, addedDay: 3),
            Item(3, "Saga", "1", fav: true, addedDay: 2, status: ReadStatus.Completed, readDay: 9),
            Item(4, null, null, title: "Watchmen", addedDay: 4)
        };

        private static IEnumerable<int> Ids(LibraryQuery q) => LibraryQuery.Apply(Library, q).Select(i => i.Id);

        [Fact]
        public void Default_sort_is_series_then_natural_issue_number()
        {
            // Batman 2, Batman 10, Saga 1, Watchmen
            Assert.Equal(new[] { 2, 1, 3, 4 }, Ids(new LibraryQuery()));
        }

        [Fact]
        public void Reserve_flips_the_order()
        {
            Assert.Equal(new[] { 4, 3, 1, 2 }, Ids(new LibraryQuery(Reverse: true)));
        }

        [Fact]
        public void Search_requires_every_word_and_ignores_case()
        {
            Assert.Equal(new[] { 1 }, Ids(new LibraryQuery("BATMAN 10")));
            Assert.Equal(new[] { 4 }, Ids(new LibraryQuery("watch")));
            Assert.Empty(Ids(new LibraryQuery("batman saga")));
        }

        [Fact]
        public void Search_also_matches_the_file_name()
        {
            Assert.Equal(new[] { 3 }, Ids(new LibraryQuery("file3")));
        }

        [Theory]
        [InlineData(LibraryFilter.Unread, new[] { 1,4 })]
        [InlineData(LibraryFilter.InProgress, new[] { 2 })]
        [InlineData(LibraryFilter.Completed, new[] { 3 })]
        [InlineData(LibraryFilter.Favorites, new[] { 3})]
        public void Filters_by_state(LibraryFilter filter, int[] expected)
        {
            Assert.Equal(expected, Ids(new LibraryQuery(Filter: filter)).Order());
        }

        [Fact]
        public void Recently_added_puts_newest_first()
        {
            Assert.Equal(new[] { 4, 2, 3, 1 }, Ids(new LibraryQuery(Sort: LibrarySort.RecentlyAdded)));
        }

        [Fact]
        public void Recently_read_puts_never_read_comics_first()
        {
            var ids = Ids(new LibraryQuery(Sort: LibrarySort.RecentlyRead)).ToList();
            Assert.Equal(new[] { 3, 2 }, ids.Take(2));
        }

        [Fact]
        public void Search_filter_combine()
        {
            Assert.Equal(new[] { 2 }, Ids(new LibraryQuery("batman", Filter: LibraryFilter.InProgress)));
        }
    }
}

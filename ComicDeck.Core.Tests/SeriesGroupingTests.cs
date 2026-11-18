using System;
using System.Collections.Generic;
using System.Text;
using ComicDeck.Core.Library;
using ComicDeck.Core.Models;

namespace ComicDeck.Core.Tests
{
    public class SeriesGroupingTests
    {
        private static ComicListItem I(int id, string? series, string? number, ReadStatus status = ReadStatus.Unread, int? readDay = null) => new()
        {
            Id = id, FilePath = $@"D:\Comics\{id}.cbz", SeriesName = series, Number = number, Status = status, LastReadUtc = readDay is int d ? new DateTime(2026, 11, d, 0, 0, 0, DateTimeKind.Utc) : null
        };

        [Fact]
        public void Groups_by_series_ignoring_case_and_skips_comics_without_one()
        {
            var result = SeriesGrouping.Build(new[]
            {
                I(1, "Batman", "1"), I(2, "BATMAN", "2"), I(3, "Saga", "1"), I(4, null, null)
            });

            Assert.Equal(2, result.Count);
            Assert.Equal(2, result.Single(s => s.Name.Equals("Batman", StringComparison.OrdinalIgnoreCase)).Count);
        }

        [Fact]
        public void Issues_use_natural_order()
        {
            var issues = SeriesGrouping.IssuesOf(
                new[] { I(1, "Batman", "10"), I(2, "Batman", "2"), I(3, "Saga", "1") }, "batman");

            Assert.Equal(new[] { 2, 1 }, issues.Select(i => i.Id));
        }

        [Fact]
        public void Next_prefers_the_latest_in_progress_issue()
        {
            var s = SeriesGrouping.Build(new[]
            {
                I(1, "A", "1", ReadStatus.Completed, 1),
                I(2, "A", "2", ReadStatus.InProgress, 3),
                I(3, "A", "3", ReadStatus.InProgress, 8),
                I(4, "A", "4")
            }).Single();

            Assert.Equal(3, s.Next.Id);
            Assert.Equal((1, 2, 1), (s.Completed, s.InProgress, s.Unread));
        }

        [Fact]
        public void Next_falls_back_to_first_unread_then_first_issue()
        {
            var unread = SeriesGrouping.Build(new[]
            {I(1, "A", "1", ReadStatus.Completed), I(2, "A", "2"), I(3, "A", "3")}).Single();
            Assert.Equal(2, unread.Next.Id);

            var finished = SeriesGrouping.Build(new[]
            {I(1, "A", "1", ReadStatus.Completed), I(2, "A", "2", ReadStatus.Completed)}).Single();
            Assert.Equal(1, finished.Next.Id);
        }

        [Fact]
        public void Names_differing_only_in_punctuation_are_one_series()
        {
            var result = SeriesGrouping.Build(new[]
            {
                I(1, "Sonic the Hedgehog - The IDW Collection", "1"),
                I(2, "Sonic The Hedgehog: The IDW Collection", "2"),
                I(3, "Sonic the Hedgehog - The IDW Collection", "3")
            });

            var series = Assert.Single(result);
            Assert.Equal(3, series.Count);
            Assert.Equal("Sonic the Hedgehog - The IDW Collection", series.Name);
        }
    }
}

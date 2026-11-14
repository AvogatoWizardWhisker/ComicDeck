using System;
using System.Collections.Generic;
using System.Reflection.PortableExecutable;
using System.Text;
using ComicDeck.Core.Models;
using ComicDeck.Core.Utilities;

namespace ComicDeck.Core.Tests
{
    public class SpreadPlannerTests
    {
        private static bool None(int _) => false;

        private static SpreadView At(int start, bool cover = true, Func<int, bool>? wide = null, bool dbl = true) =>
            SpreadPlanner.ViewAt(start, 10, dbl, cover, wide ?? None);

        private static SpreadView Before(int start, bool cover = true, Func<int, bool>? wide = null, bool dbl = true) =>
            SpreadPlanner.ViewBefore(start, 10, dbl, cover, wide ?? None);

        [Fact]
        public void Comic_is_shown_alone()
        {
            Assert.Equal(new SpreadView(0, 1), At(0));
            Assert.Equal(new SpreadView(0, 2), At(0, cover: false));
        }

        [Fact]
        public void Portrait_pages_are_paired()
        {
            Assert.Equal(new SpreadView(1, 2), At(1));
            Assert.Equal(new SpreadView(8, 2), At(8));
        }

        [Fact]
        public void Last_page_is_alone()
        {
            Assert.Equal(new SpreadView(9, 1), At(9));
        }

        [Fact]
        public void Landscape_page_is_alone_and_breaks_the_pair()
        {
            Assert.Equal(new SpreadView(3, 1), At(3, wide: i => i == 3));
            Assert.Equal(new SpreadView(2, 1), At(2, wide: i => i == 3));
        }

        [Fact]
        public void Single_page_mode_never_pairs()
        {
            Assert.Equal(new SpreadView(1, 1), At(1, dbl: false));
            Assert.Equal(new SpreadView(4, 1), Before(5, dbl: false));
        }

        [Theory]
        [InlineData(5, 3, 2)]
        [InlineData(3, 1, 2)]
        [InlineData(2, 1, 1)]   // page 0 is the cover, so 1 stands alone
        [InlineData(1, 0, 1)]
        [InlineData(10, 8, 2)]  // the last view
        public void Going_back_pairs_portrait_pages(int from, int expectedStart, int expectedCount)
        {
            Assert.Equal(new SpreadView(expectedStart, expectedCount), Before(from));
        }

        [Fact]
        public void Going_back_over_a_landscape_page_shows_it_alone()
        {
            Assert.Equal(new SpreadView(3, 1), Before(4, wide: i => i == 3));
            Assert.Equal(new SpreadView(3, 1), Before(4, wide: i => i == 2));
        }

        [Fact]
        public void Fit_modes_choose_the_decode_box()
        {
            Assert.Equal(new DecodeRequest(1024, 768), DecodeRequest.ForFit(FitMode.FitPage, 1000, 700, 1.0));
            Assert.Equal(new DecodeRequest(1024, 0), DecodeRequest.ForFit(FitMode.FitWidth, 1000, 700, 1.0));
            Assert.Equal(new DecodeRequest(0, 768), DecodeRequest.ForFit(FitMode.FitHeight, 1000, 700, 1.0));
            Assert.Equal(DecodeRequest.Unlimited, DecodeRequest.ForFit(FitMode.OriginalSize, 1000, 700, 1.0));

        }
    }
}

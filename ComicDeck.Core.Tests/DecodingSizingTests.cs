using System;
using System.Collections.Generic;
using System.Text;
using ComicDeck.Core.Utilities;

namespace ComicDeck.Core.Tests
{
    public class DecodingSizingTests
    {
        [Theory]
        //          natW   natH    maxW  maxH    expected
        [InlineData(2000, 3000, 0,    0,    null)]   // no limit
        [InlineData(2000, 3000, 0, 4000, null)]   // never upscale
        [InlineData(2000, 3000, 0, 1500, 1500)]   // height-limited
        [InlineData(2000, 3000, 500, 0, 750)]    // width-limited: scale 0.25
        [InlineData(2000, 3000, 1000, 1000, 1000)]   // both limits: the tighter one wins
        [InlineData(0, 0, 100, 100, null)]   // unknown size
        public void Chooses_decode_height(int w, int h, int maxW, int maxH, int? expected)
        {
            Assert.Equal(expected, DecodeSizing.ChooseDecodeHeight(w, h, new DecodeRequest(maxW, maxH)));
        }

        [Fact]
        public void Viewport_is_rounded_up_to_128_pixel_steps()
        {
            var request = DecodeRequest.ForViewport(1000, 700, rasterizationScale: 1.5);

            Assert.Equal(new DecodeRequest(1536, 1152), request);
        }

        [Fact]
        public void Headroom_asks_for_more_resolution()
        {
            var request = DecodeRequest.ForViewport(600, 400, rasterizationScale: 1.0, headroom: 2.0);

            Assert.Equal(new DecodeRequest(1280, 896), request);
        }

        [Fact]
        public void Non_positive_sizes_mean_no_limit()
        {
            Assert.Equal(new DecodeRequest(0, 768), DecodeRequest.ForViewport(0, 700, 1.0));
        }
    }
}

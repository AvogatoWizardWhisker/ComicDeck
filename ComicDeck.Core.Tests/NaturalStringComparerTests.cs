using System;
using System.Collections.Generic;
using System.Text;
using ComicDeck.Core.Utilities;

namespace ComicDeck.Core.Tests
{
    public class NaturalStringComparerTests
    {
        [Fact]
        public void Sort_numbers_by_value_not_text()
        {
            var input = new[] { "page10.jpg", "page2.jpg", "page1.jpg" };
            var sorted = input.OrderBy(s => s, NaturalStringComparer.Instance).ToArray();
            Assert.Equal(new[] { "page1.jpg", "page2.jpg", "page10.jpg" }, sorted);
        }

        [Theory]
        [InlineData("a0001", "a1", 0)]  // leading zeros are equal in value
        [InlineData("a2", "a10", -1)]
        [InlineData("A1", "a1", 0)]  // case-insensitive
        [InlineData("99999999999999999999", "100000000000000000000", -1)] // no overflow
        public void Compares_correctly(string x, string y, int expectedSign)
        {
            var result = NaturalStringComparer.Instance.Compare(x, y);
            Assert.Equal(expectedSign, Math.Sign(result));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace ComicDeck.Core.Utilities
{
    public sealed class NaturalStringComparer : IComparer<string>
    {
        public static readonly NaturalStringComparer Instance = new();
        
        public int Compare(string? x, string? y)
        {
            // Implementation for natural string comparison
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;

            int i = 0, j = 0;
            while (i < x.Length && j < y.Length)
            {
                if (char.IsAsciiDigit(x[i]) && char.IsAsciiDigit(y[j]))
                {
                    int startX = i, startY = j;
                    while (i < x.Length && char.IsAsciiDigit(x[i])) i++;
                    while (j < y.Length && char.IsAsciiDigit(y[j])) j++;
                    
                    var numX = x.AsSpan(startX, i - startX).TrimStart('0');
                    var numY = y.AsSpan(startY, j - startY).TrimStart('0');

                    // More digits means a larger number (after trimming zeroes)
                    if (numX.Length != numY.Length)
                        return numX.Length.CompareTo(numY.Length);

                    int cmp = numX.CompareTo(numY, StringComparison.Ordinal);
                    if (cmp != 0) return cmp;
                }
                else
                {
                    // Compare characters
                    int cmp = char.ToUpperInvariant(x[i]).CompareTo(char.ToUpperInvariant(y[j]));
                    if (cmp != 0) return cmp;
                    i++;
                    j++;
                }
            }

            // Shorter strings come first
            return (x.Length - i).CompareTo(y.Length - j);
            {

            }
        }
    }
}

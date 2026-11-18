using System.Globalization;
using System.Text;
using ComicDeck.Core.Models;

namespace ComicDeck.Core.Library;

public sealed record LetterGroup<T>(string Key, IReadOnlyList<T> Items);

public static class LibraryGrouping
{
    public const string Other = "#";

    /// <summary> Every key the jump list shows, in display order.</summary>
    public static readonly IReadOnlyList<string> AllKeys = new[]
    {Other}.Concat(Enumerable.Range('A', 26).Select(c => ((char)c).ToString())).ToList();

    public static bool IsAlphabetical(LibrarySort sort) => sort is LibrarySort.Series or LibrarySort.Title;

    /// <summary> The group a comic belongs to, using the same text sort uses.</summary>
    public static string KeyOf(ComicListItem item, LibrarySort sort) => LetterOf(sort == LibrarySort.Series ? item.SeriesName ?? item.DisplayTitle : item.DisplayTitle);

    /// <summary> "A" to "Z" for the first letter (accents ignored), otherwise "#"</summary>
    public static string LetterOf(string? text)
    {
        if (text is null) return Other;

        foreach (var ch in text)
        {
            if (!char.IsLetterOrDigit(ch)) continue; // skip leading quotes, brackets, spaces
            if (char.IsDigit(ch)) return Other;

            var baseChar = char.ToUpperInvariant(ch.ToString().Normalize(NormalizationForm.FormD)[0]);
            return baseChar is >= 'A' and <= 'Z' ? baseChar.ToString() : Other;
        }
        return Other;
    }

    /// <summary>
    /// Groups laready-sorted items by key. Gropus come out in key order (# then A to Z, or the reverse),
    /// and items keep their incoming order inside each group
    /// </summary>
    public static IReadOnlyList<LetterGroup<T>> Group<T>(IEnumerable<T> items, Func<T, string> keyOf, bool reverse)
    {
        var groups = items
            .GroupBy(keyOf)
            .OrderBy(g => g.Key == Other ? 0 : g.Key[0] - 'A' + 1)
            .Select(g => new LetterGroup<T>(g.Key, g.ToList()))
            .ToList();

        if (reverse) groups.Reverse();
        return groups;
    }
}
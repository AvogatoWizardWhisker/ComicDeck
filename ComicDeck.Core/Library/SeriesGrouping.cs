using ComicDeck.Core.Models;
using ComicDeck.Core.Utilities;
using System.Globalization;
using System.Text;

namespace ComicDeck.Core.Library;

public sealed record SeriesSummary(string Name, int Count, int Unread, int InProgress, int Completed, ComicListItem Cover, ComicListItem Next);

public static class SeriesGrouping
{
    public static string KeyOf(string name)
    {
        var decomposed = name.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
        }
        return sb.Length > 0 ? sb.ToString() : name.Trim().ToLowerInvariant();
    }

    public static IReadOnlyList<SeriesSummary> Build(IEnumerable<ComicListItem> items) =>
        items
            .Where(i => !string.IsNullOrWhiteSpace(i.SeriesName))
            .GroupBy(i => KeyOf(i.SeriesName!))
            .Select(g =>
            {
                // Display the spelling most of the files use.
                var name = g.GroupBy(i => i.SeriesName!)
                            .OrderByDescending(x => x.Count())
                            .ThenBy(x => x.Key, StringComparer.Ordinal)
                            .First().Key;

                var issues = IssueOrder(g).ToList();
                var next = issues.Where(i => i.Status == ReadStatus.InProgress)
                                 .OrderByDescending(i => i.LastReadUtc ?? DateTime.MinValue)
                                 .FirstOrDefault()
                           ?? issues.FirstOrDefault(i => i.Status == ReadStatus.Unread)
                           ?? issues[0];

                return new SeriesSummary(
                    name, issues.Count,
                    issues.Count(i => i.Status == ReadStatus.Unread),
                    issues.Count(i => i.Status == ReadStatus.InProgress),
                    issues.Count(i => i.Status == ReadStatus.Completed),
                    issues[0], next);
            })
            .OrderBy(s => s.Name, NaturalStringComparer.Instance)
            .ToList();

    public static IReadOnlyList<ComicListItem> IssuesOf(IEnumerable<ComicListItem> items, string seriesName)
    {
        var key = KeyOf(seriesName);
        return IssueOrder(items.Where(i => i.SeriesName is not null && KeyOf(i.SeriesName) == key)).ToList();
    }

    private static IOrderedEnumerable<ComicListItem> IssueOrder(IEnumerable<ComicListItem> items) =>
        items.OrderBy(i => i.Number ?? string.Empty, NaturalStringComparer.Instance)
             .ThenBy(i => i.DisplayTitle, NaturalStringComparer.Instance);




}
using ComicDeck.Core.Models;
using ComicDeck.Core.Utilities;

namespace ComicDeck.Core.Library;

public enum LibrarySort { Series, Title, RecentlyAdded, RecentlyRead }

public enum LibraryFilter { All, Unread, InProgress, Completed, Favorites }

public sealed record LibraryQuery(string Search = "", LibrarySort Sort = LibrarySort.Series, bool Reverse = false, LibraryFilter Filter = LibraryFilter.All)
{
    public bool IsFiltered => !string.IsNullOrWhiteSpace(Search) || Filter != LibraryFilter.All;

    ///<summary> Filters, searches and sorts. Pute, so it is safe to run on any thread.</summary>
    public static IReadOnlyList<ComicListItem> Apply(IEnumerable<ComicListItem> items, LibraryQuery query)
    {
        var terms = (query.Search ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var matching = items.Where(i => PassesFilter(i, query.Filter) && Matches(i, terms));

        var comparer = NaturalStringComparer.Instance;
        IOrderedEnumerable<ComicListItem> ordered = query.Sort switch
        {
            LibrarySort.Title => matching
            .OrderBy(i => i.DisplayTitle, comparer)
            .ThenBy(i => i.Number ?? string.Empty, comparer),

            LibrarySort.RecentlyAdded => matching
            .OrderByDescending(i => i.AddedUtc)
            .ThenBy(i => i.DisplayTitle, comparer),

            LibrarySort.RecentlyRead => matching
            .OrderByDescending(i => i.LastReadUtc ?? DateTime.MinValue)
            .ThenBy(i => i.DisplayTitle, comparer),

            _ => matching
            .OrderBy(i => i.SeriesName ?? i.DisplayTitle, comparer)
            .ThenBy(i => i.Number ?? string.Empty, comparer)
            .ThenBy(i => i.DisplayTitle, comparer)
        };

        var list = ordered.ToList();
        if (query.Reverse) list.Reverse();
        return list;
    }

    private static bool PassesFilter(ComicListItem i, LibraryFilter filter) => filter switch
    {
        LibraryFilter.Unread => i.Status == ReadStatus.Unread,
        LibraryFilter.InProgress => i.Status == ReadStatus.InProgress,
        LibraryFilter.Completed => i.Status == ReadStatus.Completed,
        LibraryFilter.Favorites => i.IsFavorite,
        _ => true
    };

    private static bool Matches(ComicListItem i, string[] terms)
    {
        if (terms.Length == 0) return true;

        var fileName = Path.GetFileNameWithoutExtension(i.FilePath);
        return terms.All(term =>
        Has(i.DisplayTitle, term) || Has(i.SeriesName, term) || Has(i.Number, term) || Has(fileName, term));
    }

    private static bool Has(string? field, string term) => field is not null && field.Contains(term, StringComparison.OrdinalIgnoreCase);
}
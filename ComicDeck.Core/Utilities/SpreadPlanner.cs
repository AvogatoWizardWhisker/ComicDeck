namespace ComicDeck.Core.Utilities;

/// <summary> A run of one or two pages shown together, starting at <paramref name="Start"/> (O-based).</summary>
public readonly record struct SpreadView(int Start, int Count);

public static class SpreadPlanner
{
    ///<summary> The view that starts at <paramref name="start"/>.</summary>
    public static SpreadView ViewAt(int start, int pageCount, bool doublePage, bool coverAlone, Func<int, bool> isLandscape)
    {
        if (!doublePage || start >= pageCount - 1) return new(start, 1);
        if (coverAlone && start == 0) return new(start, 1);
        if (isLandscape(start) || isLandscape(start + 1)) return new(start, 1);
        return new(start, 2);
    }

    /// <summary>
    /// The view that ends just before <paramref name="start"/>. Pass the page count to get the last view.
    /// Pairing is re-anchored here, so after a jump it may differ slightly from forward pairing.
    /// </summary>
    public static SpreadView ViewBefore(int start, int pageCount, bool doublePage, bool coverAlone, Func<int, bool> isLandscape)
    {
        if (start <= 1) return new(0, 1);

        var previous = start - 1;
        if (!doublePage || isLandscape(previous)) return new(previous, 1);

        var candidate = start - 2;
        return ViewAt(candidate, pageCount, doublePage, coverAlone, isLandscape).Count == 2
            ? new(candidate, 2)
            : new(previous, 1);
    }
}
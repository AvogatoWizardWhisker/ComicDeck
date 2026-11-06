using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;
using ComicDeck.Core.Models;

namespace ComicDeck.Core.Metadata;

public static partial class FileNameParser
{
    public static ComicMetadata Parse(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        if (string.IsNullOrWhiteSpace(name))
            return new ComicMetadata();

        name = name.Replace('_', ' ');

        // 1. Year from "(2016)" and issue count from "(of 12)", before we strip the brackets.
        int? year = null;
        foreach (Match m in YearGroup().Matches(name))
        {
            if (int.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var y)
                && y is >= 1900 and <= 2100)
            {
                year = y;
                break;
            }
        }

        int? count = null;
        var ofMatch = OfGroup().Match(name);
        if (ofMatch.Success)
            count = ParsePositive(ofMatch.Groups[1].Value);

        // 2. Drop every "(...)" and "[...]" group: scanner tags, "(Digital)", "[Group]", etc.
        name = Collapse(BracketGroup().Replace(name, " "));

        // 3. Volume: "v2", "Vol 2", "Vol. 2", "Volume 2".
        int? volume = null;
        var volumeMatch = VolumeToken().Match(name);
        if (volumeMatch.Success)
        {
            volume = ParsePositive(volumeMatch.Groups[1].Value);
            name = Collapse(name[..volumeMatch.Index] + " " + name[(volumeMatch.Index + volumeMatch.Length)..]);
        }

        name = name.Trim(' ', '-');
        if (name.Length == 0)
            return new ComicMetadata { Year = year, Count = count, Volume = volume };

        // 4. "Series [#]Number [- Title]"
        var layout = IssueLayout().Match(name);
        if (layout.Success)
        {
            return new ComicMetadata
            {
                Series = layout.Groups["series"].Value.Trim(' ', '-'),
                Number = NormalizeNumber(layout.Groups["num"].Value),
                Title = layout.Groups["title"].Success ? layout.Groups["title"].Value.Trim() : null,
                Volume = volume,
                Year = year,
                Count = count
            };
        }

        // 5. No issue number. "Saga Vol 3" is a series volume; anything else is a standalone title.
        if (volume is not null)
            return new ComicMetadata { Series = name, Volume = volume, Year = year, Count = count };

        return new ComicMetadata { Title = name, Year = year, Count = count };
    }

    private static string Collapse(string s) => Whitespace().Replace(s, " ").Trim();

    private static int? ParsePositive(string s) =>
        int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n > 0 ? n : null;

    /// <summary>"012" becomes "12", "000" becomes "0", "0.5" stays "0.5".</summary>
    private static string NormalizeNumber(string raw)
    {
        var dot = raw.IndexOf('.');
        var whole = dot < 0 ? raw : raw[..dot];
        var fraction = dot < 0 ? string.Empty : raw[dot..];

        whole = whole.TrimStart('0');
        if (whole.Length == 0) whole = "0";
        return whole + fraction;
    }

    [GeneratedRegex(@"\(\s*(\d{4})\s*\)")]
    private static partial Regex YearGroup();

    [GeneratedRegex(@"\(\s*of\s*(\d+)\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex OfGroup();

    [GeneratedRegex(@"\([^)]*\)|\[[^\]]*\]")]
    private static partial Regex BracketGroup();

    [GeneratedRegex(@"(?<![A-Za-z])(?:vol(?:ume)?\.?\s*|v)(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex VolumeToken();

    // Lazy series match: "2000 AD 1234" gives series "2000 AD", number 1234.
    [GeneratedRegex(@"^(?<series>.+?)(?:\s*#\s*|\s+-\s+|\s+)(?<num>\d+(?:\.\d+)?)(?:\s+-\s+(?<title>.+))?$")]
    private static partial Regex IssueLayout();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
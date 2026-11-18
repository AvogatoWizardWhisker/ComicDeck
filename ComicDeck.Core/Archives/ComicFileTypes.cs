namespace ComicDeck.Core.Archives;

public static class ComicFileTypes
{
    public static readonly IReadOnlyList<string> Extensions = new[] { ".cbz", ".cbr", "cbt", "cb7" };

    public static bool IsComic(string? path) => path is not null && Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
}
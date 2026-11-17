using ComicDeck.Core.Models;

namespace ComicDeck.Core.Library;

public static class FolderRemoval
{
    /// <summary>
    /// Comics that should be deleted when a folder is removed: those not still under on of the folders that remain registered.
    /// </summary>
    public static IReadOnlyList<int> ComicsToRemove(
        IEnumerable<Comic> comicsInRemovedFolder, IEnumerable<string> remainingFolders)
    {
        var prefixes = remainingFolders
            .Select(f => Path.EndsInDirectorySeparator(f) ? f : f + Path.DirectorySeparatorChar)
            .ToList();

        return comicsInRemovedFolder
            .Where(c => !prefixes.Any(p => c.FilePath.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            .Select(c => c.Id)
            .ToList();
    }
}
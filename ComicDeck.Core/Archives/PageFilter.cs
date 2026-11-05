using System;
using System.Collections.Generic;
using System.Text;

namespace ComicDeck.Core.Archives
{
    public static class PageFilter
    {
        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".avif", ".tif", ".tiff"
    };

        public static bool IsPage(string entryName)
        {
            if (string.IsNullOrWhiteSpace(entryName)) return false;

            var normalized = entryName.Replace('\\', '/');
            if (normalized.EndsWith('/')) return false; // directory entry

            var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0) return false;

            // Skip __MACOSX, hidden files and folders (".DS_Store", "._page1.jpg").
            foreach (var segment in segments)
            {
                if (segment.Equals("__MACOSX", StringComparison.OrdinalIgnoreCase)) return false;
                if (segment.StartsWith('.')) return false;
            }

            var fileName = segments[^1];
            if (fileName.Equals("Thumbs.db", StringComparison.OrdinalIgnoreCase)) return false;

            return ImageExtensions.Contains(Path.GetExtension(fileName));
        }
    }
}

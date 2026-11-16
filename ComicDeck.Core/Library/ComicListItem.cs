using System;
using System.Collections.Generic;
using System.Text;
using ComicDeck.Core.Models;

namespace ComicDeck.Core.Library
{
    /// <summary>
    /// The few columns a library card needs, so listing doesn't load whole Comic rows
    /// </summary>
    public class ComicListItem
    {
        public int Id { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string? SeriesName { get; set; }
        public string? Number { get; set; }
        public int PageCount { get; set; }
        public long FileSize { get; set; }
        public DateTime FileModifiedUtc { get; set; }
        public bool IsFavorite { get; set; }
        public int CurrentPage { get; set; }
        public ReadStatus Status { get; set; }
        public DateTime AddedUtc { get; set; }
        public DateTime? LastReadUtc { get; set; }

        public string DisplayTitle => !string.IsNullOrWhiteSpace(Title) ? Title : Path.GetFileNameWithoutExtension(FilePath);
    }
}

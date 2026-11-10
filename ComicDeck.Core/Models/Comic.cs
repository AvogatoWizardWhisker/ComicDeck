using System;
using System.Collections.Generic;
using System.Text;

namespace ComicDeck.Core.Models
{
    public class Comic
    {
        public int Id { get; set; }

        // file identity and change detection
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime FileModifiedUtc { get; set; }
        public ArchiveFormat Format { get; set; }

        // descriptive fields (copied from ComicMetadata or the file name)
        public string? Title { get; set; }
        public int? SeriesId { get; set; }
        public string? Number { get; set; }
        public int? Volume { get; set; }
        public int? Year { get; set; }
        public string? Publisher { get; set; }
        public string? Writer { get; set; }
        public string? Summary { get; set; }
        public ReadingDirection? ReadingDirection { get; set; }

        // dirived from the archive
        public int PageCount { get; set; }
        public string? CoverPath { get; set; }

        // library state
        public bool IsFavorite { get; set; }
        public DateTime AddedUtc { get; set; }

        /// <summary> Title, or the file name when the comic has no title </summary>
        public string DisplayTitle => !string.IsNullOrWhiteSpace(Title) ? Title : Path.GetFileNameWithoutExtension(FilePath);
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace ComicDeck.Core.Models
{
    public sealed record ComicMetadata
    {
        public string? Title { get; init; }
        public string? Series { get; init; }
        public string? Number { get; init; }
        public int? Count { get; init; }    // total issues in the series
        public int? Volume{ get; init; }
        public string? Summary { get; init; }

        public int? Year { get; init; }
        public int? Month { get; init; }
        public int? Day { get; init; }

        public string? Writer { get; init; }
        public string? Penciller { get; init; }
        public string? Inker { get; init; }
        public string? Colorist { get; init; }
        public string? Letterer { get; init; }
        public string? CoverArtist { get; init; }
        public string? Editor { get; init; }

        public string? Publisher { get; init; }
        public string? Imprint { get; init; }
        public string? Genre { get; init; }
        public string? Tags { get; init; }
        public string? LanguageIsoCode { get; init; }
        public string? AgeRating { get; init; }
        public string? Web { get; init; }

        public int? PageCount { get; init; }

        /// <summary>
        /// Null when the file doesn't say, so the user's default applies
        /// </summary>
        /// 

        public ReadingDirection? ReadingDirection { get; init; }
        public bool HasAnyData => Title is not null || Series is not null || Year is not null;
    }
}

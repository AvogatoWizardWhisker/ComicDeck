using System;
using System.Collections.Generic;
using System.Text;

namespace ComicDeck.Core.Models
{
    public class Series
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Publisher { get; set; }
        public int? Year { get; set; }
        public DateTime AddedUtc { get; set; }
    }
}

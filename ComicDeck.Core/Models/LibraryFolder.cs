using System;
using System.Collections.Generic;
using System.Text;
using ComicDeck.Core.Models;

namespace ComicDeck.Core.Models
{
    public class LibraryFolder
    {
        public int Id { get; set; }
        public string Path { get; set; } = string.Empty;
        public DateTime AddedUtc { get; set; }
    }
}

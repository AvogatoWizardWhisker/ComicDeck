using System;
using System.Collections.Generic;
using System.Text;

namespace ComicDeck.Core.Models
{
    public enum ArchiveFormat
    {
        Unknown = 0, 
        Zip = 1,        // .cbz
        Rar = 2,        // .cbr
        SevenZip = 3,   // .cb7
        Tar = 4,        // .cbt
    }
}

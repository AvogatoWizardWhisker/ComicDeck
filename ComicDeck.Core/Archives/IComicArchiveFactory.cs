using System;
using System.Collections.Generic;
using System.Text;
using ComicDeck.Core.Archives;

namespace ComicDeck.Core.Archives
{
    public interface IComicArchiveFactory
    {
        /// <summary>Opens the archive, choosing the implementation by the file content. The caller disposes it.</summary>
        /// <exception cref="ArchiveException">Missing, unsupported, corrupt or empty archive</exception>
        IComicArchive Open(string filePath);
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace ComicDeck.Core.Archives
{
    public sealed record ComicPage(int Index, string EntryName, long Size /*DateTimeOffset? LastModified*/);

    public interface IComicArchive : IDisposable
    {
        string FilePath { get; }

        /// <summary> Image pages only, in reading order. </summary>
        IReadOnlyList<ComicPage> Pages { get; }

        /// <summary> Returns a seekable stream with the page's bytes. The caller disposes it. </summary>
        Task<Stream> OpenPageAsync(int index, CancellationToken cancellationToken = default);

        /// <summary> Opens a non-page entry such as ComicInfo.xml. Returns null if not found. </summary>
        Task<Stream?> OpenEntryAsync(string entryName, CancellationToken cancellationToken = default);
    }
}

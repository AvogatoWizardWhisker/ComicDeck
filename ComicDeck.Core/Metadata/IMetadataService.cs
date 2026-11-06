using System;
using System.Collections.Generic;
using System.Text;
using ComicDeck.Core.Archives;
using ComicDeck.Core.Models;

namespace ComicDeck.Core.Metadata
{
    public interface IMetadataService
    {
        ///<summary>Reads ComicInfo.xml from the archive, falling back to the file name for missing fields</summary>
        Task<ComicMetadata> ReadAsync(IComicArchive archive, CancellationToken cancellationToken = default);
    }
}

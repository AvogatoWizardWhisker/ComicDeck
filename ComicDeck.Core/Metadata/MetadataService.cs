using System;
using System.Collections.Generic;
using System.Text;
using ComicDeck.Core.Archives;
using ComicDeck.Core.Models;

namespace ComicDeck.Core.Metadata
{
    public sealed class MetadataService : IMetadataService
    {
        public const string ComicInfoEntryName = "ComicInfo.xml";

        public async Task<ComicMetadata> ReadAsync(IComicArchive archive, CancellationToken cancellationToken = default)
        {
            var fromName = FileNameParser.Parse(archive.FilePath);

            ComicMetadata? fromXml = null;
            try
            {
                await using var stream = await archive.OpenEntryAsync(ComicInfoEntryName, cancellationToken);
                if (stream is not null)
                    fromXml = ComicInfoParser.Parse(stream);
            }
            catch (ArchiveException)
            {
                // a damaged metadata entry shouldn't stop the comic from opening
            }

            var info = fromXml ?? new ComicMetadata();

            return info with
            {
                Title = info.Title ?? fromName.Title,
                Series = info.Series ?? fromName.Series,
                Number = info.Number ?? fromName.Number,
                Volume = info.Volume ?? fromName.Volume,
                Year = info.Year ?? fromName.Year,
                Count = info.Count ?? fromName.Count,
                PageCount = archive.Pages.Count // trust the archive, not the xml
            };
        }
    }
}

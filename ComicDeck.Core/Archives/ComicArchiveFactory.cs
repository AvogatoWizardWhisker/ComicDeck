using System;
using System.Collections.Generic;
using System.Text;
using ComicDeck.Core.Models;

namespace ComicDeck.Core.Archives
{
    public sealed class ComicArchiveFactory : IComicArchiveFactory
    {
        public IComicArchive Open(string filePath)
        {
            if (!File.Exists(filePath))
                throw new ArchiveException(ArchiveErrorKind.NotFound, $"File not found: {filePath}");

            var format = ArchiveFormatDetector.DetectFromFile(filePath);

            return format switch
            {
                ArchiveFormat.Zip => ZipComicArchive.Open(filePath),

                ArchiveFormat.Rar or ArchiveFormat.SevenZip or ArchiveFormat.Tar =>
                throw new ArchiveException(ArchiveErrorKind.Unsupported, $"{format} archive aren't supported yet."),

                _ => throw new ArchiveException(ArchiveErrorKind.Unsupported, $"Not recongnised comic archive format.")
            };
        }
    }
}

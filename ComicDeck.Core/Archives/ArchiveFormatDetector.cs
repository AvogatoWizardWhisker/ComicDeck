using System;
using System.Collections.Generic;
using System.Text;
using ComicDeck.Core.Models;

namespace ComicDeck.Core.Archives
{
    /// <summary>
    /// Identifies an archive by its leading bytes, not its extension
    /// </summary>
    public static class ArchiveFormatDetector
    {
        private const int HeaderSize = 262; // enough to reach the "ustar" marker at offset 257

        public static ArchiveFormat DetectFromFile(string filePath)
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var header = new byte[HeaderSize];
            var read = stream.ReadAtLeast(header, HeaderSize, throwOnEndOfStream: false);
            return Detect(header.AsSpan(0, read));
        }

        public static ArchiveFormat Detect(ReadOnlySpan<byte> header)
        {
            // ZIP: PH 03 04 (normal), PK 05 06 (empty), PK 07 08 (spanned)
            if (header.Length >= 4 && header[0] == 0x50 && header[1] == 0x4B &&
                ((header[2] == 0x03 && header[3] == 0x04) ||
                (header[2] == 0x05 && header[3] == 0x06) ||
                (header[2] == 0x07 && header[3] == 0x08)))
                return ArchiveFormat.Zip;

            // RAR 4 and RAR 5:"Rar!" 1A 07
            if (header.StartsWith("Rar!"u8) && header.Length >= 6 && header[4] == 0x1A && header[5] == 0x07)
                return ArchiveFormat.Rar;

            // 7z: '7' BC AF 27 1C
            if (header.Length >= 6 && header[0] == 0x37 && header[1] == 0x7A && header[2] == 0xBC && header[3] == 0xAF && header[4] == 0x27 && header[5] == 0x1C)
                return ArchiveFormat.SevenZip;

            // TAR: "ustar" at offset 257
            if (header.Length >= 262 && header.Slice(257, 5).SequenceEqual("ustar"u8))
                return ArchiveFormat.Tar;

            return ArchiveFormat.Unknown;

        }
    }
}

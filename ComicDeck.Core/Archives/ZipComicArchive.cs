using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Compression;
using ComicDeck.Core.Utilities;
using System.Security.Cryptography.X509Certificates;

namespace ComicDeck.Core.Archives
{
    public sealed class ZipComicArchive : IComicArchive
    {
        private readonly ZipArchive _zip;
        private readonly Dictionary<string, ZipArchiveEntry> _entriesByName;
        private readonly List<ZipArchiveEntry> _pageEntries;
        private readonly SemaphoreSlim _gate = new(1, 1); // ZipArchive is not safe for concurrent access, so we need to synchronize access to it.

        public string FilePath { get; }
        public IReadOnlyList<ComicPage> Pages { get; }

        private ZipComicArchive (string filePath, ZipArchive zip)
        {
            FilePath = filePath;
            _zip = zip;

            _entriesByName = new(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in zip.Entries)
                _entriesByName[entry.FullName.Replace('\\', '/')] = entry;

            _pageEntries = zip.Entries
                .Where(e => PageFilter.IsPage(e.FullName))
                .OrderBy(e => e.FullName.Replace('\\', '/'), NaturalStringComparer.Instance)
                .ToList();

            Pages = _pageEntries
                .Select((e, i) => new ComicPage(i, e.FullName, e.Length))
                .ToList();
        }

        public static ZipComicArchive Open(string filePath)
        {
            if (!File.Exists(filePath))
                throw new ArchiveException(ArchiveErrorKind.NotFound, $"File not found: {filePath}");

            FileStream? stream = null;
            try
            {
                stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
                var archive = new ZipComicArchive(filePath, zip);

                if (archive.Pages.Count == 0)
                {
                    archive.Dispose();
                    throw new ArchiveException(ArchiveErrorKind.Empty, $"No image pages found in archive: {filePath}");
                }
                return archive;
            }

            catch (InvalidDataException ex)
            {
                stream?.Dispose();
                throw new ArchiveException(ArchiveErrorKind.Corrupt, $"Invalid or corrupt ZIP archive: {filePath}", ex);
            }
        }

        public async Task<Stream> OpenPageAsync(int index, CancellationToken cancellationToken = default)
        {
            if ((uint)index >= (uint)_pageEntries.Count)
                throw new ArgumentOutOfRangeException(nameof(index), $"Index must be between 0 and {_pageEntries.Count - 1}.");

            return await ReadEntryAsync(_pageEntries[index], cancellationToken);
        }
        public async Task<Stream?> OpenEntryAsync(string entryName, CancellationToken cancellationToken = default)
        {
            if (!_entriesByName.TryGetValue(entryName.Replace('\\', '/'), out var entry))
                return null;

            return await ReadEntryAsync(entry, cancellationToken);
        }

        private async Task<Stream> ReadEntryAsync(ZipArchiveEntry entry, CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try
            {
                var buffer = new MemoryStream(capacity: (int)Math.Min(entry.Length, int.MaxValue));
                try
                {
                    await using var source = entry.Open();
                    await source.CopyToAsync(buffer, ct);
                }
                catch (InvalidDataException ex)
                {
                    buffer.Dispose();
                    throw new ArchiveException(ArchiveErrorKind.Corrupt, $"Invalid or corrupt entry: {entry.FullName}", ex);
                }

                buffer.Position = 0;
                return buffer;
            }
            finally
            {
                _gate.Release();
            }
        }

            public void Dispose()
        {
            _zip.Dispose();
            _gate.Dispose();
        }
    }
    
}


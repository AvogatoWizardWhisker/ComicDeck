using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using ComicDeck.Core.Archives;
using ComicDeck.Core.Metadata;
using ComicDeck.Core.Models;
using ComicDeck.Core.Utilities;

namespace ComicDeck.Core.Library
{
    public sealed class LibraryScanner : ILibraryScanner
    {
        private static readonly HashSet<string> ComicExtension =
            new(StringComparer.OrdinalIgnoreCase) { ".cbz", ".cbr", "cb7", ".cbt" };

        // File systems and databases round timestamps differently, so don't demand an exact match
        private static readonly TimeSpan TimestampTolerance = TimeSpan.FromSeconds(2);

        private enum FileOutcome { Added, Updated, Unchanged }

        private readonly IComicRepository _repository;
        private readonly IComicArchiveFactory _archiveFactory;
        private readonly IMetadataService _metadataService;
        private readonly TimeProvider _time;

        public LibraryScanner(
            IComicRepository repository,
            IComicArchiveFactory archiveFactory,
            IMetadataService metadataService,
            TimeProvider? timeProvider = null)
        {
            _repository = repository;
            _archiveFactory = archiveFactory;
            _metadataService = metadataService;
            _time = timeProvider ?? TimeProvider.System;
        }

        public async Task<ScanResults> ScanAsync(
            string folderPath,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

            folderPath = Path.GetFullPath(folderPath);
            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException($"Library folder not found: {folderPath}");

            var files = await Task.Run(() => EnumerateComicFiles(folderPath), cancellationToken);

            var known = new Dictionary<string, Comic>(StringComparer.OrdinalIgnoreCase);
            foreach (var comic in await _repository.GetComicsInFolderAsync(folderPath, cancellationToken))
                known[comic.FilePath] = comic;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var failures = new List<ScanFailure>();
            int added = 0, updated = 0, unchanged = 0, processed = 0;

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                seen.Add(file);
                known.TryGetValue(file, out var existing);

                try
                {
                    switch (await ProcessFileAsync(file, existing, cancellationToken))
                    {
                        case FileOutcome.Added: added++; break;
                        case FileOutcome.Updated: updated++; break;
                        default: unchanged++; break;
                    }
                }
                catch (Exception ex) when (ex is ArchiveException or IOException or UnauthorizedAccessException)
                {
                    // one bad file must not stop the scan
                    failures.Add(new ScanFailure(file, ex.Message));
                }

                processed++;
                progress?.Report(new ScanProgress(processed, files.Count, file));
            }

            var goneIds = known.Values
                .Where(c => !seen.Contains(c.FilePath))
                .Select(c => c.Id)
                .ToList();

            if (goneIds.Count > 0)
                await _repository.RemoveComicsAsync(goneIds, cancellationToken);

            return new ScanResults(added, updated, unchanged, goneIds.Count, failures);
        }

        private async Task<FileOutcome> ProcessFileAsync(string file, Comic? existing, CancellationToken ct)
        {
            var info = new FileInfo(file);
            var size = info.Length;
            var modifiedUtc = info.LastWriteTimeUtc;

            if (existing is not null
                && existing.FileSize == size
                && (existing.FileModifiedUtc - modifiedUtc).Duration() <= TimestampTolerance)
            {
                return FileOutcome.Unchanged;
            }

            using var archive = await Task.Run(() => _archiveFactory.Open(file), ct);
            var metadata = await _metadataService.ReadAsync(archive, ct);
            var format = ArchiveFormatDetector.DetectFromFile(file);

            Series? series = null;
            if (!string.IsNullOrWhiteSpace(metadata.Series))
                series = await _repository.GetOrCreateSeriesAsync(
                    metadata.Series, metadata.Publisher, metadata.Year, ct);

            var isNew = existing is null;
            var comic = existing ?? new Comic { AddedUtc = _time.GetUtcNow().UtcDateTime };

            comic.FilePath = file;
            comic.FileSize = size;
            comic.FileModifiedUtc = modifiedUtc;
            comic.Format = format;

            comic.Title = metadata.Title;
            comic.SeriesId = series?.Id;
            comic.Number = metadata.Number;
            comic.Volume = metadata.Volume;
            comic.Year = metadata.Year;
            comic.Publisher = metadata.Publisher;
            comic.Writer = metadata.Writer;
            comic.Summary = metadata.Summary;
            comic.ReadingDirection = metadata.ReadingDirection;

            comic.PageCount = archive.Pages.Count;
            comic.CoverPath = null; // the file changed so any cached thumbnail is stale

            //Id, AddedUtc and IsFavorite are delibrately left alone on updates
            await _repository.SaveComicAsync(comic, ct);

            return isNew ? FileOutcome.Added : FileOutcome.Updated;
        }

        private static List<string> EnumerateComicFiles(string folderPath)
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System
            };

            return Directory.EnumerateFiles(folderPath, "*", options)
                .Where(f => ComicExtension.Contains(Path.GetExtension(f)))
                .OrderBy(f => f, NaturalStringComparer.Instance)
                .ToList();
        }
    }
}

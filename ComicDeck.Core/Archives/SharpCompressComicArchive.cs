using System;
using System.Collections.Generic;
using System.Text;
using ComicDeck.Core.Models;
using ComicDeck.Core.Utilities;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Tar;

namespace ComicDeck.Core.Archives;

/// <summary>Reads RAR (.cbr), 7z (.cb7) and TAR (.cbt) comics through SharpCompress.</summary>
public sealed class SharpCompressComicArchive : IComicArchive
{
    private const int MaxInitialBuffer = 64 * 1024 * 1024;

    private readonly IArchive _archive;
    private readonly bool _isSolid;
    private readonly List<IArchiveEntry> _pageEntries;
    private readonly Dictionary<string, IArchiveEntry> _entriesByName;
    private readonly Dictionary<string, int> _pageIndexByName;
    private readonly SemaphoreSlim _gate = new(1, 1); // one reader at a time, as with ZIP

    private string? _cacheDir;   // solid archives only; set once the pages are extracted

    public string FilePath { get; }
    public IReadOnlyList<ComicPage> Pages { get; }

    private SharpCompressComicArchive(string filePath, IArchive archive)
    {
        FilePath = filePath;
        _archive = archive;
        _isSolid = archive.IsSolid;

        var files = archive.Entries
            .Where(e => !e.IsDirectory && e.Key is not null)
            .ToList();

        _entriesByName = new(StringComparer.OrdinalIgnoreCase);
        foreach (var e in files)
            _entriesByName[Normalize(e.Key!)] = e;

        _pageEntries = files
            .Where(e => PageFilter.IsPage(e.Key!))
            .OrderBy(e => Normalize(e.Key!), NaturalStringComparer.Instance)
            .ToList();

        _pageIndexByName = new(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < _pageEntries.Count; i++)
            _pageIndexByName[Normalize(_pageEntries[i].Key!)] = i;

        Pages = _pageEntries
            .Select((e, i) => new ComicPage(i, e.Key!, e.Size))
            .ToList();
    }

    public static SharpCompressComicArchive Open(string filePath, ArchiveFormat format)
    {
        if (!File.Exists(filePath))
            throw new ArchiveException(ArchiveErrorKind.NotFound, $"File not found: {filePath}");

        IArchive? archive = null;
        try
        {
            archive = format switch
            {
                ArchiveFormat.Rar => RarArchive.OpenArchive(filePath),
                ArchiveFormat.SevenZip => SevenZipArchive.OpenArchive(filePath),
                ArchiveFormat.Tar => TarArchive.OpenArchive(filePath),
                _ => throw new ArchiveException(ArchiveErrorKind.Unsupported,
                        $"{format} is not handled by this reader.")
            };

            var result = new SharpCompressComicArchive(filePath, archive);

            if (result._pageEntries.Count == 0)
                throw new ArchiveException(ArchiveErrorKind.Empty, "The archive contains no image pages.");

            if (result._pageEntries.Any(e => e.IsEncrypted))
                throw new ArchiveException(ArchiveErrorKind.PasswordProtected, "The archive is password protected.");

            return result;
        }
        catch (ArchiveException)
        {
            archive?.Dispose();
            throw;
        }
        catch (SharpCompress.Common.CryptographicException ex)
        {
            archive?.Dispose();
            throw new ArchiveException(ArchiveErrorKind.PasswordProtected, "The archive is password protected.", ex);
        }
        catch (Exception ex) when (ex is SharpCompress.Common.SharpCompressException
                                  or InvalidOperationException or InvalidDataException
                                  or EndOfStreamException or NotSupportedException
                                  or ArgumentException or IndexOutOfRangeException)
        {
            archive?.Dispose();
            throw new ArchiveException(ArchiveErrorKind.Corrupt, "The archive is corrupt or unreadable.", ex);
        }
        catch
        {
            archive?.Dispose();
            throw;
        }
    }

    public async Task<Stream> OpenPageAsync(int index, CancellationToken cancellationToken = default)
    {
        if ((uint)index >= (uint)_pageEntries.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_isSolid)
            {
                await EnsureSolidCacheAsync(cancellationToken);
                var cached = Path.Combine(_cacheDir!, $"{index}.page");

                if (!File.Exists(cached))
                    throw new ArchiveException(ArchiveErrorKind.Corrupt,
                        $"Page '{_pageEntries[index].Key}' could not be extracted.");

                var bytes = await File.ReadAllBytesAsync(cached, cancellationToken);
                return new MemoryStream(bytes, writable: false);
            }

            return await CopyEntryAsync(_pageEntries[index], cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Stream?> OpenEntryAsync(string entryName, CancellationToken cancellationToken = default)
    {
        var name = Normalize(entryName);
        if (!_entriesByName.TryGetValue(name, out var entry))
            return null;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            return _isSolid
                ? await Task.Run(() => ReadEntryFromSequentialPass(name, cancellationToken), cancellationToken)
                : await CopyEntryAsync(entry, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<MemoryStream> CopyEntryAsync(IArchiveEntry entry, CancellationToken ct)
    {
        var buffer = new MemoryStream((int)Math.Clamp(entry.Size, 0, MaxInitialBuffer));
        try
        {
            using var source = entry.OpenEntryStream();
            await source.CopyToAsync(buffer, ct);
        }
        catch (SharpCompress.Common.CryptographicException ex)
        {
            buffer.Dispose();
            throw new ArchiveException(ArchiveErrorKind.PasswordProtected, "The archive is password protected.", ex);
        }
        catch (SharpCompress.Common.SharpCompressException ex)
        {
            buffer.Dispose();
            throw new ArchiveException(ArchiveErrorKind.Corrupt, $"Entry '{entry.Key}' is corrupt.", ex);
        }

        buffer.Position = 0;
        return buffer;
    }

    /// <summary>Solid archives: walk forward and stop at the entry. Nothing is written to disk.</summary>
    private MemoryStream? ReadEntryFromSequentialPass(string normalizedName, CancellationToken ct)
    {
        try
        {
            using var reader = _archive.ExtractAllEntries();
            while (reader.MoveToNextEntry())
            {
                ct.ThrowIfCancellationRequested();
                var entry = reader.Entry;
                if (entry.IsDirectory || entry.Key is null) continue;
                if (!Normalize(entry.Key).Equals(normalizedName, StringComparison.OrdinalIgnoreCase)) continue;

                var buffer = new MemoryStream();
                using var source = reader.OpenEntryStream();
                source.CopyTo(buffer);
                buffer.Position = 0;
                return buffer;
            }
            return null;
        }
        catch (SharpCompress.Common.CryptographicException ex)
        {
            throw new ArchiveException(ArchiveErrorKind.PasswordProtected, "The archive is password protected.", ex);
        }
        catch (SharpCompress.Common.SharpCompressException ex)
        {
            throw new ArchiveException(ArchiveErrorKind.Corrupt, $"Entry '{normalizedName}' is corrupt.", ex);
        }
    }

    /// <summary>Solid archives: one sequential pass extracts every page, so any page is then cheap.</summary>
    private async Task EnsureSolidCacheAsync(CancellationToken ct)
    {
        if (_cacheDir is not null) return;

        var dir = Path.Combine(Path.GetTempPath(), "ComicDeck", "cache", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        try
        {
            await Task.Run(() =>
            {
                using var reader = _archive.ExtractAllEntries();
                while (reader.MoveToNextEntry())
                {
                    ct.ThrowIfCancellationRequested();
                    var entry = reader.Entry;
                    if (entry.IsDirectory || entry.Key is null) continue;
                    if (!_pageIndexByName.TryGetValue(Normalize(entry.Key), out var index)) continue;

                    using var source = reader.OpenEntryStream();
                    using var target = File.Create(Path.Combine(dir, $"{index}.page"));
                    source.CopyTo(target);
                }
            }, ct);

            _cacheDir = dir; // only set on success, so a failed pass is retried next time
        }
        catch (SharpCompress.Common.CryptographicException ex)
        {
            TryDeleteDirectory(dir);
            throw new ArchiveException(ArchiveErrorKind.PasswordProtected, "The archive is password protected.", ex);
        }
        catch (SharpCompress.Common.SharpCompressException ex)
        {
            TryDeleteDirectory(dir);
            throw new ArchiveException(ArchiveErrorKind.Corrupt, "The archive could not be extracted.", ex);
        }
        catch
        {
            TryDeleteDirectory(dir); // includes cancellation
            throw;
        }
    }

    private static string Normalize(string name) => name.Replace('\\', '/');

    private static void TryDeleteDirectory(string path)
    {
        try { Directory.Delete(path, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public void Dispose()
    {
        _archive.Dispose();
        _gate.Dispose();
        if (_cacheDir is not null)
            TryDeleteDirectory(_cacheDir);
    }
}
using System;
using System.Collections.Generic;
using System.Text;
using System;
using System.Collections.Concurrent;
using ComicDeck.Core.Library;
using ComicDeck.Core.Archives;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace ComicDeck.Services
{
    /// <summary>
    /// Creates and caches cover thumbnails (page 1, about 360px tall) in LocalState\covers.
    /// </summary>
    public sealed class CoverThumbnailService
    {
        private const int ThumbnailHeight = 360;

        private readonly IComicArchiveFactory _archiveFactory;
        private readonly SemaphoreSlim _gate = new(2);
        private readonly ConcurrentDictionary<string, byte> _failed = new(StringComparer.OrdinalIgnoreCase);
        private readonly string _folder = Path.Combine(AppPaths.DataFolder, "covers");

        public CoverThumbnailService(IComicArchiveFactory archiveFactory) => _archiveFactory = archiveFactory;

        /// <summary> Returns the thumbnail's file name inside the covers folder, or null if none could be made.</summary>
        public async Task<string?> GetOrCreateAsync(ComicListItem item)
        {
            // the modified time in the name means a changed comic automatically get a fresh cover
            var fileName = $"{item.Id}_{item.FileModifiedUtc.Ticks}.jpg";
            var target = Path.Combine(_folder, fileName);

            if (File.Exists(target)) return fileName;
            if (_failed.ContainsKey(item.FilePath)) return null; // don't retry broken files on every scroll

            await _gate.WaitAsync();
            try
            {
                if (File.Exists(target)) return fileName;

                if (await Task.Run(() => TryCreateAsync(item.FilePath, target)))
                    return fileName;

                _failed[item.FilePath] = 0;
                return null;
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task<bool> TryCreateAsync(string comicPath, string target)
        {
            var temp = target + ".tmp";
            try
            {
                using var archive = _archiveFactory.Open(comicPath);
                using var source = await archive.OpenPageAsync(0);
                using var input = source.AsRandomAccessStream();

                var decoder = await BitmapDecoder.CreateAsync(input);
                var scale = Math.Min(1.0, ThumbnailHeight / (double)decoder.PixelHeight);
                var width = (uint)Math.Max(1, Math.Round(decoder.PixelWidth * scale));
                var height = (uint)Math.Max(1, Math.Round(decoder.PixelHeight * scale));

                var transform = new BitmapTransform
                {
                    ScaledWidth = width,
                    ScaledHeight = height,
                    InterpolationMode = BitmapInterpolationMode.Fant
                };
                var pixels = await decoder.GetPixelDataAsync(
                    BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, transform, ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);

                using var output = new InMemoryRandomAccessStream();
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, output);
                encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, width, height, 96, 96, pixels.DetachPixelData());
                await encoder.FlushAsync();

                Directory.CreateDirectory(_folder);
                output.Seek(0);
                using (var file = File.Create(temp))
                    await output.AsStream().CopyToAsync(file);

                File.Move(temp, target, overwrite: true); // never leaves a half-written thumbnail behind
                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // unreadable archive, unsupported image format, disk error: this comic just shows no cover
                try { File.Delete(temp); } catch (IOException) {  }
                return false;
            }
        }

    }
}

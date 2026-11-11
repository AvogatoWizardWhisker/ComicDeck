using System;
using System.Collections.Generic;
using System.Text;
using ComicDeck.Core.Archives;
using ComicDeck.Core.Utilities;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;

namespace ComicDeck.Services
{
    /// <param name="NaturalWidth">Width of the source image, before any decoding downscaling.</param>
    public sealed record LoadedPage(int Index, BitmapImage Image, int NaturalWidth, int NaturalHeight)
    {
        ///<summary>Wider than tall: usually a double page spread</summary>
        public bool IsLandscape => NaturalWidth > NaturalHeight;
    }

    public sealed class PageLoadException(int pageIndex, string message, Exception inner) : Exception(message, inner)
    {
        public int PageIndex { get; } = pageIndex;
    }

    public sealed class PageImageLoader
    {
        /// <summary>
        /// Reads one page and turns into a BitmapImage. Call this from the UI thread: The archive read
        /// is moved to the thread pool here, and BitImage must be created on the UI thread.
        /// </summary>
        /// <exception cref="ArchiveException">The archive entry couldn't be read.</exception>
        /// <exception cref="PageLoadException">The bytes couldn't be decoded as an image.</exception>
        public async Task<LoadedPage> LoadAsync( IComicArchive archive, int index, DecodeRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                // Core code awaits without ConfigureAwait(false), so run it off the UI thread entirely.
                using var source = await Task.Run(() => archive.OpenPageAsync(index, cancellationToken), cancellationToken);
                using var stream = source.AsRandomAccessStream();

                // Reading the header is cheap, and gives us the size before we choose a decode size.
                var decoder = await BitmapDecoder.CreateAsync(stream);
                var width = (int)decoder.PixelWidth;
                var height = (int)decoder.PixelHeight;

                stream.Seek(0);

                var bitmap = new BitmapImage();
                if (DecodeSizing.ChooseDecodeHeight(width, height, request) is int decodeHeight)
                    bitmap.DecodePixelHeight = decodeHeight; // must be set before the source

                await bitmap.SetSourceAsync(stream);

                return new LoadedPage(index, bitmap, width, height);

            }
            catch (Exception ex) when (ex is not OperationCanceledException and not ArchiveException)
            {
                throw new PageLoadException(index, $"Page {index + 1} couldn't be decoded. WebP and AVIF pages need Microsoft's image extensions installed.", ex);
            }
        }
    }
}

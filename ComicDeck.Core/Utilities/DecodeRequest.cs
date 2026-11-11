namespace ComicDeck.Core.Utilities;

///<summary>
/// The largest box of a page should be decoded at. A value of 0 means "no limit on that axis".
/// Fit-Height mode sets only MaxHeight, fit-width sets only MaxWidth, fit-page sets both.
/// </summary>
public readonly record struct DecodeRequest(int MaxWidth, int MaxHeight)
{
    private const int Step = 128;

    public static DecodeRequest Unlimited => default;
    
    ///<summary>
    /// Builds a request from a viewpoint size in device-independent pixels. Sizes are rounded up to 128-pixel
    /// steps so that dragging the window edge doesn't invalidate the cache on every pixel.
    /// <paramref name="headroom"/> asks for extra resolution, for example 2.0 to allow 2x zoom.
    ///</summary>
    public static DecodeRequest ForViewport(
        double widthDip, double heightDip, double rasterizationScale, double headroom = 1.0)
    {
        var factor = rasterizationScale * headroom;
        return new DecodeRequest(ToPixels(widthDip, factor), ToPixels(heightDip, factor));
    }

    private static int ToPixels(double dip, double factor)
    {
        if (dip <= 0 || factor <= 0) return 0;
        var pixels = (int)Math.Ceiling(dip * factor);
        return (pixels + Step - 1) / Step * Step;
    }
}

public static class DecodeSizing
{
    ///<summary>
    /// The height to decode at, or null to decode at natural size (the page already fits the request).
    /// Setting only the height keeps the aspect ratio
    /// </summary>
    public static int? ChooseDecodeHeight(int naturalWidth, int naturalHeight, DecodeRequest request)
    {
        if (naturalWidth <= 0 || naturalHeight <= 0) return null;

        var scale = 1.0;
        if (request.MaxWidth > 0) scale = Math.Min(scale, (double)request.MaxWidth / naturalWidth);
        if (request.MaxHeight > 0) scale = Math.Min(scale, (double)request.MaxHeight / naturalHeight);

        if (scale >= 1.0) return null;
        return Math.Max(1, (int)Math.Round(naturalHeight * scale));
    }
}
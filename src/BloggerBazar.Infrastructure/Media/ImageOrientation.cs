using SkiaSharp;

namespace BloggerBazar.Infrastructure.Media;

// Phone cameras store pixels sideways and record the display rotation in EXIF. Re-encoding drops that
// metadata, so the rotation (and mirroring) is applied to the pixels before the image is resized.
internal static class ImageOrientation
{
    public static SKBitmap Apply(SKBitmap source, SKEncodedOrigin origin)
    {
        if (origin is SKEncodedOrigin.TopLeft) return source;

        var width = source.Width;
        var height = source.Height;
        var swapsSides = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        // Maps a source point (x, y) to its displayed position: x' = ScaleX*x + SkewX*y + TransX, y' = SkewY*x + ScaleY*y + TransY.
        SKMatrix? transform = origin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, width, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, width, 0, -1, height, 0, 0, 1),
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, height, 0, 0, 1),
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, height, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, height, -1, 0, width, 0, 0, 1),
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, width, 0, 0, 1),
            _ => null
        };
        if (transform is not { } matrix) return source;

        var oriented = new SKBitmap(new SKImageInfo(swapsSides ? height : width, swapsSides ? width : height, source.ColorType, source.AlphaType));
        using (var canvas = new SKCanvas(oriented))
        {
            canvas.SetMatrix(matrix);
            canvas.DrawBitmap(source, 0, 0);
        }

        source.Dispose();
        return oriented;
    }
}

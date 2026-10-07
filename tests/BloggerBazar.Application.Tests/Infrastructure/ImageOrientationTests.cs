using BloggerBazar.Infrastructure.Media;
using SkiaSharp;

namespace BloggerBazar.Application.Tests.Infrastructure;

public sealed class ImageOrientationTests
{
    // Source is 2x1: red on the left, blue on the right.
    [Theory]
    [InlineData(SKEncodedOrigin.TopLeft, 2, 1, 0, 0, 1, 0)]
    [InlineData(SKEncodedOrigin.TopRight, 2, 1, 1, 0, 0, 0)]
    [InlineData(SKEncodedOrigin.BottomRight, 2, 1, 1, 0, 0, 0)]
    [InlineData(SKEncodedOrigin.BottomLeft, 2, 1, 0, 0, 1, 0)]
    [InlineData(SKEncodedOrigin.LeftTop, 1, 2, 0, 0, 0, 1)]
    [InlineData(SKEncodedOrigin.RightTop, 1, 2, 0, 0, 0, 1)]
    [InlineData(SKEncodedOrigin.RightBottom, 1, 2, 0, 1, 0, 0)]
    [InlineData(SKEncodedOrigin.LeftBottom, 1, 2, 0, 1, 0, 0)]
    public void Applies_the_exif_origin_to_the_pixels(SKEncodedOrigin origin, int width, int height, int redX, int redY, int blueX, int blueY)
    {
        var source = new SKBitmap(new SKImageInfo(2, 1, SKColorType.Rgba8888, SKAlphaType.Premul));
        source.SetPixel(0, 0, SKColors.Red);
        source.SetPixel(1, 0, SKColors.Blue);

        using var oriented = ImageOrientation.Apply(source, origin);

        Assert.Equal(width, oriented.Width);
        Assert.Equal(height, oriented.Height);
        Assert.Equal(SKColors.Red, oriented.GetPixel(redX, redY));
        Assert.Equal(SKColors.Blue, oriented.GetPixel(blueX, blueY));
    }
}

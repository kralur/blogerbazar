using BloggerBazar.Infrastructure.Configuration;
using BloggerBazar.Infrastructure.Media;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace BloggerBazar.Application.Tests.Infrastructure;

// Pre-launch audit 2026-11-04: a big photo is decoded already scaled down, so it never sits in memory at full size.
public sealed class ProfileImageDecodeTests
{
    private static readonly S3ProfileMediaStorage Storage = new(Options.Create(new ProfileMediaOptions { MaxImageDimension = 512 }), NullLogger<S3ProfileMediaStorage>.Instance);

    [Fact]
    public void A_big_jpeg_is_decoded_at_a_fraction_of_its_size_and_keeps_its_shape()
    {
        using var input = Encode(4000, 3000, SKEncodedImageFormat.Jpeg, SKColors.Red);

        using var bitmap = Storage.LoadAndValidateImage(input);

        Assert.True(bitmap.Width <= 2000, $"decoded {bitmap.Width}x{bitmap.Height}");
        Assert.True(bitmap.Width >= 512, "never below the stored size, so nothing is upscaled");
        Assert.Equal(4d / 3, (double)bitmap.Width / bitmap.Height, 2);
        Assert.InRange(bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2).Red, (byte)200, (byte)255);
    }

    [Fact]
    public void A_small_png_keeps_its_size_and_colours()
    {
        using var input = Encode(40, 20, SKEncodedImageFormat.Png, SKColors.Blue);

        using var bitmap = Storage.LoadAndValidateImage(input);

        Assert.Equal((40, 20), (bitmap.Width, bitmap.Height));
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(10, 10));
    }

    private static MemoryStream Encode(int width, int height, SKEncodedImageFormat format, SKColor color)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return new MemoryStream(data.ToArray());
    }
}

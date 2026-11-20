using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using BloggerBazar.Application.Abstractions.Media;
using BloggerBazar.Infrastructure.Configuration;
using BloggerBazar.Infrastructure.Media;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace BloggerBazar.Application.Tests.Infrastructure;

// Cloudflare R2 refuses what newer AWS SDKs like to send by default: a body streamed in signed chunks and checksum
// trailers. The options are checked in ProfileMediaOptionsTests; this looks at the request itself, so an SDK update
// that changes what goes out fails here and not in production.
public sealed class ProfileMediaWireTests
{
    [Fact]
    public async Task A_photo_goes_out_as_one_signed_body_of_known_length_without_checksum_extras()
    {
        var port = FreePort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var received = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync();
            using var body = new MemoryStream();
            await context.Request.InputStream.CopyToAsync(body);
            var headers = context.Request.Headers.AllKeys.ToDictionary(name => name!, name => context.Request.Headers[name]!, StringComparer.OrdinalIgnoreCase);
            var request = (context.Request.HttpMethod, Path: context.Request.RawUrl!, Headers: headers, Body: body.ToArray());
            context.Response.StatusCode = 200;
            context.Response.Headers["ETag"] = "\"0123456789abcdef0123456789abcdef\"";
            context.Response.Close();
            return request;
        });

        var storage = new S3ProfileMediaStorage(
            Options.Create(new ProfileMediaOptions
            {
                ServiceUrl = $"http://127.0.0.1:{port}",
                PublicBaseUrl = "https://media.example.test",
                Bucket = "media",
                AccessKey = "access-key",
                SecretKey = "secret-key"
            }),
            NullLogger<S3ProfileMediaStorage>.Instance);

        var stored = await storage.UploadAsync(
            new ProfileMediaUpload(ProfileMediaTarget.Blogger, Guid.NewGuid(), Jpeg(800, 600), "photo.jpg", "image/jpeg"),
            CancellationToken.None);
        var request = await received.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal("PUT", request.HttpMethod);
        Assert.StartsWith("/media/profiles/bloggers/", request.Path);
        Assert.EndsWith(request.Path["/media".Length..], stored.PublicUrl);
        Assert.Equal("image/webp", request.Headers["Content-Type"]);
        Assert.Equal(request.Body.Length.ToString(), request.Headers["Content-Length"]);
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(request.Body, 0, 4));
        Assert.Matches(new Regex("^[0-9a-f]{64}$"), request.Headers["X-Amz-Content-SHA256"]);
        Assert.DoesNotContain(request.Headers.Keys, name =>
            name.Equals("Content-Encoding", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("X-Amz-Trailer", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("X-Amz-Decoded-Content-Length", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("X-Amz-Checksum-", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("X-Amz-Sdk-Checksum-Algorithm", StringComparison.OrdinalIgnoreCase));
    }

    private static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }

    private static byte[] Jpeg(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.Red);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        return data.ToArray();
    }
}

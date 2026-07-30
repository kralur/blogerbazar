using System.Net;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace BloggerBazar.Application.Tests.Features.Payments;

public sealed class TelegramWebhookSecurityTests
{
    [Fact]
    public async Task Rejects_missing_secret_before_binding_the_body()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();
        using var request = CreateRequest("{ invalid json", secret: null);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Rejects_invalid_secret_without_response_details()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();
        using var request = CreateRequest("{}", "invalid-secret");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Rejects_non_json_content()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();
        using var request = CreateRequest("{}", contentType: "text/plain");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Rejects_oversized_payload()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();
        using var request = CreateRequest(new string('a', 33));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Rejects_malformed_json_with_a_safe_response()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();
        using var request = CreateRequest("{]");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Accepts_a_valid_unsupported_update()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();
        using var request = CreateRequest("{}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Applies_the_dedicated_webhook_rate_limit()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        for (var requestNumber = 0; requestNumber < 120; requestNumber++)
        {
            using var response = await client.SendAsync(CreateRequest("{}"));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        using var rejected = await client.SendAsync(CreateRequest("{}"));

        Assert.Equal((HttpStatusCode)429, rejected.StatusCode);
    }

    private static HttpRequestMessage CreateRequest(string body, string? secret = "test-webhook-secret", string contentType = "application/json")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/telegram")
        {
            Content = new StringContent(body, Encoding.UTF8, contentType)
        };
        if (secret is not null)
        {
            request.Headers.Add("X-Telegram-Bot-Api-Secret-Token", secret);
        }
        return request;
    }

    private sealed class TestApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Port=5432;Database=bloggerbazar_test;Username=postgres;Password=test",
                ["Telegram:WebhookSecret"] = "test-webhook-secret",
                ["Telegram:WebhookMaxBodyBytes"] = "32",
                ["RateLimiting:PermitLimit"] = "120",
                ["RateLimiting:WindowSeconds"] = "60",
                ["RateLimiting:TelegramWebhook:PermitLimit"] = "120",
                ["RateLimiting:TelegramWebhook:WindowSeconds"] = "60",
                ["Database:ApplyMigrationsOnStartup"] = "false",
                ["DevelopmentData:Seed"] = "false"
            }));
        }
    }
}

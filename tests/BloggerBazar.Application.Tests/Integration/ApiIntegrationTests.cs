using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BloggerBazar.Application.Tests.Integration;

public sealed class ApiIntegrationTests(BloggerBazarApiFactory factory) : IClassFixture<BloggerBazarApiFactory>
{
    [IntegrationFact]
    public async Task Liveness_and_readiness_endpoints_return_success()
    {
        using var client = factory.CreateClient();

        var live = await client.GetAsync("/health/live");
        var ready = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.NotEmpty(live.Headers.GetValues("X-Request-ID"));
    }

    [IntegrationFact]
    public async Task Missing_telegram_authorization_returns_unified_problem()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/users/me");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("authentication_required", problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
    }

    [IntegrationFact]
    public async Task Authenticated_member_cannot_access_admin_dashboard()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("tma", CreateInitData(900_001));
        await client.GetAsync("/api/v1/users/me");

        var response = await client.GetAsync("/api/v1/admin/dashboard");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("access_denied", problem.GetProperty("code").GetString());
    }

    [IntegrationFact]
    public async Task Business_profile_can_be_created_and_read_through_v1()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("tma", CreateInitData(900_002));
        var payload = new
        {
            name = "Integration Coffee",
            username = "@integrationcoffee",
            city = "tashkent",
            logoUrl = (string?)null,
            websiteUrl = "https://integration.example",
            description = "Integration test business profile.",
            phone = "+998901234567",
            email = "integration@example.com"
        };

        var create = await client.PostAsJsonAsync("/api/v1/businesses", payload);
        var getMine = await client.GetAsync("/api/v1/businesses/me");

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getMine.StatusCode);
    }

    [IntegrationFact]
    public async Task Invalid_business_profile_returns_validation_problem()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("tma", CreateInitData(900_003));

        var response = await client.PostAsJsonAsync("/api/v1/businesses", new { name = "" });
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        Assert.Equal("validation_failed", problem.GetProperty("code").GetString());
        Assert.True(problem.TryGetProperty("errors", out _));
    }

    private static string CreateInitData(long telegramUserId)
    {
        var authDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var user = JsonSerializer.Serialize(new { id = telegramUserId, first_name = "Integration", username = $"integration{telegramUserId}" });
        var dataCheckString = $"auth_date={authDate}\nuser={user}";
        using var secretKey = new HMACSHA256(Encoding.UTF8.GetBytes("WebAppData"));
        var secret = secretKey.ComputeHash(Encoding.UTF8.GetBytes(BloggerBazarApiFactory.BotToken));
        using var signature = new HMACSHA256(secret);
        var hash = Convert.ToHexString(signature.ComputeHash(Encoding.UTF8.GetBytes(dataCheckString))).ToLowerInvariant();
        return $"auth_date={authDate}&user={Uri.EscapeDataString(user)}&hash={hash}";
    }
}

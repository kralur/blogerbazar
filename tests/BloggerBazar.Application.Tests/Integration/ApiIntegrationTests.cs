using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BloggerBazar.Application.Tests.Integration;

[Collection(ApiHostCollection.Name)]
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
        await client.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.OK, (await ShareContactAsync(client, 900_002, 900_002)).StatusCode);
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
        // The phone comes from Telegram, not from the form ("+998901234567" above is ignored).
        var mine = await getMine.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("+998 88 197 29 29", mine.GetProperty("phone").GetString());
    }

    [IntegrationFact]
    public async Task Profile_cannot_be_saved_without_a_phone_from_telegram_and_a_forwarded_contact_does_not_count()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("tma", CreateInitData(900_004));
        await client.GetAsync("/api/v1/users/me");
        await ShareContactAsync(client, 900_004, contactOwnerId: 123);

        var create = await client.PostAsJsonAsync("/api/v1/businesses", new { name = "No Phone", city = "tashkent", description = "Test", phone = "+998 90 123 45 67" });
        var problem = await create.Content.ReadFromJsonAsync<JsonElement>();
        var me = await client.GetFromJsonAsync<JsonElement>("/api/v1/users/me");

        Assert.Equal(HttpStatusCode.Conflict, create.StatusCode);
        Assert.Equal("phone_not_verified", problem.GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.Null, me.GetProperty("verifiedPhone").ValueKind);
    }

    [IntegrationFact]
    public async Task Invalid_business_profile_returns_validation_problem()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("tma", CreateInitData(900_003));
        await client.GetAsync("/api/v1/users/me");
        await ShareContactAsync(client, 900_003, 900_003);

        var response = await client.PostAsJsonAsync("/api/v1/businesses", new { name = "" });
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        Assert.Equal("validation_failed", problem.GetProperty("code").GetString());
        Assert.True(problem.TryGetProperty("errors", out _));
    }

    // What Telegram posts to the webhook when a user shares a contact; contactOwnerId != fromId is a forwarded card.
    private static Task<HttpResponseMessage> ShareContactAsync(HttpClient client, long fromId, long contactOwnerId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/telegram")
        {
            Content = JsonContent.Create(new
            {
                update_id = fromId,
                message = new
                {
                    message_id = 1,
                    from = new { id = fromId, first_name = "Integration" },
                    chat = new { id = fromId },
                    contact = new { phone_number = "998881972929", user_id = contactOwnerId }
                }
            })
        };
        request.Headers.Add("X-Telegram-Bot-Api-Secret-Token", "integration-webhook-secret");
        return client.SendAsync(request);
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

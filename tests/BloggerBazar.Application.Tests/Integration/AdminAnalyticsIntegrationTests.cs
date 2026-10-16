using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using BloggerBazar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BloggerBazar.Application.Tests.Integration;

// Web admin analytics (D57, phase 2) on a real database: SQL grouping by Tashkent day, retention and sources.
// Rows are dated in 2025 through SQL so they never mix with rows other tests create today.
[Collection(ApiHostCollection.Name)]
public sealed class AdminAnalyticsIntegrationTests(BloggerBazarApiFactory factory) : IClassFixture<BloggerBazarApiFactory>
{
    private const long AdminTelegramUserId = 1_400_001;

    [IntegrationFact]
    public async Task Days_are_tashkent_calendar_days_and_quiet_days_are_zero()
    {
        await EnsureAdminAsync();
        // 18:30 UTC is 23:30 in Tashkent (the 10th); 19:30 UTC is 00:30 the next day there (the 11th).
        await SeedUserAsync(1_400_011, createdAtUtc: new DateTime(2025, 3, 10, 18, 30, 0, DateTimeKind.Utc));
        await SeedUserAsync(1_400_012, createdAtUtc: new DateTime(2025, 3, 10, 19, 30, 0, DateTimeKind.Utc));
        using var client = CreateClient(AdminTelegramUserId);

        var daily = await client.GetFromJsonAsync<JsonElement>("/api/admin/analytics/daily?from=2025-03-09&to=2025-03-11");
        var days = daily.GetProperty("days").EnumerateArray().ToDictionary(day => day.GetProperty("date").GetString()!, day => day.GetProperty("newUsers").GetInt32());

        Assert.Equal(new Dictionary<string, int> { ["2025-03-09"] = 0, ["2025-03-10"] = 1, ["2025-03-11"] = 1 }, days);
    }

    [IntegrationFact]
    public async Task Retention_counts_people_whose_last_open_is_at_least_n_days_after_the_first()
    {
        await EnsureAdminAsync();
        var first = new DateTime(2025, 5, 1, 9, 0, 0, DateTimeKind.Utc);
        await SeedUserAsync(1_400_021, first, lastSeenAtUtc: first.AddDays(2));
        await SeedUserAsync(1_400_022, first, lastSeenAtUtc: first.AddDays(8));
        await SeedUserAsync(1_400_023, first, lastSeenAtUtc: null);
        using var client = CreateClient(AdminTelegramUserId);

        var retention = await client.GetFromJsonAsync<JsonElement>("/api/admin/analytics/retention?from=2025-05-01&to=2025-05-01");
        var steps = retention.GetProperty("steps").EnumerateArray().ToDictionary(step => step.GetProperty("days").GetInt32(),
            step => (step.GetProperty("eligible").GetInt32(), step.GetProperty("returned").GetInt32()));

        Assert.Equal(3, retention.GetProperty("cohort").GetInt32());
        Assert.Equal((3, 2), steps[1]);
        Assert.Equal((3, 1), steps[7]);
        Assert.Equal((3, 0), steps[30]);
    }

    [IntegrationFact]
    public async Task Sources_count_people_per_ad_label()
    {
        await EnsureAdminAsync();
        await SeedUserAsync(1_400_031, new DateTime(2025, 6, 1, 9, 0, 0, DateTimeKind.Utc), source: "it_analytics");
        await SeedUserAsync(1_400_032, new DateTime(2025, 6, 2, 9, 0, 0, DateTimeKind.Utc), source: "it_analytics");
        using var client = CreateClient(AdminTelegramUserId);

        var sources = await client.GetFromJsonAsync<JsonElement>("/api/admin/analytics/sources");
        var label = sources.GetProperty("sources").EnumerateArray().Single(row => row.GetProperty("source").GetString() == "it_analytics");

        Assert.Equal(2, label.GetProperty("users").GetInt32());
        Assert.Equal(0, label.GetProperty("withDeal").GetInt32());
    }

    [IntegrationFact]
    public async Task Every_report_answers_and_months_are_filled()
    {
        await EnsureAdminAsync();
        using var client = CreateClient(AdminTelegramUserId);

        var money = await client.GetFromJsonAsync<JsonElement>("/api/admin/analytics/money?months=3");
        var geography = await client.GetAsync("/api/admin/analytics/geography");

        Assert.Equal(3, money.GetProperty("months").GetArrayLength());
        Assert.Equal(HttpStatusCode.OK, geography.StatusCode);
    }

    [IntegrationFact]
    public async Task Members_are_refused_and_bad_periods_are_rejected()
    {
        await EnsureAdminAsync();
        using var member = CreateClient(1_400_041);
        await member.GetAsync("/api/users/me");
        using var admin = CreateClient(AdminTelegramUserId);

        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/admin/analytics/daily")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.GetAsync("/api/admin/analytics/daily?from=2025-03-10&to=2025-03-01")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.GetAsync("/api/admin/analytics/daily?from=2024-01-01&to=2025-03-01")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.GetAsync("/api/admin/analytics/money?months=37")).StatusCode);
    }

    private async Task EnsureAdminAsync()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
        if (await dbContext.PlatformUsers.AnyAsync(user => user.TelegramUserId == AdminTelegramUserId)) return;
        var admin = PlatformUser.Create(AdminTelegramUserId, "Analytics admin", null);
        admin.SetRole(PlatformRole.Admin);
        dbContext.Add(admin);
        await dbContext.SaveChangesAsync();
    }

    private async Task SeedUserAsync(long telegramUserId, DateTime createdAtUtc, DateTime? lastSeenAtUtc = null, string? source = null)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
        var user = PlatformUser.Create(telegramUserId, "Analytics", null);
        user.SetSourceOnce(source);
        dbContext.Add(user);
        await dbContext.SaveChangesAsync();
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE platform_users SET "CreatedAtUtc" = {createdAtUtc}, "LastSeenAtUtc" = {lastSeenAtUtc} WHERE "TelegramUserId" = {telegramUserId}""");
    }

    private HttpClient CreateClient(long telegramUserId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("tma", CreateInitData(telegramUserId));
        return client;
    }

    private static string CreateInitData(long telegramUserId)
    {
        var authDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var user = JsonSerializer.Serialize(new { id = telegramUserId, first_name = "Integration", username = $"integration{telegramUserId}" });
        var dataCheckString = $"auth_date={authDate}\nuser={user}";
        var secret = HMACSHA256.HashData(Encoding.UTF8.GetBytes("WebAppData"), Encoding.UTF8.GetBytes(BloggerBazarApiFactory.BotToken));
        var hash = Convert.ToHexString(HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(dataCheckString))).ToLowerInvariant();
        return $"auth_date={authDate}&user={Uri.EscapeDataString(user)}&hash={hash}";
    }
}

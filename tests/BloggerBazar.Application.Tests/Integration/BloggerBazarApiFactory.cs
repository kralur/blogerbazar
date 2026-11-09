using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BloggerBazar.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace BloggerBazar.Application.Tests.Integration;

public sealed class BloggerBazarApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string BotToken = "123456:integration-test-token";
    public const string AdminWebOrigin = "https://admin.integration.test";
    public const string MiniAppOrigin = "https://app.integration.test";

    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("bloggerbazar_integration")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await postgres.DisposeAsync();
    }

    // A client that signs every request as this Telegram user, the way the Mini App does (D59: the catalog needs it too).
    public HttpClient CreateClientFor(long telegramUserId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new("tma", CreateInitData(telegramUserId));
        return client;
    }

    public static string CreateInitData(long telegramUserId)
    {
        var authDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var user = JsonSerializer.Serialize(new { id = telegramUserId, first_name = "Integration", username = $"integration{telegramUserId}" });
        var dataCheckString = $"auth_date={authDate}\nuser={user}";
        var secret = HMACSHA256.HashData(Encoding.UTF8.GetBytes("WebAppData"), Encoding.UTF8.GetBytes(BotToken));
        var hash = Convert.ToHexString(HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(dataCheckString))).ToLowerInvariant();
        return $"auth_date={authDate}&user={Uri.EscapeDataString(user)}&hash={hash}";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // UseSetting values reach Program as host arguments, so they are visible before builder.Build();
        // ConfigureAppConfiguration would only apply them during Build, after AddInfrastructure read them.
        builder.UseSetting("ConnectionStrings:Postgres", postgres.GetConnectionString());
        builder.UseSetting("ConnectionStrings:Redis", string.Empty);
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");
        builder.UseSetting("DevelopmentData:Seed", "false");
        builder.UseSetting("DealReminders:Enabled", "false");
        builder.UseSetting("Telegram:BotToken", BotToken);
        builder.UseSetting("Telegram:WebhookSecret", "integration-webhook-secret");
        // Web admin on, with room for every sign-in of a test class (the test server has no client address).
        builder.UseSetting("AdminWeb:Origin", AdminWebOrigin);
        builder.UseSetting("RateLimiting:AdminWebSignIn:PermitLimit", "1000");
        // Browser sign-in for company members (D64 stage B) on the Mini App's address.
        builder.UseSetting("Telegram:MiniAppUrl", MiniAppOrigin + "/");
        builder.UseSetting("Telegram:BotUsername", "BloggerBazarIntegrationBot");
        builder.UseSetting("RateLimiting:BrowserSignIn:PermitLimit", "1000");
        // Every anonymous request of the whole run shares one address budget here; the route walk alone takes dozens.
        builder.UseSetting("RateLimiting:PermitLimit", "100000");
        // Owner counts only while the id is in the settings (audit 2026-10-22): the owners the tests seed are listed.
        builder.UseSetting("Administration:TelegramUserIds:0", "1100939");
        builder.UseSetting("Administration:TelegramUserIds:1", "1300011");
    }
}

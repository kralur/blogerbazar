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
    }
}

using BloggerBazar.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = postgres.GetConnectionString(),
            ["ConnectionStrings:Redis"] = string.Empty,
            ["Database:ApplyMigrationsOnStartup"] = "false",
            ["DevelopmentData:Seed"] = "false",
            ["Telegram:BotToken"] = BotToken,
            ["Telegram:WebhookSecret"] = "integration-webhook-secret"
        }));
    }
}

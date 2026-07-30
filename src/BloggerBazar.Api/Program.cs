using BloggerBazar.Api.Middleware;
using BloggerBazar.Api.Security;
using BloggerBazar.Application;
using BloggerBazar.Infrastructure;
using BloggerBazar.Infrastructure.Caching;
using BloggerBazar.Infrastructure.Configuration;
using BloggerBazar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Threading.RateLimiting;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    DotEnvConfiguration.Load();
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.Services.AddProblemDetails();
    builder.Services.AddControllers();
    builder.Services.AddOpenApi();
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<BloggerBazarDbContext>("postgres")
        .AddCheck<DistributedCacheHealthCheck>("cache");
    builder.Services.AddSwaggerGen();
    var permitLimit = builder.Configuration.GetValue<int?>("RateLimiting:PermitLimit")
        ?? throw new InvalidOperationException("RateLimiting:PermitLimit must be configured.");
    var windowSeconds = builder.Configuration.GetValue<int?>("RateLimiting:WindowSeconds")
        ?? throw new InvalidOperationException("RateLimiting:WindowSeconds must be configured.");
    var webhookPermitLimit = builder.Configuration.GetValue<int?>("RateLimiting:TelegramWebhook:PermitLimit")
        ?? throw new InvalidOperationException("RateLimiting:TelegramWebhook:PermitLimit must be configured.");
    var webhookWindowSeconds = builder.Configuration.GetValue<int?>("RateLimiting:TelegramWebhook:WindowSeconds")
        ?? throw new InvalidOperationException("RateLimiting:TelegramWebhook:WindowSeconds must be configured.");
    var globalConcurrentPermitLimit = builder.Configuration.GetValue<int?>("RateLimiting:GlobalConcurrentPermitLimit")
        ?? throw new InvalidOperationException("RateLimiting:GlobalConcurrentPermitLimit must be configured.");
    var globalQueueLimit = builder.Configuration.GetValue<int?>("RateLimiting:GlobalQueueLimit")
        ?? throw new InvalidOperationException("RateLimiting:GlobalQueueLimit must be configured.");
    var maxIpPartitions = builder.Configuration.GetValue<int?>("RateLimiting:MaxIpPartitions")
        ?? throw new InvalidOperationException("RateLimiting:MaxIpPartitions must be configured.");
    var maxTelegramUserPartitions = builder.Configuration.GetValue<int?>("RateLimiting:MaxTelegramUserPartitions")
        ?? throw new InvalidOperationException("RateLimiting:MaxTelegramUserPartitions must be configured.");
    if (permitLimit <= 0 || windowSeconds <= 0 || webhookPermitLimit <= 0 || webhookWindowSeconds <= 0
        || globalConcurrentPermitLimit <= 0 || globalQueueLimit < 0 || maxIpPartitions <= 0 || maxTelegramUserPartitions <= 0)
    {
        throw new InvalidOperationException("Rate limiting values must be positive.");
    }

    builder.Services.AddSingleton(serviceProvider => new RateLimitPartitionKeyResolver(
        serviceProvider.GetRequiredService<BloggerBazar.Application.Abstractions.Security.ITelegramWebAppValidator>(),
        maxIpPartitions,
        maxTelegramUserPartitions));
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = (context, _) =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("RateLimiting");
            var partitionKind = context.HttpContext.Items.TryGetValue(RateLimitPartitionKeyResolver.PartitionKindItemKey, out var value)
                ? value?.ToString() ?? "global"
                : "global";
            logger.LogWarning("Request rate limited. Path {Path}; TraceId {TraceId}; PartitionKind {PartitionKind}", context.HttpContext.Request.Path, context.HttpContext.TraceIdentifier, partitionKind);
            return ValueTask.CompletedTask;
        };
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
            RateLimitPartition.GetConcurrencyLimiter("global", _ => new ConcurrencyLimiterOptions
            {
                PermitLimit = globalConcurrentPermitLimit,
                QueueLimit = globalQueueLimit,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));
        options.AddPolicy("api", context =>
        {
            var resolver = context.RequestServices.GetRequiredService<RateLimitPartitionKeyResolver>();
            var partitionKey = context.Request.Path.StartsWithSegments("/api/webhooks/telegram")
                ? resolver.ResolveIpPartitionKey(context, "telegram-webhook")
                : resolver.ResolveApiPartitionKey(context);
            var isWebhook = context.Request.Path.StartsWithSegments("/api/webhooks/telegram");
            return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = isWebhook ? webhookPermitLimit : permitLimit,
                Window = TimeSpan.FromSeconds(isWebhook ? webhookWindowSeconds : windowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true
            });
        });
        options.AddPolicy("telegram-webhook", context =>
        {
            var resolver = context.RequestServices.GetRequiredService<RateLimitPartitionKeyResolver>();
            var partitionKey = resolver.ResolveIpPartitionKey(context, "telegram-webhook");
            return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = webhookPermitLimit,
                Window = TimeSpan.FromSeconds(webhookWindowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true
            });
        });
    });
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddCors(options => options.AddPolicy("mini-app", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
    }));

    var app = builder.Build();
    if (builder.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
        await dbContext.Database.MigrateAsync();
    }
    if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("DevelopmentData:Seed"))
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
        await DevelopmentDataSeeder.SeedAsync(dbContext, app.Logger);
    }
    app.UseSerilogRequestLogging();
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    if (!app.Environment.IsProduction())
    {
        app.UseHttpsRedirection();
    }
    app.UseRouting();
    app.UseCors("mini-app");
    app.UseRateLimiter();
    app.UseMiddleware<TelegramWebhookSecurityMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.MapHealthChecks("/health");
    app.MapControllers().RequireRateLimiting("api");
    await app.RunAsync();
}
catch (Exception exception)
{
    Log.Fatal(exception, "BloggerBazar API terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

public partial class Program
{
}

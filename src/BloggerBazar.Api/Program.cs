using BloggerBazar.Api.Middleware;
using BloggerBazar.Api.OpenApi;
using BloggerBazar.Api.Routing;
using BloggerBazar.Api.Security;
using BloggerBazar.Api.Errors;
using BloggerBazar.Api.Filters;
using BloggerBazar.Application;
using BloggerBazar.Infrastructure;
using BloggerBazar.Infrastructure.Caching;
using BloggerBazar.Infrastructure.Configuration;
using BloggerBazar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
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

    var profileMediaOptions = builder.Configuration.GetSection(ProfileMediaOptions.SectionName).Get<ProfileMediaOptions>() ?? new ProfileMediaOptions();
    if (!profileMediaOptions.IsConfigured)
    {
        Log.Warning("Profile media storage is not configured. Profile image uploads are unavailable until ProfileMedia settings are provided.");
    }

    builder.Services.AddProblemDetails();
    builder.Services.AddControllers(options =>
    {
        options.Conventions.Add(new ApiV1RouteConvention());
        options.Filters.Add<ProblemDetailsEnrichmentFilter>();
    });
    builder.Services.AddOpenApi();
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<BloggerBazarDbContext>("postgres", tags: ["ready", "postgres"])
        .AddCheck<DistributedCacheHealthCheck>("cache", tags: ["ready", "cache"]);
    builder.Services.AddSwaggerGen(options => options.OperationFilter<DeprecatedOperationFilter>());
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
            if (context.HttpContext.Request.Path.StartsWithSegments("/api/webhooks/telegram"))
            {
                return ValueTask.CompletedTask;
            }

            return new ValueTask(ApiProblemWriter.WriteAsync(context.HttpContext, StatusCodes.Status429TooManyRequests));
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
    app.Logger.LogInformation("Starting BloggerBazar API. Environment {Environment}; Version {Version}", app.Environment.EnvironmentName, typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown");
    if (builder.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
        await dbContext.Database.MigrateAsync();
        app.Logger.LogInformation("Database migrations applied successfully");
    }
    if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("DevelopmentData:Seed"))
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
        await DevelopmentDataSeeder.SeedAsync(dbContext, app.Logger);
        app.Logger.LogInformation("Development data seed completed");
    }
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseSerilogRequestLogging(options => options.EnrichDiagnosticContext = (diagnosticContext, context) =>
    {
        diagnosticContext.Set("TraceId", context.TraceIdentifier);
        diagnosticContext.Set("CorrelationId", context.Items.TryGetValue(CorrelationIdMiddleware.CorrelationIdItemKey, out var correlationId) ? correlationId ?? "none" : "none");
    });
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseStatusCodePages(async statusContext =>
    {
        var context = statusContext.HttpContext;
        if (!context.Request.Path.StartsWithSegments("/api/webhooks/telegram"))
        {
            await ApiProblemWriter.WriteAsync(context, context.Response.StatusCode);
        }
    });
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
    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
    app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
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

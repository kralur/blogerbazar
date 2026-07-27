using BloggerBazar.Api.Middleware;
using BloggerBazar.Application;
using BloggerBazar.Infrastructure;
using BloggerBazar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.Services.AddProblemDetails();
    builder.Services.AddControllers();
    builder.Services.AddOpenApi();
    builder.Services.AddHealthChecks();
    var permitLimit = builder.Configuration.GetValue<int?>("RateLimiting:PermitLimit")
        ?? throw new InvalidOperationException("RateLimiting:PermitLimit must be configured.");
    var windowSeconds = builder.Configuration.GetValue<int?>("RateLimiting:WindowSeconds")
        ?? throw new InvalidOperationException("RateLimiting:WindowSeconds must be configured.");
    if (permitLimit <= 0 || windowSeconds <= 0)
    {
        throw new InvalidOperationException("Rate limiting values must be positive.");
    }

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy("api", context =>
        {
            var authorization = context.Request.Headers.Authorization.ToString();
            var partitionKey = string.IsNullOrWhiteSpace(authorization)
                ? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous"
                : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(authorization)));

            return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(windowSeconds),
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
    app.UseSerilogRequestLogging();
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    if (!app.Environment.IsProduction())
    {
        app.UseHttpsRedirection();
    }
    app.UseCors("mini-app");
    app.UseRateLimiter();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
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

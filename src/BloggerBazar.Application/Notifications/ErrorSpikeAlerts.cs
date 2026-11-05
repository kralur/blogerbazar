using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Abstractions.Telegram;
using Microsoft.Extensions.Logging;

namespace BloggerBazar.Application.Notifications;

// D60: the owners hear from the bot when the API answers with many server errors (5xx) or many rate-limit refusals
// (429) in a short time, so a broken deploy or an attack during an ad campaign is noticed in minutes, not days.
// Defaults live here (no settings file change); Alerts:ErrorSpike:Enabled=false turns it off.
public sealed class ErrorSpikeOptions
{
    public const string SectionName = "Alerts:ErrorSpike";
    public bool Enabled { get; set; } = true;
    public int WindowMinutes { get; set; } = 5;
    public int ServerErrors { get; set; } = 20;
    public int RateLimited { get; set; } = 300;
    public int QuietMinutes { get; set; } = 30;
}

public enum ErrorSpikeKind
{
    ServerErrors,
    RateLimited
}

public sealed record ErrorSpikeAlert(ErrorSpikeKind Kind, int Count, int WindowMinutes);

// Counts per fixed window, in memory of this instance. Only error answers take the lock, so ordinary requests cost
// one comparison. One alert per kind per quiet period.
public sealed class ErrorSpikeMonitor(ErrorSpikeOptions settings, TimeProvider? clock = null)
{
    private readonly object sync = new();
    private readonly TimeProvider time = clock ?? TimeProvider.System;
    private long windowNumber = -1;
    private int serverErrors;
    private int rateLimited;
    private DateTimeOffset serverErrorsAlertedAt = DateTimeOffset.MinValue;
    private DateTimeOffset rateLimitedAlertedAt = DateTimeOffset.MinValue;

    public ErrorSpikeAlert? Record(int statusCode)
    {
        var kind = statusCode >= 500 ? ErrorSpikeKind.ServerErrors : statusCode == 429 ? ErrorSpikeKind.RateLimited : (ErrorSpikeKind?)null;
        if (!settings.Enabled || kind is null) return null;

        var now = time.GetUtcNow();
        var window = Math.Max(1, settings.WindowMinutes);
        var current = now.ToUnixTimeSeconds() / (window * 60L);
        lock (sync)
        {
            if (current != windowNumber)
            {
                windowNumber = current;
                serverErrors = 0;
                rateLimited = 0;
            }

            var quiet = TimeSpan.FromMinutes(Math.Max(1, settings.QuietMinutes));
            if (kind == ErrorSpikeKind.ServerErrors)
            {
                serverErrors++;
                if (serverErrors < settings.ServerErrors || now - serverErrorsAlertedAt < quiet) return null;
                serverErrorsAlertedAt = now;
                return new ErrorSpikeAlert(ErrorSpikeKind.ServerErrors, serverErrors, window);
            }

            rateLimited++;
            if (rateLimited < settings.RateLimited || now - rateLimitedAlertedAt < quiet) return null;
            rateLimitedAlertedAt = now;
            return new ErrorSpikeAlert(ErrorSpikeKind.RateLimited, rateLimited, window);
        }
    }
}

public sealed class ErrorSpikeAlertSender(ITelegramBotClient? botClient, IConfiguredOwners owners, ILogger<ErrorSpikeAlertSender>? logger = null)
{
    public async Task SendAsync(ErrorSpikeAlert alert, CancellationToken cancellationToken)
    {
        var text = alert.Kind == ErrorSpikeKind.ServerErrors
            ? BotMessages.ServerErrorSpike(alert.Count, alert.WindowMinutes)
            : BotMessages.RateLimitSpike(alert.Count, alert.WindowMinutes);
        logger?.LogWarning("Error spike: {Kind} {Count} in {Minutes} min, telling {Owners} owner(s)", alert.Kind, alert.Count, alert.WindowMinutes, owners.All.Count);
        foreach (var owner in owners.All)
        {
            await BestEffortTelegramNotification.SendAsync(botClient, logger, owner, text, cancellationToken);
        }
    }
}

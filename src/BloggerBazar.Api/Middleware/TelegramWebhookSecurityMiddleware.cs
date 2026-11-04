using BloggerBazar.Api.Controllers;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Infrastructure.Security;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Options;

namespace BloggerBazar.Api.Middleware;

public sealed class TelegramWebhookSecurityMiddleware(
    RequestDelegate next,
    ITelegramWebhookValidator webhookValidator,
    IOptions<TelegramOptions> options,
    ILogger<TelegramWebhookSecurityMiddleware> logger)
{
    private const string WebhookPath = "/api/webhooks/telegram";
    private const string SecretHeaderName = "X-Telegram-Bot-Api-Secret-Token";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsWebhookRequest(context))
        {
            await next(context);
            return;
        }

        var maxBodyBytes = options.Value.WebhookMaxBodyBytes;
        if (maxBodyBytes <= 0)
        {
            throw new InvalidOperationException("Telegram:WebhookMaxBodyBytes must be positive.");
        }

        if (!context.Request.Headers.TryGetValue(SecretHeaderName, out var secretHeader)
            || secretHeader.Count != 1
            || !webhookValidator.IsValid(secretHeader[0]))
        {
            await RejectAsync(context, StatusCodes.Status401Unauthorized, "invalid_secret");
            return;
        }

        if (!context.Request.HasJsonContentType())
        {
            await RejectAsync(context, StatusCodes.Status415UnsupportedMediaType, "unsupported_media_type");
            return;
        }

        if (context.Request.ContentLength is long declaredLength && declaredLength > maxBodyBytes)
        {
            await RejectAsync(context, StatusCodes.Status413PayloadTooLarge, "payload_too_large");
            return;
        }

        var bodySizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (bodySizeFeature is { IsReadOnly: false })
        {
            bodySizeFeature.MaxRequestBodySize = maxBodyBytes;
        }

        try
        {
            await next(context);
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge && !context.Response.HasStarted)
        {
            await RejectAsync(context, StatusCodes.Status413PayloadTooLarge, "payload_too_large");
        }
    }

    // Pre-launch audit 2026-11-04 (BLOCKER): an exact path comparison let "/api/webhooks/telegram/" (trailing slash,
    // other letter case) reach the controller without the secret, so anyone could post fake updates (a contact card
    // that sets another person's phone). The check now follows the endpoint routing chose, whatever the spelling,
    // plus the path itself (and anything under it) for requests that matched no endpoint.
    internal static bool IsWebhookRequest(HttpContext context)
    {
        var action = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
        if (action?.ControllerTypeInfo.AsType() == typeof(TelegramWebhookController))
        {
            return true;
        }

        return HttpMethods.IsPost(context.Request.Method) && context.Request.Path.StartsWithSegments(WebhookPath, StringComparison.OrdinalIgnoreCase);
    }

    private async Task RejectAsync(HttpContext context, int statusCode, string outcome)
    {
        logger.LogWarning(
            "Telegram webhook request rejected. Outcome {Outcome}; TraceId {TraceId}; SourceIp {SourceIp}",
            outcome,
            context.TraceIdentifier,
            context.Connection.RemoteIpAddress?.ToString());
        context.Response.StatusCode = statusCode;
        context.Response.ContentLength = 0;
        await context.Response.CompleteAsync();
    }
}

using BloggerBazar.Api.Middleware;
using BloggerBazar.Api.Security;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.AdminWeb;
using BloggerBazar.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BloggerBazar.Api.Controllers;

// Web admin sign-in (admin-web site): Telegram Login Widget data in, an HttpOnly session cookie out.
// Switched off (404) until AdminWeb:Origin names the admin site.
[ApiController]
[Route("api/admin-web")]
public sealed class AdminWebController(
    ISender sender,
    AdminWebRequestGuard guard,
    IAdminWebSessionStore sessions,
    IOptions<TelegramOptions> telegramOptions,
    ITelegramWebAppValidator telegramValidator) : TelegramControllerBase(telegramValidator)
{
    // The bot the sign-in widget belongs to; public anyway (t.me/<bot>).
    [HttpGet("config")]
    public ActionResult<AdminWebConfigResponse> GetConfig()
    {
        if (!guard.IsEnabled) return NotFound();
        return Ok(new AdminWebConfigResponse(telegramOptions.Value.BotUsername.Trim().TrimStart('@')));
    }

    // The widget sends a handful of short fields; anything bigger is refused before it is read.
    [HttpPost("session")]
    [AdminWebSignInLimit]
    [RequestSizeLimit(16_384)]
    public async Task<ActionResult<AdminWebSessionDto>> StartSession([FromBody] Dictionary<string, string> fields, CancellationToken cancellationToken)
    {
        if (!guard.IsEnabled) return NotFound();
        guard.EnsureSameOrigin(Request);
        var result = await sender.Send(new StartAdminWebSessionCommand(fields, CorrelationId()), cancellationToken);
        // A new sign-in replaces the session this browser had.
        if (AdminWebCookie.Read(Request) is { } previous)
        {
            await sessions.RemoveAsync(previous, cancellationToken);
        }

        Response.Cookies.Append(AdminWebCookie.Name, result.Token, AdminWebCookie.Create(result.Session.ExpiresAtUtc));
        return Ok(result.Session);
    }

    [HttpGet("session")]
    public async Task<ActionResult<AdminWebSessionDto>> GetSession(CancellationToken cancellationToken)
    {
        if (!guard.IsEnabled) return NotFound();
        var token = AdminWebCookie.Read(Request) ?? throw AdminWebRequestGuard.SessionEnded();
        return Ok(await sender.Send(new GetAdminWebSessionQuery(token), cancellationToken));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (!guard.IsEnabled) return NotFound();
        guard.EnsureSameOrigin(Request);
        if (AdminWebCookie.Read(Request) is { } token)
        {
            await sender.Send(new EndAdminWebSessionCommand(token, CorrelationId()), cancellationToken);
        }

        Response.Cookies.Delete(AdminWebCookie.Name, AdminWebCookie.Expired());
        return NoContent();
    }

    // The journal entry for a CSV export the browser is about to save; no entry, no file.
    [HttpPost("exports")]
    [RequestSizeLimit(4_096)]
    public async Task<IActionResult> RecordExport([FromBody] AdminWebExportRequest request, CancellationToken cancellationToken)
    {
        if (!guard.IsEnabled) return NotFound();
        guard.EnsureSameOrigin(Request);
        var token = AdminWebCookie.Read(Request) ?? throw AdminWebRequestGuard.SessionEnded();
        await sender.Send(new RecordAdminWebExportCommand(token, request.Kind, request.Rows, CorrelationId()), cancellationToken);
        return NoContent();
    }

    // Ends this person's sessions in every browser, this one included.
    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutEverywhere(CancellationToken cancellationToken)
    {
        if (!guard.IsEnabled) return NotFound();
        guard.EnsureSameOrigin(Request);
        var token = AdminWebCookie.Read(Request) ?? throw AdminWebRequestGuard.SessionEnded();
        await sender.Send(new EndAllAdminWebSessionsCommand(token, CorrelationId()), cancellationToken);
        Response.Cookies.Delete(AdminWebCookie.Name, AdminWebCookie.Expired());
        return NoContent();
    }

    private string? CorrelationId() =>
        HttpContext.Items.TryGetValue(CorrelationIdMiddleware.CorrelationIdItemKey, out var value) ? value?.ToString() : null;
}

public sealed record AdminWebConfigResponse(string BotUsername);

public sealed record AdminWebExportRequest(string Kind, int Rows);

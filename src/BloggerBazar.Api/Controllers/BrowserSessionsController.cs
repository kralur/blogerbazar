using BloggerBazar.Api.Middleware;
using BloggerBazar.Api.Security;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.BrowserSessions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BloggerBazar.Api.Controllers;

// D64 stage B: signing a browser in through the Mini App, and the person's list of browsers.
// The browser asks (sign-in), shows the link and a number, and checks back; the Mini App, opened from the link inside
// Telegram, shows what asks to sign in (preview) and confirms it with the number or declines it.
[ApiController]
[Route("api/web")]
public sealed class BrowserSessionsController(ISender sender, BrowserRequestGuard guard, ITelegramWebAppValidator telegramValidator)
    : TelegramControllerBase(telegramValidator)
{
    private const string StartParameterPrefix = "web_";

    [HttpPost("sign-in")]
    [BrowserSignInLimit]
    [RequestSizeLimit(1_024)]
    public async Task<ActionResult<BrowserSignInStartResponse>> StartSignIn(CancellationToken cancellationToken)
    {
        if (!guard.IsEnabled) return NotFound();
        guard.EnsureSameOrigin(Request);
        var started = await sender.Send(new StartBrowserSignInCommand(BrowserDevices.FromUserAgent(Request.Headers.UserAgent.ToString())), cancellationToken);
        Response.Cookies.Append(BrowserCookies.SignIn, $"{started.Code}.{started.Secret}", BrowserCookies.Create(started.ExpiresAtUtc));
        var link = $"https://t.me/{guard.BotUsername}?startapp={StartParameterPrefix}{started.Code}";
        return Ok(new BrowserSignInStartResponse(started.Code, started.Number, link, started.ExpiresAtUtc));
    }

    // The browser checks every few seconds; "approved" comes with the session cookie.
    [HttpPost("sign-in/check")]
    [RequestSizeLimit(1_024)]
    public async Task<ActionResult<BrowserSignInStatusResponse>> CheckSignIn(CancellationToken cancellationToken)
    {
        if (!guard.IsEnabled) return NotFound();
        guard.EnsureSameOrigin(Request);
        var parts = BrowserCookies.Read(Request, BrowserCookies.SignIn)?.Split('.');
        if (parts is not { Length: 2 })
        {
            return Ok(new BrowserSignInStatusResponse(BrowserSignInStatuses.Expired, null));
        }

        var result = await sender.Send(new CheckBrowserSignInCommand(parts[0], parts[1], CorrelationId()), cancellationToken);
        if (result.Status != BrowserSignInStatuses.Pending)
        {
            Response.Cookies.Delete(BrowserCookies.SignIn, BrowserCookies.Expired());
        }

        if (result.Ticket is { } ticket)
        {
            // A new sign-in replaces the session this browser had.
            if (BrowserCookies.Read(Request, BrowserCookies.Session) is { } previous)
            {
                await sender.Send(new SignOutBrowserCommand(previous, CorrelationId()), cancellationToken);
            }

            Response.Cookies.Append(BrowserCookies.Session, ticket.Token, BrowserCookies.Create(ticket.Session.ExpiresAtUtc));
        }

        return Ok(new BrowserSignInStatusResponse(result.Status, result.FirstName));
    }

    // Mini App only (initData): what the link asks for, and whether this person may sign in to a browser.
    [HttpPost("sign-in/preview")]
    [RequestSizeLimit(1_024)]
    public async Task<ActionResult<BrowserSignInPreviewDto>> PreviewSignIn(BrowserSignInCodeRequest request, CancellationToken cancellationToken)
    {
        if (!guard.IsEnabled) return NotFound();
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new PreviewBrowserSignInQuery(actor.Id, request.Code), cancellationToken));
    }

    [HttpPost("sign-in/confirm")]
    [RequestSizeLimit(1_024)]
    public async Task<IActionResult> ConfirmSignIn(ConfirmBrowserSignInRequest request, CancellationToken cancellationToken)
    {
        if (!guard.IsEnabled) return NotFound();
        var actor = GetTelegramUser();
        await sender.Send(new ConfirmBrowserSignInCommand(actor.Id, request.Code, request.Number), cancellationToken);
        return NoContent();
    }

    [HttpPost("sign-in/decline")]
    [RequestSizeLimit(1_024)]
    public async Task<IActionResult> DeclineSignIn(BrowserSignInCodeRequest request, CancellationToken cancellationToken)
    {
        if (!guard.IsEnabled) return NotFound();
        var actor = GetTelegramUser();
        await sender.Send(new DeclineBrowserSignInCommand(actor.Id, request.Code), cancellationToken);
        return NoContent();
    }

    // The person's browsers, from the Mini App or from one of those browsers.
    [HttpGet("sessions")]
    [BrowserSessionAllowed]
    public async Task<ActionResult<IReadOnlyList<BrowserSessionDto>>> GetSessions(CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new GetBrowserSessionsQuery(actor.Id, BrowserRequestGuard.Current(HttpContext)?.Session.Id), cancellationToken));
    }

    [HttpDelete("sessions/{sessionId}")]
    [BrowserSessionAllowed]
    public async Task<IActionResult> EndSession(string sessionId, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        await sender.Send(new EndBrowserSessionCommand(actor.Id, sessionId), cancellationToken);
        if (BrowserRequestGuard.Current(HttpContext)?.Session.Id == sessionId)
        {
            Response.Cookies.Delete(BrowserCookies.Session, BrowserCookies.Expired());
        }

        return NoContent();
    }

    // Ends every browser session of this person, this browser included.
    [HttpPost("sessions/end-all")]
    [BrowserSessionAllowed]
    public async Task<IActionResult> EndAllSessions(CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        await sender.Send(new EndAllBrowserSessionsCommand(actor.Id, CorrelationId()), cancellationToken);
        if (BrowserRequestGuard.Current(HttpContext) is not null)
        {
            Response.Cookies.Delete(BrowserCookies.Session, BrowserCookies.Expired());
        }

        return NoContent();
    }

    // The browser's own "Sign out".
    [HttpPost("sign-out")]
    public async Task<IActionResult> SignOutBrowser(CancellationToken cancellationToken)
    {
        if (!guard.IsEnabled) return NotFound();
        guard.EnsureSameOrigin(Request);
        if (BrowserCookies.Read(Request, BrowserCookies.Session) is { } token)
        {
            await sender.Send(new SignOutBrowserCommand(token, CorrelationId()), cancellationToken);
        }

        Response.Cookies.Delete(BrowserCookies.Session, BrowserCookies.Expired());
        return NoContent();
    }

    private string? CorrelationId() =>
        HttpContext.Items.TryGetValue(CorrelationIdMiddleware.CorrelationIdItemKey, out var value) ? value?.ToString() : null;
}

public sealed record BrowserSignInStartResponse(string Code, int Number, string Link, DateTime ExpiresAtUtc);

// Status: "pending", "approved", "declined" or "expired".
public sealed record BrowserSignInStatusResponse(string Status, string? FirstName);

public sealed record BrowserSignInCodeRequest(string Code);

public sealed record ConfirmBrowserSignInRequest(string Code, int Number);

using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.Admin;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BloggerBazar.Api.Controllers;

// Web admin analytics (D57, phase 2): read-only, Owner/Admin only, Mini App initData or the web admin cookie.
// Dates are Tashkent calendar days, "yyyy-MM-dd"; without them the last 30 days (retention: 90).
[ApiController]
[Route("api/admin/analytics")]
public sealed class AdminAnalyticsController(ISender sender, ITelegramWebAppValidator telegramValidator) : TelegramControllerBase(telegramValidator)
{
    [HttpGet("daily")]
    public async Task<ActionResult<AdminDailyAnalyticsDto>> GetDaily([FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null, CancellationToken cancellationToken = default)
    {
        var actorId = await GetAdminActorIdAsync(cancellationToken);
        return Ok(await sender.Send(new GetAdminDailyAnalyticsQuery(actorId, from, to), cancellationToken));
    }

    [HttpGet("money")]
    public async Task<ActionResult<AdminMoneyAnalyticsDto>> GetMoney([FromQuery] int months = AdminAnalytics.DefaultMonths, CancellationToken cancellationToken = default)
    {
        var actorId = await GetAdminActorIdAsync(cancellationToken);
        return Ok(await sender.Send(new GetAdminMoneyAnalyticsQuery(actorId, months), cancellationToken));
    }

    [HttpGet("geography")]
    public async Task<ActionResult<AdminGeographyDto>> GetGeography(CancellationToken cancellationToken)
    {
        var actorId = await GetAdminActorIdAsync(cancellationToken);
        return Ok(await sender.Send(new GetAdminGeographyQuery(actorId), cancellationToken));
    }

    [HttpGet("retention")]
    public async Task<ActionResult<AdminRetentionDto>> GetRetention([FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null, CancellationToken cancellationToken = default)
    {
        var actorId = await GetAdminActorIdAsync(cancellationToken);
        return Ok(await sender.Send(new GetAdminRetentionQuery(actorId, from, to), cancellationToken));
    }

    [HttpGet("sources")]
    public async Task<ActionResult<AdminSourcesDto>> GetSources(CancellationToken cancellationToken)
    {
        var actorId = await GetAdminActorIdAsync(cancellationToken);
        return Ok(await sender.Send(new GetAdminSourcesQuery(actorId), cancellationToken));
    }
}

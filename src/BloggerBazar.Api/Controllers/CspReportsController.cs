using BloggerBazar.Api.Security;
using BloggerBazar.Application.Abstractions.Security;
using Microsoft.AspNetCore.Mvc;

namespace BloggerBazar.Api.Controllers;

// Security B4: the app sends what the report-only Content-Security-Policy would have blocked; admins read the list.
[ApiController]
[Route("api")]
public sealed class CspReportsController(CspReportLog log, IAdminAccessPolicy adminAccess, ITelegramWebAppValidator telegramValidator) : TelegramControllerBase(telegramValidator)
{
    [HttpPost("csp-reports")]
    public IActionResult Report(CspReportRequest request)
    {
        GetTelegramIdentity();
        log.Add(request.Directive, request.Blocked, request.Source, DateTime.UtcNow);
        return NoContent();
    }

    [HttpGet("admin/csp-reports")]
    public ActionResult<IReadOnlyList<CspReportEntry>> List()
    {
        var actor = GetTelegramUser();
        adminAccess.EnsureAllowed(actor.Id);
        return Ok(log.Snapshot());
    }
}

public sealed record CspReportRequest(string? Directive, string? Blocked, string? Source);

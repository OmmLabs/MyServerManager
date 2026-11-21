using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyServerManager.Models;
using MyServerManager.Security;
using MyServerManager.Services;

namespace MyServerManager.Controllers;

[Authorize(Policy = AuthorizationPolicies.Administrator)]
public sealed class AuditController(AuditService audit, TimeProvider time) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(DateOnly? from, DateOnly? to, [FromQuery] AuditAction? action, CancellationToken cancellationToken)
    {
        to ??= DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        from ??= to.Value.DayNumber >= 6 ? to.Value.AddDays(-6) : DateOnly.MinValue;
        if (!ModelState.IsValid || from > to || to.Value.DayNumber - from.Value.DayNumber > 366)
            return BadRequest("Select a valid UTC date range of at most 367 days and a supported action.");

        return View(new AuditPage(from.Value, to.Value, action, await audit.HistoryAsync(from.Value, to.Value, action, cancellationToken)));
    }
}

public sealed record AuditPage(DateOnly From, DateOnly To, AuditAction? Action, List<AuditEntry> Entries);

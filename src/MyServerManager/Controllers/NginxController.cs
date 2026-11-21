using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyServerManager.Models;
using MyServerManager.Security;
using MyServerManager.Services;

namespace MyServerManager.Controllers;

public sealed class NginxController(InfrastructureMonitor monitor, NginxManagement management, AuditService audit) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var status = monitor.NginxAsync(cancellationToken);
        var lastOperation = audit.LatestAsync(ServiceAllowlist.Nginx, cancellationToken);
        await Task.WhenAll(status, lastOperation);
        return View(new NginxPage(status.Result, lastOperation.Result));
    }

    [HttpGet]
    public async Task<IActionResult> Status(bool refresh, CancellationToken cancellationToken) =>
        PartialView("_Status", await monitor.NginxAsync(cancellationToken, refresh));

    [HttpPost, Authorize(Policy = AuthorizationPolicies.Administrator)]
    public async Task<IActionResult> Operate(string? operation, bool confirmed)
    {
        var action = operation switch
        {
            "check" => NginxAction.Check,
            "reload" => NginxAction.Reload,
            "restart" => NginxAction.Restart,
            _ => (NginxAction?)null
        };
        if (!ModelState.IsValid || action is null || action != NginxAction.Check && !confirmed)
            return BadRequest();

        var result = await management.ExecuteAsync(action.Value, User.Identity!.Name!);
        TempData["Message"] = result.Message;
        TempData["Succeeded"] = result.Succeeded;
        return RedirectToAction(nameof(Index));
    }
}

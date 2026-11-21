using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyServerManager.Configuration;
using MyServerManager.Security;
using MyServerManager.Services;

namespace MyServerManager.Controllers;

public sealed class DatabaseController(InfrastructureMonitor monitor, SqlServerManagement management, ManagerMode mode) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) => View(await monitor.SqlAsync(cancellationToken));

    [HttpGet]
    public async Task<IActionResult> Status(bool refresh, CancellationToken cancellationToken) =>
        PartialView("_Status", await monitor.SqlAsync(cancellationToken, refresh));

    [HttpPost, Authorize(Policy = AuthorizationPolicies.Administrator)]
    public async Task<IActionResult> Restart(bool confirmed)
    {
        if (!mode.UsesSsh)
            return NotFound();
        if (!confirmed)
            return BadRequest();

        var result = await management.RestartAsync(User.Identity!.Name!);
        TempData["Message"] = result.Message;
        TempData["Succeeded"] = result.Succeeded;
        return RedirectToAction(nameof(Index));
    }
}

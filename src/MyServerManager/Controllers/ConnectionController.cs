using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyServerManager.Configuration;
using MyServerManager.Models;
using MyServerManager.Security;
using MyServerManager.Services;

namespace MyServerManager.Controllers;

[Authorize(Policy = AuthorizationPolicies.Administrator)]
public sealed class ConnectionController(ManagerMode mode, ServerProfileStore profiles, SshConnection connection,
    OperationCoordinator operations) : Controller
{
    [HttpGet]
    public IActionResult Index() => mode.UsesSsh && profiles.Current is { } server
        ? View(model: server.Endpoint.DisplayName) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Deregister(bool confirmed)
    {
        if (!mode.UsesSsh || profiles.Current is not { } server)
            return NotFound();
        if (!confirmed)
            return BadRequest();

        var result = await operations.ExecuteAsync(User.Identity!.Name!, server.Endpoint.DisplayName, null,
            AuditAction.DeregisterServer, () => connection.DeregisterAsync(User));
        TempData["Message"] = result.Message;
        TempData["Succeeded"] = result.Succeeded;
        if (!result.Succeeded)
            return RedirectToAction(nameof(Index));

        await HttpContext.SignOutAsync();
        return RedirectToAction("Index", "Setup");
    }
}

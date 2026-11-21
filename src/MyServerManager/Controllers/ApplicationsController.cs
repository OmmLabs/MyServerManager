using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyServerManager.Models;
using MyServerManager.Security;
using MyServerManager.Services;

namespace MyServerManager.Controllers;

public sealed class ApplicationsController(ApplicationReadService applications, ApplicationRegistry registry,
    ApplicationManagement management) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await applications.GetAsync(cancellationToken, includeDisabled: true));

    [HttpGet]
    public async Task<IActionResult> Table(bool refresh, CancellationToken cancellationToken) =>
        PartialView("_Table", await applications.GetAsync(cancellationToken, refresh, includeDisabled: true));

    [HttpGet]
    public async Task<IActionResult> Status(string? service, CancellationToken cancellationToken) =>
        await GetApplicationAsync(service, false, cancellationToken) is { } application ? View(application) : NotFound();

    [HttpGet]
    public async Task<IActionResult> Details(string? service, bool refresh, CancellationToken cancellationToken) =>
        await GetApplicationAsync(service, refresh, cancellationToken) is { } application ? PartialView("_Details", application) : NotFound();

    private async Task<ApplicationStatus?> GetApplicationAsync(string? service, bool refresh, CancellationToken cancellationToken)
    {
        if (!ServiceAllowlist.IsApplicationUnit(service))
            return null;
        return (await applications.GetAsync(cancellationToken, refresh, includeDisabled: true))
            .FirstOrDefault(app => app.Definition.ServiceName == service);
    }

    [HttpPost, Authorize(Policy = AuthorizationPolicies.Administrator)]
    public async Task<IActionResult> Operate(string? service, string? operation, bool confirmed, string? source,
        CancellationToken cancellationToken)
    {
        var action = operation switch
        {
            "start" => ServiceAction.Start,
            "stop" => ServiceAction.Stop,
            "restart" => ServiceAction.Restart,
            _ => (ServiceAction?)null
        };
        if (!ModelState.IsValid || action is null || action != ServiceAction.Start && !confirmed)
            return BadRequest();

        var entry = await registry.FindEnabledAsync(service, cancellationToken);
        if (entry is null)
            return NotFound();

        var result = await management.ControlAsync(entry, action.Value, User.Identity!.Name!);
        TempData["Message"] = result.Message;
        TempData["Succeeded"] = result.Succeeded;
        return source switch
        {
            "dashboard" => RedirectToAction("Index", "Dashboard"),
            "details" => RedirectToAction(nameof(Status), new { service }),
            _ => RedirectToAction(nameof(Index))
        };
    }
}

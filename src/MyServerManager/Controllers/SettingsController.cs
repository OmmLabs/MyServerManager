using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyServerManager.Models;
using MyServerManager.Security;
using MyServerManager.Services;

namespace MyServerManager.Controllers;

[Authorize(Policy = AuthorizationPolicies.Administrator)]
public sealed class SettingsController(ApplicationRegistry registry, PreferencesStore preferences,
    ServiceAllowlist allowlist, SettingsService settings, DotNetDiscovery discovery) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? service, CancellationToken cancellationToken)
    {
        var entries = await registry.ReadAsync(cancellationToken);
        var selected = entries.FirstOrDefault(app => app.ServiceName == service);
        if (service is not null && selected is null)
            return NotFound();
        return View(new SettingsPage(entries, await allowlist.ReadApprovedAsync(cancellationToken),
            selected ?? new(), selected?.ServiceName, preferences.Current, allowlist.RequiresOperatorApproval));
    }

    [HttpGet]
    public async Task<IActionResult> Discovery(CancellationToken cancellationToken)
    {
        var registered = (await registry.ReadAsync(cancellationToken)).Select(application => application.ServiceName)
            .ToHashSet(StringComparer.Ordinal);
        return View(new DiscoveryPage(discovery.Latest, registered));
    }

    [HttpPost]
    public async Task<IActionResult> ScanApplications(CancellationToken cancellationToken)
    {
        await discovery.ScanAsync(cancellationToken);
        return RedirectToAction(nameof(Discovery));
    }

    [HttpGet]
    public async Task<IActionResult> ReviewDiscovered(string? service, CancellationToken cancellationToken)
    {
        var found = discovery.Latest?.Applications.FirstOrDefault(application => application.Definition.ServiceName == service);
        if (found is null)
        {
            TempData["Message"] = "Scan for apps again before reviewing a service.";
            return RedirectToAction(nameof(Discovery));
        }

        var entries = await registry.ReadAsync(cancellationToken);
        if (entries.Any(application => application.ServiceName == service))
            return RedirectToAction(nameof(Index), new { service });

        var order = Math.Min(10000, (entries.LastOrDefault()?.DisplayOrder ?? 0) + 10);
        return View(nameof(Index), new SettingsPage(entries, await allowlist.ReadApprovedAsync(cancellationToken),
            found.Definition with { DisplayOrder = order }, null, preferences.Current, allowlist.RequiresOperatorApproval));
    }

    [HttpPost]
    public async Task<IActionResult> SaveApplication([Bind(Prefix = "Editor")] ApplicationDefinition editor,
        string? originalService, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid && ApplicationRegistry.Validate(editor) is { } validation)
            ModelState.AddModelError("", validation);
        if (!ModelState.IsValid)
            return View(nameof(Index), new SettingsPage(await registry.ReadAsync(cancellationToken),
                await allowlist.ReadApprovedAsync(cancellationToken), editor, originalService, preferences.Current, allowlist.RequiresOperatorApproval));

        var result = await settings.SaveApplicationAsync(editor, originalService, User.Identity!.Name!);
        TempData["Message"] = result.Message;
        TempData["Succeeded"] = result.Succeeded;
        return RedirectToAction(nameof(Index), new { service = result.Succeeded ? editor.ServiceName : originalService });
    }

    [HttpPost]
    public async Task<IActionResult> RemoveApplication(string? service, bool confirmed, string? source,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || !confirmed || !ServiceAllowlist.IsApplicationUnit(service))
            return BadRequest();

        var entry = (await registry.ReadAsync(cancellationToken)).FirstOrDefault(application => application.ServiceName == service);
        if (entry is null)
            return NotFound();

        var result = await settings.RemoveApplicationAsync(entry, User.Identity!.Name!);
        TempData["Message"] = result.Message;
        TempData["Succeeded"] = result.Succeeded;
        return source == "settings"
            ? RedirectToAction(nameof(Index))
            : RedirectToAction("Index", "Applications");
    }

    [HttpPost]
    public async Task<IActionResult> SavePreferences([Bind(Prefix = "Preferences")] ManagerPreferences value,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(nameof(Index), new SettingsPage(await registry.ReadAsync(cancellationToken),
                await allowlist.ReadApprovedAsync(cancellationToken), new(), null, value, allowlist.RequiresOperatorApproval));

        var result = await settings.SavePreferencesAsync(value, User.Identity!.Name!);
        TempData["Message"] = result.Message;
        TempData["Succeeded"] = result.Succeeded;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ChangePassword(PasswordChange input)
    {
        if (!allowlist.RequiresOperatorApproval)
            return NotFound();
        if (!ModelState.IsValid)
        {
            TempData["Message"] = "Enter your current password and matching new passwords of 15–128 characters.";
            TempData["Succeeded"] = false;
            return RedirectToAction(nameof(Index));
        }

        var result = await settings.ChangePasswordAsync(input.CurrentPassword, input.NewPassword, User.Identity!.Name!);
        TempData["Message"] = result.Message;
        TempData["Succeeded"] = result.Succeeded;
        if (!result.Succeeded)
            return RedirectToAction(nameof(Index));

        await HttpContext.SignOutAsync();
        return RedirectToAction("Login", "Account");
    }
}

public sealed record SettingsPage(ApplicationDefinition[] Applications, HashSet<string> Approved,
    ApplicationDefinition Editor, string? OriginalService, ManagerPreferences Preferences, bool RequiresOperatorApproval);

public sealed record DiscoveryPage(DiscoveryResult? Scan, HashSet<string> Registered);

public sealed record PasswordChange
{
    [Required, StringLength(128)] public string CurrentPassword { get; init; } = "";
    [Required, StringLength(128, MinimumLength = 15)] public string NewPassword { get; init; } = "";
    [Required, Compare(nameof(NewPassword))] public string ConfirmPassword { get; init; } = "";
}

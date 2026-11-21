using Microsoft.AspNetCore.Mvc;
using MyServerManager.Services;

namespace MyServerManager.Controllers;

public sealed class WebsitesController(ApplicationRegistry registry, WebsiteHealthService health) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) => View(await health.GetAsync(cancellationToken));

    [HttpGet]
    public async Task<IActionResult> Table(CancellationToken cancellationToken) => PartialView("_Table", await health.GetAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Check(string? service, CancellationToken cancellationToken)
    {
        var entry = await registry.FindEnabledAsync(service, cancellationToken);
        if (entry?.WebsiteUrl is null)
            return NotFound();

        var result = await health.CheckAsync(entry, true, cancellationToken);
        TempData["Message"] = result.Available ? $"{entry.DisplayName} responded with HTTP {result.StatusCode}." :
            $"{entry.DisplayName} is unavailable. {result.Error}";
        TempData["Succeeded"] = result.Available;
        return RedirectToAction(nameof(Index));
    }
}

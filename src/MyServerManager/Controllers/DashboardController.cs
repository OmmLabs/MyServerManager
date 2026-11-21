using Microsoft.AspNetCore.Mvc;
using MyServerManager.Models;
using MyServerManager.Services;

namespace MyServerManager.Controllers;

public sealed class DashboardController(ApplicationReadService applications, ServerMetrics server) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) => View(await SnapshotAsync(false, cancellationToken));

    [HttpGet]
    public async Task<IActionResult> Summary(bool refresh, CancellationToken cancellationToken) =>
        PartialView("_Summary", await SnapshotAsync(refresh, cancellationToken));

    [HttpGet]
    public async Task<IActionResult> ApplicationList(bool refresh, CancellationToken cancellationToken) =>
        PartialView("_ApplicationList", await applications.GetAsync(cancellationToken, refresh));

    private async Task<DashboardPage> SnapshotAsync(bool manual, CancellationToken cancellationToken)
    {
        var appStatus = applications.GetAsync(cancellationToken, manual, includeDisabled: true);
        var metrics = server.GetAsync(manual, cancellationToken);
        await Task.WhenAll(appStatus, metrics);
        return new(appStatus.Result, metrics.Result);
    }
}

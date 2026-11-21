using Microsoft.AspNetCore.Mvc;
using MyServerManager.Services;

namespace MyServerManager.Controllers;

public sealed class MonitoringController(ServerMetrics server) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) => View(await server.GetAsync(false, cancellationToken));

    [HttpGet]
    public async Task<IActionResult> Metrics(bool refresh, CancellationToken cancellationToken) =>
        PartialView("_Metrics", await server.GetAsync(refresh, cancellationToken));
}

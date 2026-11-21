using Microsoft.AspNetCore.Mvc;
using MyServerManager.Models;
using MyServerManager.Services;

namespace MyServerManager.Controllers;

public sealed class LogsController(ApplicationRegistry registry, JournalService journals) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? service, int lines = 100, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid || lines is not (100 or 250 or 500))
            return BadRequest();
        var entries = (await registry.ReadAsync(cancellationToken)).Where(entry => entry.Enabled).ToArray();
        var selected = service is null ? entries.FirstOrDefault() : entries.FirstOrDefault(entry => entry.ServiceName == service);
        if (service is not null && selected is null)
            return NotFound();

        var snapshot = selected is null ? new JournalSnapshot([], null) : await journals.ReadAsync(selected, lines, cancellationToken);
        return View(new LogsPage(entries, selected, lines, snapshot));
    }
}

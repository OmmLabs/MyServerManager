using MyServerManager.Models;
using MyServerManager.Security;

namespace MyServerManager.Services;

public sealed record DiscoveredApplication(ApplicationDefinition Definition, UnitState State);
public sealed record DiscoveryResult(DiscoveredApplication[] Applications, DateTimeOffset CheckedAt, string? Notice = null);

public sealed class DotNetDiscovery(ICommandRunner commands, TimeProvider time, LinuxHostRevision revision)
{
    private readonly RefreshCache<DiscoveryResult> _scan = new(time, revision);

    public DiscoveryResult? Latest => _scan.TryRead(out var scan) && time.GetUtcNow() - scan.CheckedAt < TimeSpan.FromMinutes(10)
        ? scan : null;

    public Task<DiscoveryResult> ScanAsync(CancellationToken cancellationToken) =>
        _scan.GetAsync(TimeSpan.FromSeconds(3), ScanHostAsync, cancellationToken);

    private async Task<DiscoveryResult> ScanHostAsync(CancellationToken cancellationToken)
    {
        var result = await commands.RunAsync(DotNetDiscoveryCommand.Create(), cancellationToken);
        var checkedAt = time.GetUtcNow();
        if (!result.Succeeded)
            return new([], checkedAt, result.FailureMessage("Application discovery"));

        var fields = result.Output.Split('\0');
        if (fields.Length == 0 || fields[0] is not ("0" or "1"))
            return new([], checkedAt, "The server did not return a usable scan.");

        var found = new Dictionary<string, DiscoveredApplication>(StringComparer.Ordinal);
        // Ignore any final record cut off by the output limit.
        for (var i = 1; i + 5 < fields.Length; i += 5)
        {
            var unit = fields[i];
            if (!ServiceAllowlist.IsApplicationUnit(unit))
                continue;
            var name = unit[..^".service".Length].Replace('-', ' ').Replace('_', ' ');
            var definition = new ApplicationDefinition
            {
                DisplayName = name[..Math.Min(100, name.Length)],
                ServiceName = unit,
                DeploymentDirectory = fields[i + 2] is { Length: > 0 and <= 400 } directory ? directory : null,
                AssemblyName = fields[i + 3] is { Length: > 0 and <= 150 } assembly ? assembly : null,
                Port = int.TryParse(fields[i + 4], out var port) && port is >= 1 and <= 65535 ? port : null
            };
            if (ApplicationRegistry.Validate(definition) is not null)
                definition = definition with { DeploymentDirectory = null, AssemblyName = null };
            if (ApplicationRegistry.Validate(definition) is not null)
                continue;

            var state = fields[i + 1] switch
            {
                "active" => UnitState.Running,
                "inactive" => UnitState.Stopped,
                "failed" => UnitState.Failed,
                _ => UnitState.Unknown
            };
            found.TryAdd(unit, new(definition, state));
        }

        return new(found.Values.OrderBy(application => application.Definition.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray(),
            checkedAt, result.OutputTruncated || fields[0] == "1" ? "The scan reached its size limit. Review the results below." : null);
    }
}

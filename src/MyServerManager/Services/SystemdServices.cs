using MyServerManager.Models;
using MyServerManager.Security;

namespace MyServerManager.Services;

public sealed class SystemdServices(ICommandRunner commands, LinuxHostMetrics metrics)
{
    public async Task<ServiceStatus> ReadAsync(string unit, CancellationToken cancellationToken = default) =>
        (await ReadManyAsync([unit], cancellationToken))[unit];

    public async Task<ServiceStatus> InspectAsync(string unit, CancellationToken cancellationToken = default) =>
        (await ReadManyAsync([unit], cancellationToken, includeMetrics: false))[unit];

    public async Task<Dictionary<string, ServiceStatus>> ReadManyAsync(string[] units,
        CancellationToken cancellationToken = default, bool includeMetrics = true)
    {
        var statuses = units.ToDictionary(unit => unit, unit => new ServiceStatus(unit), StringComparer.Ordinal);
        if (units.Length == 0)
            return statuses;

        var result = await commands.RunAsync(LinuxCommands.Show(units), cancellationToken);
        if (result.ConnectionUnavailable)
            return units.ToDictionary(unit => unit, unit => new ServiceStatus(unit, Detail: "Connect to Ubuntu over SSH",
                ConnectionUnavailable: true), StringComparer.Ordinal);
        if (result.TimedOut || result.OutputTruncated)
            return statuses;

        using var reader = new StringReader(result.Output);
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        var sources = new Dictionary<string, LinuxHostMetrics.ServiceSource>(StringComparer.Ordinal);
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0)
            {
                AddStatus();
                properties.Clear();
                continue;
            }
            var equals = line.IndexOf('=');
            if (equals > 0)
                properties[line[..equals]] = line[(equals + 1)..];
        }
        AddStatus();
        if (includeMetrics)
            await metrics.EnrichServicesAsync(statuses, sources, cancellationToken);
        return statuses;

        void AddStatus()
        {
            if (properties.GetValueOrDefault("Id") is { } unit && statuses.ContainsKey(unit))
            {
                var state = Parse(unit, properties);
                statuses[unit] = state;
                if (includeMetrics && state.Exists)
                    sources[unit] = new(properties.GetValueOrDefault("ControlGroup"), properties.GetValueOrDefault("ActiveEnterTimestamp"),
                        Counter(properties.GetValueOrDefault("ActiveEnterTimestampMonotonic")));
            }
        }
    }

    private static ServiceStatus Parse(string unit, Dictionary<string, string> properties)
    {
        var load = properties.GetValueOrDefault("LoadState");
        if (load != "loaded")
            return new(unit, Detail: load switch
            {
                "not-found" => "Unit not found",
                "masked" => "Unit masked",
                _ => "Unit could not be loaded"
            });

        var active = properties.GetValueOrDefault("ActiveState");
        var state = active switch
        {
            "active" or "reloading" or "refreshing" => UnitState.Running,
            "inactive" => UnitState.Stopped,
            "failed" => UnitState.Failed,
            _ => UnitState.Unknown
        };
        var detail = active switch
        {
            "activating" => "Starting",
            "deactivating" => "Stopping",
            "reloading" or "refreshing" => "Reloading",
            "maintenance" => "Maintenance",
            _ => null
        };
        var job = properties.GetValueOrDefault("Job");
        var pending = active is "activating" or "deactivating" or "reloading" or "refreshing" ||
            uint.TryParse(job, out var jobId) && jobId > 0;
        return new(unit, state, detail, Exists: true, HasPendingJob: pending,
            MainPid: int.TryParse(properties.GetValueOrDefault("MainPID"), out var pid) && pid > 0 ? pid : null,
            MemoryBytes: Counter(properties.GetValueOrDefault("MemoryCurrent")),
            PeakBytes: Counter(properties.GetValueOrDefault("MemoryPeak")),
            CpuNanoseconds: Counter(properties.GetValueOrDefault("CPUUsageNSec")),
            Tasks: Counter(properties.GetValueOrDefault("TasksCurrent")),
            SubState: properties.GetValueOrDefault("SubState") is { Length: > 0 and <= 40 } subState ? subState : null);
    }

    public static ulong? Counter(string? value) => ulong.TryParse(value, out var number) && number != ulong.MaxValue
        ? number : null;

}

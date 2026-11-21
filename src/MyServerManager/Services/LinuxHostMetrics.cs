using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using MyServerManager.Configuration;
using MyServerManager.Models;
using MyServerManager.Security;

namespace MyServerManager.Services;

public sealed class LinuxHostMetrics(ManagerMode mode, ICommandRunner commands, CgroupMetrics cgroups, TimeProvider time)
{
    public sealed record CpuFiles(string? Stat, string? LoadAverage, string? Uptime, DateTimeOffset? ServerTime);
    public sealed record ServiceSource(string? ControlGroup, string? ActiveTimestamp, ulong? ActiveMicroseconds);
    private sealed record GroupCounters(ulong? Memory, ulong? Peak, ulong? Tasks, ulong? CpuNanoseconds, int? Processes);

    public async Task<CpuFiles> ReadCpuAsync(CancellationToken cancellationToken)
    {
        if (!mode.UsesSsh)
            return new(ReadLinux("/proc/stat"), ReadLinux("/proc/loadavg"), ReadLinux("/proc/uptime"), time.GetLocalNow());

        var fields = await ReadFieldsAsync(RemoteMetricCommands.Cpu(), cancellationToken);
        var serverTime = DateTimeOffset.TryParseExact(fields.ElementAtOrDefault(3)?.Trim(), "yyyy-MM-dd'T'HH:mm:sszzz",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var sampled) ? sampled : (DateTimeOffset?)null;
        return new(fields.ElementAtOrDefault(0), fields.ElementAtOrDefault(1), fields.ElementAtOrDefault(2), serverTime);
    }

    public async Task<string?> ReadMemoryAsync(CancellationToken cancellationToken)
    {
        if (!mode.UsesSsh)
            return ReadLinux("/proc/meminfo");

        var result = await commands.RunAsync(RemoteMetricCommands.Memory(), cancellationToken);
        return result.Succeeded && !result.OutputTruncated ? result.Output : null;
    }

    public async Task<HostInformation> ReadIdentityAsync(CancellationToken cancellationToken)
    {
        if (!mode.UsesSsh)
        {
            const string name = "PRETTY_NAME=";
            var distribution = ReadLinux("/etc/os-release")?.Split('\n').FirstOrDefault(line => line.StartsWith(name, StringComparison.Ordinal));
            return new(Environment.MachineName, distribution?[name.Length..].Trim().Trim('"', '\''),
                ReadLinux("/proc/sys/kernel/osrelease")?.Trim(), RuntimeInformation.FrameworkDescription);
        }

        var fields = await ReadFieldsAsync(RemoteMetricCommands.Identity(), cancellationToken);
        var runtimes = fields.ElementAtOrDefault(3)?.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return new(Trim(fields, 0), Trim(fields, 1)?.Trim('"', '\''), Trim(fields, 2),
            runtimes is { Length: > 0 } ? string.Join(", ", runtimes) : null);
    }

    public async Task EnrichServicesAsync(Dictionary<string, ServiceStatus> statuses,
        Dictionary<string, ServiceSource> sources, CancellationToken cancellationToken)
    {
        if (sources.Count == 0)
            return;

        if (!mode.UsesSsh)
        {
            var mounts = cgroups.Discover();
            foreach (var (unit, source) in sources)
            {
                var status = statuses[unit];
                statuses[unit] = CgroupMetrics.Enrich(status with
                {
                    Uptime = status.State == UnitState.Running ? LocalActiveFor(source.ActiveMicroseconds) : null
                }, mounts, source.ControlGroup);
            }
            return;
        }

        var groups = sources.Values.Select(source => source.ControlGroup).OfType<string>()
            .Where(RemoteMetricCommands.IsCgroupPath).Distinct(StringComparer.Ordinal).ToArray();
        if (groups.Length == 0 && !sources.Values.Any(source => source.ActiveTimestamp is { Length: > 0 }))
            return;

        var fields = await ReadFieldsAsync(RemoteMetricCommands.Cgroups(groups), cancellationToken);
        if (fields.Length == 0)
            return;
        var now = long.TryParse(fields[0], out var seconds) && seconds is >= 0 and <= 253402300799
            ? DateTimeOffset.FromUnixTimeSeconds(seconds) : (DateTimeOffset?)null;
        var metrics = new Dictionary<string, GroupCounters>(StringComparer.Ordinal);
        for (var offset = 1; offset + 5 < fields.Length; offset += 6)
        {
            var microseconds = SystemdServices.Counter(fields[offset + 4]);
            metrics[fields[offset]] = new(SystemdServices.Counter(fields[offset + 1]), SystemdServices.Counter(fields[offset + 2]),
                SystemdServices.Counter(fields[offset + 3]), microseconds <= ulong.MaxValue / 1000 ? microseconds * 1000 : null,
                int.TryParse(fields[offset + 5], out var count) && count >= 0 ? count : null);
        }

        foreach (var (unit, source) in sources)
        {
            var status = statuses[unit];
            var group = source.ControlGroup is { } path ? metrics.GetValueOrDefault(path) : null;
            statuses[unit] = status with
            {
                MemoryBytes = group?.Memory ?? status.MemoryBytes,
                PeakBytes = group?.Peak ?? status.PeakBytes,
                Tasks = group?.Tasks ?? status.Tasks,
                CpuNanoseconds = status.CpuNanoseconds ?? group?.CpuNanoseconds,
                ProcessCount = group?.Processes,
                Uptime = status.State == UnitState.Running ? RemoteActiveFor(now, source.ActiveTimestamp) : null
            };
        }
    }

    private async Task<string[]> ReadFieldsAsync(CommandRequest command, CancellationToken cancellationToken)
    {
        var result = await commands.RunAsync(command, cancellationToken);
        return result.Succeeded && !result.OutputTruncated ? result.Output.Split('\0') : [];
    }

    private static TimeSpan? RemoteActiveFor(DateTimeOffset? now, string? start) =>
        now is { } sampled && DateTimeOffset.TryParseExact(start, "ddd yyyy-MM-dd HH:mm:ss 'UTC'",
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var entered) && sampled >= entered
            ? sampled - entered : null;

    private static TimeSpan? LocalActiveFor(ulong? entered)
    {
        if (!OperatingSystem.IsLinux() || entered is null or 0)
            return null;

        // Linux systemd timestamps and Stopwatch use CLOCK_MONOTONIC.
        var now = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency * 1_000_000;
        return now >= entered ? TimeSpan.FromMicroseconds(now - entered.Value) : null;
    }

    private static string? Trim(string[] values, int index) => values.ElementAtOrDefault(index)?.Trim() is { Length: > 0 } value ? value : null;

    private static string? ReadLinux(string path) => OperatingSystem.IsLinux() ? LinuxFiles.Read(path) : null;
}

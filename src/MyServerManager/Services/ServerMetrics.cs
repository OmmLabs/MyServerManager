using System.Globalization;
using System.Text.RegularExpressions;
using MyServerManager.Models;
using MyServerManager.Security;

namespace MyServerManager.Services;

public sealed partial class ServerMetrics(ICommandRunner commands, LinuxHostMetrics hostMetrics, PreferencesStore preferences,
    TimeProvider time, LinuxHostRevision revision)
{
    private readonly RefreshCache<CpuMetrics> _cpu = new(time, revision);
    private readonly RefreshCache<MemoryMetrics> _memory = new(time, revision);
    private readonly RefreshCache<DiskMetrics[]> _disk = new(time, revision);
    private readonly RefreshCache<HostInformation> _host = new(time, revision);
    private CpuCounters? _lastCpu;
    private long _cpuRevision = -1;

    public async Task<ServerSnapshot> GetAsync(bool manual, CancellationToken cancellationToken)
    {
        var cpu = CpuAsync(cancellationToken);
        var memory = MemoryAsync(cancellationToken, manual);
        var disks = DisksAsync(cancellationToken, manual);
        var host = _host.GetAsync(TimeSpan.FromMinutes(5), hostMetrics.ReadIdentityAsync, cancellationToken);
        await Task.WhenAll(cpu, memory, disks, host);
        return new(cpu.Result, memory.Result, disks.Result, host.Result, cpu.Result.ServerTime);
    }

    public Task<CpuMetrics> CpuAsync(CancellationToken cancellationToken) =>
        _cpu.GetAsync(TimeSpan.FromSeconds(preferences.Current.CpuSeconds), SampleCpuAsync, cancellationToken);

    public Task<MemoryMetrics> MemoryAsync(CancellationToken cancellationToken, bool manual = false) =>
        _memory.GetAsync(TimeSpan.FromSeconds(manual ? 2 : preferences.Current.MemorySeconds),
            async token => ParseMemory(await hostMetrics.ReadMemoryAsync(token) ?? ""), cancellationToken);

    public Task<DiskMetrics[]> DisksAsync(CancellationToken cancellationToken, bool manual = false) =>
        _disk.GetAsync(TimeSpan.FromSeconds(manual ? 5 : preferences.Current.DiskSeconds), async token =>
        {
            var result = await commands.RunAsync(LinuxCommands.Disk(), token);
            return result.Succeeded && !result.OutputTruncated ? ParseDisks(result.Output) : [];
        }, cancellationToken);

    private async Task<CpuMetrics> SampleCpuAsync(CancellationToken cancellationToken)
    {
        var hostRevision = revision.Value;
        if (_cpuRevision != hostRevision)
        {
            _lastCpu = null;
            _cpuRevision = hostRevision;
        }
        var files = await hostMetrics.ReadCpuAsync(cancellationToken);
        var lines = files.Stat?.Split('\n');
        var counters = ParseCpuCounters(lines?.FirstOrDefault() ?? "");
        var usage = CalculateCpuUsage(_lastCpu, counters);
        _lastCpu = counters;

        var cores = lines?.Count(line => line.Length > 3 && line.StartsWith("cpu", StringComparison.Ordinal) && char.IsAsciiDigit(line[3]));
        var load = files.LoadAverage?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
        var uptime = ParseNumber(files.Uptime?.Split(' ', 2)[0]);
        return new(usage, cores is > 0 ? cores : null, ParseNumber(load.ElementAtOrDefault(0)),
            ParseNumber(load.ElementAtOrDefault(1)), ParseNumber(load.ElementAtOrDefault(2)),
            uptime is { } seconds && seconds < TimeSpan.MaxValue.TotalSeconds ? TimeSpan.FromSeconds(seconds) : null,
            files.ServerTime);
    }

    public readonly record struct CpuCounters(ulong Total, ulong Idle);

    public static CpuCounters? ParseCpuCounters(string line)
    {
        var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length < 5 || fields[0] != "cpu")
            return null;

        // user and nice already contain guest times.
        ulong total = 0, idle = 0;
        for (var i = 1; i <= Math.Min(8, fields.Length - 1); i++)
        {
            if (!ulong.TryParse(fields[i], out var counter) || counter > ulong.MaxValue - total)
                return null;
            total += counter;
            if (i is 4 or 5)
                idle += counter;
        }
        return new(total, idle);
    }

    public static double? CalculateCpuUsage(CpuCounters? previous, CpuCounters? current)
    {
        if (previous is not { } first || current is not { } second || second.Total <= first.Total || second.Idle < first.Idle)
            return null;

        var total = second.Total - first.Total;
        var idle = second.Idle - first.Idle;
        return idle <= total ? 100.0 * (total - idle) / total : null;
    }

    public static MemoryMetrics ParseMemory(string contents)
    {
        var fields = new Dictionary<string, ulong>(StringComparer.Ordinal);
        foreach (var line in contents.Split('\n'))
        {
            var colon = line.IndexOf(':');
            if (colon < 0)
                continue;
            var value = line[(colon + 1)..].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (value.Length == 2 && value[1] == "kB" && ulong.TryParse(value[0], out var kib) && kib <= ulong.MaxValue / 1024)
                fields[line[..colon]] = kib * 1024;
        }

        ulong? Get(string key) => fields.TryGetValue(key, out var bytes) ? bytes : null;
        static ulong? Used(ulong? total, ulong? available) => available <= total ? total - available : null;
        var total = Get("MemTotal");
        var available = Get("MemAvailable");
        var swap = Get("SwapTotal");
        var cached = Get("Cached");
        var buffers = Get("Buffers");
        return new(total, Used(total, available), available,
            cached <= ulong.MaxValue - buffers ? cached + buffers : null, swap, Used(swap, Get("SwapFree")));
    }

    public static DiskMetrics[] ParseDisks(string output)
    {
        var disks = new List<DiskMetrics>();
        foreach (var line in output.Split('\n', StringSplitOptions.TrimEntries))
        {
            var match = DiskLine().Match(line);
            if (match.Success && ulong.TryParse(match.Groups[2].Value, out var total) && ulong.TryParse(match.Groups[3].Value, out var used) &&
                ParseNumber(match.Groups[5].Value) is { } percent)
            {
                var available = ulong.TryParse(match.Groups[4].Value, out var bytes) ? bytes : (ulong?)null;
                disks.Add(new(match.Groups[1].Value, match.Groups[6].Value, total, used, available, percent));
            }
        }
        return disks.OrderBy(disk => disk.MountPoint, StringComparer.Ordinal).ToArray();
    }

    private static double? ParseNumber(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number) && number >= 0
            ? number : null;

    [GeneratedRegex(@"\A(.+?)\s+([0-9]+)\s+([0-9]+)\s+(-?[0-9]+)\s+([0-9]+)%\s+(.+)\z", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex DiskLine();
}

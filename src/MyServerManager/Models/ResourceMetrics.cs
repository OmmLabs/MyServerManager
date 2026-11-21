namespace MyServerManager.Models;

public sealed record CpuMetrics(double? Usage = null, int? Cores = null, double? Load1 = null,
    double? Load5 = null, double? Load15 = null, TimeSpan? Uptime = null, DateTimeOffset? ServerTime = null);

public sealed record MemoryMetrics(ulong? Total = null, ulong? Used = null, ulong? Available = null,
    ulong? CachedAndBuffered = null, ulong? SwapTotal = null, ulong? SwapUsed = null)
{
    public double? Usage => Total is > 0 && Used is { } used ? 100.0 * used / Total.Value : null;
}

public sealed record DiskMetrics(string FileSystem, string MountPoint, ulong Total, ulong Used, ulong? Available, double Usage);

public sealed record HostInformation(string? Hostname, string? Distribution, string? Kernel, string? Runtime);

public sealed record ServerSnapshot(CpuMetrics Cpu, MemoryMetrics Memory, DiskMetrics[] Disks, HostInformation Host,
    DateTimeOffset? ServerTime)
{
    public DiskMetrics? RootDisk => Disks.FirstOrDefault(disk => disk.MountPoint == "/");
}

public sealed record DashboardPage(ApplicationStatus[] Applications, ServerSnapshot Server);

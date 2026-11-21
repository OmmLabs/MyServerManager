using System.Globalization;
using MyServerManager.Models;
using MyServerManager.Services;
using MyServerManager.Views;

namespace MyServerManager.Tests;

public sealed class MetricsTests
{
    [Fact]
    public void CpuUsageRequiresTwoSamplesAndExcludesGuestDoubleCounting()
    {
        var first = ServerMetrics.ParseCpuCounters("cpu  100 10 80 1000 30 4 10 6 20 2");
        var second = ServerMetrics.ParseCpuCounters("cpu  120 10 90 1060 40 4 10 6 35 2");
        Assert.Equal(new ServerMetrics.CpuCounters(1240, 1030), first);
        Assert.Null(ServerMetrics.CalculateCpuUsage(null, first));
        Assert.Null(ServerMetrics.CalculateCpuUsage(first, first));
        Assert.Null(ServerMetrics.CalculateCpuUsage(second, first));
        Assert.Equal(30, ServerMetrics.CalculateCpuUsage(first, second));
    }

    [Fact]
    public void MemoryUsesKibibytesAndLeavesUnavailableValuesEmpty()
    {
        var memory = ServerMetrics.ParseMemory("MemTotal: 1048576 kB\nMemAvailable: 524288 kB\nCached: 65536 kB\nBuffers: 32768 kB\nSwapTotal: 512 kB\nSwapFree: 128 kB\n");
        Assert.Equal(1UL << 30, memory.Total);
        Assert.Equal(1UL << 29, memory.Used);
        Assert.Equal(50, memory.Usage);
        Assert.Equal(96UL << 20, memory.CachedAndBuffered);
        Assert.Equal(384UL << 10, memory.SwapUsed);
        Assert.Null(ServerMetrics.ParseMemory("MemTotal: 10 kB").Used);
        Assert.Null(SystemdServices.Counter("18446744073709551615"));
        Assert.Equal("1 GiB", ViewFormat.Bytes(memory.Total));
        Assert.Equal("512 MiB", ViewFormat.Bytes(memory.Used));
        Assert.Equal("N/A", ViewFormat.Bytes(null));
    }

    [Fact]
    public void CgroupMountsResolveNestedSlicesAndPreferCgroupMemory()
    {
        var root = Directory.CreateTempSubdirectory("myservermanager-cgroup-test-");
        try
        {
            var unit = root.CreateSubdirectory("site.service");
            var worker = unit.CreateSubdirectory("worker");
            File.WriteAllText(Path.Combine(unit.FullName, "memory.current"), "2048\n");
            File.WriteAllText(Path.Combine(unit.FullName, "cpu.stat"), "usage_usec 20\nuser_usec 10\n");
            File.WriteAllText(Path.Combine(unit.FullName, "pids.current"), "8\n");
            File.WriteAllText(Path.Combine(unit.FullName, "cgroup.procs"), "123\n456\n");
            File.WriteAllText(Path.Combine(worker.FullName, "cgroup.procs"), "123\n987\n");
            var escaped = root.FullName.Replace("\\", "\\134").Replace(" ", "\\040");
            var mounts = CgroupMetrics.ParseMountInfo($"42 33 0:32 /apps.slice {escaped} rw,nosuid - cgroup2 cgroup rw\n");
            Assert.Equal(root.FullName, Assert.Single(mounts).MountPoint);
            Assert.Null(CgroupMetrics.ResolveDirectory(mounts, "/apps.slice/../site.service"));
            Assert.Null(CgroupMetrics.ResolveDirectory(mounts, "/apps.slice-backup/site.service"));
            var status = CgroupMetrics.Enrich(new("site.service", MemoryBytes: 1024, PeakBytes: 4096), mounts, "/apps.slice/site.service");
            Assert.Equal(2048UL, status.MemoryBytes);
            Assert.Equal(4096UL, status.PeakBytes);
            Assert.Equal(20000UL, status.CpuNanoseconds);
            Assert.Equal(3, status.ProcessCount);
            Assert.Equal(8UL, status.Tasks);
        }
        finally
        {
            if (root.Parent?.FullName == Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) &&
                root.Name.StartsWith("myservermanager-cgroup-test-", StringComparison.Ordinal))
                root.Delete(recursive: true);
        }
    }

    [Fact]
    public void DiskParsingSupportsSpacesInMountPoints()
    {
        var disks = ServerMetrics.ParseDisks("Filesystem 1-blocks Used Available Capacity Mounted on\n/dev/sda1 4096 2048 2048 50% /\n/dev/volume name 8192 1024 7168 13% /mnt/test data\n");
        Assert.Equal(2, disks.Length);
        Assert.Equal(50, disks[0].Usage);
        Assert.Equal("/dev/volume name", disks[1].FileSystem);
        Assert.Equal("/mnt/test data", disks[1].MountPoint);
    }

    [Fact]
    public async Task DashboardFragmentsReuseServiceSamplesAndShowMissingMetrics()
    {
        await using var factory = new ManagerFactory();
        using var client = factory.Client();
        using var login = await ManagerFactory.LoginAsync(client);
        var initial = await client.GetStringAsync("/Applications/Table");
        var second = await client.GetStringAsync("/Dashboard/ApplicationList");
        Assert.Contains("1 MiB", initial);
        Assert.Contains("1 MiB", second);
        Assert.Single(factory.Commands.Calls, command => command.Executable == "/usr/bin/systemctl" && command.Arguments.Contains(ManagerFactory.Unit));
        var monitoring = await client.GetStringAsync("/Monitoring");
        Assert.Contains("Load average (15 min)", monitoring);
        Assert.Contains("Disk metrics are unavailable.", monitoring);
    }
}

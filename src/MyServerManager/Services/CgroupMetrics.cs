using System.Text.RegularExpressions;
using MyServerManager.Models;

namespace MyServerManager.Services;

public sealed partial class CgroupMetrics
{
    public sealed record Mount(string Root, string MountPoint);

    public Mount[] Discover() => OperatingSystem.IsLinux()
        ? ParseMountInfo(LinuxFiles.Read("/proc/self/mountinfo") ?? "") : [];

    public static Mount[] ParseMountInfo(string contents)
    {
        var mounts = new List<Mount>();
        foreach (var line in contents.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = line.IndexOf(" - cgroup2 ", StringComparison.Ordinal);
            if (separator < 0)
                continue;
            var fields = line[..separator].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length >= 6)
                mounts.Add(new Mount(Decode(fields[3]), Decode(fields[4])));
        }
        return mounts.OrderByDescending(mount => mount.Root.Length).ToArray();
    }

    public static string? ResolveDirectory(IEnumerable<Mount> mounts, string? controlGroup)
    {
        if (string.IsNullOrEmpty(controlGroup) || controlGroup == "/" || !controlGroup.StartsWith('/'))
            return null;
        var parts = controlGroup[1..].Split('/');
        if (parts.Any(part => part is "." or ".." or "" || part.Contains('\0') ||
            OperatingSystem.IsWindows() && (part.Contains('\\') || part.Contains(':'))))
            return null;

        foreach (var mount in mounts)
        {
            var root = mount.Root.TrimEnd('/');
            if (controlGroup != root && !controlGroup.StartsWith(root + "/", StringComparison.Ordinal))
                continue;
            var relative = controlGroup[root.Length..].TrimStart('/');
            var directory = Path.Combine(mount.MountPoint, relative.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(directory))
                return directory;
        }
        return null;
    }

    public static ServiceStatus Enrich(ServiceStatus state, IEnumerable<Mount> mounts, string? controlGroup)
    {
        if (ResolveDirectory(mounts, controlGroup) is not { } directory)
            return state;

        return state with
        {
            MemoryBytes = ReadCounter(directory, "memory.current") ?? state.MemoryBytes,
            PeakBytes = ReadCounter(directory, "memory.peak") ?? state.PeakBytes,
            CpuNanoseconds = state.CpuNanoseconds ?? ReadCpu(directory),
            Tasks = ReadCounter(directory, "pids.current") ?? state.Tasks,
            ProcessCount = CountProcesses(directory)
        };
    }

    private static ulong? ReadCounter(string directory, string name) =>
        SystemdServices.Counter(LinuxFiles.Read(Path.Combine(directory, name)));

    private static ulong? ReadCpu(string directory)
    {
        const string field = "usage_usec ";
        var lines = LinuxFiles.Read(Path.Combine(directory, "cpu.stat"))?.Split('\n');
        var usage = lines?.FirstOrDefault(line => line.StartsWith(field, StringComparison.Ordinal));
        var value = usage is null ? null : SystemdServices.Counter(usage[field.Length..]);
        return value <= ulong.MaxValue / 1000 ? value * 1000 : null;
    }

    private static int? CountProcesses(string directory)
    {
        var groups = new Queue<string>();
        var processes = new HashSet<int>();
        var visited = 0;
        groups.Enqueue(directory);
        try
        {
            while (groups.TryDequeue(out var group))
            {
                if (++visited > 128)
                    return null;
                foreach (var line in File.ReadLines(Path.Combine(group, "cgroup.procs")))
                {
                    if (!int.TryParse(line, out var pid) || pid <= 0)
                        return null;
                    processes.Add(pid);
                    if (processes.Count > 20_000)
                        return null;
                }
                foreach (var child in Directory.EnumerateDirectories(group))
                {
                    if (visited + groups.Count >= 128)
                        return null;
                    groups.Enqueue(child);
                }
            }
            return processes.Count;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return null; }
    }

    private static string Decode(string input) => MountEscape().Replace(input, match =>
        ((char)Convert.ToInt32(match.Value[1..], 8)).ToString());

    [GeneratedRegex(@"\\(?:040|011|012|134)", RegexOptions.CultureInvariant)]
    private static partial Regex MountEscape();
}

namespace MyServerManager.Security;

public enum ServiceAction { Start, Stop, Restart }
public enum NginxLayout { Ubuntu, AaPanel }

public sealed class CommandRequest
{
    internal CommandRequest(string executable, TimeSpan timeout, int outputLimit, params string[] arguments)
    {
        Executable = executable;
        Arguments = Array.AsReadOnly(arguments);
        Timeout = timeout;
        OutputLimit = outputLimit;
    }

    public string Executable { get; }
    public IReadOnlyList<string> Arguments { get; }
    public TimeSpan Timeout { get; }
    public int OutputLimit { get; }
}

public static class LinuxCommands
{
    private const string Systemctl = "/usr/bin/systemctl";
    private const string Journalctl = "/usr/bin/journalctl";
    private const string SafeJobMode = "--job-mode=fail";
    private const string Properties = "Id,LoadState,ActiveState,SubState,MainPID,ControlGroup,MemoryCurrent," +
        "MemoryPeak,CPUUsageNSec,TasksCurrent,ActiveEnterTimestamp,ActiveEnterTimestampMonotonic,Job";

    public static CommandRequest Show(params string[] units)
    {
        if (units.Length is < 1 or > 100 || !units.All(ServiceAllowlist.IsServiceName))
            throw new ArgumentException("Invalid service name list.", nameof(units));

        return new(Systemctl, TimeSpan.FromSeconds(5), 256 * 1024,
            ["show", "--no-pager", "--all", "--property=" + Properties, "--", .. units]);
    }

    public static CommandRequest ControlApplication(string unit, ServiceAction action)
    {
        EnsureApplicationUnit(unit);
        var verb = action switch
        {
            ServiceAction.Start => "start",
            ServiceAction.Stop => "stop",
            ServiceAction.Restart => "restart",
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
        return Privileged(35, 16384, Systemctl, verb, SafeJobMode, "--", unit);
    }

    public static CommandRequest Journal(string unit, int lines)
    {
        EnsureApplicationUnit(unit);
        var limit = lines switch
        {
            100 => "--lines=100",
            250 => "--lines=250",
            500 => "--lines=500",
            _ => throw new ArgumentOutOfRangeException(nameof(lines))
        };
        return Privileged(10, 4 * 1024 * 1024, Journalctl, "--no-pager", "--quiet", "--output=json",
            "--output-fields=MESSAGE,PRIORITY", "--unit=" + unit, limit);
    }

    public static CommandRequest Disk() => new("/usr/bin/df", TimeSpan.FromSeconds(5), 128 * 1024,
        "-P", "-B1", "-l", "-x", "tmpfs", "-x", "devtmpfs", "-x", "squashfs");

    public static CommandRequest NginxVersion(NginxLayout layout) => new(NginxBinary(layout), TimeSpan.FromSeconds(3), 4096, "-v");
    public static CommandRequest NginxTest(NginxLayout layout) => Privileged(12, 16384, NginxBinary(layout), "-t");
    public static CommandRequest NginxReload() => Privileged(35, 16384, Systemctl, "reload", SafeJobMode, "--", ServiceAllowlist.Nginx);
    public static CommandRequest NginxRestart() => Privileged(35, 16384, Systemctl, "restart", SafeJobMode, "--", ServiceAllowlist.Nginx);
    public static CommandRequest RestartSqlServer() => Privileged(55, 16384, Systemctl, "restart", SafeJobMode, "--", ServiceAllowlist.SqlServer);

    private static CommandRequest Privileged(int timeoutSeconds, int outputLimit, params string[] arguments) =>
        new("/usr/bin/sudo", TimeSpan.FromSeconds(timeoutSeconds), outputLimit, ["-n", "--", .. arguments]);

    private static string NginxBinary(NginxLayout layout) => layout switch
    {
        NginxLayout.Ubuntu => "/usr/sbin/nginx",
        NginxLayout.AaPanel => "/www/server/nginx/sbin/nginx",
        _ => throw new ArgumentOutOfRangeException(nameof(layout))
    };

    private static void EnsureApplicationUnit(string unit)
    {
        if (!ServiceAllowlist.IsApplicationUnit(unit))
            throw new ArgumentException("Invalid application service name.", nameof(unit));
    }
}

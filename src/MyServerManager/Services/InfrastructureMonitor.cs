using System.Text.RegularExpressions;
using MyServerManager.Configuration;
using MyServerManager.Models;
using MyServerManager.Security;

namespace MyServerManager.Services;

public sealed partial class InfrastructureMonitor(SystemdServices systemd, ICommandRunner commands, NginxInstallation nginx,
    PreferencesStore preferences, TimeProvider time, LinuxHostRevision revision, ManagerMode mode)
{
    private readonly RefreshCache<ServiceStatus> _nginxService = new(time, revision);
    private readonly RefreshCache<string?> _nginxVersion = new(time, revision);
    private readonly RefreshCache<ServiceStatus> _sqlServer = new(time, revision);

    public void InvalidateNginx() => _nginxService.Invalidate();
    public void InvalidateSql() => _sqlServer.Invalidate();

    public async Task<NginxStatus> NginxAsync(CancellationToken cancellationToken, bool manual = false)
    {
        var status = _nginxService.GetAsync(TimeSpan.FromSeconds(manual ? 2 : preferences.Current.ApplicationSeconds),
            token => systemd.ReadAsync(ServiceAllowlist.Nginx, token), cancellationToken);
        var version = _nginxVersion.GetAsync(TimeSpan.FromHours(1), ReadNginxVersionAsync, cancellationToken);
        await Task.WhenAll(status, version);
        return new(status.Result, version.Result, nginx.ControlsEnabled);
    }

    public Task<ServiceStatus> SqlAsync(CancellationToken cancellationToken, bool manual = false) =>
        _sqlServer.GetAsync(TimeSpan.FromSeconds(manual ? 5 : preferences.Current.SqlSeconds),
            token => systemd.ReadAsync(ServiceAllowlist.SqlServer, token), cancellationToken);

    private async Task<string?> ReadNginxVersionAsync(CancellationToken cancellationToken)
    {
        var result = await commands.RunAsync(LinuxCommands.NginxVersion(nginx.Layout), cancellationToken);
        if (mode.UsesSsh && !result.Succeeded && !result.ConnectionUnavailable)
        {
            var alternate = nginx.Layout == NginxLayout.Ubuntu ? NginxLayout.AaPanel : NginxLayout.Ubuntu;
            result = await commands.RunAsync(LinuxCommands.NginxVersion(alternate), cancellationToken);
        }
        if (!result.Succeeded)
            return null;

        var match = Version().Match(result.Error + "\n" + result.Output);
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex(@"(?:nginx|openresty|tengine) version:\s*((?:nginx|openresty|Tengine)/[0-9]+(?:\.[0-9]+){1,4})\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Version();
}

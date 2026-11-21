using MyServerManager.Models;
using MyServerManager.Security;

namespace MyServerManager.Services;

public sealed class ApplicationReadService(ApplicationRegistry registry, ServiceAllowlist allowlist, SystemdServices systemd,
    PreferencesStore preferences, TimeProvider time, LinuxHostRevision revision)
{
    private readonly RefreshCache<Dictionary<string, ServiceStatus>> _statuses = new(time, revision);

    public void Invalidate() => _statuses.Invalidate();

    public async Task<ApplicationStatus[]> GetAsync(CancellationToken cancellationToken, bool manual = false, bool includeDisabled = false)
    {
        var entries = await registry.ReadAsync(cancellationToken);
        var enabled = entries.Where(entry => entry.Enabled).ToArray();
        var approved = await allowlist.ReadApprovedAsync(cancellationToken);
        var states = await ReadStatesAsync(enabled, manual, cancellationToken);
        return (includeDisabled ? entries : enabled).Select(entry => new ApplicationStatus(entry,
            entry.Enabled ? states[entry.ServiceName] : new ServiceStatus(entry.ServiceName, Detail: "Monitoring disabled"),
            entry.Enabled && allowlist.IsApproved(approved, entry.ServiceName))).ToArray();
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var entries = (await registry.ReadAsync(cancellationToken)).Where(entry => entry.Enabled).ToArray();
        await ReadStatesAsync(entries, false, cancellationToken);
    }

    private Task<Dictionary<string, ServiceStatus>> ReadStatesAsync(ApplicationDefinition[] entries, bool manual, CancellationToken cancellationToken)
    {
        var units = entries.Select(entry => entry.ServiceName).ToArray();
        return _statuses.GetAsync(TimeSpan.FromSeconds(manual ? 2 : preferences.Current.ApplicationSeconds),
            token => systemd.ReadManyAsync(units, token), cancellationToken,
            statuses => statuses.Count == units.Length && units.All(statuses.ContainsKey));
    }
}

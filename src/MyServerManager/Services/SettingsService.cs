using MyServerManager.Models;
using MyServerManager.Security;

namespace MyServerManager.Services;

public sealed class SettingsService(ApplicationRegistry registry, PreferencesStore preferences, AdministratorStore admin,
    ServiceAllowlist allowlist, SystemdServices systemd, OperationCoordinator operations, ApplicationReadService applications)
{
    public Task<OperationResult> SaveApplicationAsync(ApplicationDefinition entry, string? originalService, string username) =>
        operations.ExecuteAsync(username, entry.DisplayName, entry.ServiceName,
            originalService is null ? AuditAction.RegisterApplication : AuditAction.UpdateApplication, async () =>
            {
                if (ApplicationRegistry.Validate(entry) is { } error)
                    return OperationResult.Failure(error);
                var entries = await registry.ReadAsync();
                var existing = Array.FindIndex(entries, app => app.ServiceName == entry.ServiceName);
                if (originalService is null && existing >= 0)
                    return OperationResult.Failure("This service is already registered.");
                if (originalService is not null && (existing < 0 || originalService != entry.ServiceName))
                    return OperationResult.Failure("Select the existing service again before editing its registration.");
                if (existing < 0 && entries.Length >= ApplicationRegistry.MaximumApplications)
                    return OperationResult.Failure("The registry supports up to 100 applications.");

                if (existing < 0 || entry.Enabled && !entries[existing].Enabled)
                {
                    if (!allowlist.IsApproved(await allowlist.ReadApprovedAsync(), entry.ServiceName))
                        return OperationResult.Failure("The server operator must approve this unit before registration or re-enabling.");
                    var status = await systemd.InspectAsync(entry.ServiceName);
                    if (!status.Exists)
                        return OperationResult.Failure(status.ConnectionUnavailable
                            ? "Connect to Ubuntu over SSH before registering or re-enabling an application."
                            : "systemd did not find a loaded unit with this exact canonical name.");
                }

                if (existing < 0)
                    entries = [.. entries, entry];
                else
                    entries[existing] = entry;

                await registry.SaveAsync(entries);
                return OperationResult.Success("Application registry saved.");
            });

    public Task<OperationResult> RemoveApplicationAsync(ApplicationDefinition entry, string username) =>
        operations.ExecuteAsync(username, entry.DisplayName, entry.ServiceName, AuditAction.RemoveApplication, async () =>
        {
            var entries = await registry.ReadAsync();
            if (!entries.Any(application => application.ServiceName == entry.ServiceName))
                return OperationResult.Failure("This application has already been removed.");

            await registry.SaveAsync(entries.Where(application => application.ServiceName != entry.ServiceName).ToArray());
            applications.Invalidate();
            return OperationResult.Success($"{entry.DisplayName} was removed from the dashboard.");
        });

    public Task<OperationResult> SavePreferencesAsync(ManagerPreferences value, string username) =>
        operations.ExecuteAsync(username, "Manager settings", null, AuditAction.UpdateSettings, async () =>
        {
            await preferences.SaveAsync(value);
            return OperationResult.Success("Monitoring and theme settings saved.");
        });

    public Task<OperationResult> ChangePasswordAsync(string currentPassword, string newPassword, string username) =>
        operations.ExecuteAsync(username, "Administrator account", null, AuditAction.ChangePassword,
            () => admin.ChangePasswordAsync(currentPassword, newPassword));
}

using MyServerManager.Configuration;
using MyServerManager.Models;
using MyServerManager.Security;

namespace MyServerManager.Services;

public sealed class NginxManagement(NginxInstallation installation, SystemdServices systemd, ICommandRunner commands,
    OperationCoordinator operations, InfrastructureMonitor monitor)
{
    public Task<OperationResult> ExecuteAsync(NginxAction action, string username) =>
        operations.ExecuteAsync(username, "Nginx", ServiceAllowlist.Nginx, action switch
        {
            NginxAction.Check => AuditAction.NginxCheck,
            NginxAction.Reload => AuditAction.NginxReload,
            NginxAction.Restart => AuditAction.NginxRestart,
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        }, async () =>
        {
            if (!installation.ControlsEnabled)
                return OperationResult.Failure("The server operator must enable Nginx controls after verifying the installation.");

            try
            {
                var state = await systemd.InspectAsync(ServiceAllowlist.Nginx);
                if (!state.Exists)
                    return OperationResult.Failure(state.Detail ?? "systemd could not inspect the canonical nginx.service unit.");
                if (state.HasPendingJob)
                    return OperationResult.Failure("A systemd job is already in progress for Nginx.");

                var validation = await commands.RunAsync(LinuxCommands.NginxTest(installation.Layout));
                if (!validation.Succeeded)
                    return OperationResult.Failure(validation.FailureMessage("Nginx configuration validation"));
                if (action == NginxAction.Check)
                    return OperationResult.Success("Nginx configuration validation passed.");

                var result = await commands.RunAsync(action == NginxAction.Reload ? LinuxCommands.NginxReload() : LinuxCommands.NginxRestart());
                return result.Succeeded
                    ? OperationResult.Success($"Nginx configuration validation passed and systemd completed {action.ToString().ToLowerInvariant()}.")
                    : OperationResult.Failure(result.FailureMessage($"Nginx {action.ToString().ToLowerInvariant()}"));
            }
            finally { monitor.InvalidateNginx(); }
        });
}

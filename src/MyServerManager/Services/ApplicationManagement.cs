using MyServerManager.Models;
using MyServerManager.Security;

namespace MyServerManager.Services;

public sealed class ApplicationManagement(ApplicationRegistry registry, ServiceAllowlist allowlist, SystemdServices systemd,
    ICommandRunner commands, OperationCoordinator coordinator, ApplicationReadService status)
{
    public Task<OperationResult> ControlAsync(ApplicationDefinition entry, ServiceAction action, string username) =>
        coordinator.ExecuteAsync(username, entry.DisplayName, entry.ServiceName, action switch
        {
            ServiceAction.Start => AuditAction.Start,
            ServiceAction.Stop => AuditAction.Stop,
            ServiceAction.Restart => AuditAction.Restart,
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        }, async () =>
        {
            var unit = entry.ServiceName;
            if (await registry.FindEnabledAsync(unit, CancellationToken.None) is null ||
                !allowlist.IsApproved(await allowlist.ReadApprovedAsync(), unit))
                return OperationResult.Failure("This service needs operator approval and an enabled registry entry.");

            try
            {
                var state = await systemd.InspectAsync(unit);
                if (!state.Exists)
                    return OperationResult.Failure(state.Detail ?? "systemd did not return the registered unit as a loaded canonical service.");
                if (state.HasPendingJob)
                    return OperationResult.Failure("A systemd job is already in progress for this service.");

                var result = await commands.RunAsync(LinuxCommands.ControlApplication(unit, action));
                return result.Succeeded
                    ? OperationResult.Success($"systemd completed {action.ToString().ToLowerInvariant()} for {entry.DisplayName}.")
                    : OperationResult.Failure(result.FailureMessage("systemctl"));
            }
            finally { status.Invalidate(); }
        });
}

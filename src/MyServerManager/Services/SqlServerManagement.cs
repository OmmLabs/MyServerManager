using MyServerManager.Models;
using MyServerManager.Security;

namespace MyServerManager.Services;

public sealed class SqlServerManagement(SystemdServices systemd, ICommandRunner commands, InfrastructureMonitor monitor,
    OperationCoordinator operations)
{
    public Task<OperationResult> RestartAsync(string username) => operations.ExecuteAsync(username, "Microsoft SQL Server",
        ServiceAllowlist.SqlServer, AuditAction.RestartSqlServer, async () =>
        {
            try
            {
                var state = await systemd.InspectAsync(ServiceAllowlist.SqlServer);
                if (!state.Exists)
                    return OperationResult.Failure(state.Detail ?? "SQL Server is unavailable.");
                if (state.HasPendingJob)
                    return OperationResult.Failure("A systemd job is already in progress for SQL Server.");

                var result = await commands.RunAsync(LinuxCommands.RestartSqlServer());
                return result.Succeeded
                    ? OperationResult.Success("Systemd completed the SQL Server restart.")
                    : OperationResult.Failure(result.FailureMessage("SQL Server restart"));
            }
            finally { monitor.InvalidateSql(); }
        });
}

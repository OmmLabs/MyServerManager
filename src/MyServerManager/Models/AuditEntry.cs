namespace MyServerManager.Models;

public enum AuditAction
{
    Start, Stop, Restart, NginxCheck, NginxReload, NginxRestart,
    RegisterApplication, UpdateApplication, UpdateSettings, ChangePassword,
    RegisterServer, DeregisterServer, RestartSqlServer, RemoveApplication
}

public sealed record AuditEntry(Guid Id, DateTimeOffset Timestamp, string Target, string? ServiceName,
    AuditAction Action, string Username, bool? Succeeded, string Message);

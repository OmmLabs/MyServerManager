namespace MyServerManager.Models;

public enum NginxAction { Check, Reload, Restart }

public sealed record NginxStatus(ServiceStatus Service, string? Version, bool ControlsEnabled);

public sealed record NginxPage(NginxStatus Status, AuditEntry? LastOperation);

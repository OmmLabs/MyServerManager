namespace MyServerManager.Models;

public enum UnitState { Unknown, Running, Stopped, Failed }

public sealed record ServiceStatus(
    string ServiceName,
    UnitState State = UnitState.Unknown,
    string? Detail = null,
    bool Exists = false,
    bool HasPendingJob = false,
    int? MainPid = null,
    ulong? MemoryBytes = null,
    ulong? PeakBytes = null,
    ulong? CpuNanoseconds = null,
    int? ProcessCount = null,
    ulong? Tasks = null,
    TimeSpan? Uptime = null,
    string? SubState = null,
    bool ConnectionUnavailable = false);

public sealed record ApplicationStatus(ApplicationDefinition Definition, ServiceStatus Status, bool CanControl);

public sealed record OperationResult(bool Succeeded, string Message)
{
    public static OperationResult Success(string message) => new(true, message);
    public static OperationResult Failure(string message) => new(false, message);
}

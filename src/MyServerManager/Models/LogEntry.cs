namespace MyServerManager.Models;

public enum LogLevel { Error, Warning, Information, Debug, Unknown }

public sealed record LogEntry(DateTimeOffset? Timestamp, LogLevel Level, string Message);

public sealed record JournalSnapshot(LogEntry[] Entries, DateTimeOffset? LoadedAt, string? Notice = null);

public sealed record LogsPage(ApplicationDefinition[] Applications, ApplicationDefinition? Selected, int LineLimit,
    JournalSnapshot Journal);

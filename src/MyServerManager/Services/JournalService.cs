using System.Text.Json;
using System.Text.RegularExpressions;
using MyServerManager.Models;
using MyServerManager.Security;
using LogLevel = MyServerManager.Models.LogLevel;

namespace MyServerManager.Services;

public sealed partial class JournalService(ICommandRunner commands, ServiceAllowlist allowlist, TimeProvider time)
{
    private readonly SemaphoreSlim _reader = new(1);

    public async Task<JournalSnapshot> ReadAsync(ApplicationDefinition application, int lines, CancellationToken cancellationToken)
    {
        if (!allowlist.IsApproved(await allowlist.ReadApprovedAsync(cancellationToken), application.ServiceName))
            return new([], null, "The server operator must approve journal access for this service.");
        if (!await _reader.WaitAsync(0, cancellationToken))
            return new([], null, "A journal read is in progress. Use Refresh to try again.");

        try
        {
            var result = await commands.RunAsync(LinuxCommands.Journal(application.ServiceName, lines), cancellationToken);
            if (!result.Succeeded)
                return new([], null, result.FailureMessage("journalctl"));
            return new(Parse(result.Output, lines), time.GetUtcNow(),
                result.OutputTruncated ? "The journal output reached the size limit. Showing the entries captured so far." : null);
        }
        finally { _reader.Release(); }
    }

    public static LogEntry[] Parse(string output, int maxEntries)
    {
        var entries = new List<LogEntry>();
        using var reader = new StringReader(output);
        while (entries.Count < maxEntries && reader.ReadLine() is { } line)
        {
            try
            {
                using var document = JsonDocument.Parse(line);
                if (document.RootElement is not { ValueKind: JsonValueKind.Object } entry)
                    continue;
                var message = Get(entry, "MESSAGE") ?? "[Non-text or oversized journal message]";
                var level = Classify(Get(entry, "PRIORITY"), message);
                entries.Add(new(Timestamp(Get(entry, "__REALTIME_TIMESTAMP")), level, Redact(message)));
            }
            catch (JsonException) { }
        }
        entries.Reverse();
        return entries.ToArray();
    }

    private static string? Get(JsonElement entry, string property) =>
        entry.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static DateTimeOffset? Timestamp(string? microseconds) =>
        long.TryParse(microseconds, out var value) && value >= 0 && value / 1000 <= DateTimeOffset.MaxValue.ToUnixTimeMilliseconds()
            ? DateTimeOffset.FromUnixTimeMilliseconds(value / 1000) : null;

    private static LogLevel Classify(string? priority, string message)
    {
        var level = priority switch
        {
            "0" or "1" or "2" or "3" => LogLevel.Error,
            "4" => LogLevel.Warning,
            "5" or "6" => LogLevel.Information,
            "7" => LogLevel.Debug,
            _ => LogLevel.Unknown
        };
        var text = message.AsSpan().TrimStart();
        if (level == LogLevel.Error || text.StartsWith("fail:", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("[ERR]", StringComparison.OrdinalIgnoreCase) || text.Contains("[FTL]", StringComparison.OrdinalIgnoreCase))
            return LogLevel.Error;
        if (level == LogLevel.Warning || text.StartsWith("warn:", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("[WRN]", StringComparison.OrdinalIgnoreCase))
            return LogLevel.Warning;
        if (text.StartsWith("info:", StringComparison.OrdinalIgnoreCase) || text.Contains("[INF]", StringComparison.OrdinalIgnoreCase))
            return LogLevel.Information;
        return level;
    }

    private static string Redact(string message)
    {
        if (message.Length > 8192 || Sensitive().IsMatch(message))
            return "[Message hidden because it may contain sensitive data]";
        var text = new string(message.Where(character => !char.IsControl(character) || character is '\n' or '\t').ToArray());
        return text.Length > 2000 ? text[..2000] + "…" : text;
    }

    [GeneratedRegex(@"pass(?:word|wd|phrase)|\bpwd\b|secret|credential|authorization|\bbearer\b|token|api[-_ ]?key|access[-_ ]?key|private[-_ ]?key|connection\s*strings?|cookie|(?:data\s*source|initial\s*catalog|user\s*id|server|host|username|database)\s*=|(?-i:\b[A-Z][A-Z0-9_]*\s*=)|https?://[^/\s]+@|https?://[^\s]*\?|[A-Za-z0-9+/=_-]{48,}",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.NonBacktracking)]
    private static partial Regex Sensitive();
}

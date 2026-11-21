using System.Globalization;
using System.Text.Json;
using MyServerManager.Configuration;
using MyServerManager.Models;

namespace MyServerManager.Services;

public sealed class AuditService(ServerProfileStore profiles)
{
    public const int DisplayLimit = 250;
    private readonly SemaphoreSlim _writer = new(1);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal string? CurrentDirectory => profiles.AuditDirectory;

    internal async Task AppendAsync(AuditEntry entry, string? directory)
    {
        if (directory is null)
            throw new IOException("Register a server before writing an audit entry.");
        var data = JsonSerializer.SerializeToUtf8Bytes(entry, Json);
        var date = entry.Timestamp.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        await _writer.WaitAsync();
        try
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, date + ".jsonl");
            await using var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            if (stream.Length > 0)
            {
                stream.Seek(-1, SeekOrigin.End);
                // An interrupted append may have left an incomplete final line.
                if (stream.ReadByte() != '\n')
                    stream.WriteByte((byte)'\n');
            }
            await stream.WriteAsync(data);
            stream.WriteByte((byte)'\n');
            stream.Flush(flushToDisk: true);
        }
        finally { _writer.Release(); }
    }

    public Task<List<AuditEntry>> HistoryAsync(DateOnly from, DateOnly to, AuditAction? action, CancellationToken cancellationToken) =>
        ReadAsync(from, to, action, service: null, DisplayLimit, cancellationToken);

    public async Task<AuditEntry?> LatestAsync(string service, CancellationToken cancellationToken) =>
        (await ReadAsync(DateOnly.MinValue, DateOnly.MaxValue, null, service, 1, cancellationToken)).FirstOrDefault();

    private async Task<List<AuditEntry>> ReadAsync(DateOnly from, DateOnly to, AuditAction? action, string? service,
        int limit, CancellationToken cancellationToken)
    {
        var results = new List<AuditEntry>();
        var directory = CurrentDirectory;
        if (directory is null || !Directory.Exists(directory))
            return results;

        var seen = new HashSet<Guid>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.jsonl").OrderDescending(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!DateOnly.TryParseExact(Path.GetFileNameWithoutExtension(path), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date) || date < from || date > to)
                continue;

            var recent = new Queue<AuditEntry>();
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                AuditEntry? entry;
                try { entry = JsonSerializer.Deserialize<AuditEntry>(line, Json); }
                catch (JsonException) { continue; }

                if (entry is null || action is not null && entry.Action != action || service is not null && entry.ServiceName != service)
                    continue;
                if (recent.Count == 2 * limit)
                    recent.Dequeue();
                recent.Enqueue(entry);
            }

            foreach (var entry in recent.Reverse())
            {
                if (seen.Add(entry.Id))
                    results.Add(entry);
                if (results.Count == limit)
                    return results;
            }
        }
        return results;
    }
}

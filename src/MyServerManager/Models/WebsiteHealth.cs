namespace MyServerManager.Models;

public sealed record WebsiteHealth(string Url, bool Available, int? StatusCode, long Milliseconds, DateTimeOffset CheckedAt,
    string? Error = null);

public sealed record WebsiteRow(ApplicationDefinition Application, WebsiteHealth? Health);

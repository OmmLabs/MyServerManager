using System.Collections.Concurrent;
using MyServerManager.Models;

namespace MyServerManager.Services;

public sealed class WebsiteHealthService(ApplicationRegistry registry, PreferencesStore preferences, WebsiteProbe probe, TimeProvider time)
{
    private readonly ConcurrentDictionary<string, RefreshCache<WebsiteHealth>> _samples = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _outbound = new(4);

    public async Task<WebsiteRow[]> GetAsync(CancellationToken cancellationToken)
    {
        var entries = await ReadSitesAsync(cancellationToken);
        return entries.Select(entry => new WebsiteRow(entry, _samples.TryGetValue(entry.ServiceName, out var cache) &&
            cache.TryRead(out var check) && check.Url == entry.WebsiteUrl ? check : null)).ToArray();
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var entries = await ReadSitesAsync(cancellationToken);
        var units = entries.Select(entry => entry.ServiceName).ToHashSet(StringComparer.Ordinal);
        foreach (var unit in _samples.Keys.Where(unit => !units.Contains(unit)))
            _samples.TryRemove(unit, out _);

        await Parallel.ForEachAsync(entries, new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = 4 },
            async (entry, token) => await CheckAsync(entry, false, token));
    }

    public Task<WebsiteHealth> CheckAsync(ApplicationDefinition entry, bool manual, CancellationToken cancellationToken)
    {
        var url = entry.WebsiteUrl ?? throw new ArgumentException("The application has no website URL.", nameof(entry));
        var cache = _samples.GetOrAdd(entry.ServiceName, _ => new RefreshCache<WebsiteHealth>(time));
        return cache.GetAsync(TimeSpan.FromSeconds(manual ? 5 : preferences.Current.WebsiteSeconds), async token =>
        {
            await _outbound.WaitAsync(token);
            try { return await probe.CheckAsync(url, token); }
            finally { _outbound.Release(); }
        }, cancellationToken, check => check.Url == url);
    }

    private async Task<ApplicationDefinition[]> ReadSitesAsync(CancellationToken cancellationToken) =>
        (await registry.ReadAsync(cancellationToken)).Where(entry => entry.Enabled && entry.WebsiteUrl is not null).ToArray();
}

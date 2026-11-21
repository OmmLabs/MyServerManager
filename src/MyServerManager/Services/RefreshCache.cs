namespace MyServerManager.Services;

internal sealed class RefreshCache<T>(TimeProvider time, LinuxHostRevision? host = null)
{
    private sealed record Sample(T Value, long Timestamp, long Generation, long HostRevision);
    private Sample? _sample;
    private readonly SemaphoreSlim _refresh = new(1);
    private long _generation;

    public void Invalidate() => Interlocked.Increment(ref _generation);

    public bool TryRead(out T value)
    {
        var sample = Volatile.Read(ref _sample);
        value = sample is not null ? sample.Value : default!;
        return sample is not null && IsCurrent(sample);
    }

    public async Task<T> GetAsync(TimeSpan maxAge, Func<CancellationToken, Task<T>> load, CancellationToken cancellationToken,
        Func<T, bool>? accept = null)
    {
        if (Recent() is { } sample)
            return sample.Value;

        await _refresh.WaitAsync(cancellationToken);
        try
        {
            if (Recent() is { } current)
                return current.Value;

            var generation = Volatile.Read(ref _generation);
            var hostRevision = host?.Value ?? 0;
            var value = await load(cancellationToken);
            Volatile.Write(ref _sample, new Sample(value, time.GetTimestamp(), generation, hostRevision));
            return value;
        }
        finally { _refresh.Release(); }

        Sample? Recent()
        {
            var value = Volatile.Read(ref _sample);
            return value is not null && IsCurrent(value) &&
                time.GetElapsedTime(value.Timestamp) < maxAge && (accept is null || accept(value.Value)) ? value : null;
        }
    }

    private bool IsCurrent(Sample sample) => sample.Generation == Volatile.Read(ref _generation) &&
        sample.HostRevision == (host?.Value ?? 0);
}

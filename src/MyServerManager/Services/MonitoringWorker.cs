using MyServerManager.Configuration;

namespace MyServerManager.Services;

public sealed class MonitoringWorker(ApplicationReadService applications, ServerMetrics server, WebsiteHealthService websites,
    InfrastructureMonitor infrastructure, PreferencesStore preferences, ManagerMode mode, SshConnection connection,
    ILogger<MonitoringWorker> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.WhenAll(
        PollAsync("application status", applications.RefreshAsync, () => preferences.Current.ApplicationSeconds, stoppingToken),
        PollAsync("CPU", server.CpuAsync, () => preferences.Current.CpuSeconds, stoppingToken),
        PollAsync("memory", token => server.MemoryAsync(token), () => preferences.Current.MemorySeconds, stoppingToken),
        PollAsync("disk", token => server.DisksAsync(token), () => preferences.Current.DiskSeconds, stoppingToken),
        PollAsync("website health", websites.RefreshAsync, () => preferences.Current.WebsiteSeconds, stoppingToken),
        PollAsync("Nginx", token => infrastructure.NginxAsync(token), () => preferences.Current.ApplicationSeconds, stoppingToken),
        PollAsync("SQL Server", token => infrastructure.SqlAsync(token), () => preferences.Current.SqlSeconds, stoppingToken));

    private async Task PollAsync(string category, Func<CancellationToken, Task> sample, Func<int> interval, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (!mode.UsesSsh || connection.IsConnected)
                    await sample(token);
            }
            catch (Exception error) when (!token.IsCancellationRequested)
            {
                logger.LogWarning("Could not sample {Category}. {ErrorType}.", category, error.GetType().Name);
            }
            await Task.Delay(TimeSpan.FromSeconds(interval()), token);
        }
    }
}

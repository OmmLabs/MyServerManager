namespace MyServerManager.Configuration;

public sealed class ManagerPaths
{
    public ManagerPaths(IConfiguration configuration, IWebHostEnvironment environment, ManagerMode mode)
    {
        var data = configuration["Manager:DataDirectory"];
        var approvals = configuration["Manager:ApprovedServicesPath"];
        if (!mode.UsesSsh && environment.IsProduction() &&
            (string.IsNullOrWhiteSpace(data) || !Path.IsPathFullyQualified(data) ||
             string.IsNullOrWhiteSpace(approvals) || !Path.IsPathFullyQualified(approvals)))
        {
            throw new InvalidOperationException("Production requires absolute Manager data and approval paths.");
        }

        var defaultData = mode.UsesSsh && environment.IsProduction()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyServerManager")
            : ".manager-state";
        DataDirectory = Path.GetFullPath(data ?? defaultData, environment.ContentRootPath);
        Approvals = string.IsNullOrWhiteSpace(approvals)
            ? null : Path.GetFullPath(approvals, environment.ContentRootPath);
        SeedRegistry = Path.Combine(environment.ContentRootPath, "config", "applications.json");
    }

    public string DataDirectory { get; }
    public string? Approvals { get; }
    public string SeedRegistry { get; }
    public string Registry => Path.Combine(DataDirectory, "config", "applications.json");
    public string Preferences => Path.Combine(DataDirectory, "config", "preferences.json");
    public string Administrator => Path.Combine(DataDirectory, "auth", "administrator.json");
    public string Keys => Path.Combine(DataDirectory, "keys");
    public string Audit => Path.Combine(DataDirectory, "audit");
    public string ServerProfile => Path.Combine(DataDirectory, "config", "server.json");
    public string ServerRegistry(Guid id) => Path.Combine(DataDirectory, "servers", id.ToString("N"), "applications.json");
    public string ServerAudit(Guid id) => Path.Combine(DataDirectory, "servers", id.ToString("N"), "audit");
}

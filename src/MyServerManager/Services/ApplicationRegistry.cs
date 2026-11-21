using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using MyServerManager.Configuration;
using MyServerManager.Models;
using MyServerManager.Security;

namespace MyServerManager.Services;

public sealed partial class ApplicationRegistry(ManagerPaths paths, IWebHostEnvironment environment, ManagerMode mode,
    ServerProfileStore profiles)
{
    public const int MaximumApplications = 100;

    public async Task InitializeAsync()
    {
        if (mode.UsesSsh)
        {
            await ReadAsync();
            return;
        }
        if (!File.Exists(paths.Registry))
        {
            if (environment.IsProduction())
                throw new InvalidOperationException("Install a reviewed registry in the Manager data directory.");

            var seed = await AtomicJson.ReadAsync<ApplicationDefinition[]>(paths.SeedRegistry);
            await SaveAsync(seed);
        }
        await ReadAsync();
    }

    public Task<ApplicationDefinition[]> ReadAsync(CancellationToken cancellationToken = default) =>
        profiles.RegistryPath is { } path && (!mode.UsesSsh || File.Exists(path))
            ? ReadFileAsync(path, cancellationToken) : Task.FromResult<ApplicationDefinition[]>([]);

    public static async Task<ApplicationDefinition[]> ReadFileAsync(string path, CancellationToken cancellationToken = default)
    {
        var entries = await AtomicJson.ReadAsync<ApplicationDefinition[]>(path, cancellationToken);
        ValidateEntries(entries);
        return entries.OrderBy(entry => entry.DisplayOrder)
            .ThenBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<ApplicationDefinition?> FindEnabledAsync(string? unit, CancellationToken cancellationToken)
    {
        if (!ServiceAllowlist.IsApplicationUnit(unit))
            return null;
        return (await ReadAsync(cancellationToken)).FirstOrDefault(entry => entry.Enabled && entry.ServiceName == unit);
    }

    internal Task SaveAsync(ApplicationDefinition[] entries)
    {
        ValidateEntries(entries);
        return AtomicJson.WriteAsync(profiles.RegistryPath ?? throw new InvalidDataException("Register a server first."), entries);
    }

    public static string? Validate(ApplicationDefinition entry)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(entry, new ValidationContext(entry), errors, validateAllProperties: true))
            return errors[0].ErrorMessage;

        if (!ServiceAllowlist.IsApplicationUnit(entry.ServiceName))
            return "Enter a supported systemd application service name ending in .service.";

        if (new[] { entry.DisplayName, entry.Description, entry.Environment, entry.DeploymentDirectory, entry.WebsiteUrl }
            .Any(value => value?.Any(char.IsControl) == true))
            return "Text fields must not contain control characters.";

        if (entry.DeploymentDirectory is { } directory &&
            (!directory.StartsWith('/') || directory.Split('/').Any(part => part is "." or "..")))
            return "Enter an absolute Linux deployment directory.";

        if (entry.AssemblyName is { } assembly && !AssemblyPattern().IsMatch(assembly))
            return "Enter a .dll filename without directories.";

        if (entry.WebsiteUrl is { } url && !TryWebsiteUri(url, out _))
            return "Enter an absolute HTTP or HTTPS URL without credentials, a query string, or a fragment.";

        return null;
    }

    public static bool TryWebsiteUri(string url, out Uri? uri) =>
        Uri.TryCreate(url, UriKind.Absolute, out uri) && uri.Scheme is "http" or "https" &&
        uri.HostNameType is UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6 &&
        string.IsNullOrEmpty(uri.UserInfo) && !url.Contains('\\') &&
        string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment) && uri.Port > 0;

    private static void ValidateEntries(ApplicationDefinition[] entries)
    {
        if (entries.Length > MaximumApplications)
            throw new InvalidDataException("The registry supports up to 100 applications.");

        var units = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            var error = entry is null ? "The registry contains an empty entry." : Validate(entry);
            if (error is not null)
                throw new InvalidDataException(error);
            if (!units.Add(entry!.ServiceName))
                throw new InvalidDataException("Each systemd service can appear only once in the registry.");
        }
    }

    [GeneratedRegex(@"\A[A-Za-z0-9_][A-Za-z0-9_.-]*\.dll\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AssemblyPattern();
}

public sealed class PreferencesStore(ManagerPaths paths)
{
    private ManagerPreferences _current = new();
    public ManagerPreferences Current => Volatile.Read(ref _current);

    public async Task InitializeAsync()
    {
        if (File.Exists(paths.Preferences))
            Volatile.Write(ref _current, Validate(await AtomicJson.ReadAsync<ManagerPreferences>(paths.Preferences)));
    }

    internal async Task SaveAsync(ManagerPreferences preferences)
    {
        await AtomicJson.WriteAsync(paths.Preferences, Validate(preferences));
        Volatile.Write(ref _current, preferences);
    }

    private static ManagerPreferences Validate(ManagerPreferences preferences)
    {
        Validator.ValidateObject(preferences, new ValidationContext(preferences), validateAllProperties: true);
        return preferences;
    }
}

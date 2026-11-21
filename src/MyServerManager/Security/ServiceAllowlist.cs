using System.Text.RegularExpressions;
using MyServerManager.Configuration;

namespace MyServerManager.Security;

public sealed partial class ServiceAllowlist(ManagerPaths paths, ManagerMode mode)
{
    public const string Nginx = "nginx.service";
    public const string SqlServer = "mssql-server.service";
    public const string Manager = "myservermanager.service";

    [GeneratedRegex(@"\A[A-Za-z0-9_][A-Za-z0-9_.-]*(?:@[A-Za-z0-9_.-]+)?\.service\z", RegexOptions.CultureInvariant)]
    private static partial Regex ServicePattern();

    public static bool IsServiceName(string? value) => value is { Length: <= 128 } && ServicePattern().IsMatch(value);

    public static bool IsApplicationUnit(string? value) => IsServiceName(value) &&
        value is not (Nginx or SqlServer or Manager);

    public bool RequiresOperatorApproval => !mode.UsesSsh;

    public bool IsApproved(IReadOnlySet<string> approved, string unit) => !RequiresOperatorApproval || approved.Contains(unit);

    public async Task<HashSet<string>> ReadApprovedAsync(CancellationToken cancellationToken = default)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (!RequiresOperatorApproval || paths.Approvals is null)
            return result;

        foreach (var line in await File.ReadAllLinesAsync(paths.Approvals, cancellationToken))
        {
            var unit = line.Trim();
            if (unit.Length == 0 || unit.StartsWith('#'))
                continue;
            if (!IsApplicationUnit(unit) || !result.Add(unit))
                throw new InvalidDataException("The operator approval file contains an invalid or repeated service name.");
        }

        return result;
    }
}

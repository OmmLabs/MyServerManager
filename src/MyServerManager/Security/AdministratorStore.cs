using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using MyServerManager.Configuration;
using MyServerManager.Models;
using MyServerManager.Services;

namespace MyServerManager.Security;

public static class AuthorizationPolicies
{
    public const string Administrator = "Administrator";
    public const string LoginRateLimit = "login";
    public const string SessionStampClaim = "manager:session-stamp";
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(30);

    public static ClaimsPrincipal AdministratorPrincipal(string username, string stamp) => new(new ClaimsIdentity([
        new Claim(ClaimTypes.Name, username),
        new Claim(ClaimTypes.Role, Administrator),
        new Claim(SessionStampClaim, stamp)
    ], CookieAuthenticationDefaults.AuthenticationScheme));
}

public sealed record Administrator(string Username, string PasswordHash, string SessionStamp);

public sealed partial class AdministratorStore(ManagerPaths paths, IPasswordHasher<Administrator> hasher)
{
    public const int MinimumPasswordLength = 15;
    public const int MaximumPasswordLength = 128;

    public async Task<Administrator> ReadAsync(CancellationToken cancellationToken = default)
    {
        var admin = await AtomicJson.ReadAsync<Administrator>(paths.Administrator, cancellationToken);
        if (!IsUsername(admin.Username) || string.IsNullOrWhiteSpace(admin.PasswordHash) ||
            !Guid.TryParseExact(admin.SessionStamp, "N", out _))
            throw new InvalidDataException("The administrator file is invalid.");
        return admin;
    }

    public async Task<ClaimsPrincipal?> SignInAsync(string username, string password, CancellationToken cancellationToken)
    {
        if (password.Length is < 1 or > MaximumPasswordLength)
            return null;

        var admin = await ReadAsync(cancellationToken);
        var verified = hasher.VerifyHashedPassword(admin, admin.PasswordHash, password);
        if (verified == PasswordVerificationResult.Failed || !string.Equals(username, admin.Username, StringComparison.OrdinalIgnoreCase))
            return null;

        return AuthorizationPolicies.AdministratorPrincipal(admin.Username, admin.SessionStamp);
    }

    public async Task<bool> IsCurrentSessionAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var admin = await ReadAsync(cancellationToken);
        return principal.Identity?.Name == admin.Username &&
            principal.FindFirstValue(AuthorizationPolicies.SessionStampClaim) == admin.SessionStamp;
    }

    internal async Task<OperationResult> ChangePasswordAsync(string currentPassword, string newPassword)
    {
        var admin = await ReadAsync();
        if (currentPassword.Length > MaximumPasswordLength ||
            hasher.VerifyHashedPassword(admin, admin.PasswordHash, currentPassword) == PasswordVerificationResult.Failed)
            return OperationResult.Failure("The current password was incorrect.");

        if (!IsPassword(newPassword))
            return OperationResult.Failure($"Use {MinimumPasswordLength} to {MaximumPasswordLength} characters for the new password.");

        await SetAsync(admin.Username, newPassword);
        return OperationResult.Success("Password changed. Sign in with your new password.");
    }

    internal Task SetAsync(string username, string password)
    {
        if (!IsUsername(username) || !IsPassword(password))
            throw new ArgumentException("The username or password does not meet the administrator credential requirements.");

        var admin = new Administrator(username, "", Guid.NewGuid().ToString("N"));
        return AtomicJson.WriteAsync(paths.Administrator, admin with { PasswordHash = hasher.HashPassword(admin, password) });
    }

    public static bool IsUsername(string? username) => username is not null && UsernamePattern().IsMatch(username);

    public static bool IsPassword(string password) => password.Length is >= MinimumPasswordLength and <= MaximumPasswordLength &&
        !string.IsNullOrWhiteSpace(password) && !password.Any(char.IsControl);

    [GeneratedRegex(@"\A[A-Za-z0-9][A-Za-z0-9_.-]{0,63}\z", RegexOptions.CultureInvariant)]
    private static partial Regex UsernamePattern();
}

internal static class AdministratorSetup
{
    public static async Task<bool> RunAsync(AdministratorStore store, OperationCoordinator operations)
    {
        if (Console.IsInputRedirected)
        {
            Console.Error.WriteLine("Administrator setup requires an interactive terminal.");
            return false;
        }

        Console.Write("Administrator username: ");
        var username = Console.ReadLine();
        if (!AdministratorStore.IsUsername(username))
        {
            Console.Error.WriteLine("Use 1–64 letters, digits, dots, underscores or hyphens, starting with a letter or digit.");
            return false;
        }

        var password = ReadPassword("New password: ");
        if (!AdministratorStore.IsPassword(password) || password != ReadPassword("Repeat password: "))
        {
            Console.Error.WriteLine("The passwords must match and contain 15–128 characters.");
            return false;
        }

        var result = await operations.ExecuteAsync(Environment.UserName, "Local administrator setup", null, AuditAction.ChangePassword,
            async () =>
            {
                await store.SetAsync(username!, password);
                return OperationResult.Success("Administrator credentials saved. Existing sessions are invalidated.");
            });
        Console.WriteLine(result.Message);
        return result.Succeeded;
    }

    private static string ReadPassword(string prompt)
    {
        Console.Write(prompt);
        var value = new StringBuilder();
        var length = 0;
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
                break;
            if (key.Key == ConsoleKey.Backspace && length > 0)
            {
                if (--length < value.Length)
                    value.Length--;
            }
            else if (!char.IsControl(key.KeyChar))
            {
                if (++length <= AdministratorStore.MaximumPasswordLength)
                    value.Append(key.KeyChar);
            }
        }
        Console.WriteLine();
        return length > AdministratorStore.MaximumPasswordLength ? "" : value.ToString();
    }
}

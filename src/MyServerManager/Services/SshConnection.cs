using System.Net.Sockets;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using MyServerManager.Configuration;
using MyServerManager.Models;
using MyServerManager.Security;
using Renci.SshNet.Common;

namespace MyServerManager.Services;

public sealed class LinuxHostRevision
{
    private long _value;
    public long Value => Volatile.Read(ref _value);
    public void Advance() => Interlocked.Increment(ref _value);
}

public sealed record SshSignInResult(ClaimsPrincipal? Principal, string? Error);

public sealed class SshConnection(ManagerMode mode, ServerProfileStore profiles, ManagerPaths paths, ISshSessionFactory factory,
    LinuxHostRevision revision, TimeProvider time, AuditService audit, IHttpContextAccessor requests) : ICommandRunner, IDisposable
{
    private sealed record ActiveConnection(ISshSession Session, byte[] Password, CancellationTokenSource Lifetime, string Stamp, Guid ServerId);
    private readonly SemaphoreSlim _gate = new(1);
    private ActiveConnection? _current;
    private bool _disposed;

    public bool IsConnected => Volatile.Read(ref _current) is { } current && IsActive(current);

    public bool IsCurrentSession(ClaimsPrincipal principal) => Volatile.Read(ref _current) is { } current &&
        profiles.Current is { } profile && profile.Id == current.ServerId && principal.Identity?.Name == profile.Endpoint.Username &&
        principal.FindFirstValue(AuthorizationPolicies.SessionStampClaim) == current.Stamp && IsActive(current);

    public Task<SshSignInResult> SignInAsync(string password, DateTimeOffset dashboardExpiresAt, CancellationToken cancellationToken) =>
        SignInCoreAsync(null, password, dashboardExpiresAt, cancellationToken);

    public Task<SshSignInResult> RegisterAndSignInAsync(SshEndpoint endpoint, string password, DateTimeOffset dashboardExpiresAt,
        CancellationToken cancellationToken) => SignInCoreAsync(endpoint, password, dashboardExpiresAt, cancellationToken);

    public async Task<(string? Fingerprint, string? Error)> ReadHostKeyAsync(SshEndpoint endpoint, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(12));
        try
        {
            var key = await factory.ReadHostKeyAsync(endpoint, deadline.Token);
            return SshEndpoint.IsFingerprint(key) ? (key, null) : (null, "The server returned an unsupported SSH key.");
        }
        catch (Exception error) when (error is SshException or SocketException or TimeoutException or IOException ||
            error is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return (null, "Could not read the server’s SSH key. Check the address and try again.");
        }
    }

    private async Task<SshSignInResult> SignInCoreAsync(SshEndpoint? registration, string password, DateTimeOffset dashboardExpiresAt,
        CancellationToken cancellationToken)
    {
        if (password.Length is < 1 or > 1024 || password.Contains('\r') || password.Contains('\n') || password.Contains('\0'))
            return new(null, "Enter your Ubuntu password.");

        await _gate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var saved = profiles.Current;
            if (!mode.UsesSsh || registration is not null && saved is not null)
                return new(null, "A server is already registered. Open the sign-in page.");
            var endpoint = registration ?? saved?.Endpoint;
            if (endpoint?.HasTrustedKeys() != true)
                return new(null, "Set up the server before signing in.");
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(12));
            var credential = Encoding.UTF8.GetBytes(password);
            ISshSession? opened = null;
            var connected = false;
            try
            {
                opened = await factory.ConnectAsync(endpoint, credential, deadline.Token);
                deadline.Token.ThrowIfCancellationRequested();
                if (saved is null)
                {
                    saved = new RegisteredServer(Guid.NewGuid(), endpoint);
                    try
                    {
                        await audit.AppendAsync(new AuditEntry(Guid.NewGuid(), time.GetUtcNow(), endpoint.DisplayName, null,
                            AuditAction.RegisterServer, endpoint.Username, true, "Server registered."), paths.ServerAudit(saved.Id));
                        await profiles.SaveAsync(saved);
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                    {
                        return new(null, "Could not save the server on this computer.");
                    }
                }
                CloseCurrent();
                var remaining = dashboardExpiresAt - time.GetUtcNow();
                if (remaining <= TimeSpan.Zero)
                    return new(null, "The sign-in session expired. Try again.");

                var lifetime = new CancellationTokenSource(remaining, time);
                var stamp = Guid.NewGuid().ToString("N");
                var active = new ActiveConnection(opened, credential, lifetime, stamp, saved.Id);
                Volatile.Write(ref _current, active);
                revision.Advance();
                connected = true;
                _ = CloseAtExpiryAsync(active);
                return new(AuthorizationPolicies.AdministratorPrincipal(endpoint.Username, stamp), null);
            }
            catch (SshHostKeyRejectedException)
            {
                return new(null, "The server’s SSH key could not be verified. Check the saved key before signing in.");
            }
            catch (SshAuthenticationException)
            {
                return new(null, "Ubuntu rejected the password. Try the password for the account shown above.");
            }
            catch (Exception error) when (error is SshOperationTimeoutException or TimeoutException ||
                error is OperationCanceledException && !cancellationToken.IsCancellationRequested)
            {
                return new(null, "Ubuntu took too long to answer. Try again.");
            }
            catch (Exception error) when (error is SshException or SocketException or IOException)
            {
                return new(null, "Could not connect to Ubuntu. Check your connection and try again.");
            }
            finally
            {
                if (!connected)
                {
                    CryptographicOperations.ZeroMemory(credential);
                    opened?.Dispose();
                }
            }
        }
        finally { _gate.Release(); }
    }

    public async Task<CommandResult> RunAsync(CommandRequest command, CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _current) is null)
            return new(null, ConnectionUnavailable: true);

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(command.Timeout);
        try { await _gate.WaitAsync(deadline.Token); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(null, TimedOut: true);
        }

        try
        {
            if (_current is not { } current || !IsActive(current))
            {
                CloseCurrent();
                return new(null, ConnectionUnavailable: true);
            }
            if (requests.HttpContext is { } request && !IsCurrentSession(request.User))
                return new(null, ConnectionUnavailable: true);
            using var execution = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, current.Lifetime.Token);
            try
            {
                return await current.Session.RunAsync(command, SshCommandLine.RequiresPassword(command) ? current.Password : default,
                    execution.Token);
            }
            catch (OperationCanceledException) when (current.Lifetime.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                CloseCurrent();
                return new(null, ConnectionUnavailable: true);
            }
            catch (Exception error) when (error is SshOperationTimeoutException or TimeoutException ||
                error is OperationCanceledException && !cancellationToken.IsCancellationRequested)
            {
                return new(null, TimedOut: true);
            }
            catch (Exception error) when (error is SshException or SocketException or IOException)
            {
                if (current.Session.IsConnected)
                    return new(null);
                CloseCurrent();
                return new(null, ConnectionUnavailable: true);
            }
        }
        finally { _gate.Release(); }
    }

    public async Task DisconnectAsync(string? stamp)
    {
        await _gate.WaitAsync();
        try
        {
            if (_current is { } current && current.Stamp == stamp)
                CloseCurrent();
        }
        finally { _gate.Release(); }
    }

    public async Task<OperationResult> DeregisterAsync(ClaimsPrincipal principal)
    {
        await _gate.WaitAsync();
        try
        {
            if (!mode.UsesSsh || !IsCurrentSession(principal))
                return OperationResult.Failure("Sign in again before removing the server.");

            await profiles.SaveAsync(null);
            CloseCurrent();
            return OperationResult.Success("Server removed. You can set up a server again.");
        }
        finally { _gate.Release(); }
    }

    private static bool IsActive(ActiveConnection current) =>
        !current.Lifetime.IsCancellationRequested && current.Session.IsConnected;

    private async Task CloseAtExpiryAsync(ActiveConnection active)
    {
        try { await Task.Delay(Timeout.InfiniteTimeSpan, active.Lifetime.Token); }
        catch (OperationCanceledException) { }
        await _gate.WaitAsync();
        try
        {
            if (ReferenceEquals(_current, active))
                CloseCurrent();
        }
        finally { _gate.Release(); }
    }

    private void CloseCurrent()
    {
        if (Interlocked.Exchange(ref _current, null) is not { } active)
            return;
        revision.Advance();
        active.Lifetime.Cancel();
        CryptographicOperations.ZeroMemory(active.Password);
        try { active.Session.Dispose(); }
        finally { active.Lifetime.Dispose(); }
    }

    public void Dispose()
    {
        _gate.Wait();
        try
        {
            _disposed = true;
            CloseCurrent();
        }
        finally { _gate.Release(); }
    }
}

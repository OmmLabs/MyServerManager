using System.Text;
using MyServerManager.Configuration;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace MyServerManager.Security;

public interface ISshSessionFactory
{
    Task<string> ReadHostKeyAsync(SshEndpoint endpoint, CancellationToken cancellationToken);
    Task<ISshSession> ConnectAsync(SshEndpoint endpoint, byte[] password, CancellationToken cancellationToken);
}

public interface ISshSession : IDisposable
{
    bool IsConnected { get; }
    Task<CommandResult> RunAsync(CommandRequest request, ReadOnlyMemory<byte> password, CancellationToken cancellationToken);
}

public sealed class SshNetSessionFactory : ISshSessionFactory
{
    public async Task<string> ReadHostKeyAsync(SshEndpoint endpoint, CancellationToken cancellationToken)
    {
        using var authentication = new NoneAuthenticationMethod(endpoint.Username);
        var connection = new Renci.SshNet.ConnectionInfo(endpoint.Host, endpoint.Port, endpoint.Username, authentication)
        {
            Timeout = TimeSpan.FromSeconds(8)
        };
        using var client = new SshClient(connection);
        string? fingerprint = null;
        client.HostKeyReceived += (_, key) =>
        {
            fingerprint = "SHA256:" + key.FingerPrintSHA256;
            key.CanTrust = false;
        };
        try { await client.ConnectAsync(cancellationToken); }
        catch (SshException) when (fingerprint is not null) { }

        return fingerprint ?? throw new SshConnectionException("The server did not supply an SSH key.");
    }

    public async Task<ISshSession> ConnectAsync(SshEndpoint endpoint, byte[] password, CancellationToken cancellationToken)
    {
        var authentication = new PasswordAuthenticationMethod(endpoint.Username, password);
        authentication.PasswordExpired += (_, _) =>
            throw new SshAuthenticationException("The SSH account password has expired.");
        var connection = new Renci.SshNet.ConnectionInfo(endpoint.Host, endpoint.Port, endpoint.Username, authentication)
        {
            Timeout = TimeSpan.FromSeconds(8),
            MaxSessions = 2,
            Encoding = Encoding.UTF8
        };
        var client = new SshClient(connection) { KeepAliveInterval = TimeSpan.FromSeconds(30) };
        var verified = false;
        var rejected = false;
        client.HostKeyReceived += (_, key) =>
        {
            key.CanTrust = endpoint.Trusts(key.FingerPrintSHA256);
            if (key.CanTrust)
                Volatile.Write(ref verified, true);
            else
                Volatile.Write(ref rejected, true);
        };
        try
        {
            await client.ConnectAsync(cancellationToken);
            if (!Volatile.Read(ref verified))
                throw new SshHostKeyRejectedException();
            return new SshNetSession(client, authentication);
        }
        catch
        {
            client.Dispose();
            authentication.Dispose();
            if (Volatile.Read(ref rejected))
                throw new SshHostKeyRejectedException();
            throw;
        }
    }
}

internal sealed class SshNetSession(SshClient client, PasswordAuthenticationMethod authentication) : ISshSession
{
    public bool IsConnected => client.IsConnected;

    public async Task<CommandResult> RunAsync(CommandRequest request, ReadOnlyMemory<byte> password, CancellationToken cancellationToken)
    {
        var prompt = SshCommandLine.RequiresPassword(request) ? $"[myservermanager:{Guid.NewGuid():N}]" : null;
        using var command = client.CreateCommand(SshCommandLine.Format(request, prompt), Encoding.UTF8);
        command.CommandTimeout = request.Timeout;
        var execution = command.ExecuteAsync(cancellationToken);
        using var output = new StreamReader(command.OutputStream, Encoding.UTF8, leaveOpen: true);
        using var error = new StreamReader(command.ExtendedOutputStream, Encoding.UTF8, leaveOpen: true);
        var stdout = CommandRunner.CaptureAsync(output, request.OutputLimit, cancellationToken);
        var pendingPrompt = "";
        var passwordSent = false;
        var stderr = CommandRunner.CaptureAsync(error, 16384, cancellationToken, prompt is null ? null : OnSudoOutputAsync);
        await Task.WhenAll(execution, stdout, stderr).WaitAsync(cancellationToken);
        return new(command.ExitStatus, stdout.Result.Text, stderr.Result.Text,
            TimedOut: command.ExitStatus is 124 or 137, OutputTruncated: stdout.Result.Truncated);

        async ValueTask OnSudoOutputAsync(ReadOnlyMemory<char> data)
        {
            if (passwordSent)
                return;
            var text = pendingPrompt + data.ToString();
            if (!text.Contains(prompt!, StringComparison.Ordinal))
            {
                pendingPrompt = text[Math.Max(0, text.Length - prompt!.Length + 1)..];
                return;
            }

            passwordSent = true;
            try
            {
                using var input = command.CreateInputStream();
                await input.WriteAsync(password, cancellationToken);
                input.WriteByte((byte)'\n');
            }
            catch (InvalidOperationException) when (execution.IsCompleted) { }
        }
    }

    public void Dispose()
    {
        try { client.Dispose(); }
        finally { authentication.Dispose(); }
    }
}

public sealed class SshHostKeyRejectedException : Exception;

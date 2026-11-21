using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace MyServerManager.Security;

public sealed record CommandResult(int? ExitCode, string Output = "", string Error = "",
    bool TimedOut = false, bool OutputTruncated = false, bool ConnectionUnavailable = false)
{
    public bool Succeeded => ExitCode == 0 && !TimedOut;

    public string FailureMessage(string subject)
    {
        if (ConnectionUnavailable)
            return "Connect to Ubuntu over SSH and try again.";
        if (TimedOut || ExitCode is 124 or 137 or 143)
            return $"{subject} timed out. A systemd job may still be in progress.";
        if (ExitCode is null)
            return $"{subject} could not be run on this host.";
        if (Error.Contains("Sorry, try again.", StringComparison.OrdinalIgnoreCase) ||
            Error.Contains("incorrect password", StringComparison.OrdinalIgnoreCase))
            return "Sudo rejected the SSH password for this operation.";
        if (Error.Contains("sudo:", StringComparison.Ordinal) &&
            (Error.Contains("password is required", StringComparison.OrdinalIgnoreCase) ||
             Error.Contains("not allowed", StringComparison.OrdinalIgnoreCase) ||
             Error.Contains("sudoers", StringComparison.OrdinalIgnoreCase) ||
             Error.Contains("terminal", StringComparison.OrdinalIgnoreCase)))
            return $"{subject} was denied by the server's sudo policy.";
        return $"{subject} exited with code {ExitCode}. Check the server journal for details.";
    }
}

public interface ICommandRunner
{
    Task<CommandResult> RunAsync(CommandRequest command, CancellationToken cancellationToken = default);
}

public sealed class CommandRunner : ICommandRunner
{
    public async Task<CommandResult> RunAsync(CommandRequest command, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsLinux() || LinuxIdentity.IsRoot)
            return new(null);

        cancellationToken.ThrowIfCancellationRequested();
        var start = new ProcessStartInfo(command.Executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            WorkingDirectory = "/"
        };
        foreach (var argument in command.Arguments)
            start.ArgumentList.Add(argument);
        start.Environment.Clear();
        start.Environment["PATH"] = "/usr/sbin:/usr/bin:/sbin:/bin";
        start.Environment["LC_ALL"] = "C";
        start.Environment["SYSTEMD_COLORS"] = "0";

        using var process = new Process { StartInfo = start };
        try
        {
            if (!process.Start())
                return new(null);
        }
        catch (Win32Exception)
        {
            return new(null);
        }

        process.StandardInput.Close();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(command.Timeout);
        var stdout = CaptureAsync(process.StandardOutput, command.OutputLimit, deadline.Token);
        var stderr = CaptureAsync(process.StandardError, 16384, deadline.Token);
        try
        {
            await Task.WhenAll(process.WaitForExitAsync(deadline.Token), stdout, stderr);
            return new(process.ExitCode, stdout.Result.Text, stderr.Result.Text,
                OutputTruncated: stdout.Result.Truncated);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            TryKill(process);
            cancellationToken.ThrowIfCancellationRequested();
            return new(null, TimedOut: true);
        }
        catch (IOException)
        {
            TryKill(process);
            return new(null);
        }
    }

    internal static async Task<(string Text, bool Truncated)> CaptureAsync(StreamReader reader, int limit,
        CancellationToken cancellationToken, Func<ReadOnlyMemory<char>, ValueTask>? onRead = null)
    {
        var buffer = new char[4096];
        var result = new StringBuilder();
        var truncated = false;
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken)) != 0)
        {
            if (onRead is not null)
                await onRead(buffer.AsMemory(0, count));
            var take = Math.Min(count, limit - result.Length);
            result.Append(buffer, 0, take);
            truncated |= take != count;
        }
        return (result.ToString(), truncated);
    }

    private static void TryKill(Process process)
    {
        try { process.Kill(entireProcessTree: true); }
        catch (Exception error) when (error is InvalidOperationException or Win32Exception or AggregateException) { }
    }
}

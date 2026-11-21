using System.Globalization;

namespace MyServerManager.Security;

internal static class SshCommandLine
{
    public static bool RequiresPassword(CommandRequest command) => command.Executable == "/usr/bin/sudo";

    public static string Format(CommandRequest command, string? sudoPrompt)
    {
        var arguments = sudoPrompt is not null
            ? new[] { "/usr/bin/sudo", "-S", "-p", sudoPrompt, "--" }
                .Concat(command.Arguments.Skip(2))
            : command.Arguments.Prepend(command.Executable);

        // The remote SSH exec request runs through the account's shell. Quote every argument as one word.
        var timeout = Math.Max(1, (int)command.Timeout.TotalSeconds - 2).ToString(CultureInfo.InvariantCulture);
        return "exec /usr/bin/env -i PATH=/usr/sbin:/usr/bin:/sbin:/bin LC_ALL=C TZ=UTC SYSTEMD_COLORS=0 " +
            "/usr/bin/timeout --kill-after=1s " + timeout + "s " + string.Join(' ', arguments.Select(Quote));
    }

    private static string Quote(string argument) => "'" + argument.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
}

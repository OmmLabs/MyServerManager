using System.Text.Json;
using MyServerManager.Security;
using MyServerManager.Services;

namespace MyServerManager.Configuration;

internal static class RegistryVerification
{
    public static async Task<int> RunAsync(string[] arguments)
    {
        if (arguments.Length != 2 || !OperatingSystem.IsLinux() || LinuxIdentity.IsRoot)
        {
            Console.Error.WriteLine("Run --verify-registry FILE on Linux as the dedicated service user.");
            return 1;
        }

        try
        {
            var entries = await ApplicationRegistry.ReadFileAsync(arguments[1]);
            var units = entries.Where(entry => entry.Enabled).Select(entry => entry.ServiceName).ToArray();
            var commands = new CommandRunner();
            var native = new ManagerMode(usesSsh: false);
            var systemd = new SystemdServices(commands, new LinuxHostMetrics(native, commands, new CgroupMetrics(), TimeProvider.System));
            var statuses = await systemd.ReadManyAsync(units, includeMetrics: false);
            var unresolved = statuses.Values.Where(status => !status.Exists).Select(status => status.ServiceName).ToArray();
            if (unresolved.Length > 0)
            {
                Console.Error.WriteLine("systemd could not verify these enabled units by canonical name: " + string.Join(", ", unresolved));
                return 1;
            }

            Console.WriteLine($"Registry schema and {units.Length} enabled systemd unit names verified.");
            return 0;
        }
        catch (InvalidDataException error)
        {
            Console.Error.WriteLine(error.Message);
            return 1;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            Console.Error.WriteLine("The reviewed registry could not be read. Check its permissions and JSON schema.");
            return 1;
        }
    }
}

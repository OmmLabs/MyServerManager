using System.Runtime.InteropServices;

namespace MyServerManager.Security;

internal static class LinuxIdentity
{
    public static bool IsRoot => OperatingSystem.IsLinux() && GetEffectiveUserId() == 0;

    [DllImport("libc", EntryPoint = "geteuid")]
    private static extern uint GetEffectiveUserId();
}

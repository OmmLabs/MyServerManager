namespace MyServerManager.Services;

internal static class LinuxFiles
{
    public static string? Read(string path)
    {
        try { return File.ReadAllText(path); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return null; }
    }
}

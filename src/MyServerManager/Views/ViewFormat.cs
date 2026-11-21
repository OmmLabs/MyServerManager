using System.Globalization;

namespace MyServerManager.Views;

public static class ViewFormat
{
    public const string Unavailable = "N/A";

    public static string Bytes(ulong? bytes)
    {
        if (bytes is not { } amount)
            return Unavailable;

        string[] units = ["B", "KiB", "MiB", "GiB", "TiB", "PiB", "EiB"];
        var value = (double)amount;
        var index = 0;
        while (value >= 1024 && index < units.Length - 1)
        {
            value /= 1024;
            index++;
        }
        return $"{value.ToString("0.#", CultureInfo.CurrentCulture)} {units[index]}";
    }

    public static string Duration(TimeSpan? time) => time switch
    {
        null => Unavailable,
        { TotalDays: >= 1 } value => $"{(int)value.TotalDays}d {value.Hours}h",
        { TotalHours: >= 1 } value => $"{(int)value.TotalHours}h {value.Minutes}m",
        { TotalMinutes: >= 1 } value => $"{(int)value.TotalMinutes}m {value.Seconds}s",
        { } value => $"{value.TotalSeconds:0.#}s"
    };

    public static string CpuTime(ulong? nanoseconds) =>
        Duration(nanoseconds is { } value ? TimeSpan.FromTicks((long)(value / 100)) : null);

    public static string Number<T>(T? value) where T : struct => value?.ToString() ?? Unavailable;

    public static string Percent(double? value) => value is { } percent ? $"{percent:0.#}%" : Unavailable;

    public static string Timestamp(DateTimeOffset? value) => value?.ToString("yyyy-MM-dd HH:mm:ss zzz") ?? Unavailable;
}

using System.Text.Json.Serialization;

namespace MyServerManager.Configuration;

public sealed record SshEndpoint(string Host, int Port, string Username, string[] Fingerprints)
{
    [JsonIgnore]
    public string DisplayName
    {
        get
        {
            var address = Host.Contains(':') ? $"[{Host}]" : Host;
            return Port == 22 ? $"{Username}@{address}" : $"{Username}@{address}:{Port}";
        }
    }

    public bool HasTrustedKeys() => HasValidAddress() && Fingerprints is { Length: > 0 and <= 10 } && Fingerprints.All(IsFingerprint);

    public bool Trusts(string fingerprint) => Fingerprints.Contains("SHA256:" + fingerprint, StringComparer.Ordinal);

    public static SshEndpoint? ParseAddress(string? value)
    {
        var address = value?.Trim();
        if (address is not { Length: > 0 and <= 350 } || address.Count(character => character == '@') != 1 ||
            address.Any(character => char.IsWhiteSpace(character) || char.IsControl(character) || character is '/' or '\\' or '?' or '#' or '%') ||
            !Uri.TryCreate("ssh://" + address, UriKind.Absolute, out var uri))
            return null;

        var endpoint = new SshEndpoint(uri.Host.Trim('[', ']'), uri.Port < 0 ? 22 : uri.Port, uri.UserInfo, []);
        return endpoint.HasValidAddress() ? endpoint : null;
    }

    private bool HasValidAddress() => Host is { Length: > 0 and <= 253 } && Uri.CheckHostName(Host) != UriHostNameType.Unknown &&
        Username is { Length: > 0 and <= 64 } && !Username.Equals("root", StringComparison.OrdinalIgnoreCase) &&
        Username.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.') && Port is >= 1 and <= 65535;

    internal static bool IsFingerprint(string value)
    {
        if (value is not { Length: 50 } || !value.StartsWith("SHA256:", StringComparison.Ordinal))
            return false;
        Span<byte> decoded = stackalloc byte[32];
        return Convert.TryFromBase64String(value[7..] + "=", decoded, out var length) && length == decoded.Length;
    }
}

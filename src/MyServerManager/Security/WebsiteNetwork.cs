using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace MyServerManager.Security;

public static class WebsiteNetwork
{
    private static readonly IPNetwork[] ExcludedV4 = Parse([
        "0.0.0.0/8", "10.0.0.0/8", "100.64.0.0/10", "127.0.0.0/8", "169.254.0.0/16", "172.16.0.0/12",
        "192.0.0.0/24", "192.0.2.0/24", "192.88.99.0/24", "192.168.0.0/16", "198.18.0.0/15", "198.51.100.0/24",
        "203.0.113.0/24", "224.0.0.0/4", "240.0.0.0/4"
    ]);
    private static readonly IPNetwork GlobalV6 = IPNetwork.Parse("2000::/3");
    private static readonly IPNetwork[] ExcludedV6 = Parse(["2001::/23", "2001:db8::/32", "2002::/16", "3fff::/20"]);

    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();
        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => !ExcludedV4.Any(network => network.Contains(address)),
            AddressFamily.InterNetworkV6 => address.ScopeId == 0 && GlobalV6.Contains(address) &&
                !ExcludedV6.Any(network => network.Contains(address)),
            _ => false
        };
    }

    public static SocketsHttpHandler CreateHandler() => new()
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        UseProxy = false,
        ConnectTimeout = TimeSpan.FromSeconds(3),
        PooledConnectionLifetime = TimeSpan.FromMinutes(1),
        MaxResponseHeadersLength = 16,
        MaxResponseDrainSize = 0,
        SslOptions = new SslClientAuthenticationOptions
        {
            CertificateChainPolicy = new X509ChainPolicy
            {
                DisableCertificateDownloads = true,
                RevocationMode = X509RevocationMode.NoCheck
            }
        },
        ConnectCallback = (context, token) => ConnectAsync(context.DnsEndPoint, token)
    };

    public static async ValueTask<Stream> ConnectAsync(DnsEndPoint endpoint, CancellationToken cancellationToken)
    {
        var candidates = IPAddress.TryParse(endpoint.Host, out var literal) ? [literal] :
            await Dns.GetHostAddressesAsync(endpoint.Host, cancellationToken);
        var addresses = candidates.Where(IsPublicAddress).Take(16).ToArray();
        if (addresses.Length == 0)
            throw new BlockedWebsiteException();

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            // Connect to the filtered IPs without a second DNS lookup.
            await socket.ConnectAsync(addresses, endpoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static IPNetwork[] Parse(string[] ranges) => ranges.Select(IPNetwork.Parse).ToArray();
}

public sealed class BlockedWebsiteException : IOException;

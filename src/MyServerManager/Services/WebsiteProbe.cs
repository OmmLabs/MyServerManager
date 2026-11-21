using System.Net;
using System.Net.Sockets;
using MyServerManager.Models;
using MyServerManager.Security;

namespace MyServerManager.Services;

public sealed class WebsiteProbe(HttpMessageHandler handler, TimeProvider time) : IDisposable
{
    private readonly HttpClient _client = new(handler) { Timeout = Timeout.InfiniteTimeSpan };

    public async Task<WebsiteHealth> CheckAsync(string url, CancellationToken cancellationToken)
    {
        var start = time.GetTimestamp();
        int? code = null;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        if (!ApplicationRegistry.TryWebsiteUri(url, out var uri))
            return Result("Invalid website URL.");

        try
        {
            var redirects = 0;
            var method = HttpMethod.Head;
            while (true)
            {
                if (IPAddress.TryParse(uri!.IdnHost.Trim('[', ']'), out var literal) && !WebsiteNetwork.IsPublicAddress(literal))
                    return Result("This URL points to a private or reserved address.");

                using var request = new HttpRequestMessage(method, uri);
                using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
                code = (int)response.StatusCode;
                if (method == HttpMethod.Head && code is 405 or 501)
                {
                    method = HttpMethod.Get;
                    continue;
                }

                if (code is 301 or 302 or 303 or 307 or 308)
                {
                    if (++redirects > 3)
                        return Result("The redirect limit was reached.");
                    if (response.Headers.Location is not { } location || !Uri.TryCreate(uri, location, out var next) ||
                        !ApplicationRegistry.TryWebsiteUri(next.AbsoluteUri, out _) || uri.Scheme == "https" && next.Scheme == "http")
                        return Result("The website returned an unsupported or unsafe redirect.");
                    uri = next;
                    method = HttpMethod.Head;
                    continue;
                }

                return Result(response.IsSuccessStatusCode ? null : $"The website returned HTTP {code}.");
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result("The website check timed out.");
        }
        catch (UriFormatException)
        {
            return Result("The website hostname or redirect is invalid.");
        }
        catch (HttpRequestException error)
        {
            return Result(error.InnerException switch
            {
                BlockedWebsiteException => "The hostname resolves to a private or reserved address.",
                SocketException { SocketErrorCode: SocketError.HostNotFound or SocketError.NoData or SocketError.TryAgain } => "DNS lookup failed.",
                _ => error.HttpRequestError switch
                {
                    HttpRequestError.NameResolutionError => "DNS lookup failed.",
                    HttpRequestError.SecureConnectionError => "TLS validation or negotiation failed.",
                    HttpRequestError.ConnectionError => "The website connection failed.",
                    _ => "The website did not return a valid HTTP response."
                }
            });
        }

        WebsiteHealth Result(string? error) => new(url, error is null, code,
            (long)time.GetElapsedTime(start).TotalMilliseconds, time.GetUtcNow(), error);
    }

    public void Dispose() => _client.Dispose();
}

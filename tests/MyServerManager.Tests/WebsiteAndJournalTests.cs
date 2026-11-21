using System.Net;
using MyServerManager.Security;
using MyServerManager.Services;

namespace MyServerManager.Tests;

public sealed class WebsiteAndJournalTests
{
    [Theory]
    [InlineData("127.0.0.1", false)]
    [InlineData("10.2.1.1", false)]
    [InlineData("169.254.169.254", false)]
    [InlineData("100.64.1.2", false)]
    [InlineData("::ffff:192.168.1.1", false)]
    [InlineData("fc00::1", false)]
    [InlineData("2001:db8::1", false)]
    [InlineData("8.8.8.8", true)]
    [InlineData("2606:4700:4700::1111", true)]
    public void ProbeOnlyConnectsToPublicAddresses(string address, bool expected) =>
        Assert.Equal(expected, WebsiteNetwork.IsPublicAddress(IPAddress.Parse(address)));

    [Fact]
    public async Task PrivateAddressIsRejectedBeforeTheSocketConnects()
    {
        await Assert.ThrowsAsync<BlockedWebsiteException>(async () =>
            await WebsiteNetwork.ConnectAsync(new DnsEndPoint("169.254.169.254", 80), CancellationToken.None));
    }

    [Fact]
    public async Task ProbeUsesHeadAndFallsBackToGetWithoutBufferingTheBody()
    {
        var handler = new FakeWebsiteHandler
        {
            Respond = request => request.Method == HttpMethod.Head ? new(HttpStatusCode.MethodNotAllowed) :
                new(HttpStatusCode.OK) { Content = new HeaderOnlyContent() }
        };
        using var probe = new WebsiteProbe(handler, TimeProvider.System);
        var result = await probe.CheckAsync("https://example.com/health", CancellationToken.None);
        Assert.True(result.Available);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(new[] { HttpMethod.Head, HttpMethod.Get }, handler.Calls.Select(call => call.Method));
    }

    [Fact]
    public async Task ProbeBlocksRedirectsToMetadataAddresses()
    {
        var handler = new FakeWebsiteHandler
        {
            Respond = _ => new HttpResponseMessage(HttpStatusCode.Redirect)
            {
                Headers = { Location = new Uri("http://169.254.169.254/") }
            }
        };
        using var probe = new WebsiteProbe(handler, TimeProvider.System);
        var result = await probe.CheckAsync("http://example.com", CancellationToken.None);
        Assert.False(result.Available);
        Assert.Contains("private or reserved", result.Error);
        Assert.Single(handler.Calls);
    }

    [Fact]
    public async Task HealthCheckRequiresCsrfAndReportsHttpIndependentlyOfSystemd()
    {
        await using var factory = new ManagerFactory("https://fixture.example/health");
        factory.Websites.Respond = _ => new(HttpStatusCode.ServiceUnavailable);
        using var client = factory.Client();
        using var login = await ManagerFactory.LoginAsync(client);
        using var rejected = await client.PostAsync("/Websites/Check", ManagerFactory.Form("", ("service", ManagerFactory.Unit)));
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        var token = await ManagerFactory.TokenAsync(client, "/Websites");
        using var checkedSite = await client.PostAsync("/Websites/Check", ManagerFactory.Form(token, ("service", ManagerFactory.Unit)));
        Assert.Equal(HttpStatusCode.Redirect, checkedSite.StatusCode);
        Assert.Contains("The website returned HTTP 503", await client.GetStringAsync("/Websites"));
        Assert.Contains(">Running</span>", await client.GetStringAsync("/Applications"));
        Assert.Single(factory.Websites.Calls);
    }

    [Fact]
    public async Task JournalArgumentsAreBoundedAndMessagesAreEncodedAndRedacted()
    {
        await using var factory = new ManagerFactory();
        factory.Commands.Override = command => Task.FromResult(command.Arguments.Contains("/usr/bin/journalctl")
            ? new CommandResult(0, """
                {"__REALTIME_TIMESTAMP":"1700000000000000","PRIORITY":"6","MESSAGE":"warn: <script>fixture</script>"}
                {"__REALTIME_TIMESTAMP":"1700000001000000","PRIORITY":"3","MESSAGE":"Password=fixture-password-secret"}
                """) : FakeCommands.Default(command));
        using var client = factory.Client();
        using var login = await ManagerFactory.LoginAsync(client);
        using var invalidLines = await client.GetAsync($"/Logs?service={ManagerFactory.Unit}&lines=1000");
        using var invalidUnit = await client.GetAsync("/Logs?service=ssh.service");
        Assert.Equal(HttpStatusCode.BadRequest, invalidLines.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, invalidUnit.StatusCode);
        var html = await client.GetStringAsync($"/Logs?service={ManagerFactory.Unit}&lines=250");
        Assert.Contains("&lt;script&gt;fixture&lt;/script&gt;", html);
        Assert.Contains("data-log-entry=\"Warning\"", html);
        Assert.Contains("data-log-entry=\"Error\"", html);
        Assert.Contains("Message hidden", html);
        Assert.DoesNotContain("fixture-password-secret", html);
        var call = Assert.Single(factory.Commands.Calls, command => command.Arguments.Contains("/usr/bin/journalctl"));
        Assert.Equal("--lines=250", call.Arguments[^1]);
    }

    private sealed class HeaderOnlyContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            throw new InvalidOperationException("The health check attempted to buffer the response body.");

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}

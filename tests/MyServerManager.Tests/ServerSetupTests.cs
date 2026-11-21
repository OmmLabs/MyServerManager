using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using MyServerManager.Configuration;
using MyServerManager.Services;

namespace MyServerManager.Tests;

public sealed class ServerSetupTests
{
    [Theory]
    [InlineData("ubuntu@server.example.com", "ubuntu", "server.example.com", 22)]
    [InlineData(" deploy@192.0.2.10:2222 ", "deploy", "192.0.2.10", 2222)]
    [InlineData("ubuntu@[2001:db8::1]:2202", "ubuntu", "2001:db8::1", 2202)]
    public void ParsesSshAddress(string value, string username, string host, int port)
    {
        var endpoint = Assert.IsType<SshEndpoint>(SshEndpoint.ParseAddress(value));
        Assert.Equal((username, host, port), (endpoint.Username, endpoint.Host, endpoint.Port));
        Assert.False(endpoint.HasTrustedKeys());
    }

    [Theory]
    [InlineData("root@server.example.com")]
    [InlineData("ubuntu:password@server.example.com")]
    [InlineData("ubuntu@server.example.com:0")]
    [InlineData("ubuntu@server.example.com:70000")]
    [InlineData("ubuntu@server.example.com/path")]
    [InlineData("ubuntu@server.example.com@elsewhere.example.com")]
    [InlineData("ssh://ubuntu@server.example.com")]
    public void RejectsInvalidAddresses(string value) => Assert.Null(SshEndpoint.ParseAddress(value));

    [Fact]
    public async Task NewServerRequiresTrustedKeyConfirmationAndValidPasswordBeforeSaving()
    {
        await using var factory = new ManagerFactory(provisionAdministrator: false, provisionServer: false) { UsesSsh = true };
        using var client = factory.Client();
        using var login = await client.GetAsync("/Account/Login");
        Assert.Equal("/Setup", login.Headers.Location?.OriginalString);
        var (csrf, setupToken) = await CheckAddressAsync(client, "ubuntu@fresh-server.example:2222");
        Assert.Equal(1, factory.Ssh.KeyChecks);
        Assert.Equal(0, factory.Ssh.Attempts);

        using var missingConfirmation = await RegisterAsync(client, csrf, setupToken, confirmed: false);
        Assert.Equal(HttpStatusCode.OK, missingConfirmation.StatusCode);
        Assert.Equal(0, factory.Ssh.Attempts);
        using var wrongPassword = await RegisterAsync(client, csrf, setupToken, password: "wrong password");
        Assert.Contains("Ubuntu rejected the password", await wrongPassword.Content.ReadAsStringAsync());
        var profiles = factory.Services.GetRequiredService<ServerProfileStore>();
        Assert.Null(profiles.Current);
        Assert.False(File.Exists(Path.Combine(factory.State.FullName, "config", "server.json")));

        using var registered = await RegisterAsync(client, csrf, setupToken);
        Assert.Equal("/", registered.Headers.Location?.OriginalString);
        var server = Assert.IsType<RegisteredServer>(profiles.Current);
        Assert.Equal("ubuntu@fresh-server.example:2222", server.Endpoint.DisplayName);
        Assert.Equal(FakeSshFactory.Fingerprint, Assert.Single(server.Endpoint.Fingerprints));
        Assert.DoesNotContain(FakeSshFactory.Password, await File.ReadAllTextAsync(Path.Combine(factory.State.FullName, "config", "server.json")));
        Assert.Contains("No registered applications", await client.GetStringAsync("/Applications"));
        Assert.Contains("Server registered", await client.GetStringAsync("/Audit"));
    }

    [Fact]
    public async Task AChangedHostKeyOrTamperedSetupTokenCannotRegisterTheServer()
    {
        await using var factory = new ManagerFactory(provisionServer: false) { UsesSsh = true };
        using var client = factory.Client();
        var (csrf, setupToken) = await CheckAddressAsync(client, "ubuntu@fresh-server.example");
        using var tampered = await RegisterAsync(client, csrf, setupToken + "tampered");
        Assert.Equal("/Setup", tampered.Headers.Location?.OriginalString);
        Assert.Equal(0, factory.Ssh.Attempts);

        factory.Ssh.PresentedFingerprint = "SHA256:BAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        using var changed = await RegisterAsync(client, csrf, setupToken);
        Assert.Contains("SSH key could not be verified", await changed.Content.ReadAsStringAsync());
        Assert.Null(factory.Services.GetRequiredService<ServerProfileStore>().Current);
        Assert.All(factory.Ssh.Credential!, value => Assert.Equal(0, value));
    }

    [Fact]
    public async Task RemovingServerRequiresSignedInAdminAndKeepsNewServerRegistrySeparate()
    {
        await using var factory = new ManagerFactory { UsesSsh = true };
        using var client = factory.Client();
        using var anonymous = await client.PostAsync("/Connection/Deregister", ManagerFactory.Form("", ("confirmed", "true")));
        Assert.Contains("/Account/Login", anonymous.Headers.Location?.OriginalString);

        using var login = await ManagerFactory.LoginAsync(client, FakeSshFactory.Password);
        var csrf = await ManagerFactory.TokenAsync(client, "/Connection");
        using var noToken = await client.PostAsync("/Connection/Deregister", ManagerFactory.Form("", ("confirmed", "true")));
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        using var noConfirm = await client.PostAsync("/Connection/Deregister", ManagerFactory.Form(csrf));
        Assert.Equal(HttpStatusCode.BadRequest, noConfirm.StatusCode);
        using var removed = await client.PostAsync("/Connection/Deregister", ManagerFactory.Form(csrf, ("confirmed", "true")));
        Assert.Equal("/Setup", removed.Headers.Location?.OriginalString);
        Assert.False(factory.Services.GetRequiredService<SshConnection>().IsConnected);
        Assert.Null(factory.Services.GetRequiredService<ServerProfileStore>().Current);

        var archived = Path.Combine(factory.State.FullName, "servers", factory.ServerId.ToString("N"));
        Assert.True(File.Exists(Path.Combine(archived, "applications.json")));
        var audit = Assert.Single(Directory.GetFiles(Path.Combine(archived, "audit"), "*.jsonl"));
        Assert.Contains("Server removed", await File.ReadAllTextAsync(audit));

        var (newCsrf, token) = await CheckAddressAsync(client, "ubuntu@another-server.example");
        using var another = await RegisterAsync(client, newCsrf, token);
        Assert.Equal("/", another.Headers.Location?.OriginalString);
        Assert.DoesNotContain(ManagerFactory.Unit, await client.GetStringAsync("/Applications"));
        Assert.DoesNotContain("Server removed", await client.GetStringAsync("/Audit"));
    }

    private static async Task<(string Csrf, string Setup)> CheckAddressAsync(HttpClient client, string address)
    {
        var csrf = await ManagerFactory.TokenAsync(client, "/Setup");
        using var check = await client.PostAsync("/Setup/CheckAddress", ManagerFactory.Form(csrf, ("Address", address)));
        check.EnsureSuccessStatusCode();
        var page = await check.Content.ReadAsStringAsync();
        Assert.Contains(FakeSshFactory.Fingerprint, page);
        var token = Regex.Match(page, "name=\"Token\"[^>]+value=\"([^\"]+)\"");
        Assert.True(token.Success);
        return (csrf, WebUtility.HtmlDecode(token.Groups[1].Value));
    }

    private static Task<HttpResponseMessage> RegisterAsync(HttpClient client, string csrf, string setup,
        bool confirmed = true, string password = FakeSshFactory.Password) => client.PostAsync("/Setup/Register",
        ManagerFactory.Form(csrf, ("Token", setup), ("Confirmed", confirmed.ToString()), ("Password", password)));
}

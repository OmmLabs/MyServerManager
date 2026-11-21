using System.Globalization;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using MyServerManager.Security;
using MyServerManager.Services;

namespace MyServerManager.Tests;

public sealed class SshDashboardTests
{
    private const string Group = "/apps.slice/test-app.service";

    [Fact]
    public async Task UbuntuPasswordAloneOpensDashboardAndSignOutClosesTheConnection()
    {
        await using var factory = new ManagerFactory(provisionAdministrator: false) { UsesSsh = true };
        factory.Commands.Override = command => Task.FromResult(Fixture(command));
        using var client = factory.Client();
        using var protectedPage = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, protectedPage.StatusCode);
        var loginPage = await client.GetStringAsync("/Account/Login");
        Assert.Contains("Ubuntu password", loginPage);
        Assert.DoesNotContain("name=\"Username\"", loginPage);
        Assert.Empty(factory.Commands.Calls);

        using var rejected = await client.PostAsync("/Account/Login", ManagerFactory.Form("", ("Password", FakeSshFactory.Password)));
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal(0, factory.Ssh.Attempts);

        using var connected = await SshLoginAsync(client);
        Assert.Equal("/", connected.Headers.Location?.OriginalString);
        Assert.True(factory.Services.GetRequiredService<SshConnection>().IsConnected);
        Assert.True(factory.Ssh.Endpoint!.HasTrustedKeys());

        var dashboard = await client.GetStringAsync("/");
        Assert.Contains("Connected to Ubuntu", dashboard);
        Assert.Contains("256 MiB", dashboard);
        Assert.Contains("2 GiB", dashboard);
        Assert.Contains("40%", dashboard);
        Assert.DoesNotContain(FakeSshFactory.Password, dashboard);
        var monitoring = await client.GetStringAsync("/Monitoring");
        Assert.Contains("fixture-ubuntu", monitoring);
        Assert.Contains("Microsoft.AspNetCore.App 10.0.13", monitoring);
        var details = await client.GetStringAsync("/Applications/Status?service=" + ManagerFactory.Unit);
        Assert.Contains("512 MiB", details);
        Assert.Contains(">3</dd>", details);
        Assert.Contains("30s", details);
        Assert.DoesNotContain("Administrator password", await client.GetStringAsync("/Settings"));

        var token = await ManagerFactory.TokenAsync(client, "/");
        using var disconnected = await client.PostAsync("/Account/Logout", ManagerFactory.Form(token));
        Assert.Equal(HttpStatusCode.Redirect, disconnected.StatusCode);
        Assert.False(factory.Services.GetRequiredService<SshConnection>().IsConnected);
        Assert.All(factory.Ssh.Credential!, value => Assert.Equal(0, value));
        using var signedOut = await client.GetAsync("/Applications");
        Assert.Contains("/Account/Login", signedOut.Headers.Location?.OriginalString);
        var status = await factory.Services.GetRequiredService<ApplicationReadService>().GetAsync(CancellationToken.None);
        Assert.All(status, app => Assert.Null(app.Status.MemoryBytes));
    }

    [Fact]
    public async Task RemoteCommandsUseOnlyTheRegistryAndExistingSshAccount()
    {
        await using var factory = new ManagerFactory { UsesSsh = true };
        factory.Commands.Override = command => Task.FromResult(Fixture(command));
        using var client = factory.Client();
        using var connected = await SshLoginAsync(client);
        var token = await ManagerFactory.TokenAsync(client, "/");
        using var unknown = await client.PostAsync("/Applications/Operate", ManagerFactory.Form(token,
            ("service", "ssh.service"), ("operation", "stop"), ("confirmed", "true")));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        using var restart = await client.PostAsync("/Applications/Operate", ManagerFactory.Form(token,
            ("service", ManagerFactory.Unit), ("operation", "restart"), ("confirmed", "true")));
        Assert.Equal(HttpStatusCode.Redirect, restart.StatusCode);
        var privileged = Assert.Single(factory.Commands.Calls, call => call.Executable == "/usr/bin/sudo");
        Assert.Equal(ManagerFactory.Unit, privileged.Arguments[^1]);
        var audit = await client.GetStringAsync("/Audit");
        Assert.Contains("systemd completed restart", audit);
        Assert.DoesNotContain(FakeSshFactory.Password, audit);
        using var logout = await client.PostAsync("/Account/Logout", ManagerFactory.Form(token));
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.False(factory.Services.GetRequiredService<SshConnection>().IsConnected);
        Assert.All(factory.Ssh.Credential!, value => Assert.Equal(0, value));
    }

    [Fact]
    public async Task FailedSshAuthenticationClearsTheCredentialAndRunsNoCommands()
    {
        await using var factory = new ManagerFactory { UsesSsh = true };
        using var client = factory.Client();
        const string invalid = "fixture incorrect password";
        using var failed = await SshLoginAsync(client, invalid);
        Assert.Equal(HttpStatusCode.OK, failed.StatusCode);
        var page = await failed.Content.ReadAsStringAsync();
        Assert.Contains("Ubuntu rejected the password", page);
        Assert.DoesNotContain(invalid, page);
        Assert.All(factory.Ssh.Credential!, value => Assert.Equal(0, value));
        Assert.Empty(factory.Commands.Calls);
        using var protectedPage = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, protectedPage.StatusCode);
    }

    [Fact]
    public async Task ExistingConnectionNeverAuthenticatesANewBrowserAndSuccessfulLoginRotatesTheSession()
    {
        await using var factory = new ManagerFactory(provisionAdministrator: false) { UsesSsh = true };
        using var first = factory.Client();
        using var second = factory.Client();
        using var login = await SshLoginAsync(first);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        var credential = factory.Ssh.Credential!;

        using var rejected = await SshLoginAsync(second, "incorrect password");
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Equal(2, factory.Ssh.Attempts);
        using var allowed = await first.GetAsync("/Applications");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);

        using var replacement = await SshLoginAsync(second);
        Assert.Equal(HttpStatusCode.Redirect, replacement.StatusCode);
        Assert.All(credential, value => Assert.Equal(0, value));
        using var revoked = await first.GetAsync("/Applications");
        Assert.Equal(HttpStatusCode.Redirect, revoked.StatusCode);
        using var current = await second.GetAsync("/Applications");
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
    }

    [Fact]
    public async Task SshConnectionExpiresWithTheDashboardSession()
    {
        await using var factory = new ManagerFactory { UsesSsh = true };
        var session = factory.Services.GetRequiredService<SshConnection>();
        var result = await session.SignInAsync(FakeSshFactory.Password, DateTimeOffset.UtcNow.AddSeconds(1), CancellationToken.None);
        Assert.NotNull(result.Principal);
        await Task.Delay(TimeSpan.FromSeconds(1.1));
        Assert.True(SpinWait.SpinUntil(() => factory.Ssh.Credential!.All(value => value == 0), TimeSpan.FromSeconds(3)));
        Assert.False(session.IsConnected);
        Assert.False(session.IsCurrentSession(result.Principal));
    }

    private static async Task<HttpResponseMessage> SshLoginAsync(HttpClient client, string password = FakeSshFactory.Password)
    {
        var token = await ManagerFactory.TokenAsync(client, "/Account/Login");
        return await client.PostAsync("/Account/Login", ManagerFactory.Form(token, ("Password", password)));
    }

    private static CommandResult Fixture(CommandRequest command)
    {
        if (command.Executable == "/usr/bin/systemctl" && command.Arguments.Contains(ManagerFactory.Unit))
        {
            var result = FakeCommands.Default(command);
            return result with { Output = result.Output.Replace("Id=" + ManagerFactory.Unit + "\n",
                $"Id={ManagerFactory.Unit}\nControlGroup={Group}\nActiveEnterTimestamp=Sun 2026-11-08 12:34:00 UTC\n") };
        }
        if (command.Executable == "/bin/bash")
        {
            Assert.Contains(Group, command.Arguments);
            var now = new DateTimeOffset(2026, 11, 8, 12, 34, 30, TimeSpan.Zero).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
            return new(0, string.Join('\0', now, Group, "268435456", "536870912", "21", "6000000", "3", ""));
        }
        if (command.Executable == "/bin/sh" && command.Arguments[1].Contains("/proc/stat", StringComparison.Ordinal))
            return new(0, "cpu 100 10 80 1000 30 4 10 6\ncpu0 100 10 80 1000\n\0" +
                "0.1 0.2 0.3 1/100 100\n\0" + "900 1200\n\0" + "2026-11-08T12:34:30+00:00\n");
        if (command.Executable == "/bin/sh")
            return new(0, "fixture-ubuntu\n\0\"Ubuntu fixture\"\n\0" + "6.8.0\n\0Microsoft.AspNetCore.App 10.0.13\n");
        if (command.Executable == "/usr/bin/cat")
            return new(0, "MemTotal: 2097152 kB\nMemAvailable: 1048576 kB\n");
        if (command.Executable == "/usr/bin/df")
            return new(0, "/dev/fixture 10000 4000 6000 40% /\n");
        return FakeCommands.Default(command);
    }
}

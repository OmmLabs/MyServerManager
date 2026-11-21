using System.Net;
using MyServerManager.Security;

namespace MyServerManager.Tests;

public sealed class InfrastructureTests
{
    [Fact]
    public async Task NginxReloadIsBlockedWhenConfigurationValidationFails()
    {
        await using var factory = new ManagerFactory { NginxLayout = "aapanel" };
        factory.Commands.Override = command => Task.FromResult(command.Executable == "/usr/bin/sudo"
            ? new CommandResult(1, Error: "test-only nginx configuration detail") : FakeCommands.Default(command));
        using var client = factory.Client();
        using var login = await ManagerFactory.LoginAsync(client);
        var token = await ManagerFactory.TokenAsync(client, "/Nginx");

        using var noConfirm = await NginxAction(client, token, "reload", "false");
        Assert.Equal(HttpStatusCode.BadRequest, noConfirm.StatusCode);
        Assert.DoesNotContain(factory.Commands.Calls, command => command.Executable == "/usr/bin/sudo");

        using var reload = await NginxAction(client, token, "reload", "true");
        Assert.Equal(HttpStatusCode.Redirect, reload.StatusCode);
        var check = Assert.Single(factory.Commands.Calls, command => command.Executable == "/usr/bin/sudo");
        Assert.Equal(new[] { "-n", "--", "/www/server/nginx/sbin/nginx", "-t" }, check.Arguments);
        var nginx = await client.GetStringAsync("/Nginx");
        Assert.Contains("Nginx configuration validation exited with code 1", nginx);
        Assert.Contains("Last operation result", nginx);
        Assert.DoesNotContain("test-only nginx configuration detail", nginx);
    }

    [Fact]
    public async Task NginxValidatesBeforeReloadingAndReportsTheVersion()
    {
        await using var factory = new ManagerFactory();
        factory.Commands.Override = command => Task.FromResult(command.Executable == "/usr/sbin/nginx"
            ? new CommandResult(0, Error: "nginx version: nginx/1.26.3 (Ubuntu)") : FakeCommands.Default(command));
        using var client = factory.Client();
        using var login = await ManagerFactory.LoginAsync(client);
        var token = await ManagerFactory.TokenAsync(client, "/Nginx");

        using var noToken = await NginxAction(client, "", "restart", "true");
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        using var reload = await NginxAction(client, token, "reload", "true");
        Assert.Equal(HttpStatusCode.Redirect, reload.StatusCode);
        var elevated = factory.Commands.Calls.Where(command => command.Executable == "/usr/bin/sudo").ToArray();
        Assert.Equal(2, elevated.Length);
        Assert.Equal("-t", elevated[0].Arguments[^1]);
        Assert.Equal(new[] { "-n", "--", "/usr/bin/systemctl", "reload", "--job-mode=fail", "--", "nginx.service" }, elevated[1].Arguments);
        var nginx = await client.GetStringAsync("/Nginx");
        Assert.Contains("nginx/1.26.3", nginx);
        Assert.Contains("systemd completed reload", nginx);
    }

    [Fact]
    public async Task SqlServerMonitoringUsesOnlyReadCommands()
    {
        await using var factory = new ManagerFactory();
        factory.Commands.Override = command => Task.FromResult(command.Arguments.LastOrDefault() == ServiceAllowlist.SqlServer
            ? new CommandResult(0, "Id=mssql-server.service\nLoadState=loaded\nActiveState=active\nMemoryCurrent=1073741824\nCPUUsageNSec=120000000000\n")
            : FakeCommands.Default(command));
        using var client = factory.Client();
        using var login = await ManagerFactory.LoginAsync(client);
        var database = await client.GetStringAsync("/Database");
        Assert.Contains("1 GiB", database);
        Assert.Contains("2m 0s", database);
        Assert.Contains(">Running</span>", database);
        Assert.DoesNotContain(factory.Commands.Calls, command => command.Executable == "/usr/bin/sudo");
    }

    private static Task<HttpResponseMessage> NginxAction(HttpClient client, string token, string operation, string confirmed) =>
        client.PostAsync("/Nginx/Operate", ManagerFactory.Form(token, ("operation", operation), ("confirmed", confirmed)));
}

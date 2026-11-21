using System.Net;
using Microsoft.Extensions.DependencyInjection;
using MyServerManager.Security;
using MyServerManager.Services;

namespace MyServerManager.Tests;

public sealed class DiscoveryAndDatabaseTests
{
    [Fact]
    public async Task DiscoverySuggestsAppsAndSavesOnlyAfterReviewAndApproval()
    {
        await using var factory = new ManagerFactory { UsesSsh = true };
        factory.Commands.Override = request => Task.FromResult(request.Executable == "/bin/bash" &&
            request.Arguments[1].Contains("list-unit-files", StringComparison.Ordinal)
            ? new CommandResult(0, string.Join('\0', "0", ManagerFactory.ExtraUnit, "active", "/opt/extra", "Extra.dll", "7200",
                "nginx.service", "active", "", "", "", "")) : FakeCommands.Default(request));
        using var client = factory.Client();
        using var login = await ManagerFactory.LoginAsync(client, FakeSshFactory.Password);
        var token = await ManagerFactory.TokenAsync(client, "/Applications");

        using var rejected = await client.PostAsync("/Settings/ScanApplications", ManagerFactory.Form(""));
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        using var scan = await client.PostAsync("/Settings/ScanApplications", ManagerFactory.Form(token));
        Assert.Equal("/Settings/Discovery", scan.Headers.Location?.OriginalString);
        var results = await client.GetStringAsync("/Settings/Discovery");
        Assert.Contains(ManagerFactory.ExtraUnit, results);
        Assert.Contains("7200", results);
        Assert.DoesNotContain("nginx.service", results);
        var registry = factory.Services.GetRequiredService<ApplicationRegistry>();
        Assert.DoesNotContain(await registry.ReadAsync(), app => app.ServiceName == ManagerFactory.ExtraUnit);

        var review = await client.GetStringAsync("/Settings/ReviewDiscovered?service=" + ManagerFactory.ExtraUnit);
        Assert.Contains("Extra.dll", review);
        Assert.Contains("7200", review);
        using var approved = await client.PostAsync("/Settings/SaveApplication", ManagerFactory.Form(token,
            ("Editor.DisplayName", "Extra application"), ("Editor.ServiceName", ManagerFactory.ExtraUnit),
            ("Editor.Port", "7200"), ("Editor.Enabled", "true")));
        Assert.Equal(HttpStatusCode.Redirect, approved.StatusCode);
        Assert.Contains(await registry.ReadAsync(), app => app.ServiceName == ManagerFactory.ExtraUnit && app.Port == 7200);
        Assert.DoesNotContain(factory.Commands.Calls, request => request.Executable == "/usr/bin/sudo");
    }

    [Fact]
    public async Task DatabaseRestartRequiresConfirmationAndRunsOnlyTheSqlServerCommand()
    {
        await using var factory = new ManagerFactory { UsesSsh = true };
        factory.Commands.Override = request => Task.FromResult(request.Executable == "/usr/bin/systemctl" &&
            request.Arguments[^1] == ServiceAllowlist.SqlServer
            ? new CommandResult(0, "Id=mssql-server.service\nLoadState=loaded\nActiveState=active\nJob=\n")
            : FakeCommands.Default(request));
        using var client = factory.Client();
        using var unauthorized = await client.PostAsync("/Database/Restart", ManagerFactory.Form("", ("confirmed", "true")));
        Assert.Contains("/Account/Login", unauthorized.Headers.Location?.OriginalString);
        using var login = await ManagerFactory.LoginAsync(client, FakeSshFactory.Password);
        var token = await ManagerFactory.TokenAsync(client, "/Database");
        Assert.Contains("Apps that use this database may be temporarily unavailable", await client.GetStringAsync("/Database"));

        using var noToken = await client.PostAsync("/Database/Restart", ManagerFactory.Form("", ("confirmed", "true")));
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        using var unconfirmed = await client.PostAsync("/Database/Restart", ManagerFactory.Form(token));
        Assert.Equal(HttpStatusCode.BadRequest, unconfirmed.StatusCode);
        Assert.DoesNotContain(factory.Commands.Calls, request => request.Executable == "/usr/bin/sudo");

        using var restarted = await client.PostAsync("/Database/Restart", ManagerFactory.Form(token, ("confirmed", "true")));
        Assert.Equal("/Database", restarted.Headers.Location?.OriginalString);
        var command = Assert.Single(factory.Commands.Calls, request => request.Executable == "/usr/bin/sudo");
        Assert.Equal(new[] { "-n", "--", "/usr/bin/systemctl", "restart", "--job-mode=fail", "--", "mssql-server.service" }, command.Arguments);
        Assert.Contains("completed the SQL Server restart", await client.GetStringAsync("/Database"));
        Assert.Contains("RestartSqlServer", await client.GetStringAsync("/Audit"));
    }
}

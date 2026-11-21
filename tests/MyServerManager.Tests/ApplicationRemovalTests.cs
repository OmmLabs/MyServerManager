using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MyServerManager.Security;
using MyServerManager.Services;

namespace MyServerManager.Tests;

public sealed class ApplicationRemovalTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RemovalPersistsAndRevokesControlsWhileKeepingAuditHistory(bool usesSsh)
    {
        await using var factory = new ManagerFactory { UsesSsh = usesSsh };
        using var client = factory.Client();
        using var login = usesSsh
            ? await client.PostAsync("/Account/Login", ManagerFactory.Form(
                await ManagerFactory.TokenAsync(client, "/Account/Login"), ("Password", FakeSshFactory.Password)))
            : await ManagerFactory.LoginAsync(client);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        var token = await ManagerFactory.TokenAsync(client, "/Applications");
        Assert.Contains("Remove", await client.GetStringAsync("/Applications"));

        using var removed = await Remove(client, token, ManagerFactory.Unit);
        Assert.Equal(HttpStatusCode.Redirect, removed.StatusCode);
        Assert.Equal("/Applications", removed.Headers.Location?.OriginalString);
        var registry = factory.Services.GetRequiredService<ApplicationRegistry>();
        Assert.DoesNotContain(await registry.ReadAsync(), entry => entry.ServiceName == ManagerFactory.Unit);
        Assert.Contains(await registry.ReadAsync(), entry => entry.ServiceName == ManagerFactory.ReadOnlyUnit);
        Assert.Contains("Fixture application", await client.GetStringAsync("/Audit?action=RemoveApplication"));
        Assert.DoesNotContain("Fixture application", await client.GetStringAsync("/Applications"));

        using var oldDetails = await client.GetAsync("/Applications/Status?service=" + ManagerFactory.Unit);
        using var control = await client.PostAsync("/Applications/Operate", ManagerFactory.Form(token,
            ("service", ManagerFactory.Unit), ("operation", "restart"), ("confirmed", "true")));
        Assert.Equal(HttpStatusCode.NotFound, oldDetails.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, control.StatusCode);
        Assert.DoesNotContain(factory.Commands.Calls, request => request.Executable == "/usr/bin/sudo");

        using var register = await client.PostAsync("/Settings/SaveApplication", ManagerFactory.Form(token,
            ("Editor.DisplayName", "Registered again"), ("Editor.ServiceName", ManagerFactory.Unit),
            ("Editor.Enabled", "true"), ("Editor.DisplayOrder", "0")));
        Assert.Equal(HttpStatusCode.Redirect, register.StatusCode);
        Assert.Contains("Registered again", await client.GetStringAsync("/Applications"));
    }

    [Fact]
    public async Task RemovalRequiresAuthenticationCsrfConfirmationAndARegisteredUnit()
    {
        await using var factory = new ManagerFactory();
        using var client = factory.Client();
        using var anonymous = await Remove(client, "", ManagerFactory.Unit);
        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        using var login = await ManagerFactory.LoginAsync(client);
        var token = await ManagerFactory.TokenAsync(client, "/Settings");
        using var csrf = await Remove(client, "", ManagerFactory.Unit);
        using var unconfirmed = await Remove(client, token, ManagerFactory.Unit, confirmed: false);
        using var invalid = await Remove(client, token, "app.service; reboot");
        using var missing = await Remove(client, token, ManagerFactory.ExtraUnit);
        using var get = await client.GetAsync("/Settings/RemoveApplication?service=" + ManagerFactory.Unit + "&confirmed=true");
        Assert.Equal(HttpStatusCode.BadRequest, csrf.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unconfirmed.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, get.StatusCode);
        Assert.Equal(3, (await factory.Services.GetRequiredService<ApplicationRegistry>().ReadAsync()).Length);
    }

    [Fact]
    public async Task DisabledAndReadOnlyRegistrationsCanBeRemoved()
    {
        await using var factory = new ManagerFactory();
        using var client = factory.Client();
        using var login = await ManagerFactory.LoginAsync(client);
        var token = await ManagerFactory.TokenAsync(client, "/Settings");
        using var disabled = await Remove(client, token, "disabled.service", source: "settings");
        using var readOnly = await Remove(client, token, ManagerFactory.ReadOnlyUnit, source: "settings");
        Assert.Equal("/Settings", disabled.Headers.Location?.OriginalString);
        Assert.Equal("/Settings", readOnly.Headers.Location?.OriginalString);
        var remaining = await factory.Services.GetRequiredService<ApplicationRegistry>().ReadAsync();
        Assert.Equal(ManagerFactory.Unit, Assert.Single(remaining).ServiceName);
        Assert.DoesNotContain(factory.Commands.Calls, request => request.Executable == "/usr/bin/sudo");
    }

    [Fact]
    public async Task ConflictingOperationBlocksRemovalAndARepeatedRequestIsHarmless()
    {
        await using var factory = new ManagerFactory();
        using var client = factory.Client();
        using var login = await ManagerFactory.LoginAsync(client);
        var token = await ManagerFactory.TokenAsync(client, "/Applications");
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource<CommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        factory.Commands.Override = command =>
        {
            if (command.Executable != "/usr/bin/sudo")
                return Task.FromResult(FakeCommands.Default(command));
            started.TrySetResult();
            return finish.Task;
        };

        var restart = client.PostAsync("/Applications/Operate", ManagerFactory.Form(token,
            ("service", ManagerFactory.Unit), ("operation", "restart"), ("confirmed", "true")));
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            using var blocked = await Remove(client, token, ManagerFactory.Unit).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(HttpStatusCode.Redirect, blocked.StatusCode);
            Assert.Contains(await factory.Services.GetRequiredService<ApplicationRegistry>().ReadAsync(),
                entry => entry.ServiceName == ManagerFactory.Unit);
        }
        finally { finish.TrySetResult(new(0)); }
        using var restarted = await restart;
        using var removed = await Remove(client, token, ManagerFactory.Unit);
        using var repeated = await Remove(client, token, ManagerFactory.Unit);
        Assert.Equal(HttpStatusCode.Redirect, removed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, repeated.StatusCode);
    }

    [Fact]
    public async Task RemovalRequiresAdministratorRole()
    {
        await using var factory = new ManagerFactory();
        using var client = factory.Client(cookies: false);
        var options = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var identity = new ClaimsIdentity([
            new Claim(ClaimTypes.Name, ManagerFactory.Username),
            new Claim(AuthorizationPolicies.SessionStampClaim, factory.Admin.SessionStamp)
        ], CookieAuthenticationDefaults.AuthenticationScheme);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), CookieAuthenticationDefaults.AuthenticationScheme);
        client.DefaultRequestHeaders.Add("Cookie", options.Cookie.Name + "=" + options.TicketDataFormat.Protect(ticket));
        using var forbidden = await Remove(client, "", ManagerFactory.Unit);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(3, (await factory.Services.GetRequiredService<ApplicationRegistry>().ReadAsync()).Length);
    }

    private static Task<HttpResponseMessage> Remove(HttpClient client, string token, string service,
        bool confirmed = true, string source = "applications") =>
        client.PostAsync("/Settings/RemoveApplication", ManagerFactory.Form(token, ("service", service),
            ("confirmed", confirmed ? "true" : "false"), ("source", source)));
}

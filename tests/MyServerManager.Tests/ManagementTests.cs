using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MyServerManager.Security;

namespace MyServerManager.Tests;

public sealed class ManagementTests
{
    [Fact]
    public async Task ControlsRequireAntiforgeryConfirmationAndAnEnabledRegistration()
    {
        await using var factory = new ManagerFactory();
        using var client = factory.Client();
        using var login = await ManagerFactory.LoginAsync(client);
        var token = await ManagerFactory.TokenAsync(client, "/Applications");

        using var missingToken = await Operate(client, "", ManagerFactory.Unit, "start", true);
        using var noConfirmation = await Operate(client, token, ManagerFactory.Unit, "restart", false);
        using var disabled = await Operate(client, token, "disabled.service", "start", true);
        using var unknown = await Operate(client, token, ManagerFactory.ExtraUnit, "start", true);
        using var readOnly = await Operate(client, token, ManagerFactory.ReadOnlyUnit, "start", true);
        Assert.Equal(HttpStatusCode.BadRequest, missingToken.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noConfirmation.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, disabled.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, readOnly.StatusCode);
        Assert.Contains("needs operator approval", await client.GetStringAsync("/Applications"));
        Assert.DoesNotContain(factory.Commands.Calls, IsPrivileged);
    }

    [Fact]
    public async Task ServiceOperationUsesRealExitStatusAndAuditsSanitizedResult()
    {
        await using var factory = new ManagerFactory();
        factory.Commands.Override = command => Task.FromResult(IsPrivileged(command)
            ? new CommandResult(1, Error: "private test configuration") : FakeCommands.Default(command));
        using var client = factory.Client();
        using var login = await ManagerFactory.LoginAsync(client);
        var token = await ManagerFactory.TokenAsync(client, "/Applications");
        using var failed = await Operate(client, token, ManagerFactory.Unit, "restart", true);
        Assert.Equal(HttpStatusCode.Redirect, failed.StatusCode);
        var result = await client.GetStringAsync("/Applications");
        Assert.Contains("systemctl exited with code 1", result);
        Assert.DoesNotContain("private test configuration", result);
        Assert.Single(factory.Commands.Calls, IsPrivileged);

        factory.Commands.Override = null;
        using var succeeded = await Operate(client, token, ManagerFactory.Unit, "stop", true);
        Assert.Equal(HttpStatusCode.Redirect, succeeded.StatusCode);
        Assert.Contains("systemd completed stop", await client.GetStringAsync("/Applications"));
        var audit = await client.GetStringAsync("/Audit?action=Restart");
        Assert.Contains("systemctl exited with code 1", audit);
        Assert.DoesNotContain("systemd completed stop", audit);
        Assert.DoesNotContain("private test configuration", audit);
        Assert.Contains(ManagerFactory.Username, audit);
    }

    [Fact]
    public async Task ConcurrentRequestsCannotIssueConflictingCommands()
    {
        await using var factory = new ManagerFactory();
        using var client = factory.Client();
        using var login = await ManagerFactory.LoginAsync(client);
        var token = await ManagerFactory.TokenAsync(client, "/Applications");
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource<CommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        factory.Commands.Override = command =>
        {
            if (!IsPrivileged(command))
                return Task.FromResult(FakeCommands.Default(command));
            started.TrySetResult();
            return finish.Task;
        };

        var first = Operate(client, token, ManagerFactory.Unit, "restart", true);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            using var blocked = await Operate(client, token, ManagerFactory.Unit, "stop", true).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(HttpStatusCode.Redirect, blocked.StatusCode);
            Assert.Single(factory.Commands.Calls, IsPrivileged);
        }
        finally { finish.TrySetResult(new(0)); }
        using var completed = await first;
        Assert.Equal(HttpStatusCode.Redirect, completed.StatusCode);
        Assert.Contains("Another management operation is in progress", await client.GetStringAsync("/Audit"));
    }

    [Fact]
    public async Task TimeoutIsAuditedAndPendingSystemdJobBlocksTheNextOperation()
    {
        await using var factory = new ManagerFactory();
        factory.Commands.Override = command => Task.FromResult(IsPrivileged(command)
            ? new CommandResult(null, TimedOut: true) : FakeCommands.Default(command));
        using var client = factory.Client();
        using var login = await ManagerFactory.LoginAsync(client);
        var token = await ManagerFactory.TokenAsync(client, "/Applications");
        using var timedOut = await Operate(client, token, ManagerFactory.Unit, "restart", true);
        Assert.Equal(HttpStatusCode.Redirect, timedOut.StatusCode);
        Assert.Contains("systemctl timed out", await client.GetStringAsync("/Applications"));

        factory.Commands.Override = command =>
        {
            var result = FakeCommands.Default(command);
            return Task.FromResult(result with { Output = result.Output.Replace("Job=0", "Job=42") });
        };
        using var pending = await Operate(client, token, ManagerFactory.Unit, "stop", true);
        Assert.Equal(HttpStatusCode.Redirect, pending.StatusCode);
        Assert.Contains("job is already in progress", await client.GetStringAsync("/Applications"));
        Assert.Single(factory.Commands.Calls, IsPrivileged);
    }

    [Fact]
    public async Task RegistrationVerifiesTheExactUnitAndPersistsAnApprovedService()
    {
        await using var factory = new ManagerFactory();
        using var client = factory.Client();
        using var login = await ManagerFactory.LoginAsync(client);
        var token = await ManagerFactory.TokenAsync(client, "/Settings");

        async Task<HttpResponseMessage> Register(string unit, string name) => await client.PostAsync("/Settings/SaveApplication",
            ManagerFactory.Form(token, ("Editor.DisplayName", name), ("Editor.ServiceName", unit), ("Editor.Port", "5206"),
                ("Editor.Enabled", "true"), ("Editor.DisplayOrder", "5")));

        using var missing = await Register("missing.service", "Missing fixture");
        Assert.Equal(HttpStatusCode.Redirect, missing.StatusCode);
        Assert.Contains("did not find a loaded unit", await client.GetStringAsync("/Settings"));

        using var good = await Register(ManagerFactory.ExtraUnit, "New fixture application");
        Assert.Equal(HttpStatusCode.Redirect, good.StatusCode);
        Assert.Contains("New fixture application", await client.GetStringAsync("/Applications"));
        Assert.DoesNotContain(factory.Commands.Calls, IsPrivileged);
    }

    [Fact]
    public async Task SettingsRequiresAdministratorRole()
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
        using var allowed = await client.GetAsync("/");
        using var forbidden = await client.GetAsync("/Settings");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    private static bool IsPrivileged(CommandRequest request) => request.Executable == "/usr/bin/sudo";

    private static Task<HttpResponseMessage> Operate(HttpClient client, string token, string service, string operation, bool confirmed) =>
        client.PostAsync("/Applications/Operate", ManagerFactory.Form(token, ("service", service), ("operation", operation),
            ("confirmed", confirmed ? "true" : "false")));
}

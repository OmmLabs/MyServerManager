using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MyServerManager.Security;

namespace MyServerManager.Tests;

public sealed class AuthenticationTests
{
    [Fact]
    public async Task DashboardAndApplicationsRequireAuthentication()
    {
        await using var factory = new ManagerFactory();
        using var client = factory.Client();
        using var dashboard = await client.GetAsync("/");
        using var applications = await client.GetAsync("/Applications");
        Assert.Equal(HttpStatusCode.Redirect, dashboard.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, applications.StatusCode);
        Assert.Contains("/Account/Login", dashboard.Headers.Location!.OriginalString);
        Assert.DoesNotContain("Fixture application", await dashboard.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task LoginUsesSecureCookieAndRejectsExternalReturnUrl()
    {
        await using var factory = new ManagerFactory();
        using var client = factory.Client();
        using var rejected = await ManagerFactory.LoginAsync(client, "wrong password");
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Contains("username or password was incorrect", await rejected.Content.ReadAsStringAsync());

        using var success = await ManagerFactory.LoginAsync(client, returnUrl: "https://example.com/");
        Assert.Equal(HttpStatusCode.Redirect, success.StatusCode);
        Assert.Equal("/", success.Headers.Location!.OriginalString);
        var cookie = Assert.Single(success.Headers.GetValues("Set-Cookie"), value => value.StartsWith("__Host-MyServerManager="));
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
        var dashboard = await client.GetStringAsync("/");
        Assert.Contains("Fixture application", dashboard);
        Assert.DoesNotContain("Disabled fixture", dashboard);
        var applications = await client.GetStringAsync("/Applications");
        Assert.Contains("1 MiB", applications);
        Assert.Contains("Disabled fixture", applications);
    }

    [Fact]
    public async Task LoginAndLogoutPostsRequireAntiforgery()
    {
        await using var factory = new ManagerFactory();
        using var client = factory.Client();
        using var noToken = await client.PostAsync("/Account/Login", ManagerFactory.Form("", ("Username", ManagerFactory.Username),
            ("Password", ManagerFactory.Password)));
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        using var login = await ManagerFactory.LoginAsync(client);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        using var noLogoutToken = await client.PostAsync("/Account/Logout", null);
        Assert.Equal(HttpStatusCode.BadRequest, noLogoutToken.StatusCode);
        using var logout = await client.PostAsync("/Account/Logout", ManagerFactory.Form(await ManagerFactory.TokenAsync(client, "/")));
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        using var dashboard = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, dashboard.StatusCode);
    }

    [Fact]
    public async Task LoginAttemptsAreRateLimited()
    {
        await using var factory = new ManagerFactory();
        using var client = factory.Client();
        var token = await ManagerFactory.TokenAsync(client, "/Account/Login");
        for (var i = 0; i < 5; i++)
        {
            using var attempt = await client.PostAsync("/Account/Login", ManagerFactory.Form(token,
                ("Username", ManagerFactory.Username), ("Password", "incorrect")));
            Assert.Equal(HttpStatusCode.OK, attempt.StatusCode);
        }
        using var throttled = await client.PostAsync("/Account/Login", ManagerFactory.Form(token,
            ("Username", ManagerFactory.Username), ("Password", ManagerFactory.Password)));
        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
    }

    [Fact]
    public async Task ExpiredSessionReturnsToLogin()
    {
        await using var factory = new ManagerFactory();
        using var client = factory.Client(cookies: false);
        const string scheme = CookieAuthenticationDefaults.AuthenticationScheme;
        var options = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(scheme);
        Assert.Equal(TimeSpan.FromMinutes(30), options.ExpireTimeSpan);
        Assert.False(options.SlidingExpiration);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.Name, ManagerFactory.Username),
            new Claim(ClaimTypes.Role, AuthorizationPolicies.Administrator),
            new Claim(AuthorizationPolicies.SessionStampClaim, factory.Admin.SessionStamp)
        ], scheme));
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(-1) }, scheme);
        client.DefaultRequestHeaders.Add("Cookie", options.Cookie.Name + "=" + options.TicketDataFormat.Protect(ticket));
        using var dashboard = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, dashboard.StatusCode);
        Assert.Contains("/Account/Login", dashboard.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task PasswordChangeInvalidatesOtherSessionsAndKeepsSecretsOutOfAudit()
    {
        await using var factory = new ManagerFactory();
        using var client = factory.Client();
        using var other = factory.Client();
        using var login = await ManagerFactory.LoginAsync(client);
        using var otherLogin = await ManagerFactory.LoginAsync(other);
        const string newPassword = "replacement fixture passphrase";
        var token = await ManagerFactory.TokenAsync(client, "/Settings");
        using var changed = await client.PostAsync("/Settings/ChangePassword", ManagerFactory.Form(token,
            ("CurrentPassword", ManagerFactory.Password), ("NewPassword", newPassword), ("ConfirmPassword", newPassword)));
        Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);
        Assert.Contains("/Account/Login", changed.Headers.Location!.OriginalString);
        using var invalidated = await other.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, invalidated.StatusCode);
        using var rejected = await ManagerFactory.LoginAsync(client);
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        using var accepted = await ManagerFactory.LoginAsync(client, newPassword);
        Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);
        var audit = await client.GetStringAsync("/Audit?action=ChangePassword");
        Assert.Contains("Password changed", audit);
        Assert.DoesNotContain(newPassword, audit);
        Assert.DoesNotContain(ManagerFactory.Password, audit);
    }

    [Fact]
    public async Task HttpsForwardingOnlyTrustsTheLoopbackProxy()
    {
        await using var factory = new ManagerFactory();
        async Task<int> RequestFrom(IPAddress source)
        {
            var result = await factory.Server.SendAsync(context =>
            {
                context.Connection.RemoteIpAddress = source;
                context.Request.Method = "GET";
                context.Request.Path = "/Account/Login";
                context.Request.Host = new Microsoft.AspNetCore.Http.HostString("localhost");
                context.Request.Scheme = "http";
                context.Request.Headers["X-Forwarded-For"] = "198.51.100.30";
                context.Request.Headers["X-Forwarded-Proto"] = "https";
            });
            return result.Response.StatusCode;
        }

        Assert.Equal(400, await RequestFrom(IPAddress.Parse("192.0.2.40")));
        Assert.Equal(200, await RequestFrom(IPAddress.Loopback));
    }
}

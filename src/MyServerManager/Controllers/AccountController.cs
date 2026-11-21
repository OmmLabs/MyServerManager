using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MyServerManager.Configuration;
using MyServerManager.Security;
using MyServerManager.Services;

namespace MyServerManager.Controllers;

public sealed class AccountController(AdministratorStore administrator, ManagerMode mode, ServerProfileStore profiles,
    SshConnection connection, TimeProvider time) : Controller
{
    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl)
    {
        if (mode.UsesSsh && profiles.Current is null)
            return RedirectToAction("Index", "Setup", new { returnUrl });
        return User.Identity?.IsAuthenticated == true ? SignedIn(returnUrl) : View(new LoginInput { ReturnUrl = returnUrl });
    }

    [AllowAnonymous, HttpPost, EnableRateLimiting(AuthorizationPolicies.LoginRateLimit)]
    public async Task<IActionResult> Login([FromForm] LoginInput input, CancellationToken cancellationToken)
    {
        if (mode.UsesSsh && profiles.Current is null)
            return RedirectToAction("Index", "Setup", new { input.ReturnUrl });
        ClaimsPrincipal? principal = null;
        DateTimeOffset? expiresAt = null;
        var error = mode.UsesSsh ? "Enter your Ubuntu password." : "The username or password was incorrect.";
        if (ModelState.IsValid)
        {
            if (mode.UsesSsh)
            {
                expiresAt = time.GetUtcNow() + AuthorizationPolicies.SessionLifetime;
                var signIn = await connection.SignInAsync(input.Password, expiresAt.Value, cancellationToken);
                principal = signIn.Principal;
                error = signIn.Error ?? error;
            }
            else if (!string.IsNullOrWhiteSpace(input.Username))
            {
                principal = await administrator.SignInAsync(input.Username, input.Password, cancellationToken);
            }
        }
        if (principal is null)
        {
            ModelState.Clear();
            ModelState.AddModelError("", error);
            return View(input with { Password = "" });
        }

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = false, ExpiresUtc = expiresAt });
        return SignedIn(input.ReturnUrl);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        if (mode.UsesSsh)
            await connection.DisconnectAsync(User.FindFirstValue(AuthorizationPolicies.SessionStampClaim));
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    private IActionResult SignedIn(string? returnUrl) =>
        Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction("Index", "Dashboard");
}

public sealed record LoginInput
{
    [StringLength(64)] public string? Username { get; init; }
    [Required, StringLength(1024)] public string Password { get; init; } = "";
    [StringLength(2048)] public string? ReturnUrl { get; init; }
}

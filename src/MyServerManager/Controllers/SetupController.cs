using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MyServerManager.Configuration;
using MyServerManager.Security;
using MyServerManager.Services;

namespace MyServerManager.Controllers;

[AllowAnonymous]
public sealed class SetupController(ManagerMode mode, ServerProfileStore profiles, SshConnection connection,
    IDataProtectionProvider protection, TimeProvider time) : Controller
{
    private readonly ITimeLimitedDataProtector _verifiedKey = protection.CreateProtector("MyServerManager.Setup.HostKey.v1")
        .ToTimeLimitedDataProtector();

    [HttpGet]
    public IActionResult Index(string? returnUrl)
    {
        if (!mode.UsesSsh)
            return NotFound();
        return profiles.Current is null ? View(new ServerAddressInput { ReturnUrl = returnUrl }) : RedirectToAction("Login", "Account");
    }

    [HttpPost, EnableRateLimiting(AuthorizationPolicies.LoginRateLimit)]
    public async Task<IActionResult> CheckAddress([FromForm] ServerAddressInput input, CancellationToken cancellationToken)
    {
        if (!mode.UsesSsh)
            return NotFound();
        if (profiles.Current is not null)
            return RedirectToAction("Login", "Account");
        if (!ModelState.IsValid || SshEndpoint.ParseAddress(input.Address) is not { } address)
        {
            ModelState.Clear();
            ModelState.AddModelError("", "Enter your SSH address in the form user@server or user@server:port.");
            return View(nameof(Index), input);
        }

        var key = await connection.ReadHostKeyAsync(address, cancellationToken);
        if (key.Fingerprint is null)
        {
            ModelState.AddModelError("", key.Error ?? "Could not check the server.");
            return View(nameof(Index), input);
        }

        var candidate = address with { Fingerprints = [key.Fingerprint] };
        var token = _verifiedKey.Protect(JsonSerializer.Serialize(candidate), TimeSpan.FromMinutes(10));
        return View("Verify", new VerifyServerPage(candidate, token, input.ReturnUrl));
    }

    [HttpPost, EnableRateLimiting(AuthorizationPolicies.LoginRateLimit)]
    public async Task<IActionResult> Register([FromForm] RegisterServerInput input, CancellationToken cancellationToken)
    {
        if (!mode.UsesSsh)
            return NotFound();
        if (profiles.Current is not null)
            return RedirectToAction("Login", "Account");
        if (ReadCandidate(input.Token) is not { } candidate)
        {
            TempData["Message"] = "Check the server address again to get a fresh SSH key fingerprint.";
            return RedirectToAction(nameof(Index), new { input.ReturnUrl });
        }

        var page = new VerifyServerPage(candidate, input.Token, input.ReturnUrl);
        if (!ModelState.IsValid || !input.Confirmed)
        {
            ModelState.Clear();
            ModelState.AddModelError("", "Confirm the server fingerprint and enter your Ubuntu password.");
            return View("Verify", page);
        }

        var expiresAt = time.GetUtcNow() + AuthorizationPolicies.SessionLifetime;
        var signIn = await connection.RegisterAndSignInAsync(candidate, input.Password, expiresAt, cancellationToken);
        if (signIn.Principal is null)
        {
            ModelState.Clear();
            ModelState.AddModelError("", signIn.Error ?? "Could not register the server.");
            return View("Verify", page);
        }

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, signIn.Principal,
            new AuthenticationProperties { ExpiresUtc = expiresAt, IsPersistent = false });
        return Url.IsLocalUrl(input.ReturnUrl) ? LocalRedirect(input.ReturnUrl) : RedirectToAction("Index", "Dashboard");
    }

    private SshEndpoint? ReadCandidate(string? token)
    {
        if (token is not { Length: > 0 and <= 4096 })
            return null;
        try
        {
            var candidate = JsonSerializer.Deserialize<SshEndpoint>(_verifiedKey.Unprotect(token));
            return candidate?.HasTrustedKeys() == true && candidate.Fingerprints.Length == 1 ? candidate : null;
        }
        catch (Exception error) when (error is CryptographicException or JsonException) { return null; }
    }
}

public sealed record ServerAddressInput
{
    [Required, StringLength(350)] public string Address { get; init; } = "";
    [StringLength(2048)] public string? ReturnUrl { get; init; }
}

public sealed record RegisterServerInput
{
    [Required, StringLength(4096)] public string Token { get; init; } = "";
    [Required, StringLength(1024)] public string Password { get; init; } = "";
    [StringLength(2048)] public string? ReturnUrl { get; init; }
    public bool Confirmed { get; init; }
}

public sealed record VerifyServerPage(SshEndpoint Endpoint, string Token, string? ReturnUrl);

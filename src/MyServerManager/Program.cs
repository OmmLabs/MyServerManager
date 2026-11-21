using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MyServerManager.Configuration;
using MyServerManager.Security;
using MyServerManager.Services;

if (args.FirstOrDefault() == "--verify-registry")
{
    Environment.ExitCode = await RegistryVerification.RunAsync(args);
    return;
}

var setupAdmin = args.FirstOrDefault() == "--set-admin";
var builder = WebApplication.CreateBuilder(setupAdmin ? args[1..] : args);

builder.WebHost.ConfigureKestrel((context, options) =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = 64 * 1024;
    if (new ManagerMode(context.Configuration).UsesSsh && !context.HostingEnvironment.IsEnvironment("Testing"))
    {
        var port = context.Configuration.GetValue("Manager:LocalPort", 7300);
        options.ListenLocalhost(port, listener => listener.UseHttps());
    }
});
builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ManagerMode>();
builder.Services.AddSingleton<ManagerPaths>();
builder.Services.AddSingleton<NginxInstallation>();
builder.Services.AddSingleton<ApplicationRegistry>();
builder.Services.AddSingleton<PreferencesStore>();
builder.Services.AddSingleton<ServiceAllowlist>();
builder.Services.AddSingleton<AdministratorStore>();
builder.Services.AddSingleton<IPasswordHasher<Administrator>, PasswordHasher<Administrator>>();
builder.Services.Configure<PasswordHasherOptions>(options => options.IterationCount = 210_000);
builder.Services.AddSingleton<ServerProfileStore>();
builder.Services.AddSingleton<LinuxHostRevision>();
builder.Services.AddSingleton<ISshSessionFactory, SshNetSessionFactory>();
builder.Services.AddSingleton<SshConnection>();
builder.Services.AddSingleton<ICommandRunner>(services => services.GetRequiredService<ManagerMode>().UsesSsh
    ? services.GetRequiredService<SshConnection>() : new CommandRunner());
builder.Services.AddSingleton<CgroupMetrics>();
builder.Services.AddSingleton<LinuxHostMetrics>();
builder.Services.AddSingleton<SystemdServices>();
builder.Services.AddSingleton<ApplicationReadService>();
builder.Services.AddSingleton<ServerMetrics>();
builder.Services.AddSingleton<JournalService>();
builder.Services.AddSingleton(services => new WebsiteProbe(WebsiteNetwork.CreateHandler(), services.GetRequiredService<TimeProvider>()));
builder.Services.AddSingleton<WebsiteHealthService>();
builder.Services.AddSingleton<InfrastructureMonitor>();
builder.Services.AddSingleton<SqlServerManagement>();
builder.Services.AddSingleton<DotNetDiscovery>();
builder.Services.AddSingleton<NginxManagement>();
builder.Services.AddHostedService<MonitoringWorker>();
builder.Services.AddSingleton<AuditService>();
builder.Services.AddSingleton<OperationCoordinator>();
builder.Services.AddSingleton<ApplicationManagement>();
builder.Services.AddSingleton<SettingsService>();
builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(60));

builder.Services.AddDataProtection().SetApplicationName("MyServerManager");
builder.Services.AddOptions<KeyManagementOptions>().Configure<ManagerPaths, ILoggerFactory>((options, paths, loggers) =>
    options.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(paths.Keys), loggers));
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    options.KnownProxies.Add(IPAddress.Loopback);
    options.ForwardLimit = 1;
    options.RequireHeaderSymmetry = true;
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.Cookie.Name = "__Host-MyServerManager";
    options.Cookie.Path = "/";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.ExpireTimeSpan = AuthorizationPolicies.SessionLifetime;
    options.SlidingExpiration = false;
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
    options.Events.OnValidatePrincipal = async context =>
    {
        var services = context.HttpContext.RequestServices;
        var valid = context.Principal is { } principal && (services.GetRequiredService<ManagerMode>().UsesSsh
            ? services.GetRequiredService<SshConnection>().IsCurrentSession(principal)
            : await services.GetRequiredService<AdministratorStore>().IsCurrentSessionAsync(principal, context.HttpContext.RequestAborted));
        if (!valid)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync();
        }
    };
});
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    options.AddPolicy(AuthorizationPolicies.Administrator, policy => policy.RequireRole(AuthorizationPolicies.Administrator));
});
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "__Host-MyServerManager-CSRF";
    options.Cookie.Path = "/";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});
builder.Services.Configure<CookieTempDataProviderOptions>(options =>
{
    options.Cookie.Name = "__Host-MyServerManager-Message";
    options.Cookie.Path = "/";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        context.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName == AuthorizationPolicies.LoginRateLimit
            ? RateLimitPartition.GetFixedWindowLimiter("login", _ => LoginWindow(30))
            : RateLimitPartition.GetNoLimiter("other"));
    options.AddPolicy(AuthorizationPolicies.LoginRateLimit, context =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => LoginWindow(5)));
    options.OnRejected = (context, token) => new ValueTask(context.HttpContext.Response.WriteAsync(
        "Too many sign-in attempts. Wait one minute and try again.", token));
});

var app = builder.Build();
if ((app.Environment.IsProduction() || setupAdmin) && LinuxIdentity.IsRoot)
    throw new InvalidOperationException("Run Server App Manager as a dedicated, unprivileged user.");

var paths = app.Services.GetRequiredService<ManagerPaths>();
var mode = app.Services.GetRequiredService<ManagerMode>();
if (setupAdmin)
{
    if (mode.UsesSsh)
    {
        Console.WriteLine("Start Server App Manager and sign in in your browser with your Ubuntu password.");
        return;
    }
    if (!await AdministratorSetup.RunAsync(app.Services.GetRequiredService<AdministratorStore>(), app.Services.GetRequiredService<OperationCoordinator>()))
        Environment.ExitCode = 1;
    return;
}

Directory.CreateDirectory(paths.Keys);
if (!mode.UsesSsh)
{
    if (!File.Exists(paths.Administrator))
        throw new InvalidOperationException("Initialize the administrator using the --set-admin command.");
    await app.Services.GetRequiredService<AdministratorStore>().ReadAsync();
}
await app.Services.GetRequiredService<ServerProfileStore>().InitializeAsync();
await app.Services.GetRequiredService<ApplicationRegistry>().InitializeAsync();
await app.Services.GetRequiredService<PreferencesStore>().InitializeAsync();
await app.Services.GetRequiredService<ServiceAllowlist>().ReadApprovedAsync();

if (!mode.UsesSsh)
    app.UseForwardedHeaders();
app.UseExceptionHandler(error => error.Run(async context =>
{
    context.Response.Headers.CacheControl = "no-store";
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsync("Server App Manager could not complete the request. Check the application logs.");
}));
if (app.Environment.IsProduction())
    app.UseHsts();

app.Use(async (context, next) =>
{
    if (mode.UsesSsh && context.Connection.RemoteIpAddress is { } address && !IPAddress.IsLoopback(address))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }
    if (!context.Request.IsHttps)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("HTTPS is required.");
        return;
    }

    context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; img-src 'self' data:; object-src 'none'; " +
        "base-uri 'none'; frame-ancestors 'none'; form-action 'self'";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next(context);
});
app.UseStaticFiles();
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    await next(context);
});
app.UseStatusCodePages("text/plain", "Request returned HTTP {0}.");
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Dashboard}/{action=Index}");
await app.RunAsync();

static FixedWindowRateLimiterOptions LoginWindow(int permits) => new()
{
    PermitLimit = permits,
    Window = TimeSpan.FromMinutes(1),
    QueueLimit = 0
};

public partial class Program;

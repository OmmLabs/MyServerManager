using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyServerManager.Configuration;
using MyServerManager.Models;
using MyServerManager.Security;
using MyServerManager.Services;

namespace MyServerManager.Tests;

internal sealed class ManagerFactory : WebApplicationFactory<Program>
{
    public const string Username = "fixture-admin";
    public const string Password = "unit test fixture passphrase";
    public const string Unit = "test-app.service";
    public const string ReadOnlyUnit = "read-only-app.service";
    public const string ExtraUnit = "extra-app.service";
    public readonly Administrator Admin;
    public Guid ServerId { get; } = Guid.NewGuid();

    public DirectoryInfo State { get; } = Directory.CreateTempSubdirectory("myservermanager-tests-");
    public FakeCommands Commands { get; } = new();
    public FakeWebsiteHandler Websites { get; } = new();
    public FakeSshFactory Ssh { get; }
    public string NginxLayout { get; init; } = "ubuntu";
    public bool UsesSsh { get; init; }

    public ManagerFactory(string? websiteUrl = null, bool provisionAdministrator = true, bool provisionServer = true)
    {
        Ssh = new FakeSshFactory(Commands);
        Directory.CreateDirectory(Path.Combine(State.FullName, "auth"));
        Directory.CreateDirectory(Path.Combine(State.FullName, "config"));
        Admin = new Administrator(Username, "", Guid.NewGuid().ToString("N"));
        Admin = Admin with { PasswordHash = new PasswordHasher<Administrator>().HashPassword(Admin, Password) };
        if (provisionAdministrator)
            File.WriteAllText(Path.Combine(State.FullName, "auth", "administrator.json"), JsonSerializer.Serialize(Admin));
        File.WriteAllLines(Path.Combine(State.FullName, "approved-services"), [Unit, ExtraUnit, "missing.service"]);
        var registry = JsonSerializer.Serialize(new[]
        {
            new ApplicationDefinition { DisplayName = "Fixture application", ServiceName = Unit, Port = 5000, WebsiteUrl = websiteUrl },
            new ApplicationDefinition { DisplayName = "Read-only fixture", ServiceName = ReadOnlyUnit },
            new ApplicationDefinition { DisplayName = "Disabled fixture", ServiceName = "disabled.service", Enabled = false }
        });
        File.WriteAllText(Path.Combine(State.FullName, "config", "applications.json"), registry);
        if (provisionServer)
        {
            var profile = new RegisteredServer(ServerId, new SshEndpoint("fixture-ubuntu.example", 22, "fixture-ssh", [FakeSshFactory.Fingerprint]));
            File.WriteAllText(Path.Combine(State.FullName, "config", "server.json"), JsonSerializer.Serialize(new { server = profile }));
            var serverFolder = Directory.CreateDirectory(Path.Combine(State.FullName, "servers", ServerId.ToString("N")));
            File.WriteAllText(Path.Combine(serverFolder.FullName, "applications.json"), registry);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Manager:DataDirectory"] = State.FullName,
            ["Manager:Transport"] = UsesSsh ? "ssh" : "linux",
            ["Manager:ApprovedServicesPath"] = Path.Combine(State.FullName, "approved-services"),
            ["Manager:EnableNginxControls"] = "true",
            ["Manager:NginxLayout"] = NginxLayout
        }));
        builder.ConfigureTestServices(services =>
        {
            if (UsesSsh)
            {
                services.RemoveAll<ISshSessionFactory>();
                services.AddSingleton<ISshSessionFactory>(Ssh);
            }
            else
            {
                services.RemoveAll<ICommandRunner>();
                services.AddSingleton<ICommandRunner>(Commands);
            }
            services.RemoveAll<WebsiteProbe>();
            services.AddSingleton(_ => new WebsiteProbe(Websites, TimeProvider.System));
        });
    }

    public HttpClient Client(bool cookies = true) => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = cookies
    });

    public static async Task<string> TokenAsync(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]+value=\"([^\"]+)\"");
        Assert.True(match.Success, "Expected an antiforgery field on " + path);
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    public static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string? password = null, string returnUrl = "/")
    {
        var token = await TokenAsync(client, "/Account/Login");
        return await client.PostAsync("/Account/Login", Form(token, ("Username", Username), ("Password", password ?? Password),
            ("ReturnUrl", returnUrl)));
    }

    public static FormUrlEncodedContent Form(string token, params (string Name, string Value)[] fields) =>
        new(fields.Append(("__RequestVerificationToken", token)).Select(field => new KeyValuePair<string, string>(field.Item1, field.Item2)));

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (State.Exists && State.Parent?.FullName == Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) &&
            State.Name.StartsWith("myservermanager-tests-", StringComparison.Ordinal))
            State.Delete(recursive: true);
    }
}

internal sealed class FakeSshFactory(FakeCommands commands) : ISshSessionFactory
{
    public const string Password = "t'sh $7!";
    public const string Fingerprint = "SHA256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    public int KeyChecks { get; private set; }
    public int Attempts { get; private set; }
    public SshEndpoint? Endpoint { get; private set; }
    public byte[]? Credential { get; private set; }
    public string PresentedFingerprint { get; set; } = Fingerprint;

    public Task<string> ReadHostKeyAsync(SshEndpoint endpoint, CancellationToken cancellationToken)
    {
        KeyChecks++;
        return Task.FromResult(PresentedFingerprint);
    }

    public Task<ISshSession> ConnectAsync(SshEndpoint endpoint, byte[] password, CancellationToken cancellationToken)
    {
        Attempts++;
        Endpoint = endpoint;
        Credential = password;
        if (!endpoint.Trusts(PresentedFingerprint["SHA256:".Length..]))
            throw new SshHostKeyRejectedException();
        if (System.Text.Encoding.UTF8.GetString(password) != Password)
            throw new Renci.SshNet.Common.SshAuthenticationException("Invalid fixture password.");
        return Task.FromResult<ISshSession>(new FakeSession(commands));
    }

    private sealed class FakeSession(FakeCommands commands) : ISshSession
    {
        public bool IsConnected { get; private set; } = true;

        public Task<CommandResult> RunAsync(CommandRequest request, ReadOnlyMemory<byte> password, CancellationToken cancellationToken)
        {
            if (request.Executable == "/usr/bin/sudo")
                Assert.Equal(Password, System.Text.Encoding.UTF8.GetString(password.Span));
            else
                Assert.True(password.IsEmpty);
            return commands.RunAsync(request, cancellationToken);
        }

        public void Dispose() => IsConnected = false;
    }
}

internal sealed class FakeWebsiteHandler : HttpMessageHandler
{
    public System.Collections.Concurrent.ConcurrentQueue<(HttpMethod Method, Uri Uri)> Calls { get; } = new();
    public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = _ => new(HttpStatusCode.OK);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls.Enqueue((request.Method, request.RequestUri!));
        return Task.FromResult(Respond(request));
    }
}

internal sealed class FakeCommands : ICommandRunner
{
    public System.Collections.Concurrent.ConcurrentQueue<CommandRequest> Calls { get; } = new();
    public Func<CommandRequest, Task<CommandResult>>? Override { get; set; }

    public Task<CommandResult> RunAsync(CommandRequest command, CancellationToken cancellationToken = default)
    {
        Calls.Enqueue(command);
        return Override?.Invoke(command) ?? Task.FromResult(Default(command));
    }

    public static CommandResult Default(CommandRequest command)
    {
        if (command.Executable != "/usr/bin/systemctl" || command.Arguments[0] != "show")
            return new(0);

        var rows = command.Arguments.SkipWhile(arg => arg != "--").Skip(1).Select(unit => unit switch
        {
            ManagerFactory.Unit or ManagerFactory.ReadOnlyUnit or ManagerFactory.ExtraUnit or ServiceAllowlist.Nginx =>
                $"Id={unit}\nLoadState=loaded\nActiveState=active\nMainPID=12345\nMemoryCurrent=1048576\nJob=0\n",
            _ => $"Id={unit}\nLoadState=not-found\nActiveState=inactive\n"
        });
        return new(0, string.Join('\n', rows));
    }
}

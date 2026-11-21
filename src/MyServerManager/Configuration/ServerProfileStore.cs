namespace MyServerManager.Configuration;

public sealed record RegisteredServer(Guid Id, SshEndpoint Endpoint);

public sealed class ServerProfileStore(ManagerPaths paths, ManagerMode mode)
{
    private sealed record State(RegisteredServer? Server);
    private RegisteredServer? _current;

    public RegisteredServer? Current => Volatile.Read(ref _current);
    public string? RegistryPath => mode.UsesSsh ? Current is { } profile ? paths.ServerRegistry(profile.Id) : null : paths.Registry;
    public string? AuditDirectory => mode.UsesSsh ? Current is { } profile ? paths.ServerAudit(profile.Id) : null : paths.Audit;

    public async Task InitializeAsync()
    {
        if (!mode.UsesSsh || !File.Exists(paths.ServerProfile))
            return;

        var state = await AtomicJson.ReadAsync<State>(paths.ServerProfile);
        if (state.Server is { } server && (server.Id == Guid.Empty || server.Endpoint?.HasTrustedKeys() != true))
            throw new InvalidDataException("The saved server profile is invalid.");
        Volatile.Write(ref _current, state.Server);
    }

    internal async Task SaveAsync(RegisteredServer? server)
    {
        await AtomicJson.WriteAsync(paths.ServerProfile, new State(server));
        Volatile.Write(ref _current, server);
    }
}

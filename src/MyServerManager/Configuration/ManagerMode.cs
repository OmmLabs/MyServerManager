namespace MyServerManager.Configuration;

public sealed class ManagerMode
{
    public ManagerMode(IConfiguration configuration) : this(configuration["Manager:Transport"]?.ToLowerInvariant() switch
    {
        null or "ssh" => true,
        "linux" => false,
        _ => throw new InvalidOperationException("Manager:Transport must be ssh or linux.")
    }) { }

    internal ManagerMode(bool usesSsh) => UsesSsh = usesSsh;

    public bool UsesSsh { get; }
}

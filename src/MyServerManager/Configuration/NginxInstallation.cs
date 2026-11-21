using MyServerManager.Security;

namespace MyServerManager.Configuration;

public sealed class NginxInstallation(IConfiguration configuration)
{
    public NginxLayout Layout { get; } = configuration["Manager:NginxLayout"]?.ToLowerInvariant() switch
    {
        null or "ubuntu" => NginxLayout.Ubuntu,
        "aapanel" => NginxLayout.AaPanel,
        _ => throw new InvalidOperationException("Manager:NginxLayout must be ubuntu or aapanel.")
    };

    public bool ControlsEnabled { get; } = configuration.GetValue<bool>("Manager:EnableNginxControls");
}

using MyServerManager.Models;
using MyServerManager.Security;
using MyServerManager.Services;

namespace MyServerManager.Tests;

public sealed class FoundationTests
{
    [Theory]
    [InlineData("site.service", true)]
    [InlineData("Site-blue@instance_1.service", true)]
    [InlineData("site@.service", false)]
    [InlineData("--root=x.service", false)]
    [InlineData("site*.service", false)]
    [InlineData("site.service\n", false)]
    [InlineData("../site.service", false)]
    [InlineData("x.service; reboot", false)]
    [InlineData("myservermanager.service", false)]
    [InlineData("mssql-server.service", false)]
    [InlineData("nginx.service", false)]
    public void ApplicationServiceNamesAreStrict(string unit, bool allowed) =>
        Assert.Equal(allowed, ServiceAllowlist.IsApplicationUnit(unit));

    [Fact]
    public void CommandBuildersUseLiteralArguments()
    {
        var command = LinuxCommands.ControlApplication("site.service", ServiceAction.Restart);
        Assert.Equal("/usr/bin/sudo", command.Executable);
        Assert.Equal(new[] { "-n", "--", "/usr/bin/systemctl", "restart", "--job-mode=fail", "--", "site.service" }, command.Arguments);
        Assert.Throws<ArgumentException>(() => LinuxCommands.Show("--version"));
        Assert.Throws<ArgumentException>(() => LinuxCommands.ControlApplication("site*.service", ServiceAction.Start));
        Assert.Throws<ArgumentOutOfRangeException>(() => LinuxCommands.ControlApplication("site.service", (ServiceAction)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => LinuxCommands.Journal("site.service", 1000));
        Assert.Equal("--unit=site.service", LinuxCommands.Journal("site.service", 500).Arguments[^2]);
    }

    [Fact]
    public void RegistryValidatesFieldsBeforePersistence()
    {
        var entry = new ApplicationDefinition { DisplayName = "Site", ServiceName = "site.service", Port = 443,
            WebsiteUrl = "https://example.com/health", AssemblyName = "Site.dll", DeploymentDirectory = "/opt/site" };
        Assert.Null(ApplicationRegistry.Validate(entry));
        Assert.NotNull(ApplicationRegistry.Validate(entry with { DisplayName = " " }));
        Assert.NotNull(ApplicationRegistry.Validate(entry with { WebsiteUrl = "file:///etc/passwd" }));
        Assert.NotNull(ApplicationRegistry.Validate(entry with { WebsiteUrl = "https://admin:secret@example.com/" }));
        Assert.NotNull(ApplicationRegistry.Validate(entry with { Port = 65536 }));
        Assert.NotNull(ApplicationRegistry.Validate(entry with { AssemblyName = "../Site.dll" }));
        Assert.NotNull(ApplicationRegistry.Validate(entry with { DeploymentDirectory = "/opt/../site" }));
    }
}

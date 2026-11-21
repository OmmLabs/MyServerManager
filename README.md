# Server App Manager

Server App Manager is a .NET 10 dashboard that runs on your computer and manages one Ubuntu server over SSH. It uses the server's existing SSH account, systemd, journal and Nginx. The dashboard and its state stay on your computer.

Sponsored by [Omm Rudraksha](https://ommrudraksha.com/).

[Source](https://github.com/OmmLabs/MyServerManager) · [Issues](https://github.com/OmmLabs/MyServerManager/issues) · [Releases](https://github.com/OmmLabs/MyServerManager/releases) · [MIT License](LICENSE).

- **Dashboard and Applications** show remote service status, PID, cgroup memory and peak, accumulated CPU time, configured port and service controls. The service detail view also shows process and task counts.
- **Server Monitoring** samples CPU, load, RAM, swap, local disks, uptime and host information. The dashboard refreshes its metric sections in place.
- **Website Health and Logs** show HTTP results from your computer and searchable, bounded systemd journal entries from Ubuntu.
- **Nginx and Database** monitor the proxy and Microsoft SQL Server. The server operator can enable validated Nginx controls for a verified installation. The SSH mode can restart SQL Server after confirmation.
- **Audit History and Settings** cover management results, application registration, polling intervals, the light or dark theme, and the administrator password.

## Run locally

Get the source from [OmmLabs/MyServerManager](https://github.com/OmmLabs/MyServerManager). This is a source preview; Windows installers and a tested stable release are planned.

On Windows, double-click **Create-Desktop-Shortcut.cmd** once. It builds the local app and creates **Server App Manager** on your desktop. Double-click that shortcut to start the dashboard and open your browser. Use the tray icon to reopen the dashboard or choose **Quit Server App Manager** when finished. The setup uses the .NET 10 SDK and checks your local HTTPS certificate.

The shortcut reuses your existing local settings. Run **Create-Desktop-Shortcut.cmd** again after updating the source to install the updated build.

To start directly from PowerShell, run this from the repository root.

~~~sh
dotnet run --project src/MyServerManager
~~~

Open the dashboard. On first run, enter your SSH address, such as `ubuntu@server.example.com` or `ubuntu@server.example.com:2222`. Compare the displayed SSH key fingerprint with the one you trust for your server, then confirm it and enter your Ubuntu password. The app saves the server after a successful sign-in. On later visits, enter your Ubuntu password. [Open the dashboard](https://localhost:7300).

On a new PC, run `dotnet dev-certs https --trust` once before starting the dashboard.

Use **Applications → Find apps** to scan systemd for .NET services. Pick **Review and add** to check the name, unit and any detected listening port. Add a website address if applicable, then save the application. The scan checks direct `dotnet` launches and executables with a neighboring `.runtimeconfig.json` file. You can still add services by name.

Use **Database → Restart SQL Server** if your server has the `mssql-server.service` unit. Confirm the restart when you are ready for dependent applications to briefly lose database access.

Service controls and journal operations use the SSH account's existing sudo permissions. When sudo prompts, the app supplies the SSH password through standard input. The password stays in memory for the session. Nginx controls are opt-in through the local `Manager:EnableNginxControls` setting and use the verified `Manager:NginxLayout` value of `ubuntu` or `aapanel`.

To remove a saved server, sign in and choose **Settings → Server settings → Remove server**. This signs you out and archives the app list and audit history for that registration. Local source runs store the saved server and its data under the ignored `src/MyServerManager/.manager-state` directory. The server profile is `config/server.json` within that directory. If the server becomes permanently unreachable, the owner of this computer can stop the dashboard and move that file aside to return to setup.

Use **Remove** beside an application in **Applications** or **Settings → Registered applications** to remove its dashboard registration. Confirm the application name before saving the change. You can register that service again later. Audit History records the removal.

## Build and test

The test suite uses ASP.NET Core TestServer, temporary state directories, and simulated system commands, SSH sessions and HTTP probes. It covers the local SSH flow and the dashboard features without acting on production services.

~~~sh
dotnet build ServerAppManager.slnx
dotnet test ServerAppManager.slnx
dotnet publish src/MyServerManager/ServerAppManager.csproj -c Release -p:UseAppHost=false -o artifacts/publish
~~~

The published app uses the .NET 10 ASP.NET Core runtime and SSH.NET. Run `dotnet MyServerManager.dll` from the publish directory with a local HTTPS certificate available to Kestrel. Published local state defaults to the current user's local application data folder under `MyServerManager`.

Existing installations of My Server Manager use the same local state path, assembly name and browser data-protection settings after the name change. Your existing source-run command still works.

## Project website and release

The website lives in `website/`. It builds to static pages and has its own local preview instructions. See the [website README](website/README.md) and [release plan](release-plan/README.md).

## Security and direct hosting

The local SSH mode is the default. Direct Ubuntu hosting is a separate, optional mode documented in the deployment guide. [Direct Ubuntu deployment and rollback](deploy/README.md).

The security guide covers local credential handling, host-key verification, the privilege boundary and journal redaction. [Security and operations](SECURITY.md).

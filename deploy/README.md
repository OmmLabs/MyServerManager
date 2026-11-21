# Ubuntu deployment and rollback

This guide applies to optional direct Ubuntu hosting. The default local SSH mode runs the dashboard on your computer and uses the existing SSH account. [Local setup](../README.md#run-locally).

## Discover the server

Run discovery on the target Ubuntu host. The script reads unit metadata, listening sockets, system resources, cgroup mounts, installed Nginx layouts and candidate vhost paths. Pass the canonical application unit names you want to inspect.

~~~bash
sudo bash deploy/discover.sh example-web.service example-worker.service
~~~

Review each unit and its drop-ins privately on the host with `sudo systemctl cat UNIT`. Verify the canonical unit ID, runtime account, working directory, DLL and actual listening port. Match the listener to the process and review the corresponding Nginx `server_name` and upstream. Inspect the selected Nginx instance's effective configuration path, include directories, service reload behavior and binary. Unit files and Nginx configuration can contain credentials. Keep the full contents on the server.

Copy the empty seed registry to `reviewed-applications.json` and add the verified services. Each entry needs `displayName` and `serviceName`. Optional metadata includes `deploymentDirectory`, `assemblyName`, `port`, `websiteUrl`, `environment`, `description`, `enabled` and `displayOrder`. Set `"enabled": false` for an entry that should remain registered while discovery is pending. [Seed registry](../src/MyServerManager/config/applications.json).

Review the privilege rules before approving a unit. Use a DNS hostname reserved for this dashboard and obtain a valid certificate for it through your existing certificate process. Verify the .NET 10 ASP.NET Core runtime, systemd, sudo and curl are present. [Security notes](../SECURITY.md), [Microsoft .NET installation guidance for Ubuntu](https://learn.microsoft.com/dotnet/core/install/linux-ubuntu).

## Build and install the manager

Build and test on the development machine, then copy the publish output, this `deploy` directory and the reviewed registry to a trusted staging directory on the server.

~~~bash
dotnet build ServerAppManager.slnx -c Release
dotnet test ServerAppManager.slnx -c Release
dotnet publish src/MyServerManager/ServerAppManager.csproj -c Release -p:UseAppHost=false -o artifacts/publish
~~~

The installer takes the publish directory, reviewed registry, dashboard hostname, release ID, Nginx layout, Nginx mode and an optional list of approved application units. The layout is `ubuntu` for `/usr/sbin/nginx` or `aapanel` for `/www/server/nginx/sbin/nginx`. Use `manage` only after confirming the selected binary's default configuration is the one loaded by `nginx.service` and the unit supports reload. Use `monitor` for deployments with a custom configuration invocation or service layout that still needs review. [Nginx command switches](https://nginx.org/en/docs/switches.html), [aaPanel service paths](https://www.aapanel.com/docs/faq/Service_Manage_Commands.html).

For example, after verifying both application units and the Ubuntu Nginx layout, run the installer from the server's staging directory.

~~~bash
sudo bash deploy/install.sh ./artifacts/publish ./reviewed-applications.json \
  servermanager.example.com 1.0.0 ubuntu monitor \
  example-web.service example-worker.service
~~~

Enter the administrator username and a unique 15–128 character passphrase at the terminal prompt. The installer verifies the enabled registry entries through systemd as the dedicated account. It validates the generated sudo policy with `visudo -c`, checks command matching with `sudo -l`, and enables and starts `myservermanager.service`. Its readiness check calls the manager's login route over loopback. Proxy activation is a separate step.

| Path | Purpose |
|---|---|
| `/opt/myservermanager/releases/<release>/` | Root-owned published files |
| `/opt/myservermanager/current` | Root-owned symlink to the selected release |
| `/etc/systemd/system/myservermanager.service` | Dedicated unit binding to `127.0.0.1:5300` |
| `/etc/myservermanager/manager.env` | Root-owned host, state and proxy settings |
| `/etc/myservermanager/approved-services` | Root-owned canonical unit approvals |
| `/etc/sudoers.d/myservermanager` | Generated literal command rules with mode `0440` |
| `/var/lib/myservermanager/config/` | Editable `applications.json` and dashboard preferences |
| `/var/lib/myservermanager/auth/` and `/var/lib/myservermanager/keys/` | Administrator hash and cookie protection keys |
| `/var/lib/myservermanager/audit/` | Daily UTC audit files |

The installer refuses existing manager paths, an existing service user or an occupied port. If it stops after creating artifacts, inspect `sudo journalctl -u myservermanager.service --no-pager -n 50` and correct only the manager artifacts. The rollback procedure below also applies to a partial install. Archive the manager paths before retrying an initial install. If the failed installer created the dedicated account and group, remove those account entries only after stopping the manager and archiving its files. Keep the state archive private and retain its numeric ownership for recovery.

## Activate the separate HTTPS vhost

Make a staging copy of the Nginx template. Set both `server_name` values, the redirect hostname and both certificate paths. Adjust the standalone listener directives to match the verified Nginx installation. The template sends one client address and one scheme from the local proxy to Kestrel. [Nginx template](myservermanager.nginx.conf), [ASP.NET Core forwarded headers](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0).

For an Ubuntu Nginx installation that includes `sites-enabled/*`, stage the new site under an unused manager-only filename.

~~~bash
sudo test ! -e /etc/nginx/sites-available/myservermanager.conf &&
sudo test ! -L /etc/nginx/sites-available/myservermanager.conf &&
sudo test ! -e /etc/nginx/sites-enabled/myservermanager.conf &&
sudo test ! -L /etc/nginx/sites-enabled/myservermanager.conf &&
sudo install -o root -g root -m 0644 ./myservermanager.nginx.reviewed.conf \
  /etc/nginx/sites-available/myservermanager.conf &&
sudo ln -s /etc/nginx/sites-available/myservermanager.conf /etc/nginx/sites-enabled/myservermanager.conf
~~~

For aaPanel, verify its loaded include directory and stage the template under an unused `myservermanager.conf` there. The typical path is `/www/server/panel/vhost/nginx/myservermanager.conf`. Use the existing include mechanism and the corresponding Nginx binary.

Validate the selected instance and arrange a graceful reload with the server operator. On a verified Ubuntu Nginx unit, the activation commands are

~~~bash
sudo /usr/sbin/nginx -t && sudo systemctl reload nginx.service
~~~

With aaPanel, use `/www/server/nginx/sbin/nginx -t` and its verified reload mechanism after the test passes. If validation fails, move the new vhost or symlink out of the include directory, then review it in staging.

## Verify on the target

~~~bash
sudo systemctl status myservermanager.service --no-pager
sudo visudo -c
sudo /usr/bin/sudo -l -U myservermanager
curl -sS -D - -o /dev/null https://servermanager.example.com/
~~~

The HTTPS request should redirect to the sign-in page. In the browser, check the application list, status, process and memory metrics, logs, website health and audit history. Compare application memory against the registered unit's `ControlGroup` and its `memory.current` file on the discovered cgroup v2 mount. Compare available RAM to `MemAvailable` in `/proc/meminfo`. For Nginx management, verify the Check configuration action and its audit result. Test start, stop and restart against a disposable unit in a non-production environment.

## Add or edit applications

For a new unit, verify its configuration on the host. Run `approve-services.sh` with the **complete intended approval list**, including any existing approvals to retain. The script checks each canonical unit and installs a new sudo policy and approval file. Then register the unit in **Settings**. Disabling an application removes it from live monitoring, log reads and controls while retaining its registry entry.

~~~bash
bash deploy/approve-services.sh --print ubuntu monitor example-web.service
sudo bash deploy/approve-services.sh example-web.service example-worker.service another-verified.service
~~~

For a manual registry edit, stop only `myservermanager.service`, write a replacement JSON file beside the live registry with mode `0600` and ownership `myservermanager:myservermanager`, and verify it before replacing the live file.

~~~bash
sudo -u myservermanager -- /usr/bin/dotnet /opt/myservermanager/current/MyServerManager.dll \
  --verify-registry /var/lib/myservermanager/config/applications.json.new &&
sudo mv -T /var/lib/myservermanager/config/applications.json.new /var/lib/myservermanager/config/applications.json &&
sudo systemctl start myservermanager.service
~~~

Use the web Settings page for changes that should appear in the dashboard audit history. To recover administrator access from a trusted terminal, stop the manager, change to `/opt/myservermanager/current`, run the command below, then start the manager. It replaces the stored hash and invalidates previous sessions.

~~~bash
sudo -u myservermanager -- env DOTNET_ENVIRONMENT=Production ASPNETCORE_ENVIRONMENT=Production Manager__Transport=linux \
  Manager__DataDirectory=/var/lib/myservermanager Manager__ApprovedServicesPath=/etc/myservermanager/approved-services \
  /usr/bin/dotnet MyServerManager.dll --set-admin
~~~

## Roll back

**Switch a release.** Stage new releases as root-owned, non-writable directories under `/opt/myservermanager/releases/`. Keep a private backup of the state directory before an upgrade. Switch the `current` symlink to a staged release, then restart only the manager and verify its login. To roll back, use the same sequence with the preceding release directory. Review data-format compatibility before using an older release.

~~~bash
sudo ln -s /opt/myservermanager/releases/1.0.0 /opt/myservermanager/current.next &&
sudo mv -T /opt/myservermanager/current.next /opt/myservermanager/current &&
sudo systemctl restart myservermanager.service
~~~

**Withdraw the installation.** Create a root-only archive directory outside Nginx and sudo include directories. Move only the manager vhost entry out of the active Nginx include path. On Ubuntu this is the symlink in `sites-enabled`. Validate Nginx and arrange a graceful reload as in activation. If validation fails, restore the moved manager entry and investigate before reloading.

Disable and stop the manager. Move its systemd unit and sudoers file to the archive. Reload systemd unit definitions and validate sudoers. For example, with a fresh archive directory named `/root/myservermanager-rollback` and a fully installed manager:

~~~bash
sudo install -d -m 0700 /root/myservermanager-rollback &&
sudo systemctl disable --now myservermanager.service &&
sudo mv /etc/systemd/system/myservermanager.service /root/myservermanager-rollback/ &&
sudo mv /etc/sudoers.d/myservermanager /root/myservermanager-rollback/myservermanager.sudoers &&
sudo systemctl daemon-reload &&
sudo visudo -c
~~~

For a partial install, apply only the steps for the manager artifacts that exist. Retain the state directory, operator configuration, releases and dedicated account for recovery. If a fresh install is required, first archive those manager-only paths and confirm the account is unused.

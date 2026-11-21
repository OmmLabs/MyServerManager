# Security and operations

## Reporting a vulnerability

Use **Security → Report a vulnerability** in [OmmLabs/MyServerManager](https://github.com/OmmLabs/MyServerManager/security) when private reporting is enabled. If it is unavailable, open a public issue asking for a private reporting channel without including vulnerability details. Include the affected version, impact and a safe way to reproduce the issue using sample hosts and sanitized output in the private report.

## Sign-in and sessions

Authentication uses a 30-minute fixed session with an HttpOnly, Secure, SameSite=Strict cookie. The app validates antiforgery tokens on unsafe MVC methods, including login and logout. Login POSTs are limited to five per minute per client address and 30 per minute overall. Dynamic responses use `no-store`, and the content security policy confines scripts and forms to the dashboard origin.

## Local SSH mode

The dashboard listens on HTTPS loopback. First-run setup reads the server's SSH key without attempting password authentication. The operator compares its SHA-256 fingerprint with a trusted value and confirms it. The SSH client verifies the confirmed key again before sending the password. Successful Ubuntu password authentication saves the registration and opens the dashboard. Subsequent sign-ins verify the saved key. [SSH.NET host verification](https://sshnet.github.io/SSH.NET/examples.html).

The SSH password is held in memory for the connection. The application clears its credential buffer when the connection closes, including on sign-out and dashboard session expiration. A successful new sign-in replaces the previous session. Commands use fixed executables and quoted, validated arguments. For privileged commands, the client sends the password to sudo on standard input only when it sees the corresponding prompt. The SSH account's existing sudo policy determines which actions succeed. Registry and audit state stay on the computer running the app.

Application controls require an enabled local registry entry and a loaded canonical systemd unit. The discovery scan returns filtered systemd metadata and visible listening ports. The full launch commands stay on the server. Adding a discovery result requires an administrator's review and save. Cgroup, `/proc`, disk and journal samples come through SSH. HTTP health checks run from the local computer. Nginx controls require local opt-in and a verified layout. The SQL Server restart uses a fixed unit name, requires confirmation and records the command result in Audit History.

Server removal requires an active administrator session, an antiforgery token and confirmation. It clears the saved endpoint and password buffer and closes the SSH connection. Each registration has its own app registry and audit directory. An old browser session cannot issue commands through a newly registered server.

## Direct Ubuntu hosting

The direct mode uses a local administrator created with an interactive terminal prompt. Its credential store holds an ASP.NET Core Identity PBKDF2 password hash and a session stamp. Password changes rotate the stamp and invalidate previous sessions.

The optional direct mode accepts one forwarded address and scheme from the IPv4 loopback proxy. Its dedicated Nginx vhost terminates TLS and sets the forwarded headers. For a deployment limited to a VPN or administrator network, apply that policy to the manager vhost during activation. [ASP.NET Core proxy security guidance](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0).

The runtime account owns its private state directory. Published files, unit files, Nginx configuration and operator approval files are owned by the server operator. The systemd unit permits the setuid transition used by sudo. Install with a dedicated account and keep it out of privileged groups such as `sudo` and `systemd-journal`.

An application control requires an enabled registry entry, a root-approved canonical unit and a matching literal sudo rule. The server operator reviews the unit, its aliases, executable and configuration ownership, and dependencies before granting access. Starting or stopping a unit can affect dependencies according to its existing systemd configuration. Nginx controls likewise depend on a verified binary and a trusted configuration, module and include tree. Keep all those inputs outside the manager user's writable paths.

Commands use fixed executables, typed operations and literal argument lists. The sudo policy grants only specific unit operations and three bounded journal queries per approved unit, plus the optional Nginx commands. Systemd mutations use `--job-mode=fail` and inspect pending jobs. The app serializes management changes and records a durable audit request before performing them. Timeouts bound the client commands. A systemd job may continue after its client times out, and a subsequent operation checks job state again. [Ubuntu sudoers argument matching](https://manpages.ubuntu.com/manpages/jammy/man5/sudoers.5.html), [systemctl](https://www.freedesktop.org/software/systemd/man/latest/systemctl).

## Metrics, network checks and logs

Application memory is sampled from the service's cgroup v2 when readable, then from systemd accounting. Cgroup memory includes the service's descendants and may include page cache. Host memory comes from `/proc/meminfo`. CPU usage uses successive `/proc/stat` samples. Process counts are bounded and may be unavailable for large or unreadable cgroups. [Linux cgroup v2](https://docs.kernel.org/admin-guide/cgroup-v2.html), [Linux procfs](https://docs.kernel.org/6.12/filesystems/proc.html).

Website checks accept HTTP and HTTPS, enforce a five-second deadline and follow at most three validated redirects. The connection callback filters DNS results and connects directly to the permitted IP addresses. Private, loopback, link-local and other reserved destinations are blocked. Registry URLs exclude credentials, query strings and fragments. TLS certificate validation uses the system trust store and the certificate chain presented by the endpoint, with automatic certificate downloads disabled. The page reports HTTP availability from this server. [Socket connection callbacks](https://learn.microsoft.com/dotnet/api/system.net.http.socketshttphandler.connectcallback), [IANA address registries](https://www.iana.org/numbers/registries).

Journal reads use structured timestamps and priorities. The UI encodes messages as text and suppresses entries with recognizable secret patterns. Redaction is pattern-based. Approve journal access only for applications whose logging policy excludes sensitive values, and check that policy before enabling a new unit. Use the server operator's journal access for longer investigations. [journalctl output fields](https://www.freedesktop.org/software/systemd/man/latest/journalctl).

## State and audit

The app creates private JSON state and filesystem-protected Data Protection keys in its configured state directory. For direct Ubuntu hosting that directory is `/var/lib/myservermanager`. Back it up securely and restrict access to credential hashes, key material and audit records. Atomic file replacement protects registry, preference and credential updates. Coordinate manual edits with the app stopped to avoid overwriting concurrent UI changes.

Audit entries contain the UTC timestamp, target, unit, operation, administrator and sanitized result. The history view shows the latest 250 matching operations. Keep a retention and backup policy for the daily audit files. They are writable by the dedicated service account, so an external copy is needed for an independently retained record.

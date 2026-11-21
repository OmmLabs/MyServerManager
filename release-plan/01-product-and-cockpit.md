# Product scope and the Cockpit benchmark

**Server App Manager helps developers see and operate the apps they already run on Ubuntu from their own PC.** A typical task is to find an ASP.NET Core service, check its memory and logs, restart it if needed, and verify its website in the same dashboard.

```text
Public site → documentation and GitHub download
                         ↓
Browser on your PC → local dashboard → SSH → existing Ubuntu tools
                                         └── systemd, journal, /proc and cgroups
Local dashboard → website URL for HTTP health checks
```

An Ubuntu SSH login must work from the PC. A reachable host, a trusted host-key fingerprint and suitable permissions are part of onboarding. All Server App Manager software runs on the PC. On Ubuntu, it uses the existing SSH service and system tools.

## What to learn from Cockpit

Cockpit’s site quickly shows the product in use, connects installation to documentation, and gives contributors a clear route into the code. Its project emphasizes familiar system tools, discoverability and testing. Adopt those habits and judge comparable quality on the shared day-to-day tasks. [Cockpit homepage](https://cockpit-project.org/) · [Project ideals](https://cockpit-project.org/ideals.html).

| Workflow | Cockpit | Server App Manager v1 target |
| --- | --- | --- |
| Getting started | Typical setup uses Cockpit packages and a systemd socket. Its remote-host workflow uses Cockpit’s bridge. | Start on Windows, open the local browser, enter `user@host[:port]`, verify the fingerprint, and sign in over existing SSH. |
| Service work | Inspect and interact with systemd services. | Review discovered .NET services or register a verified unit manually. Inspect, start, stop and restart with clear outcome and audit. |
| Diagnostics | System journal and broad host performance views. | Per-unit journal and cgroup memory with server CPU, RAM and disk alongside independent website health. |
| Other tools | Storage, networks, containers, VMs, accounts and extensions. | Opt-in Nginx maintenance and confirmed SQL Server restart. Core app operations stay the focus of v1. |
| Supported environments | Multiple Linux distributions and broad system administration. | One Ubuntu target from one Windows PC. Expand the support matrix once testing and maintenance capacity exist. |

Sources for the comparison. [Cockpit installation](https://cockpit-project.org/running.html) · [Cockpit startup](https://cockpit-project.org/guide/latest/startup.html) · [Cockpit host bridge](https://docs.cockpit-project.org/cockpit-guide/main/man/cockpit-bridge.1.html) · [Application catalog](https://cockpit-project.org/applications.html).

## In the workspace today

| Area | Existing implementation |
| --- | --- |
| Connection | Local HTTPS on port 7300 by default. First run takes the SSH address in the browser. The operator confirms a trusted fingerprint before password authentication. The registration persists locally. Sign-in requires a password again. |
| Removal | **Settings → Server settings → Remove server** requires sign-in and confirmation. It ends the connection and archives the app registry and audit history for that registration. Document archive and erasure steps separately. |
| Discovery | **Applications → Find apps** scans systemd for direct `dotnet` launches and executables with a neighboring `.runtimeconfig.json`. The operator reviews candidates, including any visible port, and saves the ones they want. Website URLs are entered during review. Manual registration covers additional valid units. |
| App control | Start, stop, restart, status, cgroup memory and peak, CPU time, PID, process and task counts. Stop and restart require confirmation. Actions return command outcomes and appear in Audit History. |
| Monitoring | Server CPU sampled over time, load, RAM, swap, disks and uptime. Website probes run from the PC and report public HTTP health separately from the service. |
| Logs | Search and priority filtering for bounded per-unit journal entries. The UI redacts recognizable secret patterns. Review log sensitivity before enabling journal access. |
| Infrastructure | Nginx status and opt-in config test, reload and restart for a verified layout. SQL Server status and an authenticated, confirmed restart of `mssql-server.service` in SSH mode. |
| Running locally | Source startup uses .NET and a local development certificate. The dashboard is reachable while its local process is running. The browser may close while that process stays open. After a Windows restart, start the current app again. |
| Source | ASP.NET Core MVC on .NET 10 with Bootstrap and SSH.NET. Optional direct Ubuntu hosting exists in the tree as a separate path. |

[Current setup and feature list](../README.md) · [Current security model](../SECURITY.md).

## What makes v1 ready

1. **A clear Windows launch.** A user can download, verify and open the dashboard without a command prompt or SDK. A visible status and exit control explain when it is running. Pick a Windows certificate and trust strategy for this package, scoped to localhost and the current user. Offer launch at sign-in later as an explicit choice if people ask for it.
2. **Reliable SSH setup.** Show how to obtain the fingerprint through a trusted channel, handle wrong credentials and timeouts, and explain how to reconnect after a planned key rotation. Keep password setup for hosts that allow it. Add and test key-based login before stable v1 so key-only hosts work without weakening SSH. Test passphrase handling and separate sudo prompts. An account that can connect but lacks a management permission should see that clearly before taking action.
3. **A safe operating flow.** Discovery is read-only until review and approval. Status distinguishes unavailable metrics from zeros and separates SSH, systemd and HTTP results. A service, Nginx or SQL action names the target, respects the host’s permissions, records the result and refreshes state. Nginx reload requires a passing config test. SQL Server restart warns about dependent sites and explains the difference between the unit running and the database being ready.
4. **A published compatibility table.** Start with Windows 11 x64. Lab-test Ubuntu 22.04 and 24.04 LTS first, and add newer LTS versions when validated. Record Ubuntu, systemd, cgroup, SSH authentication, proxy layout and SQL Server version where applicable. Mark each combination as tested or pending. [Ubuntu release cycle](https://ubuntu.com/about/release-cycle) · [Microsoft SQL Server on Linux support policy](https://learn.microsoft.com/en-us/sql/linux/sql-server-linux-release-notes).
5. **Evidence people can reproduce.** Provide a sample Ubuntu lab, screenshots using sample names, a short walkthrough and release notes for known limitations. A first-run tester should reach a reviewed, registered service in 10 minutes when SSH and a lab service are already available. Use the results of the [release matrix](08-execution-and-checklist.md) to define supported workflows.

| After v1, based on user demand | What to evaluate |
| --- | --- |
| Other desktop builds | Windows Arm64, Linux or macOS client packages and their local trust workflows |
| Additional discovery | Better port and service recognition, plus optional URL suggestions with operator review |
| Monitoring improvements | Clearer incident history and longer local trends with an explicit retention policy |

Keep the private dashboard loopback-only. Preserve the existing applications, hosting stack and server permissions when testing the local workflow.

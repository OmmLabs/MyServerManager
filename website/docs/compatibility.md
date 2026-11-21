---
title: Compatibility
description: Check the Windows and Ubuntu prerequisites and see what the source preview supports.
---

| Part | Source preview |
| --- | --- |
| PC | Windows with the .NET 10 SDK and a browser |
| Server | One Ubuntu host with reachable SSH and systemd |
| SSH sign-in | Existing password login with a verified SSH host key |
| Service operations | A registered systemd unit and the SSH account's existing permission |
| Metrics | Linux `/proc`, systemd accounting and cgroup v2 where readable |
| Nginx | Read status by default. Opt-in actions require a verified layout and permissions. |
| SQL Server | Supports status and an explicitly confirmed restart for the canonical `mssql-server.service` unit. |

The source preview has automated tests for these workflows using simulated commands and SSH sessions. The release checklist calls for additional disposable Ubuntu testing before a stable support claim. Windows installers and dashboard SSH key sign-in are stable release work.

Read the [connection guide](./connection.md) and [security model](/security/) for details.

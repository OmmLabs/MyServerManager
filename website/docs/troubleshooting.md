---
title: Troubleshooting
description: Resolve local startup, SSH, permissions, metrics and website check problems.
---

| Symptom | What to check |
| --- | --- |
| The local page does not open | Confirm the dashboard process is running. Read the URL printed by the app and verify the development certificate. |
| Port 7300 is in use | Find the owning process in Windows. To change the dashboard port, stop your running dashboard and pass `--Manager:LocalPort=7311` after the `--` argument separator. Use a separate data directory for a simultaneous test instance. |
| SSH fails before sign-in | Check the address, network access and SSH port from this PC. Verify a changed fingerprint with the server owner. |
| Sign-in works but an operation fails | Check the account's existing permission for that unit and operation. Read the command result and actual service state. |
| Service memory is N/A | Check whether the service is running and whether its systemd or cgroup data is available. |
| A website check fails | Check the configured URL, client DNS, TLS, proxy upstream and service separately. |

See [connections](./connection.md), [service metrics](./metrics.md) and [website health](./website-health.md). Share only a small sanitized example when asking for help.

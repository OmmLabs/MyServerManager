---
title: SQL Server service
description: Monitor the SQL Server systemd unit and understand how an authorized restart affects dependent apps.
---

The **Database** page checks for `mssql-server.service` and shows available status, uptime, memory and CPU time. It reads service information over SSH. Database readiness is a separate check performed by the authorized database operator.

If your account may restart the service, review the dependent applications and schedule a maintenance window. Choose **Restart SQL Server**, read the warning and confirm when ready. Check the command result and audit entry, then verify database and app recovery before declaring the maintenance complete.

[SQL Server on Linux setup](https://learn.microsoft.com/en-us/sql/linux/install-upgrade/setup) · [Troubleshoot SQL Server on Linux](https://learn.microsoft.com/en-us/sql/linux/sql-server-linux-troubleshooting-guide).

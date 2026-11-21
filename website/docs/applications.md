---
title: Discover and manage applications
description: Find .NET services, approve an application entry and use safe systemd controls.
---

In **Applications**, select **Find apps**. The scan looks at loaded systemd services. It can recognize direct `dotnet` launches and executables with a neighboring `.runtimeconfig.json`. Listening ports depend on what the SSH account can see.

Select **Review and add**. Check the display name, canonical unit and port. Add the public website URL if you want a health check. A service that discovery misses can be added manually in **Settings** after the app verifies the existing unit.

Use **Status** to see details and **Logs** to see recent entries. **Start**, **Stop** and **Restart** use your SSH account's permissions. The app asks for confirmation before stopping or restarting a service. Read the actual result and verify its status before retrying a failed or timed-out action. The **Audit History** page records outcomes. Check [website health](./website-health.md) independently after a change.

Select **Remove** in Applications or Settings to remove a dashboard registration. Confirm the name in the dialog. The entry disappears from monitoring, and Audit History records the change. You can register the service again later.

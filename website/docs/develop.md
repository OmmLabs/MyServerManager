---
title: Developer guide
description: Build the .NET dashboard, run the simulated integration suite and preview the website.
---

The solution uses ASP.NET Core MVC on .NET 10. Controllers call services for the application registry, systemd operations, metrics and HTTP probes. The SSH adapter sends fixed executable and argument combinations to the registered host. Source folders and the app assembly retain their earlier names for upgrade compatibility.

Run these commands from the source root.

~~~powershell
dotnet build ServerAppManager.slnx -c Release
dotnet test ServerAppManager.slnx -c Release --no-build
~~~

The test project uses a temporary state directory, simulated SSH sessions, fake system commands and test HTTP handlers. Use a disposable Ubuntu host for any actual service tests.

From `website/`, use `npm ci` and `npm run build` to check the public site. Put docs and blog entries in their corresponding folders and use the editorial briefs in `release-plan/blog-data/`. See [community](/community/) for feedback and contribution routes.

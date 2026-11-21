---
title: Windows quickstart
description: Run the source preview on Windows, verify your Ubuntu SSH host and register your first service.
---

You need the .NET 10 SDK, a working Ubuntu SSH password login from your PC, and a trusted fingerprint for that server. Get the project source from [OmmLabs/MyServerManager](https://github.com/OmmLabs/MyServerManager).

## Start the dashboard

On Windows, double-click **Create-Desktop-Shortcut.cmd** in the source folder once. The setup builds the app and creates a **Server App Manager** desktop shortcut. Open that shortcut whenever you want to use the dashboard. The tray icon offers **Open dashboard** and **Quit Server App Manager**.

For a direct source run, open PowerShell in the folder that contains `ServerAppManager.slnx` and run the following commands.

~~~powershell
dotnet dev-certs https --trust
dotnet run --project src/MyServerManager
~~~

Open [your local dashboard](https://localhost:7300/). When using the PowerShell command, keep its window open while the dashboard is running.

## Connect to Ubuntu

1. Enter the SSH address you already use, such as `user@server.example.com`. For a custom port, use `user@server.example.com:2222`.
2. Compare the displayed SSH fingerprint with the one you obtained from the server owner or a trusted console. Confirm it when it matches.
3. Enter your Ubuntu SSH password. A successful sign-in saves the server address and trusted key on your PC.
4. Open **Applications → Find apps**. Choose **Review and add**, check the unit and any detected port, and save the service.

Read [connection and fingerprint help](./connection.md) if a step fails. Sign out when you finish. Press **Ctrl+C** in PowerShell to stop the dashboard.

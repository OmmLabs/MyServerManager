---
title: Frequently asked questions
description: Answers about startup, where Server App Manager runs and how it uses SSH.
---

# Frequently asked questions

## Where does the app run?

The dashboard runs on your PC and connects to Ubuntu through the existing SSH service. The public site serves project information, downloads and guides.

## Do I need to leave PowerShell open?

The desktop shortcut starts the dashboard in the background. Its tray menu lets you reopen it or quit. A direct `dotnet run` command needs its PowerShell window open. After Windows restarts, open the desktop shortcut again. [Create the shortcut](/docs/quickstart/).

## Is the dashboard always at port 7300?

`https://localhost:7300` is the default when the app is running on your PC. Use the address printed by your local app if you configured another port.

## Which password do I enter?

In local SSH mode, use the password for the Ubuntu SSH account you registered. Verify the server fingerprint before the first sign-in. The [connection guide](/docs/connection/) shows the address format and trust steps.

---
title: Connect and sign in
description: Understand SSH address formats, host fingerprints, sign-in and starting or stopping the local dashboard.
---

The browser talks to the dashboard on your PC. The dashboard opens SSH to Ubuntu and uses the server's existing tools. Start the local dashboard before opening its browser address.

## Check the SSH address

Use the same account and host you use for SSH. Include a port only when your administrator gives you a custom one.

~~~text
user@server.example.com
user@server.example.com:2222
~~~

## Verify the server key

Obtain the server's SHA-256 SSH host-key fingerprint through a trusted server console or from your administrator. Compare the entire fingerprint and key type. The app verifies the key before submitting your password and saves the key after a successful first sign-in. Later sign-ins verify the saved key again. If the key changes, confirm the reason with the server owner before enrolling a replacement.

The preview keeps the SSH password in memory during your session. Signing out or session expiration ends the connection. Closing the browser tab leaves the local dashboard process running. Use the desktop shortcut to reopen it and the tray menu to quit. When running through PowerShell, use **Ctrl+C** in its window to quit. Start the dashboard again after a Windows restart.

[Quickstart](./quickstart.md) · [Remove a saved server](./update-and-remove.md) · [Ubuntu OpenSSH guide](https://documentation.ubuntu.com/server/how-to/security/openssh-server/).

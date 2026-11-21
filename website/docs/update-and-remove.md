---
title: Update or remove the dashboard
description: Preserve local app settings when updating and remove a saved SSH server when needed.
---

Stop the dashboard before updating your source files. If you need a backup, protect the local state folder with the same care as your connection and audit records. The source run uses `src/MyServerManager/.manager-state`. The published build uses the existing `MyServerManager` folder in your local application data. The rebrand uses these paths to find saved registrations and history.

To remove a registered host, sign in and choose **Settings → Server settings → Remove server**. Confirming ends the connection and archives the registration's app list and audit history locally. A new registration receives a separate data area.

If a saved host can no longer be reached, stop the app and move `config/server.json` from the current state folder to a protected backup location. Start again to return to setup. Review which local files you want to retain before removing your copy of the app.

---
title: Nginx
description: Check Nginx status and understand the configuration checks required for reload and restart.
---

The **Nginx** page shows available status and version. Management actions are opt-in. An operator verifies the installed binary, trusted configuration tree and canonical unit before enabling `Manager:EnableNginxControls` with the verified `Manager:NginxLayout`. The preview recognizes the Ubuntu package and aaPanel layout values.

The dashboard validates Nginx configuration before reload or restart. Review the impact, confirm the requested action and read the result. After an approved operation, verify the public website from [Website Health](./website-health.md). The SSH account's existing permissions decide whether the operation can run.

[Nginx command-line parameters](https://nginx.org/en/docs/switches.html) · [Nginx reload behavior](https://nginx.org/en/docs/control.html).

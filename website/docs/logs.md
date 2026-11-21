---
title: Read application logs
description: Find recent journal entries for one service and share a sanitized excerpt.
---

Open **Logs** on a registered application. Choose the latest 100, 250 or 500 entries, then use the priority and text filters. Refresh if you are watching a recent restart or a new error.

Start with the first failure near the time the problem began. Read a few entries before it for context. Compare the timestamp with [website health](./website-health.md) and the operation result in Audit History.

Journal reads depend on the SSH account's permissions. Pattern-based redaction helps remove recognizable secrets. Review any excerpt before sharing it, including hostnames, credentials, connection strings, user data and internal paths. [Security model](/security/) · [Journal fields](https://www.freedesktop.org/software/systemd/man/latest/systemd.journal-fields.html).

---
title: Website health
description: Understand public HTTP checks, response times and the difference between an active service and an available site.
---

Add a public HTTP or HTTPS URL to an app to see its latest website check. The probe runs from your PC. It uses HEAD first and tries GET when the server returns 405 or 501 to HEAD. It records the final HTTP status, elapsed time and an error when available.

The app evaluates allowed public endpoints and validates redirects and TLS. When a service is active and the site is unavailable, check the configured address, listener, Nginx upstream, DNS and certificate. A site can also continue responding while an individual unit is stopped, depending on the existing hosting setup.

Use [Nginx](./nginx.md) and [troubleshooting](./troubleshooting.md) to narrow the failing layer. [HTTP semantics](https://www.rfc-editor.org/rfc/rfc9110.html).

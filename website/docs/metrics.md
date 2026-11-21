---
title: Service and server metrics
description: Understand service memory, CPU time, process counts and Ubuntu host totals.
---

The app reports both service and host readings. **Service memory** uses the service's cgroup when readable, with a fallback to available systemd accounting. It can include child processes and file cache attributed to the service. **Host memory** describes the whole Ubuntu server.

| Reading | Meaning |
| --- | --- |
| Current memory and peak | Usage of a service cgroup now and its available recorded peak |
| Main PID | The process systemd identifies as the main one |
| Process and task counts | Group processes and tasks where the system provides them |
| Service CPU time | Accumulated CPU time for the unit |
| Host CPU percentage | Utilization calculated between two Linux counter samples |
| Load, RAM, swap and disk | Host-level readings from Ubuntu |

**N/A** means the reading was unavailable. Stopped services, absent accounting and limited permissions can affect what you see. [Linux cgroup v2 memory controller](https://docs.kernel.org/admin-guide/cgroup-v2.html) · [Linux procfs](https://docs.kernel.org/filesystems/proc.html).

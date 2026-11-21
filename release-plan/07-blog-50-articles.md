# Blog plan with 50 article briefs

Each article solves one practical problem for a developer or small team running apps on Ubuntu. The search phrases describe what a reader might type. They guide the brief and give us something to validate in Search Console.

Write **1,200–2,000 words per article**. The CSV gives each article an individual target, five outline sections, a lab example, three primary references, planned internal links and review notes. [Download the 50 briefs](blog-data/article-briefs.csv) · [Generation prompt and SQLite setup](09-website-content-workflow.md).

**Launch** marks the eight articles to publish with the stable release. The other 42 are scheduled below. Every tutorial uses an example server or disposable lab. Mention the app, Ubuntu and relevant tool versions you tested, and point readers to the guide that matches their next step.

## 1. Local setup and SSH

| # | Article | Search phrase | What the reader can do afterward |
| --- | --- | --- | --- |
| 01 **Launch** | Manage Ubuntu services from Windows using the SSH you already have | manage Ubuntu services from Windows over SSH | Follow the PC → SSH → Ubuntu flow and check the server prerequisites. Link to Quickstart. |
| 02 | Use a custom SSH port with an Ubuntu service dashboard | SSH username host custom port | Read the parts of `user@server.example.com:2222` and test reachability before opening the dashboard. Link to Connection. |
| 03 **Launch** | Verify an Ubuntu SSH host key before signing in | verify Ubuntu SSH host key fingerprint | Obtain a fingerprint through a trusted route and compare it with the setup screen. Link to Security. |
| 04 | What to do when an Ubuntu server’s SSH host key changes | SSH host identification has changed Ubuntu | Confirm a planned rebuild or rotation and re-enroll a trusted key through the documented recovery flow. Link to Troubleshooting. |
| 05 | Troubleshoot SSH timeouts, refused connections and sign-in errors | SSH connection refused timed out permission denied | Use a symptom table to separate network reachability, daemon state and authentication. Link to Connection. |
| 06 | Set up SSH key authentication from Windows to Ubuntu | Windows SSH key Ubuntu | Make and protect a dedicated OpenSSH key in a lab and validate the login. Link to the tested key-auth compatibility row. |
| 07 | Give an Ubuntu operator access to specific systemd services | least privilege sudo systemctl specific service | Review the owner of a unit and its launch inputs, then verify scoped permissions in a disposable environment. Link to Security. |
| 08 | Why a localhost dashboard works only while its app is running | localhost dashboard stops after Windows restart | Tell whether the launcher is running, handle a port conflict and restart it after sign-in to Windows. Link to Connection. |
| 09 | Understand the HTTPS trust prompt for a local dashboard | localhost HTTPS certificate Windows dashboard | Verify the expected localhost address and publisher, then follow the release’s scoped certificate setup and cleanup. Link to Quickstart. |
| 10 | Remove a saved server and manage the local audit archive | remove saved SSH host dashboard | Separate signing out, removing a registration and retaining or erasing its history. Link to Update and Remove. |

## 2. Service discovery and day-to-day operations

| # | Article | Search phrase | What the reader can do afterward |
| --- | --- | --- | --- |
| 11 **Launch** | Find ASP.NET Core services running under systemd | discover ASP.NET Core systemd services | Review candidates, identify the unit and visible port, add a site URL and save an approved app. Link to Applications. |
| 12 | Find the listening port of a .NET service on Ubuntu | find ASP.NET Core listening port Linux | Compare an observed socket with an expected local port and a separate public website URL. Link to Applications. |
| 13 | Why a self-contained .NET service can be harder to discover | discover self-contained dotnet service systemd | Recognize discovery clues, wrapper-based starts and cases that need manual registration. Link to Applications. |
| 14 | Register a systemd service when it is missing from discovery | add systemd service to dashboard manually | Identify the canonical unit, validate the target and add an existing unit through review. Link to Applications. |
| 15 | Read active, inactive and failed systemd service states | systemd active inactive failed meaning | Interpret unit state and substate, then decide which log or metric to inspect next. Link to Applications. |
| 16 **Launch** | Restart an ASP.NET Core service with a short safety checklist | safely restart ASP.NET Core systemd service | Check target and dependencies, confirm once, inspect the result and check the website afterward. Link to Applications. |
| 17 | Diagnose a systemd service stuck in a restart loop | systemd ASP.NET Core restart loop | Use restart count, timestamps, exit status and log patterns to narrow the cause. Link to Logs. |
| 18 | Investigate a service that runs from your shell but fails under systemd | dotnet app runs manually but fails systemd | Check working directory, file permissions and runtime version in a lab without publishing secret values. Link to Troubleshooting. |
| 19 | Check an application after a deployment in five minutes | Ubuntu ASP.NET Core post deployment checklist | Verify unit status, recent errors, memory trend, configured port and public HTTP response. Link to Website Health. |
| 20 | Read an audit history after a failed service operation | systemd restart failed audit history | Match a request to its result and distinguish denied, failed and timed-out actions. Link to Applications. |

## 3. Memory, CPU and host resources

| # | Article | Search phrase | What the reader can do afterward |
| --- | --- | --- | --- |
| 21 **Launch** | Measure the real memory used by an Ubuntu systemd service | systemd service memory usage cgroup v2 | Compare the unit’s cgroup memory with its main process and total host RAM. Link to Metrics. |
| 22 | Find a systemd unit’s cgroup path safely | systemd ControlGroup cgroup path | Discover the live cgroup location for a sample unit, including one under a custom slice. Link to Metrics. |
| 23 | Interpret current and peak memory for an ASP.NET Core service | systemd memory.current memory.peak | Compare a current reading with a peak and check why a peak can be unavailable or reset. Link to Metrics. |
| 24 | Diagnose a growing .NET service memory footprint | ASP.NET Core Linux memory keeps growing | Collect repeatable samples, compare activity with memory and identify when deeper .NET diagnostics are needed. Link to Metrics. |
| 25 | What Linux available RAM, cache and swap tell you | Linux MemAvailable buffers cache swap explained | Read a small `/proc/meminfo` example and decide whether the host is under memory pressure. Link to Metrics. |
| 26 | Read service CPU time and server CPU percentage correctly | systemd CPUUsageNSec vs CPU percentage | Compare cumulative CPU time with a rate calculated from two host samples. Link to Metrics. |
| 27 | Understand Ubuntu load average on a multicore VPS | Linux load average per core Ubuntu | Read the 1-, 5- and 15-minute values beside core count and CPU use to choose the next diagnostic. Link to Metrics. |
| 28 | Tell process count from task and thread count in systemd | systemd TasksCurrent threads vs processes | Explain why one main PID can account for many tasks and find the service-wide readings. Link to Metrics. |
| 29 | Investigate low disk space on the mount that actually matters | Ubuntu disk full check mount point df | Map a service path to its filesystem and check available blocks and inodes using read-only commands. Link to Metrics. |
| 30 | Diagnose a missing or N/A service metric | systemd MemoryCurrent unavailable N/A | Check service state, permissions, controller availability and systemd support to explain the missing value. Link to Troubleshooting. |

## 4. Logs and website health

| # | Article | Search phrase | What the reader can do afterward |
| --- | --- | --- | --- |
| 31 **Launch** | Find the first error in an ASP.NET Core systemd journal | journalctl ASP.NET Core service logs Ubuntu | Load a bounded slice for one unit, filter by priority and identify the first failure in a lab. Link to Logs. |
| 32 | Read journal priority when application log levels look different | journalctl priority vs application log level | Explain why a message can appear at a different journal priority and when to search the text. Link to Logs. |
| 33 | Trace an outage across journal timestamps and an HTTP check | correlate systemd logs website outage timestamp | Put the last successful probe, error, restart and recovery on one timeline using the same time zone. Link to Website Health. |
| 34 | Share a useful service log excerpt without leaking secrets | redact systemd logs before sharing | Extract a minimal sample and review it for tokens, credentials, host details and personal data. Link to Security. |
| 35 | Investigate a service that appears to have no journal entries | journalctl no entries for service Ubuntu | Check unit identity, log destination, boot or retention limits and account access. Link to Logs. |
| 36 | Why a running systemd service can still have a down website | systemd service running website unavailable | Follow a layered test across local listener, proxy, DNS, certificate and public response. Link to Website Health. |
| 37 | Test website health when an endpoint rejects HEAD | HTTP HEAD 405 health check GET fallback | Explain the HEAD-to-GET fallback and see what the final response tells you. Link to Website Health. |
| 38 | Read HTTP status codes in a website health dashboard | website monitoring HTTP 200 301 403 503 meaning | Distinguish redirects, access restrictions and server errors by reproducing each with a lab endpoint. Link to Website Health. |
| 39 | Diagnose DNS and TLS errors from the computer running the check | website health check DNS TLS certificate failure | Check name resolution and certificate trust from the PC and identify differences from the server’s view. Link to Troubleshooting. |
| 40 | Build a useful health endpoint for an ASP.NET Core app | ASP.NET Core health check liveness readiness | Test liveness and readiness in a sample app, choose an appropriate public signal and keep internals private. Link to Website Health. |

## 5. Nginx, SQL Server and incident response

| # | Article | Search phrase | What the reader can do afterward |
| --- | --- | --- | --- |
| 41 **Launch** | Fix an Nginx 502 in front of an ASP.NET Core app | Nginx 502 ASP.NET Core Ubuntu | Trace a lab 502 to a stopped unit, wrong upstream port or failed listener using status and logs. Link to Nginx. |
| 42 | Decide when to reload or restart Nginx | Nginx reload vs restart Ubuntu | Compare worker behavior and impact and use a passing config test before a controlled reload. Link to Nginx. |
| 43 | Read a failed Nginx configuration test | nginx -t failed Ubuntu what to check | Work through a lab failure caused by a broken include or syntax error and verify the reload remains blocked. Link to Nginx. |
| 44 | Identify an Ubuntu package or aaPanel Nginx layout | aaPanel nginx binary path systemd service | Check the binary, loaded service and config layout before enabling management. Link to Compatibility. |
| 45 | Check SQL Server status and memory on Ubuntu | mssql-server systemd status memory Linux | Read the SQL Server unit, uptime and available cgroup metrics without running a database query from the dashboard. Link to SQL Server. |
| 46 **Launch** | Restart SQL Server on Ubuntu with a maintenance checklist | safely restart mssql-server Ubuntu | Plan a window, confirm the target and dependent apps, then verify the audit result and application recovery. Link to SQL Server. |
| 47 | Verify database readiness after a SQL Server restart | SQL Server Linux running but apps cannot connect | Separate systemd running state from listener and dependency readiness using an authorized lab workflow. Link to SQL Server. |
| 48 | Investigate an ASP.NET Core app timing out after a database interruption | ASP.NET Core SQL Server timeout after restart | Read application-side error timing and confirm the dependency is reachable before retrying an app restart. Link to Troubleshooting. |
| 49 | Use an audit timeline to review a production incident | audit timeline service restart incident review | Reconstruct action order and outcomes from sanitized sample logs and document follow-up owners. Link to Applications. |
| 50 | Build a disposable Ubuntu lab for dashboard release tests | Ubuntu systemd SSH test lab dotnet | Prepare sample units, a reverse proxy and a test endpoint, then run the connection and restart matrix. Link to Developer Docs. |

## Publishing calendar

Publish **01, 03, 11, 16, 21, 31, 41 and 46** at launch. Publish the remaining 42 in pairs, two per week. The order mixes operational topics and follows the release’s verified features.

| Week after launch | Article numbers | Week after launch | Article numbers |
| --- | --- | --- | --- |
| 1 | 02, 36 | 12 | 25, 37 |
| 2 | 12, 42 | 13 | 26, 38 |
| 3 | 04, 22 | 14 | 27, 39 |
| 4 | 05, 32 | 15 | 28, 45 |
| 5 | 06, 23 | 16 | 29, 47 |
| 6 | 13, 43 | 17 | 30, 48 |
| 7 | 07, 33 | 18 | 34, 49 |
| 8 | 14, 44 | 19 | 35, 50 |
| 9 | 08, 24 | 20 | 40, 19 |
| 10 | 09, 15 | 21 | 18, 20 |
| 11 | 10, 17 | | |

## Editorial checklist

1. Name one problem, one target reader and one practical result. Use the docs for product instructions and the blog for full examples, diagnostics and context.
2. Reproduce the example on a supported lab build. Capture the versions, success and failure cases, and expected output. Use safe hostnames and sanitized screenshots. For service actions, explain the target, permission and impact before the command or confirmation.
3. Link to an upstream source for technical facts. Starting references include [Windows OpenSSH key management](https://learn.microsoft.com/en-us/windows-server/administration/openssh/openssh-keymanagement), [systemctl](https://www.freedesktop.org/software/systemd/man/latest/systemctl), [journalctl](https://www.freedesktop.org/software/systemd/man/latest/journalctl) and [Linux cgroup v2](https://docs.kernel.org/admin-guide/cgroup-v2.html).
4. Add an author, publish date, verified version, short description, descriptive images and one or two links to the relevant guide. Check links and accessibility. Use the article to answer the searcher’s question before inviting a download.
5. Recheck the eight launch posts after major releases. Review impressions, clicks and support questions monthly to adjust later topics and merge overlap. Delay a draft if its feature or example has not passed review.

# Execution and release gates

Assign a maintainer, an independent reviewer, a tester, a site editor and a sponsor/DNS owner. People can cover multiple roles. Keep a second approver for the public snapshot, security boundary and stable build.

## Sequence

Week ranges are estimates for a maintainer with parallel site and test help. A milestone moves when its checks pass.

| Milestone | Target | Owner | Deliverable and exit check |
| --- | --- | --- | --- |
| 0 · Decisions | Week 1 | Maintainer and sponsor | Confirm name, rights holder, license, GitHub owner, support scope, signing plan and sponsor subdomain. Record decisions in the [release index](README.md). |
| 1 · Source preview | Weeks 1–2 | Maintainer and reviewer | Clean source snapshot, approved license and notices, two successful clean CI builds, private reporting, source Quickstart and explicit preview label. |
| 2 · Windows preview | Weeks 3–4 | Maintainer and tester | One x64 preview package, start and quit flow, documented localhost trust, checksums and first lab walkthrough. Prepare Overview, Download, Quickstart, Compatibility, Security and Community. Run two fresh Windows onboarding sessions before handing it to testers. |
| 3 · Stable candidate | Weeks 5–6 | Tester and security reviewer | Complete the lab matrix, test key-based SSH and permissions, sign the Windows package, run 10 onboarding sessions and close release-blocking findings. |
| 4 · Stable and website | Weeks 7–8 | Maintainer, editor and sponsor | Publish `v1.0.0`, verify its download, bring up the HTTPS project subdomain, and publish 8 tested posts. Keep the planned content queue ready. |
| 5 · Follow-through | 90 days and beyond | Maintainer and editor | Triage twice weekly, publish two posts per week, review metrics monthly and publish tested fixes and support notes. |

Reserve time for packaging, certificates, signing and QA. Start the eight launch articles in parallel with the site work. Their 9,600–16,000 words require drafting, lab verification and editing before the release gate. Confirm the budget and owner for signing and disposable test machines before committing to a stable date. Preview material can ship independently once its own checks pass. [Content workflow](09-website-content-workflow.md).

## Test lab and evidence

Use a clean Windows 11 x64 non-admin account and separate disposable Ubuntu 22.04 and 24.04 LTS hosts. Use sample services with intentionally good and failed starts, a controlled website and Nginx configuration. Test Microsoft SQL Server on a lab combination supported by Microsoft, and document its exact version. A lab rebuild should recover from a test failure. [Ubuntu release cycle](https://ubuntu.com/about/release-cycle) · [SQL Server on Linux release notes](https://learn.microsoft.com/en-us/sql/linux/sql-server-linux-release-notes).

Each test record should show the package hash, Windows and Ubuntu versions, auth method, test ID, expected and observed result, and a sanitized screenshot or log if useful. Save the summary with the release. Keep product support to combinations that passed.

| ID | Scenario | Pass condition |
| --- | --- | --- |
| R01 | Clean Windows setup, run, exit and reopen | Standard Windows user starts the signed app without a command prompt or SDK. The local address opens with the documented certificate trust flow. Quit releases the port, reopening retains the expected configuration and a reboot behaves as documented. |
| R02 | Local binding and web protections | From another machine the app is unreachable. Check IPv4 and IPv6 loopback, untrusted Host and cross-origin requests. Missing or invalid CSRF tokens and anonymous actions cannot make changes. Rate limits and cookie/session rules work. |
| R03 | SSH setup and host identity | First contact displays the trusted key prompt before sending credentials. Wrong credentials and changed keys fail with clear feedback. Confirm password and key-based login, including custom port, passphrase and key rotation recovery. No credential appears in state or diagnostics. |
| R04 | One server, sign-out and removal | Remove server only after sign-in and confirmation. It ends the session. Enrollment of a different lab host uses separate registry and audit data. Old tabs cannot act against the new registration. |
| R05 | Discovery and registration | Find the sample direct and self-contained .NET units. Confirm supported port behavior and review the metadata before saving. Invalid, unloaded or unapproved units cannot be operated. |
| R06 | Service operations and errors | Verify start, stop, restart, confirmation, actual status and audit against sample units. Cancel and permission-denied paths leave the unit unchanged. Repeated clicks and simultaneous requests do not start conflicting operations. Record timeout behavior and recheck the actual unit state. |
| R07 | Resource measurements | Compare current and peak memory against the correct cgroup, including a nested path, and CPU rate against two `/proc/stat` samples. Check host RAM and disk against system values. Missing values appear as N/A. |
| R08 | Journal and website health | Verify 100, 250 and 500 line modes, priorities, search and escaping. Check redaction with dummy secrets. Test HEAD with GET fallback, public reachability and timeout behavior. HTTP and service states remain independent. |
| R09 | Nginx | Check status and version for each claimed layout. A bad lab configuration blocks reload. Valid reload and confirmed restart respect permissions and record results. Mark any layout not covered by a lab as pending. |
| R10 | SQL Server | Exercise missing unit, allowed and denied restart, cancel, duplicate request and command timeout. Verify the dependency warning, audit and readiness guidance. Exercise a real restart only on the disposable SQL lab. |
| R11 | Upgrade, uninstall and rollback | Upgrade a copy of old local state and a fresh preview. Verify archive, registry, audit and trust handling. Restore a backup with the previous signed version and test removing the app with each retention choice. |
| R12 | Performance under normal use | With 20 registered lab units, run 15 minutes of connected dashboard use at default intervals. Confirm polling does not overlap by category or pile up while disconnected. Record client working set, CPU and SSH command count. Starting budgets are 200 MiB dashboard working set and 3% average client CPU on a documented reference PC, excluding the browser. Review any overage. |
| R13 | Existing Ubuntu setup | Compare packages, service definitions, Nginx config and listening ports before and after local setup and read-only onboarding. Confirm the app uses the existing SSH path. Test service mutations separately on disposable sample units. |

## Stable sign-off checklist

- [ ] Rights holder approves MIT and notices. Sponsor approves the name, credit and domain. The public repo and installer pass a second sensitive-data review.
- [ ] Build, test and website CI pass on the release commit. The matching mock-based tests never connect to real production services. The lab reports show R01–R13 results and supported versions.
- [ ] A security reviewer checks SSH host identity, credential lifecycle, exact remote command allowlisting and sudo handling, app and registry authorization, session expiration and CSRF, SSRF protections, audit handling and the Windows signing/trust process.
- [ ] No unresolved critical or high-severity issue affects the claimed release scope. Every accepted limitation has a release note, support status and owner.
- [ ] Nine of 10 preview testers can sign in and register one service within 10 minutes after SSH and the lab service are ready. Every failed attempt has a recorded cause and follow-up.
- [ ] A tester verifies the downloaded Windows package, SHA-256 checksum, executable or installer signature, app version, update, removal and rollback from published instructions.
- [ ] The live subdomain works over HTTPS. Homepage, Download, Quickstart, Compatibility, Security, Community, RSS, sitemap and GitHub links pass smoke checks. Publish 8 launch posts of 1,200–2,000 words after technical and editorial review.
- [ ] A named maintainer and backup have access to release and private report workflows. Document how to roll back the site, advise users on a bad release and issue a corrected package.

If a stable check fails, keep the stable candidate in preview, document the finding and test the fix on the same matrix row. Review [GitHub release handling](04-github-and-release.md) and [site rollback](03-website-plan.md) before announcing the corrected version.

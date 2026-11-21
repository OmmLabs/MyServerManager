# Marketing and community

## Audience and message

| Audience | Real problem | Proof to show |
| --- | --- | --- |
| ASP.NET Core developer hosting apps on Ubuntu | Checking a failed deployment means switching between SSH, journal and browser | Find a .NET unit, read logs, review memory and check the website in one walkthrough |
| Freelancer or small agency maintaining a VPS | Several services share the same server and manual restarts can hit the wrong target | Show the approved service list, confirmation and audit result |
| Ubuntu or aaPanel user using SSH and systemd | Wants a small dashboard that fits the existing setup | Show how the Windows app connects over existing SSH and explain the privileges each operation needs |

**Short pitch**

> Server App Manager is an open-source dashboard for Ubuntu apps. Open it on your PC, connect over SSH, and check services, logs, memory and website health in one place. Sponsored by Omm Rudraksha.

Use that pitch once the source is public and licensed. State Windows and tested Ubuntu versions next to the download. Point developers to the [product and Cockpit comparison](01-product-and-cockpit.md) for the feature scope.

## Launch materials

- One 90-second captioned screen recording with a transcript. Open the app, verify a sample lab host, review a discovered app, see its log and website health, then end the session.
- Five screenshots from the same lab. Cover Dashboard, Applications, Logs, Website Health and safe restart confirmation. Keep all live credentials, host addresses and customer data out of the recording.
- One founder or maintainer post about the problem and why the tool runs locally. Include the source link, current prerequisites, support limits and exact sponsor credit.
- One short release announcement with Download, Quickstart, Compatibility, Security and Feedback links. Add a brief project link from the sponsor’s About or Projects page once the sponsor approves the wording.
- Three reusable graphics. Use one product screenshot, one PC → SSH → Ubuntu diagram and one feature card for articles or community posts. Keep the sponsor logo within the approved branding.

## Launch sequence

| When | Action | How to tell it happened |
| --- | --- | --- |
| Before public preview | Invite 10 opt-in testers across developers and Ubuntu operators. Ask them to use a lab or a host where they have permission. | Record the steps they completed, elapsed time and friction with consent. |
| Public preview | Share a tested download with the project’s own audience and one relevant .NET community, according to its posting rules. | One release post and one thread for feedback, with clear preview label. |
| Stable launch | Announce in GitHub Releases, the project blog and the sponsor’s project channel. Share the hands-on demo where communities welcome it. | Three owned announcements and at most two relevant community posts. |
| First 30 days | Reply to questions, summarize fixes in release notes, and update Quickstart based on repeated feedback. | Triage twice weekly and publish at least one update to docs or the release notes. |
| After launch | Post two tested articles per week, keep the feed active and share posts where they answer an existing question. | Use the [50-article calendar](07-blog-50-articles.md). |

The official .NET community directory can help identify relevant groups. Use Show HN only when there is a usable release readers can try and the maintainer can discuss it. GitHub Discussions can serve as the initial help channel. [Microsoft .NET community](https://dotnet.microsoft.com/en-us/platform/community) · [Show HN guidelines](https://news.ycombinator.com/showhn.html).

## Measurements and review

These are initial targets. Reset them after the first 30-day baseline if the data or capacity points to a better target.

| Metric | First target | How to measure |
| --- | --- | --- |
| Preview onboarding | 9 of 10 testers register one service in 10 minutes with SSH and the lab service ready | Observe or collect voluntary results without login details |
| Download interest | 100 Windows asset downloads within 90 days of stable launch | GitHub release asset counts, which include retries and repeat downloads |
| Useful adoption | 10 voluntary confirmations from users who connected to their own or a lab Ubuntu host | Optional GitHub Discussion or tester survey |
| Search discovery | 100 organic clicks in a single 28-day window by day 90 | Search Console for the project subdomain |
| Support | Triage new public issues within three business days and urgent security reports through the private channel | Issue activity and private report timestamps |

Publish a short internal weekly sheet with date, releases, clicks, downloads, confirmed connections and the top three obstacles. Start with GitHub counts, Search Console and voluntary reports. Keep the app free of telemetry by default. If aggregate website analytics are later approved, document the provider, data collected and retention. Tell contributors how to submit feedback without disclosing host details.

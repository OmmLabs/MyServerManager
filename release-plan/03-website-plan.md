# Public website

**Proposed address** `https://serverappmanager.ommrudraksha.com`

The site answers three questions early. What can I manage, what do I need on Ubuntu, and how do I open the dashboard on Windows? Visitors should reach a download and the matching Quickstart from the home page in one click each.

## Main navigation

**Overview · Download · Docs · Blog · Community · GitHub**

The footer links to Release Notes, FAQ, Search, Security, Privacy, About and Sponsors, the RSS feed and the source code. Put a distinct **Report a security issue** link on the Security page.

## Sitemap and first release content

| URL | Content | Main next step |
| --- | --- | --- |
| `/` | Product promise, two calls to action, PC → SSH → Ubuntu diagram, current support and sponsor credit | Download or Quickstart |
| `/features/` | Screenshots for Apps, Logs, Monitoring, Health, Nginx and Database. Label server-side actions and permission requirements. | See the demo |
| `/demo/` | Screenshots and a captioned 90-second walkthrough using an example host and generated lab data | Follow Quickstart |
| `/download/` | Latest available channel, version, Windows target, checksum, package signature, prerequisites, update and uninstall links | Get the GitHub asset |
| `/docs/` | Start here, user guides, reference, developer docs and search | Choose a task |
| `/docs/quickstart/` | Open the dashboard, enter SSH address, verify fingerprint, sign in, review the first service and sign out | Register the first app |
| `/docs/compatibility/` | Tested client and server versions, SSH and sudo requirements, Nginx and database support | Verify the setup |
| `/docs/connection/` | Starting and quitting locally, custom SSH port, credentials, key verification and removal | Complete setup |
| `/docs/applications/` | Discovery coverage, review, manual registration, app operations, confirmations and audit | Manage an approved unit |
| `/docs/metrics/` | Service memory, CPU time, host totals, disk, uptime and unavailable values | Interpret a reading |
| `/docs/logs/` | Journal access, bounded reads, priorities, filters and safe sharing | Find a failure |
| `/docs/website-health/` | HTTP check origin, timeouts, public endpoint requirements and service versus site state | Diagnose availability |
| `/docs/nginx/` and `/docs/sql-server/` | Verify the layout or unit, read-only checks, existing permissions, tested actions and readiness | Review impact before operating |
| `/docs/troubleshooting/` | Connection failures, permission errors, port 7300 already in use, missing metrics, Nginx and SQL issues | Resolve or file an issue |
| `/docs/update-and-remove/` | How to upgrade, back up local state, remove the local app or erase retained data | Choose what to keep |
| `/docs/develop/` | Architecture, build and lab tests, contributor workflow, configuration and internal contracts | Make a first PR |
| `/releases/` | Human-readable changes, known issues and links to tagged GitHub releases | View a release |
| `/blog/` and feed | Practical articles with categories, bylines, reviewed dates and links to their relevant docs | Read or subscribe |
| `/community/` | GitHub Discussions for help, Issues for reproducible bugs, contributing and support policy | Choose the right channel |
| `/faq/` | Local versus remote, startup, credentials, pricing and scope | Find a guide |
| `/security/` | Architecture and trust boundaries, supported versions, disclosure instructions | Report privately |
| `/about/` and `/privacy/` | Mission, maintainers, sponsor, site hosting, analytics and data handling | Learn about the project |
| `/404.html` | Recovery links to Search, Docs and Download | Continue browsing |

Generate the search entry from the site’s actual content. Developer API reference belongs under `/docs/develop/`, documenting the command and adapter boundaries first. Version the docs when stable release behavior diverges.

## Coverage of Cockpit’s public sections

| Cockpit’s site | Server App Manager equivalent |
| --- | --- |
| Introduction, screenshots, feature walkthrough and getting started | Overview, Features, Demo and Quickstart |
| Installation, documentation, deployment guide and feature internals | Download, Compatibility, operator guides, configuration reference and Architecture |
| Optional application catalog | Feature directory for Apps, Monitoring, Logs, Health, Nginx and Database, with a scope and support label |
| FAQ, project ideals and site search | FAQ, About and Search |
| Blog, feeds and release notes | Editorial Blog, RSS and release history |
| Issue tracker, source and contribution overview | GitHub Issues, repository and Contributing |
| Mailing list and Matrix | GitHub Discussions for help and announcements, with the blog feed for subscriptions |
| Developer tutorials and API reference | Developer Quickstart, lab walkthrough and reference for internal contracts |

Cockpit already publishes posts and feeds. Server App Manager’s editorial section adds a dedicated program of 50 practical guides focused on local SSH-based app operations. [Cockpit homepage and site sections](https://cockpit-project.org/) · [Documentation](https://cockpit-project.org/documentation.html) · [Blog](https://cockpit-project.org/blog/) · [Applications](https://cockpit-project.org/applications.html) · [50 article briefs](07-blog-50-articles.md).

## Homepage order

1. Product name and headline, followed by a **Download for Windows** button when a package is ready, or **View preview on GitHub** during the source preview. Place **Read Quickstart** beside it.
2. One real screenshot from the disposable demo server, with captions for service status, memory, logs and website health.
3. Three steps. Start Server App Manager on Windows, verify your existing SSH server, then review the discovered apps.
4. Feature cards for service controls, troubleshooting and the Nginx and SQL Server tools. Clearly distinguish available features from planned work.
5. The supported platform table, a security summary and latest release, followed by selected blog posts, contribution links and sponsor credit.

## Build and host

Use **Docusaurus** for a static site in `website/`, with Markdown docs and blog, generated sitemap and RSS. Use a build-time search index so people can search docs and articles directly on the site. Start with one language and a single maintained docs version. Deploy the static output through GitHub Actions to GitHub Pages. Keep executable assets in GitHub Releases and use Pages for the open-source project site. [Docusaurus blog](https://docusaurus.io/docs/blog) · [Deployment](https://docusaurus.io/docs/deployment) · [Local search plugin](https://github.com/easyops-cn/docusaurus-search-local) · [GitHub Pages limits](https://docs.github.com/en/pages/getting-started-with-github-pages/github-pages-limits).

Use GitHub for Markdown editing and review at launch. Editors can use a private SQLite file to organize briefs and drafts. GitHub Pages serves the built site from Markdown and static assets. See the [website stack and article workflow](09-website-content-workflow.md).

1. Verify the sponsor domain in the approved GitHub account with the TXT record GitHub provides.
2. Set the custom domain in GitHub Pages and add the dedicated DNS CNAME `serverappmanager → ommlabs.github.io` as instructed for the [OmmLabs repository](https://github.com/OmmLabs/MyServerManager). This record routes the project subdomain to the public site.
3. Enable HTTPS once GitHub provisions the certificate. Check the live domain, the HTTP redirect, canonical URLs, download links, RSS, sitemap and error page.
4. Keep a known good Pages build and redeploy it if a site release fails. Document how to remove or redirect the CNAME before decommissioning Pages to avoid a dangling DNS record.

[Verify a GitHub Pages domain](https://docs.github.com/en/pages/configuring-a-custom-domain-for-your-github-pages-site/verifying-your-custom-domain-for-github-pages) · [Configure the subdomain](https://docs.github.com/en/pages/configuring-a-custom-domain-for-your-github-pages-site/managing-a-custom-domain-for-your-github-pages-site) · [Enforce HTTPS](https://docs.github.com/en/pages/getting-started-with-github-pages/securing-your-github-pages-site-with-https).

## Website launch checks

- A visitor can reach Download, Quickstart, Privacy and Security from the homepage. Every screenshot identifies its example environment. Use meaningful image text, captions and a readable transcript.
- Keyboard navigation works on the menu, search, article pages and theme toggle. Test narrow mobile, desktop, light and dark layouts.
- The build checks internal links, headings and image references. A smoke check follows the live download, source, support, feed and release links.
- The public site serves static content. Quickstart tells readers to enter credentials on the dashboard running on their PC. Describe hosting, optional analytics and retention on the Privacy page.

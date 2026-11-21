# Server App Manager open-source release plan

**Server App Manager** is the chosen public name for **My Server Manager**. It gives people a browser dashboard on their own computer for managing one Ubuntu server through the server’s existing SSH connection. **Sponsored by Omm Rudraksha** is the public credit.

```text
Server App Manager
├── Local dashboard
│   ├── Windows first, one Ubuntu server over SSH
│   └── Apps, metrics, logs, website health, Nginx and SQL Server
├── Public website
│   ├── serverappmanager.ommrudraksha.com
│   └── Docusaurus on GitHub Pages for downloads, docs, support and blog
├── GitHub
│   ├── OmmLabs/MyServerManager, MIT license and tagged releases
│   └── Tests, issues, contributions and private security reporting
└── Launch
    ├── 10 preview testers and a tested Windows download
    └── 50 briefs with 1,200–2,000 words per article
```

The public website is where people learn and download. The dashboard opens on the user’s own computer, currently at [the local dashboard address](https://localhost:7300). They sign in with their existing Ubuntu SSH account. The server needs working SSH and systemd, and management actions depend on the account’s existing permissions.

| Plan | What it covers |
| --- | --- |
| [01 Product and Cockpit](01-product-and-cockpit.md) | Current features, comparison, release scope and quality targets |
| [02 Brand and domain](02-brand-and-domain.md) | Chosen name, project and repo names, sponsor credit and proposed subdomain |
| [03 Website](03-website-plan.md) | Sitemap, Cockpit coverage, download journey, content and hosting |
| [04 GitHub and releases](04-github-and-release.md) | License, public-source review, builds, security and downloads |
| [05 Marketing](05-marketing.md) | Who to reach, launch materials, outreach and targets |
| [06 SEO](06-seo.md) | Page and query map, search setup, performance and measurement |
| [07 Blog](07-blog-50-articles.md) | 50 distinct article briefs and an editorial schedule |
| [08 Execution](08-execution-and-checklist.md) | Owners, milestones, tests and release approval |
| [09 Website content workflow](09-website-content-workflow.md) | Website stack, CMS decision, generation prompt, SQLite setup and publishing steps |
| [10 Source preview](10-source-preview.md) | Local verification and the remaining GitHub, license and domain steps |
| [50 article briefs in CSV](blog-data/article-briefs.csv) | Titles, target words, outlines, lab examples, sources and review notes |

## Decisions before launch

| Decision | Proposed default | Who confirms |
| --- | --- | --- |
| Public subdomain | `serverappmanager.ommrudraksha.com` for the chosen Server App Manager name | Sponsor |
| Public repository | [OmmLabs/MyServerManager](https://github.com/OmmLabs/MyServerManager), confirmed by the project owner | Confirmed |
| License | [MIT](../LICENSE), copyright 2026 OmmLabs, preserved from the supplied repository | Confirmed in repository |
| First supported setup | Windows 11 x64 and one Ubuntu systemd host, with versions confirmed in the lab | Maintainer and tester |
| Public milestones | Source preview, downloadable preview, then stable v1 | Maintainer and security reviewer |
| Editorial scope | Eight articles for the launch and a 21-week queue, with 1,200–2,000 words per article | Editor |

Proposed destinations, dates and targets are decisions to confirm. Product behavior is based on the [current README](../README.md), [security notes](../SECURITY.md) and reviewed source. Read the [execution checklist](08-execution-and-checklist.md) to see what still needs release testing.

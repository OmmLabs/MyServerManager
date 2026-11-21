# Server App Manager source preview

The local candidate is `0.1.0-preview.1`. Its public website name is **Server App Manager**, with credit to Omm Rudraksha. The confirmed public repository is [OmmLabs/MyServerManager](https://github.com/OmmLabs/MyServerManager), with its existing [MIT License](../LICENSE), copyright 2026 OmmLabs. The proposed website origin is `https://serverappmanager.ommrudraksha.com`.

## Try it locally

- Open [ServerAppManager.slnx](../ServerAppManager.slnx) or use the [dashboard quickstart](../README.md). The source run still uses the `src/MyServerManager` project folder. It reuses the existing settings and local state identity.
- Start the project site from the [website folder](../website/README.md). The public site and its blog are static, with an optional SQLite file for editorial work on your PC. [Editorial workflow](09-website-content-workflow.md).
- Review the 50 [article briefs](blog-data/article-briefs.csv). Week 0 has eight launch articles, each with a target in the 1,200–2,000 word range. The website includes a separate project announcement.

## Local verification

| Check | Result |
| --- | --- |
| .NET 10 Release build | Passed, with zero warnings or errors |
| Dashboard test suite | 74 passed using simulated SSH, system commands and web probes, including application removal |
| Published dashboard | Started with fresh local state and returned the branded setup page over localhost HTTPS |
| Website | Docusaurus production build passed. 32 pages and assets responded locally, including RSS and the release-mode sitemap. Browser search found the Nginx guide and the homepage fit a 430 px viewport. |
| Article queue | All 50 CSV records match the SQLite seed and resolve to planned site pages |
| Dependency review | NuGet locked restore passed its advisory gate. The website's production npm audit reported zero vulnerabilities on 2026-11-21. |

The app setup image comes from the isolated Windows preview. The release candidate uses the framework-dependent build and expects a .NET 10 SDK for the source quickstart. The published output includes the project MIT license and dependency notices.

## Publish the first preview

1. Use [OmmLabs/MyServerManager](https://github.com/OmmLabs/MyServerManager), confirmed by the project owner. Assign the maintainer and a monitored contact.
2. Preserve the repository's MIT license and dependency notices. Review the source import and its file inventory before merging it to `main`.
3. Import the source through a pull request based on the existing `main` history. Run **Dashboard CI** and **Project website**. Require the checks on pull requests and enable GitHub Issues, Discussions and private vulnerability reporting.
4. After source review, tag `v0.1.0-preview.1` and publish the source preview instructions and change notes. [Changelog](../CHANGELOG.md).
5. Configure the sponsor subdomain and HTTPS, then run the manual Pages job. Use the [site publishing instructions](../website/README.md) to verify the live site and enable indexing when ready.

The next release milestone is a Windows preview package. Signing, key-based SSH sign-in and the full disposable Ubuntu test matrix are part of the [stable release checklist](08-execution-and-checklist.md).

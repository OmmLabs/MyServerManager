# GitHub and software releases

## Repository structure

Use the public repository [OmmLabs/MyServerManager](https://github.com/OmmLabs/MyServerManager), with a default branch of `main`. Dashboard tests and website builds run as separate checks. Publish the site from a reviewed `main` commit through the Pages workflow. Use a project board with **Release blockers**, **Next** and **Later**. Record the support matrix and known gaps under each release.

| Path or feature | Purpose |
| --- | --- |
| `README.md` | Screenshots, local architecture, supported systems, quick start, downloads and sponsor |
| `LICENSE` and `THIRD-PARTY-NOTICES.md` | Approved project license and redistribution notices |
| `SECURITY.md` and GitHub private reporting | Supported versions, safe reproduction and coordinated disclosure |
| `CONTRIBUTING.md` and `CODE_OF_CONDUCT.md` | Build and test guide, contribution expectations and conduct contact |
| `CHANGELOG.md` and GitHub Releases | What changed, known issues, packages, checksums and upgrade steps |
| `src/`, `tests/` and `website/` | Dashboard, automated tests and public site |
| `docs/development/` and `docs/lab/` | Architecture decisions and reproducible disposable Ubuntu setup |
| `.github/` | Workflows, owners, PR template and issue forms |
| Issues, Discussions and Roadmap | Reproducible bugs, user questions, proposals and milestones |

Label optional direct Ubuntu hosting as an advanced, separately supported path if it stays in the public repository. The main README and Windows packages should lead with local SSH. Track support status for the direct-mode scripts explicitly.

## Source ownership and first public snapshot

1. Preserve the repository's [MIT License](../LICENSE), supplied with `Copyright (c) 2026 OmmLabs`. Include it in published dashboard output alongside dependency notices. Keep sponsor trademarks and third-party assets subject to their applicable terms. Record maintainers who can approve releases. [Open Source Initiative MIT License](https://opensource.org/license/mit).
2. Inventory the exact versions in the shipped artifact. The current build references SSH.NET and resolves Bouncy Castle as a transitive dependency. Bootstrap is vendored. Self-contained Windows builds also bundle .NET runtime components. Review notices from the actual versions and include required notices in each artifact. Keep test and website dependencies in their respective inventories. [SSH.NET license](https://github.com/sshnet/SSH.NET/blob/develop/LICENSE) · [Bouncy Castle license](https://github.com/bcgit/bc-csharp/blob/master/LICENSE.md) · [Bootstrap license](https://github.com/twbs/bootstrap/blob/main/LICENSE) · [.NET publishing modes](https://learn.microsoft.com/en-us/dotnet/core/deploying/).
3. Build a clean publication copy of the source. Base the source import on the existing repository history and preserve its license. Use an explicit file inventory. Check build inputs, package output, test fixtures, docs, screenshots and commit metadata for real hosts, usernames, certificates, SSH material, passwords, production app names, raw logs and paths that identify the owner. Keep `.manager-state`, `.vs`, build output and runtime data outside the public tree. Open the actual ZIP or installer payload as a final check.
4. Have a second reviewer reproduce the build from the clean snapshot. Use only sample hosts such as `user@server.example.com` and a disposable lab server in instructions. Record release approval, inventory and results in the milestone.

## Reviews and automation

- Require a pull request and passing build, test and site checks for `main`. Require a second reviewer for the security boundary and release workflow. Set maintainers as code owners for SSH, authorization, command policies and packaging. Protect release tags and require MFA for organization owners. [Protected branches](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches).
- Run .NET build and unit/integration tests on Windows, add a Linux build/test job where compatible, scan dependencies and secrets, and build the site with broken-link checks. Pull requests use read-only tokens and dummy commands or SSH fixtures. Run live actions only against a named disposable lab. Limit package publishing and site deployment to reviewed workflows. Pin third-party actions to full commit SHAs and scope permissions by job. [GitHub Actions secure use](https://docs.github.com/en/actions/reference/security/secure-use).
- Enable private vulnerability reporting and keep an alternate monitored security contact. Give public support forms a field for app, Windows and Ubuntu versions, SSH mode, safe reproduction and expected result. Ask for sanitized diagnostics. Target an initial response to private reports within two business days and publish supported-version policy on the Security page. [Private vulnerability reporting](https://docs.github.com/en/code-security/how-tos/report-and-fix-vulnerabilities/configure-vulnerability-reporting/configure-for-a-repository).
- Start by supporting the latest stable release, with preview builds labeled for evaluation. For a security fix, identify affected versions, publish a fixed build and give clear upgrade steps. Review support for prior release lines as adoption grows.
- Track build inputs with a pinned SDK policy, reviewed dependencies and a lock file strategy. Produce an SBOM for the shipped Windows build that covers runtime and vendored components. Use the GitHub dependency export for the repository view and verify package coverage separately. [GitHub SBOM export](https://docs.github.com/en/code-security/how-tos/secure-your-supply-chain/establish-provenance-and-integrity/export-dependencies-as-sbom).

## Release sequence

| Milestone | Release contents | Approval |
| --- | --- | --- |
| Source preview | Reviewed, licensed source, repeatable .NET 10 build, source Quickstart and mock-backed tests | Clean snapshot review and documented host prerequisites |
| Windows preview | Windows x64 package with a visible start/quit flow, matching docs and a known-issues list | Disposable Ubuntu smoke test plus checksums and verified download |
| Stable `v1.0.0` | Signed Windows x64 download, installer or launch shortcut, checksums, release notes, upgrade and removal guide, support matrix | Full [release checklist](08-execution-and-checklist.md), lab results and preview usability target |

Use tags such as `v0.1.0-preview.1` and `v1.0.0`. For stable, prefer a self-contained Windows x64 package so the .NET SDK and separate runtime setup are unnecessary for end users. Verify start, update, exit, local TLS trust and removal from a clean non-admin Windows account. Keep the package unpacked or installed in a user-writable program location separate from private state. Use a visible signed launcher and a localhost certificate strategy with a clearly scoped trust prompt and cleanup. Check the package’s Windows download and execution experience on fresh machines, including any trust prompts. [Semantic Versioning](https://semver.org/) · [.NET publishing](https://learn.microsoft.com/en-us/dotnet/core/deploying/) · [GitHub Releases](https://docs.github.com/en/repositories/releasing-projects-on-github/about-releases).

For each downloadable release, publish the package, SHA-256 checksums, applicable signatures, build provenance, dependency inventory and a short list of changes. Download it as a user would, verify the signature and hash, and compare the version in the app to the release tag. Document which updates preserve state and how to recover a pre-upgrade backup. Keep the prior safe release available for rollback. If an asset is compromised or unsafe, mark the affected release and publish an advisory and a replacement through the reviewed workflow.

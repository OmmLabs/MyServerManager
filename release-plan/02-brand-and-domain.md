# Name, sponsor and domain

## Public identity

| Item | Choice or proposal |
| --- | --- |
| Product | **Server App Manager** |
| Site title descriptor | **Ubuntu Service Manager over SSH** |
| Tagline | **Manage Ubuntu apps and services from your PC over SSH.** |
| Home page promise | See what is running, check logs and memory, and manage your apps over the SSH connection you already use. |
| Sponsor line | **Sponsored by Omm Rudraksha** |
| Public site | `https://serverappmanager.ommrudraksha.com` |
| Public GitHub repository | [OmmLabs/MyServerManager](https://github.com/OmmLabs/MyServerManager), supplied by the project owner |
| Solution | `ServerAppManager.slnx` |
| Application and test projects | `src/MyServerManager/ServerAppManager.csproj` and `tests/MyServerManager.Tests/ServerAppManager.Tests.csproj` |
| Release artifact | `ServerAppManager-1.0.0-win-x64.zip` or a signed Windows installer with the same version |

The name describes the day-to-day task in plain language. Use the subtitle to explain that the app runs on the operator's PC and connects over SSH. The repository keeps the original MyServerManager slug. Confirm the subdomain with the sponsor before publishing the website. [Omm Rudraksha](https://ommrudraksha.com/).

## Brand and ownership

- Put the product name first on the site and in the app. Show sponsor credit in the site footer and About page, in the repository README and in the app’s About screen. The sponsor supplies its approved display name, logo and link.
- Use a wordmark and an icon legible at favicon size. Build the public design with the dashboard’s light palette, clear type, generous spacing and a dark option. Give links, alerts and service status labels readable contrast.
- Name the project maintainers and a backup in the repository. Publish who can approve releases and who receives security reports. Credit funding and trademarks separately from source copyright and license.
- Keep a short About page that explains why Omm Rudraksha sponsors the project, with a link to the sponsor’s site. Use the sponsor subdomain for software downloads and documentation.

## Migration plan for the name

1. Use Server App Manager in the app, solution, project filenames, documentation and website.
2. Keep the original source directory, internal namespace, assembly and local state identity for compatibility with existing commands, saved registrations and direct-hosting templates.
3. Test an upgrade using a copy of old state, a fresh start and rollback before stable release.
4. Use the public product name in the future Windows installer and document how to start, quit, update and remove it.

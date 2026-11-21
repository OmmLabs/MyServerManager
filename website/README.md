# Server App Manager website

This Docusaurus site serves the public project pages, guides, blog, RSS and local search index. GitHub holds the reviewed Markdown. See the [editorial workflow](../release-plan/09-website-content-workflow.md) for the private SQLite option and article briefs.

## Preview on your PC

Use Node.js 24 and run these commands from this folder.

~~~powershell
npm ci
npm start -- --host localhost
~~~

Open the address printed in the terminal. The default preview address is [localhost port 3000](http://localhost:3000/). Use `npm run build` to check links and build the static site. To check that production build locally, run `npm run serve -- --host localhost`.

The npm manifest pins patched versions of Tinypool, Serialize JavaScript and the UUID package used by SockJS. Recheck the production build and `npm audit --omit=dev --audit-level=high` when updating Docusaurus.

## Publish after source review

1. Select GitHub Actions as the Pages source in [OmmLabs/MyServerManager](https://github.com/OmmLabs/MyServerManager).
2. Verify the sponsor domain and configure the dedicated subdomain. Set the repository variable `SERVER_APP_MANAGER_SITE_URL` to the confirmed HTTPS origin with no path. The site builds at `/` on that domain.
3. Review the preview and enable `SERVER_APP_MANAGER_INDEXING` by setting the repository variable to `true` when the site is ready for indexing. This also generates the public sitemap.
4. Run the **Project website** workflow from `main` and check the home page, docs, blog, search, download links, sitemap and feed. Enable HTTPS enforcement in Pages.

Source and release links point to [OmmLabs/MyServerManager](https://github.com/OmmLabs/MyServerManager) in local previews. GitHub Actions supplies the repository name during CI, which also supports forks. The public site reads reviewed site content from this folder. The dashboard opens on its own local address.

[Docusaurus deployment](https://docusaurus.io/docs/deployment) · [GitHub Pages custom domain](https://docs.github.com/en/pages/configuring-a-custom-domain-for-your-github-pages-site/managing-a-custom-domain-for-your-github-pages-site).

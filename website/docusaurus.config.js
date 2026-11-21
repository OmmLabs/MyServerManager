import {writeFile} from 'node:fs/promises';
import {join} from 'node:path';

const repository = process.env.GITHUB_REPOSITORY ?? 'OmmLabs/MyServerManager';
const repositoryUrl = /^[a-z\d_.-]+\/[a-z\d_.-]+$/i.test(repository)
  ? `https://github.com/${repository}`
  : null;

export default {
  title: 'Server App Manager',
  tagline: 'Manage Ubuntu apps and services from your PC over SSH.',
  url: process.env.SERVER_APP_MANAGER_SITE_URL ?? 'https://serverappmanager.ommrudraksha.com',
  baseUrl: '/',
  favicon: 'img/server-mark.svg',
  trailingSlash: true,
  noIndex: process.env.SERVER_APP_MANAGER_INDEXING !== 'true',
  onBrokenLinks: 'throw',
  markdown: {hooks: {onBrokenMarkdownLinks: 'throw'}},
  customFields: {repositoryUrl},
  i18n: {defaultLocale: 'en', locales: ['en']},
  presets: [
    [
      'classic',
      {
        docs: {sidebarPath: './sidebars.js'},
        blog: {
          blogTitle: 'Server App Manager Blog',
          blogDescription: 'Practical guides for Ubuntu and .NET app operations.',
          showReadingTime: true,
          feedOptions: {type: ['rss', 'atom']},
          onInlineTags: 'throw',
          onInlineAuthors: 'throw',
          onUntruncatedBlogPosts: 'throw',
        },
        theme: {customCss: './src/css/custom.css'},
        sitemap: {ignorePatterns: ['/search/**', '/blog/tags/**']},
      },
    ],
  ],
  plugins: [
    ['@easyops-cn/docusaurus-search-local', {hashed: true, indexPages: true}],
    function robots() {
      return {
        name: 'server-app-manager-robots',
        async postBuild({outDir, siteConfig}) {
          const directives = ['User-agent: *', 'Allow: /'];
          if (!siteConfig.noIndex) {
            directives.push(`Sitemap: ${new URL('sitemap.xml', siteConfig.url).href}`);
          }
          await writeFile(join(outDir, 'robots.txt'), directives.join('\n') + '\n');
        },
      };
    },
  ],
  themeConfig: {
    image: 'img/share-card.png',
    colorMode: {defaultMode: 'light', respectPrefersColorScheme: false},
    navbar: {
      title: 'Server App Manager',
      logo: {src: 'img/server-mark.svg', alt: ''},
      items: [
        {to: '/features/', label: 'Features', position: 'left'},
        {to: '/download/', label: 'Download', position: 'left'},
        {to: '/docs/', label: 'Docs', position: 'left'},
        {to: '/blog/', label: 'Blog', position: 'left'},
        {to: '/community/', label: 'Community', position: 'left'},
        ...(repositoryUrl ? [{href: repositoryUrl, label: 'GitHub', position: 'right'}] : []),
      ],
    },
    footer: {
      style: 'dark',
      links: [
        {
          title: 'Use the project',
          items: [
            {label: 'Quickstart', to: '/docs/quickstart/'},
            {label: 'Compatibility', to: '/docs/compatibility/'},
            {label: 'Release notes', to: '/releases/'},
            {label: 'FAQ', to: '/faq/'},
          ],
        },
        {
          title: 'Project',
          items: [
            {label: 'About', to: '/about/'},
            {label: 'Security', to: '/security/'},
            {label: 'Privacy', to: '/privacy/'},
            // Keep the static XML URL separate from Docusaurus page routes.
            {html: '<a class="footer__link-item" href="/blog/rss.xml">Blog RSS</a>'},
          ],
        },
        {
          title: 'Sponsor',
          items: [{label: 'Omm Rudraksha', href: 'https://ommrudraksha.com/'}],
        },
      ],
      copyright: 'Sponsored by Omm Rudraksha',
    },
  },
};

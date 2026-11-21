# Search and site performance

Use plain language for people searching for a solution. A person who searches for “Ubuntu service manager over SSH” should land on the overview. A person debugging an Nginx 502 should find a tested troubleshooting article. Treat the following terms as hypotheses, then check Search Console for actual queries after launch. [Google SEO Starter Guide](https://developers.google.com/search/docs/fundamentals/seo-starter-guide) · [People-first content](https://developers.google.com/search/docs/fundamentals/creating-helpful-content).

## Page and query map

| Page | Main search intent | Example title |
| --- | --- | --- |
| `/` | Ubuntu service manager over SSH | Server App Manager · Ubuntu Service Manager over SSH |
| `/download/` | Download Server App Manager for Windows | Download Server App Manager for Windows |
| `/docs/quickstart/` | Manage Ubuntu services from Windows | Quickstart · Manage Ubuntu Services from Windows |
| `/docs/applications/` | Discover and manage ASP.NET Core systemd services | Discover and Manage .NET Services on Ubuntu |
| `/docs/metrics/` | Understand systemd memory, CPU time and server metrics | Service Memory and Server Metrics in Server App Manager |
| `/docs/logs/` | View systemd logs in Server App Manager | View and Filter Service Logs in Server App Manager |
| `/docs/compatibility/` | Server App Manager supported Windows and Ubuntu versions | Server App Manager Compatibility and Prerequisites |
| `/docs/nginx/` | Safely test and reload Nginx | Check and Reload Nginx in Server App Manager |
| `/docs/sql-server/` | Manage SQL Server service on Ubuntu | SQL Server Service Monitoring and Restart |
| `/blog/` | Practical Ubuntu app administration guides | Server App Manager Blog · Ubuntu and .NET Operations |

Homepage description proposal. “Manage Ubuntu apps, systemd services and logs from your Windows PC over existing SSH. Download Server App Manager and see the supported setup.”

Write a distinct description and one page-level heading for each core page. Give every article a durable slug, one primary question and useful links into the docs. Link from a guide to related troubleshooting posts when they add worked examples. For instance, the metrics guide defines how the UI calculates memory, and the blog shows how to investigate a sudden change. [Article brief and query list](07-blog-50-articles.md).

## Before publishing

- Use HTTPS and one canonical host. Make internal links, XML sitemap entries, canonical tags and the blog feed use the same absolute site origin and chosen trailing-slash pattern. Validate the built HTML and exclude search results, preview pages and duplicate archives from the indexing plan. [Google canonicalization guidance](https://developers.google.com/search/docs/crawling-indexing/consolidate-duplicate-urls) · [Docusaurus sitemap plugin](https://docusaurus.io/docs/api/plugins/@docusaurus/plugin-sitemap).
- Publish `robots.txt` and `sitemap.xml` for the public site. Add the sitemap reference to robots and submit it in Search Console. Build private previews behind access control. Use staging `noindex` where a publicly accessible preview is required and keep those URLs out of the sitemap. Keep the default site output indexable on the approved domain. [Google sitemap documentation](https://developers.google.com/search/docs/crawling-indexing/sitemaps/build-sitemap).
- Keep URLs stable. If a public slug must change, use a permanent redirect where the hosting layer supports it and verify the old URL over HTTP. For Pages, plan the redirect mechanism before changing links. Update internal links, canonicals and the sitemap at the same time.
- Add readable social card images, accessible image text, an actual byline and visible publish or update dates. Use `BlogPosting` and `BreadcrumbList` structured data when the markup describes what readers see. Consider software application markup after checking eligibility and available data. Avoid invented reviews or ratings. [Google Article data](https://developers.google.com/search/docs/appearance/structured-data/article) · [Breadcrumbs](https://developers.google.com/search/docs/appearance/structured-data/breadcrumb) · [Software application requirements](https://developers.google.com/search/docs/appearance/structured-data/software-app).
- Compress screenshots, set image dimensions, defer below-the-fold images, keep the demo video user-initiated and use a small set of fonts. Run Lighthouse on mobile for Home, Download, Quickstart and a blog article. Target a median mobile performance score of at least 90 across three runs per page. Once field data exists, target 75th-percentile LCP ≤ 2.5 s, INP ≤ 200 ms and CLS ≤ 0.1. [Core Web Vitals](https://web.dev/articles/vitals).
- Verify the subdomain in Search Console using an approved method. Check Home, Download, Quickstart, Security and the first eight articles with URL Inspection. Record results and repair indexing errors. A submitted sitemap helps discovery. Track actual indexing in Search Console. [Verify site ownership](https://support.google.com/webmasters/answer/9008080) · [Google sitemaps](https://developers.google.com/search/docs/crawling-indexing/sitemaps/build-sitemap).

## First 90 days

| Time | Measure | Change based on results |
| --- | --- | --- |
| Launch week | HTTP and HTTPS responses, sitemap, canonical tags, feed, URL Inspection, mobile build and link checks | Fix broken URLs, unintended indexing restrictions and download failures |
| Weekly for one month | Queries, clicks, impressions, indexed pages and the top troubleshooting searches | Clarify titles and add examples to pages that earn impressions for relevant questions |
| Monthly after that | Organic clicks, page-level changes and GitHub asset counts | Improve the paths from helpful articles to the relevant docs and from Quickstart to Download |
| Day 90 | Compare actual results with the [marketing targets](05-marketing.md) | Pick the next topics from real support questions and Search Console queries |

Use each post to solve a concrete problem with a tested example, a verified version and a useful next link. Seek relevant references through community contributions and credible documentation. Treat rankings, search traffic and rich results as outcomes to measure. [Search Console Performance report](https://support.google.com/webmasters/answer/7576553) · [Google helpful content guidance](https://developers.google.com/search/docs/fundamentals/creating-helpful-content).

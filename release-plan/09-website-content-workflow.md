# Website stack and article workflow

## What runs where

| Part | Choice | What it does |
| --- | --- | --- |
| Public website and blog | **Docusaurus** on **GitHub Pages** | Builds the public site, docs, blog, feed and search index from Markdown and static assets. Serve the built pages on the proposed sponsor subdomain. |
| Editing and publishing | **GitHub and Markdown** | The editor can write in an editor or use GitHub’s web editor. A pull request gives us a place to review the article and check the site build. Merge reviewed content to publish it through the site workflow. |
| Private draft database | **SQLite** on the editor’s PC | An optional local file for the 50 briefs and their draft Markdown. Use the supplied SQL scripts to initialize it or work directly from the CSV. |
| Server App Manager dashboard | **ASP.NET Core MVC on .NET 10** | The existing dashboard runs on the user’s PC, connects to Ubuntu over SSH and keeps its operational state in local files. |
| Ubuntu SQL Server | Existing managed service | The dashboard can inspect and operate the systemd service with existing permissions. The project website and editorial workflow are separate from it. |

The public site can launch with static hosting and no dedicated CMS or database server. GitHub holds the published Markdown. The optional SQLite file holds private drafts. This keeps the editorial workflow independent of the dashboard and lets the site serve content without an app server. Revisit a dedicated CMS if editors need separate accounts, scheduled publishing or a richer approval screen. [Docusaurus blog](https://docusaurus.io/docs/blog) · [GitHub Pages](https://docs.github.com/en/pages/getting-started-with-github-pages/what-is-github-pages) · [GitHub’s file editor](https://docs.github.com/en/repositories/working-with-files/managing-files/editing-files) · [SQLite uses](https://www.sqlite.org/whentouse.html).

## Article files

| File | What to do with it |
| --- | --- |
| [article-briefs.csv](blog-data/article-briefs.csv) | Open as UTF-8 in Excel or another spreadsheet, or read it with a CSV parser. One row supplies one article’s writing brief. |
| [01-create-blog-tables.sql](blog-data/01-create-blog-tables.sql) | Create the `article_briefs` and `article_drafts` tables in a private SQLite file. |
| [02-load-article-briefs.sql](blog-data/02-load-article-briefs.sql) | Load all 50 briefs from the same data as the CSV. Existing brief IDs are preserved when you rerun it. |
| [03-example-draft-insert.sql](blog-data/03-example-draft-insert.sql) | See how to save generated Markdown for an article. It inserts a short example for article 1 as a draft. |

The CSV is the brief catalog. If you revise it later, carry the change into any SQLite copy you are using. Each CSV row includes the following groups of fields.

| CSV fields | Use |
| --- | --- |
| `article_id`, `category`, `publication_week`, `publication_position` | Identify the brief and sort it into the schedule. Week 0 is launch week. |
| `title`, `slug`, `primary_keyword`, `meta_description` | Set the title, planned URL, main search intent and page description. |
| `min_words`, `target_words`, `max_words` | Write **1,200–2,000 words** and aim near the individual target. Count body prose and headings. Exclude front matter, fenced code and bare source URLs. |
| `reader_outcome`, `outline`, `lab_example` | Define what the reader can do after reading, five sections to cover and a worked example to reproduce. |
| `source_1_url` through `source_3_use` | Read three primary references and use each for the facts named in the corresponding `use` field. |
| `project_references`, `site_links` | Read the existing repository files for product behavior. Use the planned public paths for internal links once those pages exist. |
| `review_notes` | Check the release-specific behavior, lab prerequisites and limits before publishing. |

The eight launch articles represent **9,600–16,000 words**. The full 50 represent **60,000–100,000 words**. Start with the launch rows and use the [publishing calendar](07-blog-50-articles.md#publishing-calendar) to queue the rest.

## Generate one article

1. Open the CSV and select one row. Start with `article_id` 1.
2. Open its three upstream sources and the local files under `project_references`. Check feature behavior against the release you are writing about. Verify the lab example and collect safe sample output.
3. Pass the row and relevant verified facts to your writer or generator. Use the following prompt as a starting point.

~~~text
Write one Server App Manager article from the CSV row below. Write 1,200–2,000 words of
body prose and aim for the row's target_words. Use its five outline sections,
reader outcome and lab example. Start with the problem and what the reader
will accomplish. Explain each step, what to expect and how to tell it worked.

Use each source for the purpose listed in its source_1_use, source_2_use or
source_3_use field. Link to the
supporting primary source near the relevant claim. Use project_references
for dashboard behavior and site_links for links that exist in the site build.
Keep examples limited to a disposable lab, sample service names and example
addresses. Apply review_notes and distinguish current features from planned work.

Return Markdown with title, slug and description front matter. Leave the
publishing date and author for the editor. Mark any missing evidence as
[Verify in lab] and keep it visible for the technical reviewer.

CSV row and verified lab notes
[paste one row and any verified observations here]
~~~

4. Check technical claims, commands, example output, links, accessibility and word count. Replace review markers with verified information or defer that section. Ask a reviewer to reproduce the important steps.
5. After approval, use a dated file such as `website/blog/YYYY-MM-DD-manage-ubuntu-services-from-windows.md`. Fill in the actual publication date and registered author and tag keys. Keep the slug stable. Commit the reviewed Markdown and screenshots, then run the Docusaurus build and link checks before publishing. [Docusaurus blog files and front matter](https://docusaurus.io/docs/blog) · [Server App Manager SEO plan](06-seo.md).

A 1,500-word draft can use roughly 150 words to set up the problem, 240 words for each of the five sections, and 150 words for the verification or next step. Give longer articles space for the lab and diagnostic examples.

## Optional SQLite setup on Windows

The scripts use SQLite 3.37 or newer. Open PowerShell in the repository folder that contains `release-plan`. Run these commands to put the database in your Windows user profile and load the catalog. If you already created an editorial database, point the variable to that file to keep your drafts. [SQLite CLI](https://www.sqlite.org/cli.html) · [SQLite strict tables](https://www.sqlite.org/stricttables.html).

~~~powershell
$serverAppEditorialDb = Join-Path $env:LOCALAPPDATA 'ServerAppManager-Editorial.sqlite'
sqlite3 -bail $serverAppEditorialDb ".read release-plan/blog-data/01-create-blog-tables.sql" ".read release-plan/blog-data/02-load-article-briefs.sql"
sqlite3 $serverAppEditorialDb "SELECT COUNT(*) FROM article_briefs;"
~~~

The count should be **50** for a fresh database. To read a complete brief in PowerShell and try the optional sample insert, run:

~~~powershell
sqlite3 -line $serverAppEditorialDb "SELECT * FROM article_briefs WHERE article_id = 1;"
sqlite3 -bail $serverAppEditorialDb ".read release-plan/blog-data/03-example-draft-insert.sql"
sqlite3 -header -column $serverAppEditorialDb "SELECT article_id, body_word_count, status FROM article_drafts;"
~~~

`article_drafts` holds one working draft per article. To save your own draft, copy the sample insert and replace its article ID, paragraph and word count. Inside a SQL string, double embedded apostrophes, as in `the server''s fingerprint`. When writing from code, bind the Markdown as a parameter. Use `draft` while writing, `in_review` for review and `approved` after the checks. The sample insert preserves an existing draft for that ID. [SQLite insert conflict handling](https://www.sqlite.org/lang_upsert.html).

Keep the SQLite file in your user profile and back it up with your private work. Approval in SQLite is an editorial marker. To publish, put the approved Markdown in the site and merge the reviewed GitHub change.

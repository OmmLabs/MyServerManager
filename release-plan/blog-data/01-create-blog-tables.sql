-- Server App Manager private editorial workspace. Requires SQLite 3.37 or later.
PRAGMA foreign_keys = ON;

BEGIN TRANSACTION;

CREATE TABLE IF NOT EXISTS article_briefs (
    article_id INTEGER PRIMARY KEY CHECK (article_id > 0),
    category TEXT NOT NULL,
    publication_week INTEGER NOT NULL CHECK (publication_week >= 0),
    publication_position INTEGER NOT NULL CHECK (publication_position > 0),
    title TEXT NOT NULL,
    slug TEXT NOT NULL UNIQUE,
    primary_keyword TEXT NOT NULL,
    meta_description TEXT NOT NULL,
    min_words INTEGER NOT NULL,
    target_words INTEGER NOT NULL,
    max_words INTEGER NOT NULL,
    reader_outcome TEXT NOT NULL,
    outline TEXT NOT NULL,
    lab_example TEXT NOT NULL,
    source_1_url TEXT NOT NULL,
    source_1_use TEXT NOT NULL,
    source_2_url TEXT NOT NULL,
    source_2_use TEXT NOT NULL,
    source_3_url TEXT NOT NULL,
    source_3_use TEXT NOT NULL,
    project_references TEXT NOT NULL,
    site_links TEXT NOT NULL,
    review_notes TEXT NOT NULL,
    UNIQUE (publication_week, publication_position),
    CHECK (min_words >= 1200 AND max_words <= 2000 AND target_words BETWEEN min_words AND max_words)
) STRICT;

CREATE TABLE IF NOT EXISTS article_drafts (
    article_id INTEGER PRIMARY KEY REFERENCES article_briefs (article_id),
    markdown_body TEXT NOT NULL CHECK (length(trim(markdown_body)) > 0),
    body_word_count INTEGER NOT NULL CHECK (body_word_count > 0),
    status TEXT NOT NULL DEFAULT 'draft' CHECK (status IN ('draft', 'in_review', 'approved')),
    created_at_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CHECK (status <> 'approved' OR body_word_count BETWEEN 1200 AND 2000)
) STRICT;

COMMIT;

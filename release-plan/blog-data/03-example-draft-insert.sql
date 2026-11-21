-- Optional insert example for article 1 after creating and loading the tables.
-- Replace the sample paragraph and word count with your generated Markdown and its actual count.
PRAGMA foreign_keys = ON;

INSERT INTO article_drafts (article_id, markdown_body, body_word_count)
VALUES (
    1,
    'Server App Manager runs on your Windows PC and connects to Ubuntu over SSH. Before you start, confirm that your SSH account works and obtain the server fingerprint from a trusted source.',
    30
)
ON CONFLICT (article_id) DO NOTHING;

# Wiki Lint Report - 2026-10-27

Scope: full mechanical pass after the F005 batch D closeout ingest (branch `docs/005-batchD-closeout`). Config: `lint.stale_after_days` 90, `lint.auto_fix` index-and-links.

Mechanical checks: `index-drift` 0 (every page in `pages/` is listed in INDEX; no dead index lines). `links` 0 (no broken relative links, no unknown S-ids; S034-S037 are registered). `orphans` 0 (the four new batch D pages are linked from each other and from batch B/C pages).

Semantic checks: `contradictions` 1 pre-existing marker in `ui-toolkit-hud-layout-pitfalls.md` (unchanged by batch D). `citations`: new batch D claims all carry S-ids. `stale`: no page is older than 90 days; the earlier batch C finding that pages predate re-ingested source S022 is partly cleared for the pages touched in batch D, other pages still predate the re-ingest and are not rewritten by lint.

Size: `player-level-test-gaps.md` (about 913 words) and `android-emulator-validation.md` (about 1069 words) exceed `page_max_words` 600; this predates batch D (a bullet was appended to each). Split in a later ingest.

| # | Check | Severity | Page | Finding | Suggested fix |
|---|-------|----------|------|---------|---------------|
| 1 | contradictions | semantic | ui-toolkit-hud-layout-pitfalls.md | unresolved `> conflict:` marker (pre-existing) | resolve by editing the page |
| 2 | size | structural | player-level-test-gaps.md, android-emulator-validation.md | over 600 words | split in a later ingest |

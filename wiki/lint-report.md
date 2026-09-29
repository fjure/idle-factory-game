# Wiki Lint Report — 2026-09-29

This is a full pass over 23 pages, run after the S018 ingest (Feature 003 plan: `specs/003-first-factory-prestige`).

| Check | Result |
|---|---|
| index-drift | 0. Every page in `pages/` appears in `INDEX.md`, and every index line resolves. |
| links | 0 dead relative links and 0 unknown source IDs. S001–S018 are all registered. |
| orphans | 0. Each of the 4 new S018 pages has inbound links from existing pages. |
| contradictions | 0. There are no `⚠ conflict` markers. The S018 pages mark the Feature 001 buffer model as *superseded for gameplay (planned)* rather than contradicting S001. |
| stale | 0. Every page is dated within 90 days. |
| citations | 3 low-severity style notes (below). The 6 uncited lead-in lines on the new S018 pages were fixed during this ingest. |
| size | 0 pages over 600 words. The largest are `unity-presentation-boundary.md` (589) and `mobile-performance-targets.md` (587), both close to the cap. |

| # | Check | Severity | Page | Finding | Suggested fix |
|---|-------|----------|------|---------|---------------|
| 1 | citations | semantic (low) | android-runtime-rotation.md | The lead-in line "Rejected alternatives:" has no citation of its own. The list after it ends with (S001). | Append (S001) to the lead-in on the next S001 re-ingest. |
| 2 | citations | semantic (low) | android-emulator-profiling.md | The numbered procedure "1. force-stops the app …" has no citation. The paragraph that follows cites (S001). | Append (S001) to the list lead-in on the next S001 re-ingest. |
| 3 | citations | semantic (low) | json-content-migration-evidence.md | The ordered list after "`Initialize()` runs its steps in this order:" has no citation. The paragraph that follows cites (S008). | Append (S008) to the lead-in on the next S008 re-ingest. |

Mechanical fixes applied: none needed. The index and links were already consistent after the ingest.

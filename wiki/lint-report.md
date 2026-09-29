# Wiki Lint Report — 2026-09-29

Full pass over 24 pages after re-ingesting S018 (`specs/003-first-factory-prestige`, including the T093 result).

| Check | Result |
|---|---|
| index-drift | 0. All 24 pages appear in `INDEX.md`; every index link resolves. |
| links | 0 dead relative links and 0 unknown source IDs. S001–S018 are registered. |
| orphans | 0. The new bounds/offline page links to the simulation page, which links back to it. |
| contradictions | 0. There are no `⚠ conflict` markers; the new implementation notes agree with the S018 design. |
| stale | 0. Every page is dated within 90 days and no S018 page predates today's ingest. |
| citations | 3 low-severity style notes; no new uncited claims. |
| size | 0 pages over 600 words. The largest is `android-emulator-profiling.md` (593); `save-v2-and-v1-import.md` is 589. |

| # | Check | Severity | Page | Finding | Suggested fix |
|---|---|---|---|---|---|
| 1 | citations | semantic (low) | android-runtime-rotation.md | The lead-in line "Rejected alternatives:" has no citation of its own. The list after it ends with (S001). | Append (S001) to the lead-in on the next S001 re-ingest. |
| 2 | citations | semantic (low) | android-emulator-profiling.md | The numbered procedure "1. force-stops the app …" has no citation. The paragraph that follows cites (S001). | Append (S001) to the list lead-in on the next S001 re-ingest. |
| 3 | citations | semantic (low) | json-content-migration-evidence.md | The ordered list after "`Initialize()` runs its steps in this order:" has no citation. The paragraph that follows cites (S008). | Append (S008) to the lead-in on the next S008 re-ingest. |

Mechanical fixes applied: the index now includes the new S018 page. Links and citations required no mechanical repair. The over-limit simulation page was split into linked simulation and offline-bounds pages during the S018 ingest.

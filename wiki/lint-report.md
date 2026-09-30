# Wiki Lint Report — 2026-09-30

Full pass over 31 pages after re-ingesting S018 (`specs/003-first-factory-prestige`, including the T103 result).

| Check | Result |
|---|---|
| index-drift | 0. All 31 pages appear in `INDEX.md`; every index link resolves. |
| links | 0 dead relative links and 0 unknown source IDs. S001–S018 are registered. |
| orphans | 0. Every page has at least one inbound page link. |
| contradictions | 0. There are no `⚠ conflict` markers. The T103 section on `factory-run-bounds-and-offline-segmentation.md` states `startUtcMs = d` with earlier boundaries at 0, which refines the earlier handoff wording ("at or after every processed boundary") without contradicting the Advance contract. |
| stale | 0 open. |
| citations | 3 low-severity style notes carried over; no new uncited claims. |
| size | 0 pages over 600 words; `save-v2-and-v1-import.md` is 589 and `factory-run-bounds-and-offline-segmentation.md` 551. |

| # | Check | Severity | Page | Finding | Suggested fix |
|---|---|---|---|---|---|
| 1 | citations | semantic (low) | android-runtime-rotation.md | The lead-in line "Rejected alternatives:" has no citation of its own. The list after it ends with (S001). | Append (S001) to the lead-in on the next S001 re-ingest. |
| 2 | citations | semantic (low) | android-emulator-profiling.md | The numbered procedure "1. force-stops the app …" has no citation. The paragraph that follows cites (S001). | Append (S001) to the list lead-in on the next S001 re-ingest. |
| 3 | citations | semantic (low) | json-content-migration-evidence.md | The ordered list after "`Initialize()` runs its steps in this order:" has no citation. The paragraph that follows cites (S008). | Append (S008) to the lead-in on the next S008 re-ingest. |

Mechanical fixes applied: none needed. The ingest updated the index line for `factory-run-bounds-and-offline-segmentation.md`.

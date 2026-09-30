# Wiki Lint Report — 2026-09-30

Full pass over 33 pages after re-ingesting S018 (`specs/003-first-factory-prestige`, including the T138 result).

| Check | Result |
|---|---|
| index-drift | 0. All 33 pages appear in `INDEX.md`; every index link resolves. |
| links | 0 dead relative links and 0 unknown source IDs. S001–S018 are registered. |
| orphans | 0. `run-persistence.md` is linked from `save-v2-document.md` and `save-v2-and-v1-import.md`. |
| contradictions | 0. There are no `⚠ conflict` markers. `save-v2-and-v1-import.md` now names data or manifest files for the never-import rule and states that a lone pending file counts as none, matching the amended contract (owner decision 2026-09-30). |
| stale | 0 open. |
| citations | 3 low-severity style notes carried over; no new uncited claims. |
| size | 0 pages over 600 words; `save-v2-and-v1-import.md` is 586 and near the cap, so the next addition should go to `run-persistence.md`. |

| # | Check | Severity | Page | Finding | Suggested fix |
|---|---|---|---|---|---|
| 1 | citations | semantic (low) | android-runtime-rotation.md | The lead-in line "Rejected alternatives:" has no citation of its own. The list after it ends with (S001). | Append (S001) to the lead-in on the next S001 re-ingest. |
| 2 | citations | semantic (low) | android-emulator-profiling.md | The numbered procedure "1. force-stops the app …" has no citation. The paragraph that follows cites (S001). | Append (S001) to the list lead-in on the next S001 re-ingest. |
| 3 | citations | semantic (low) | json-content-migration-evidence.md | The ordered list after "`Initialize()` runs its steps in this order:" has no citation. The paragraph that follows cites (S008). | Append (S008) to the lead-in on the next S008 re-ingest. |

Mechanical fixes applied: none needed. The ingest added the index line for the new `run-persistence.md`.

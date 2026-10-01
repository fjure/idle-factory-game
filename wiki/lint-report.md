# Wiki Lint Report — 2026-10-01

Full pass over 33 pages after re-ingesting S018 (`specs/003-first-factory-prestige`, including the T105 result).

| Check | Result |
|---|---|
| index-drift | 0. All 33 pages appear in `INDEX.md`; every index link resolves. The lines for `run-prestige-transition.md` and `run-persistence.md` were updated. |
| links | 0 dead relative links and 0 unknown source IDs. S001–S018 are registered. |
| orphans | 0. |
| contradictions | 0. There are no `⚠ conflict` markers. The T104 "open owner question" on rule-4 durations and the T103 "recovery belongs to T105" statement were replaced by the owner decisions of 2026-10-01 (duration cap, value-limit forfeit). |
| stale | 0 open. |
| citations | 3 low-severity style notes carried over; no new uncited claims. |
| size | 0 pages over 600 words; `save-v2-and-v1-import.md` (586) and `factory-run-bounds-and-offline-segmentation.md` (572) are near the cap. |

| # | Check | Severity | Page | Finding | Suggested fix |
|---|---|---|---|---|---|
| 1 | citations | semantic (low) | android-runtime-rotation.md | The lead-in line "Rejected alternatives:" has no citation of its own. The list after it ends with (S001). | Append (S001) to the lead-in on the next S001 re-ingest. |
| 2 | citations | semantic (low) | android-emulator-profiling.md | The numbered procedure "1. force-stops the app …" has no citation. The paragraph that follows cites (S001). | Append (S001) to the list lead-in on the next S001 re-ingest. |
| 3 | citations | semantic (low) | json-content-migration-evidence.md | The ordered list after "`Initialize()` runs its steps in this order:" has no citation. The paragraph that follows cites (S008). | Append (S008) to the lead-in on the next S008 re-ingest. |

Mechanical fixes applied: none needed. The ingest updated two index lines.

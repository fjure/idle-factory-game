# Wiki Lint Report — 2026-09-30

Full pass over 25 pages after re-ingesting S018 (`specs/003-first-factory-prestige`, including the T095 result and the R03 bound correction).

| Check | Result |
|---|---|
| index-drift | 0. All 25 pages appear in `INDEX.md`; every index link resolves. The new `run-state-domain.md` is listed under Components. |
| links | 0 dead relative links and 0 unknown source IDs. S001–S018 are registered. |
| orphans | 0. `run-state-domain.md` is linked from `factory-run-simulation.md` and links back. |
| contradictions | 0. There are no `⚠ conflict` markers. The `≤ 2 × Tmin` statement on `run-state-domain.md` matches the corrected R03 in S018. `factory-run-simulation.md` states no cycle upper bound. |
| stale | 0 open. `factory-run-simulation.md` called Feature 003 "planned, not yet implemented". The ingest updated it to name T094/T095 as implemented and T098 as pending. |
| citations | 3 low-severity style notes carried over; no new uncited claims. |
| size | 0 pages over 600 words; `factory-run-simulation.md` is about 520 and `run-state-domain.md` 471. |

| # | Check | Severity | Page | Finding | Suggested fix |
|---|---|---|---|---|---|
| 1 | citations | semantic (low) | android-runtime-rotation.md | The lead-in line "Rejected alternatives:" has no citation of its own. The list after it ends with (S001). | Append (S001) to the lead-in on the next S001 re-ingest. |
| 2 | citations | semantic (low) | android-emulator-profiling.md | The numbered procedure "1. force-stops the app …" has no citation. The paragraph that follows cites (S001). | Append (S001) to the list lead-in on the next S001 re-ingest. |
| 3 | citations | semantic (low) | json-content-migration-evidence.md | The ordered list after "`Initialize()` runs its steps in this order:" has no citation. The paragraph that follows cites (S008). | Append (S008) to the lead-in on the next S008 re-ingest. |

Mechanical fixes applied: none needed. The ingest added the index line for the new page.

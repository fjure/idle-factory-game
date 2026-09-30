# Wiki Lint Report — 2026-09-30

Full pass over 28 pages after re-ingesting S018 (`specs/003-first-factory-prestige`, including the T140 result).

| Check | Result |
|---|---|
| index-drift | 0. All 28 pages appear in `INDEX.md`; every index link resolves. The new `run-unlocks.md` is listed under Components. |
| links | 0 dead relative links and 0 unknown source IDs. S001–S018 are registered. |
| orphans | 0. `run-unlocks.md` is linked from `run-commands-and-snapshots.md` and links back. |
| contradictions | 0. There are no `⚠ conflict` markers. The pass bound N+1+U on `run-unlocks.md` extends, and does not contradict, the N+1 bound stated for timestamps without an unlock round. |
| stale | 0 open. The ingest replaced the "Unlock gap" section on `run-commands-and-snapshots.md` with a pointer to the implemented evaluation. |
| citations | 3 low-severity style notes carried over; no new uncited claims. |
| size | 0 pages over 600 words; `run-commands-and-snapshots.md` is 493 and `run-unlocks.md` 409. |

| # | Check | Severity | Page | Finding | Suggested fix |
|---|---|---|---|---|---|
| 1 | citations | semantic (low) | android-runtime-rotation.md | The lead-in line "Rejected alternatives:" has no citation of its own. The list after it ends with (S001). | Append (S001) to the lead-in on the next S001 re-ingest. |
| 2 | citations | semantic (low) | android-emulator-profiling.md | The numbered procedure "1. force-stops the app …" has no citation. The paragraph that follows cites (S001). | Append (S001) to the list lead-in on the next S001 re-ingest. |
| 3 | citations | semantic (low) | json-content-migration-evidence.md | The ordered list after "`Initialize()` runs its steps in this order:" has no citation. The paragraph that follows cites (S008). | Append (S008) to the lead-in on the next S008 re-ingest. |

Mechanical fixes applied: none needed. The ingest added the index line for the new `run-unlocks.md`.

# F007 MAQA gates (T600)

Re-verified by the parent orchestrator (Opus) on 2026-11-27 against `origin/main` `e092e318` (PR #797 merged), before batch B0 dispatch.

| Gate | Evidence | Result |
|---|---|---|
| Constitution has no template placeholders | `.specify/memory/constitution.md`: no `[UPPER_CASE]` template token found | PASS |
| `.specify/branch-convention.yml` present | file exists (591 B) | PASS |
| Wiki initialised | `wiki/INDEX.md`, `wiki/SCHEMA.md` present | PASS |
| GitHub default branch | `gh repo view fjure/idle-factory-game`: default branch `main` | PASS |
| MAQA labels | `maqa`, `maqa:in-progress`, `maqa:in-review`, `maqa:blocked` exist | PASS |
| Resource labels | `resource:unity`, `resource:emulator`, `resource:gpu` exist | PASS |
| `maqa-config.yml` holds real Unity commands | `test_command` and `test_file_command` run `scripts/test-unity.ps1` | PASS |
| `$speckit-analyze` has no open CRITICAL finding | read-only re-run (Medium tier): CRITICAL 0, HIGH 0, MEDIUM 3, LOW 4; remediation of C1, U1, G1, G2, O1 (b540f9ae) holds | PASS |
| Orchestration ledger validates for `specs/007-stylized-industrial-visual-reboot/tasks.md` | deterministic check of the validate skill's rules: 45 checklist tasks, 45 rows WU01–WU45, every task exactly once, no unknown or duplicate IDs, prerequisites only earlier units, no empty cells | PASS |

## Open non-blocking analyze findings

These do not block implementation. They are carried to the next tasks regeneration (T637) or fixed when the touched task is dispatched.

- **M1.** T604 says "after T601 and T602" and carries `[P]`. Ledger WU06 also lists WU05 (T603) as a prerequisite and as a parallel candidate. The parent treats the ledger as binding: T604 waits for T603.
- **M2.** Ledger rows WU10 (T608 after WU09), WU14 (T612 after WU13) and WU17 (T615 after WU13, WU14) serialise more than the task text says. The ledger governs.
- **M3.** FR-038's Security/Privacy omission is recorded in research.md R20, not in plan.md "Review waves and role disposition".
- **L1–L4.** Spec numbering order (FR-016a, FR-027c, A-14). Spec base commit note is still `ac49f6c8`. T641 is likely moot because F006 T546 is closed. `[P]` hints on T611 and T612.

## Tooling availability noted at B0 start

- Unity MCP: not connected (ECONNREFUSED). Unity work uses `scripts/test-unity.ps1` under the leases.
- Pixel 11 Pro: not attached. T601 and T604 are held until the owner confirms the device (2026-11-28).

Verdict: **gates PASS**; B0 (T602, T638, optional T603) may be dispatched.

# Contract: Evidence, comparison and decision records

**Scope**: FR-006, FR-011–FR-016, FR-037; SC-001–SC-010 and SC-013; research R03, R08 and R18.

## Folder layout (`specs/007-stylized-industrial-visual-reboot/reviews/`)

| Path | Content | Written by |
|---|---|---|
| `devices-f007.md` | Phone (and tablet) identity, OS, GPU, resolution, refresh rate | Parent, from owner input (M1) |
| `audit-f007.md` | Baseline captures index, metrics, constraint dispositions, problem register (R03) | Art Director, co-signed by Technical Art and UI/UX |
| `baseline-device.md` | F006 look on the phone: frame pacing, memory, sustained run (PERF-9) | Performance Engineer |
| `directions/{A,B,C}.md` | Brief, dispositions, asset list, costs, status | Art Director (brief), 3D/Render and Asset Artists (assets) |
| `rubric-anchors.md` | 1–5 anchors for each FR-037 dimension, frozen before scoring | Art Director plus UI/UX, approved by the owner |
| `comparison/` | Captures with JSON sidecars (data-model `ComparisonCapture`), sheets per class and factory state | Capture rig; sheets by script |
| `rubric/*.json` | Rubric scores, one file per reviewer and look | Independent reviewers |
| `trials-f007/` | Blind naming, silhouettes, material prompts, timed trial | Independent sessions; owner |
| `performance-m2.md` | Measured and estimated costs per direction against the frozen budgets (R17) | Performance Engineer |
| `direction-brief.md` | The M3 decision brief (constitution VII format) | Parent, with the Art Director recommendation |
| `direction-decision.md` | Owner decision record (data-model `DirectionDecisionRecord`) | Parent, quoting the owner |
| `families/<family>.md` (M5) | Before/after sheet, checks, budget result, reviews | Producing and reviewing roles |
| `final-acceptance.md` (M6) | Per-surface and per-class owner verdicts, rubric, trials, performance | Parent and owner |

## Capture rules

- Captures come only from real Unity builds or Play Mode capture rigs. A file without a JSON sidecar is not evidence.
- Concept images live only in `directions/*/concepts/`. Each is labelled `CONCEPT — not in-engine evidence`, and none is ever placed on a comparison sheet.
- **Sheet layout.** A sheet has one row per look (F006, A, B, C) and one column per geometry (same, proposed). It covers one class and one factory state. The labels state the look, geometry, class, text scale, commit and seed.
- **Comparison matrix completeness (M2 exit).** The matrix has 70 cells: 5 class variants (emuP, emuL, tabL, phoneP-card at 100 % and phoneP-card at 200 % text) × 2 factory states × 7 look and geometry columns. The columns are F006 in the same geometry only, and A, B and C each in the same and the proposed geometry. The sheet generator lists every missing cell, and M2 is not complete while any cell is missing.

## Decision brief (M3) — required sections

1. Context and constraints: spec, budgets, devices.
2. The options: A, B, C and "revise". Per option, list strengths, weaknesses, rubric results, trial results, measured or estimated performance and memory, production effort, maintainability, and fit with free building, conveyors and regions.
3. The recommendation with its reasoning, the assumptions (labelled), and the risks.
4. The Art Bible and World Bible changes the option implies.
5. The proposed production geometry and the machine-area factor.
6. The smallest set of questions for the owner.

## Independence

- A reviewer never scores or names assets they produced.
- Rubric reviewers score in separate sessions, without seeing each other's scores.
- Trial sessions get shuffled, unlabelled captures and the answer key stays hidden. This is the established `tools/art-pipeline/trials` method.

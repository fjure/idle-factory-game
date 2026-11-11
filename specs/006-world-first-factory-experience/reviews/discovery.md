# Discovery and Feasibility Reviews — Feature 006 (2026-11-11)

These read-only reviews ran before `/speckit-specify`, in parallel at the Medium tier (Claude Code `sonnet`). Each role received a compact evidence packet: the owner brief (`Spec-006-Draft.md`), the F004 final captures, the relevant wiki pages, and the targeted source paths. The parent reconciled the findings into `spec.md`. Every percentage below comes from a reviewer's estimate on screenshots, not from a measurement. Each number that later becomes an acceptance threshold is labeled as a proposal in the spec.

## Baseline the reviewers used

The baseline is the F004 final evidence on `origin/main` at `1dab60d`, where F004 is complete: 77/77 tasks, owner acceptance T447 and convergence T449. It consists of these captures:

- `specs/004-visual-identity/reviews/final-test-evidence/e01-portrait-1080x2400-factory.png`
- `e02-landscape-2400x1080-factory.png`
- `e05-portrait-slot-sheet.png`
- `e07-1280x720-first-run-hint.png`
- `e08-1280x720-sheet-open.png`
- `specs/004-visual-identity/reviews/polish-review/player-05-contracts-tab-640x480.png`

## Verdicts and key findings

| Wave | Role | Verdict | Key findings carried forward |
|---|---|---|---|
| Discovery | Game Designer / Product Owner | Proceed with changes | (1) The world is small and floats in an empty field: about 30 % of the portrait height in e01. (2) Nine identical red missing-input badges say that something is wrong but not what or where the root cause is. (3) The machine sheet is a text wall over about 60 % of the height, and Remove sits next to Upgrade. (4) Permanent chrome competes with the world: six tabs, zoom, Mine, Overdrive and the gear. Industry is hidden under Research (Play-Tester C-F1). (5) Hit diamonds measure 44.9 pt at n_min 2 (F004 G3). Story changes: promote US6 to P1, fold the state-visible part of US5 into US4, add a direction-approval gate before planning, and treat US7 as an architecture constraint. |
| Discovery | UI/UX | Proceed; direction A recommended | Persistent chrome takes about 11–36 % of the screen depending on the screen class. The world carries almost no cause information. `HandleBack` grows one branch per panel. Baseline counts: upgrade takes 3 interactions (select, Upgrade, Close); diagnosing takes 1 tap per machine, so scanning N machines takes N taps; leaving a system takes 3 interactions. Directions: **A** slim HUD + contextual machine card + collapsed systems hub (recommended); **B** radial world actions (rejected because of 44 pt at the screen edge, 200 % text and focus order); **C** auto-hiding tab bar (rejected because of discoverability). Invariants to keep: one back stack, 44 pt / 7 mm, 100–200 % text, reduced motion, empty/error/loading states, rotation cancel, exact-value line, the T249 stable layout, and slot count as a model value. |
| Discovery | Art Director | Fits in Art Bible v1.x | The background is plain `ui_bg` navy with no ground, so the factory floats. The plates are isolated diamonds with no visible flow. The badge "alarm wall" spends the colour value, and the A-03 overlap makes a badge read as part of the plate below. Progression is invisible. Directions: **A** walled hall; **B** outdoor yard (risks a major palette bump through new hues); **C** modular platform deck that grows with unlocked slots (recommended, dressed with A's foundations). Constraints: the palette is at its cap of 48, so new families reuse existing ids, plus `cue_ok`, `warn_0`, `cvd_safe`, and `res_*` behind a minor allowance edit. Layers are limited to 5 per slot and 61 per factory. The flash limit is 3 per second, and the Assembler is already at it. Reduced motion forces frame 0 and turns effects off. A bottleneck reuses the existing badge rather than adding a second alert layer. |
| Feasibility | Software Architect (+ Technical Art, Performance lenses) | Feasible with conditions | Read models already exist for per-machine phase, blocker, progress and batch (`CopyMachineStates`); rates in and out (`MachineRates`); resource coverage (`ResourceRates.SupplyCoveragePpm`); blocker cause and corrective actions (`MachineBlockerInfo`); upgrade preview and affordability; and unlock level. **Missing:** an exact per-machine completion event, because only aggregate `LastCompletions` exists; "upgrade available" and "bottleneck" are derivable but have no state; and **there is no spatial or flow topology**, because production uses one global inventory. The 12-slot limit exists only in presentation (`SlotGridView.cs:232`, `ColumnsFor` 3/4, `CameraScaleModel`, `RunCameraFraming`, `RunShellDocument.cs:633,672`); the domain `SlotCount` already comes from the catalog. The minimal seam is a world-layout abstraction (bounds, anchor, footprint, hit shape, sort key) keyed by an opaque placement id. Conditions: split `SlotGridView` (53 KB, F004 A-4) first; the camera keeps a single writer; there is no per-machine UI Toolkit element, no per-item simulation and no per-frame allocation; new art fits F004 budgets #6 (≤ 8 MB, about 2–3 MB headroom), #7 (atlas pages ≤ 1024²), #8 (SetPass) and #11 (490 variants); and the #555 reduced-motion gap (`StartupStatusPanel`) is settled. |

## Disagreements and how the parent resolved them

- **US7 as a story or as a constraint.** The PO says US7 has no player value of its own. The owner listed it explicitly. Resolution: keep US7 at P2, stated as an observable presentation outcome. A test configuration with a slot count other than 12 lays out, frames, hit-tests and selects correctly with no code change. Its supporting requirements are architecture constraints.
- **US6 priority.** The owner drafted P2; the PO recommends P1, because US1 cannot pass while six permanent tabs remain. Resolution: P1. The deviation from the owner draft is recorded in the spec and can be reversed in clarification.
- **US5.** Visible state and activity (running versus stopped, a completion pulse) move into US4's acceptance. US5 keeps the rewarding feedback (upgrade, build, milestone, ambient life) at P2.
- **Connectors between machines.** The Art Director recommends visible conveyor or pipe links. The Architect shows that the simulation has no machine-to-machine flow. Links between machines would show the player a topology that does not exist, which conflicts with Core-Idea "transparent production systems" and constitution principle I. Resolution: open owner decision in the spec (`[NEEDS CLARIFICATION]`), with a truthful option that shows every machine connected to the shared store.

## Owner decisions surfaced (reconciled into the spec)

The spec records these with options and recommendations. At most three of them carry `[NEEDS CLARIFICATION]` markers. The others have a recorded default in Assumptions that `/speckit-clarify` can revisit.

1. Navigation shape: a systems hub replacing the six tabs (recommended); a compact rail; or keeping the tabs.
2. Depicting connections: links to the shared store (recommended); decorative links between machines; or none.
3. Definition of factory development stages for the progression presentation: which existing data defines early, partial and established.
4. Environment direction: platform deck (recommended), hall or yard. This is decided through the visual-direction gate with rendered mockups, not in the spec.
5. The manual-mining control: a world hotspot with an accessible fallback (recommended), or kept as a floating button.
6. Remove placement: in the expanded view behind confirmation (recommended).
7. Completion feedback exactness: an additive read-model completion counter (recommended by the Architect), or derivation from the progress wrap. This is a plan decision with owner visibility, because it touches Core read models but not saves or balance.
8. `SlotGridView` split before feature work (recommended): a plan decision.

## Activated and omitted roles

- **Activated:** Game Designer/Product Owner, UI/UX, Art Director, and Software Architect with the Technical Art and Performance lenses.
- **Omitted Game Systems/Economy:** the feature changes no formula, production rate, upgrade, unlock, reward, offline or balance rule. Progression visuals read only existing data. The role is re-activated if clarification introduces any derived "efficiency" figure shown to the player that needs a definition beyond existing read models.
- **Omitted Security/Privacy:** no new persisted player data or trust boundary. Any new presentation preference would reuse the local display-preferences file and trigger a review at plan time.
- **Omitted DevOps/Operations:** no build, runtime or migration change is foreseen. The plan re-evaluates this when new atlases or settings change.
- **Omitted Play-Tester:** discovery used the existing batch-C report as input. The Play-Tester activates in verification because F006 changes the player-visible core loop flow.
- **Omitted Sound Designer:** audio is not in the owner brief. It is listed as a future candidate.

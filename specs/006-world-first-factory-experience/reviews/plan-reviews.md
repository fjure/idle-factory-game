# Plan Reviews — Feature 006 (2026-11-11)

These are independent, read-only reviews of the plan at commit `9a95edb`, which was rebased to `7c00cc3` onto `01c9001`. Each review ran in a fresh agent session and checked its claims against code and documents by targeted grep. The parent reconciled every finding into `research.md` (revision 2), `plan.md`, `data-model.md`, the contracts and `spec.md`. Owner answers are in the spec Clarifications section (session 2026-11-11, plan-review block).

## Verdicts

| Role | Tier | Verdict | Blocker | Major/High | Medium | Minor/Low |
|---|---|---|---|---|---|---|
| Software Architect | Medium (`sonnet`) | Approve with conditions C1–C6 | 0 | 5 (A-1–A-5) | — | 6 (A-6–A-11) |
| Test/QA | Medium | Conditional pass | 0 | 3 (Q-1–Q-3) | 5 (Q-4–Q-8) | 4 (Q-9–Q-12) |
| UI/UX Reviewer | Medium | Conditional pass | 0 | 3 (U-1–U-3) | 4 (U-4–U-7) | 4 (U-8–U-11) |
| Art Director + Technical Art | Medium | Changes required before P0 mockups | 0 | 4 (AD-1–AD-4) | 6 (AD-5–AD-10) | 2 (AD-11, AD-12) |
| Game Systems/Economy | Low (`haiku`) | Accept with conditions | 0 | — | 2 (G-1, G-2) | 3 (G-3–G-5) |
| Security/Privacy | Low (`haiku`) | Accept with conditions | 0 | — | 1 (S-1) | 2 (S-2, S-3) |
| World Designer (new role, owner decision 2026-11-11, `01c9001`) | Medium | Accept with conditions | 0 | 1 (W-1) | 4 (W-2–W-5) | 2 (W-6, W-7) |

## Dispositions

"Fixed in" names the revision-2 location. Every finding is accepted unless marked otherwise.

| ID | Finding (short) | Disposition |
|---|---|---|
| A-1 | The simulator holds no monotonic state across advances. Faulted advances would still count completions. | R08 rewritten. A per-advance completion count is filled in `CompletionPass` and committed only on a successful advance. `RunSession` accumulates it per `InstanceId`, prunes it on removal, and surfaces it in `RefreshViews`. The count is +1 per completed cycle (see G-1). OD-11 now refers to this design. Tests: faulted advance, multiple completions in one advance, remove/replace, prestige. |
| A-2 | `Bounds` would double-count the margin. `ScaleLayout` and the framing are keyed on `columns`. | R04: `Bounds` equals today's `Extent`, and the margin stays separate. `ScaleLayout.Columns` and its `Key` are replaced by a layout identity. A `LargestFittingScale` parity test is added. The plan's component table now names `CameraScaleModel` and `RunCameraFraming` as the changed math, and `FactoryCameraController` only as the consumer. |
| A-3 | The TagManager has only `Default` and `Overlay`. The new sorting layers need a ProjectSettings change, and the names were inconsistent. | R06: the layer names are fixed (`WorldBase`, `WorldDeck`, `WorldLinks` before `Default`; `Effects` after `Overlay`). A P1 task creates them through Unity tooling with a recorded `IDLEFACTORY_ALLOW_SETTINGS_NOISE` decision and a missing-layer guard test. DevOps is activated as a Low-tier lens for this task. |
| A-4 | `DeckCells` semantics conflicted. The lattice is not an isometric Tilemap grid. | R06: `Bounds` and `DeckCells` cover **all** locations, and only the tile art varies with unlock, so no scale jump occurs on unlock. The grid is a rectangular Unity Grid with cells of 70×54 art px. Link lanes are whole cells or sub-cell sprites (see AD-2). |
| A-5 | The R11 blast radius is 209 references in 45 files. `CloseSheet` resets tab and focus. The camera sheet follows `selectedSlot`. A floating card would be circular with the unobstructed rect. | R11: a `ShellTab` shim stays during the migration. Stack and sheet logic is extracted from `RunShellDocument` first, as a pure move. The re-baseline is listed per file. The camera framing stays selection-driven. The card is **docked only**, as a bottom band or a side column, so the band is known before the reframe. Floating cards are dropped. |
| A-6 | R05: `SlotGridView.cs` sits in `Presentation/` and `SlotRenderers` is private nested. | R05: `git mv` with its `.meta` (GUID kept), described as a "pure move plus a visibility change", with a no-scene-diff check. |
| A-7 | The layout had no point-to-id query, and the builder could not express L shapes. | R04: adds `TryHit(world, out id)` and a constructor from explicit cells for `TestLayouts`. |
| A-8 | Ordering: the counter is needed in P2, and the P1 floor migration would be wasted. | R01: the counter (OD-11) moves to P1. P1 does not migrate the floor; P2 replaces it directly. |
| A-9 | The Tilemap is a first runtime use. | R06: adds an Android IL2CPP and Player module check, a material parity check, and a record that props always draw under machines. |
| A-10 | The effect pool size was unexplained. Buffers were sized at 12. Offline deltas would burst. | R12: the pool size is justified (3 active plus 5 draining tails), buffers are sized from `LocationCount`, and offline arrivals produce no effect (see G-5). |
| A-11 | Preferences v2 migration was unspecified. | R20 rewritten (see S-1). |
| Q-1 | Real-shell pointer injection is not available in batch UI Toolkit. | R02: the SC-003 probe counts panel-event and `Click` dispatches through the real shell. A manual real-input checklist on the Windows player for the hub, card, build card and back is added to P3 and P4. |
| Q-2 | The re-baseline list was incomplete. | R19: the full file list is named (six tabs: about 35 Play Mode files; zoom buttons: 5 files; 12 slots: 6 files). A `ShellTab`/`TabButton` compatibility helper goes in the shell test support. |
| Q-3 | The emulator scripts tap the bottom bar by fractions. | R19: the Default, F003, F004 and F005 scenarios are retargeted to a hub-entry tap helper, and F006 is added to `$knownScenarios`. |
| Q-4 | `core-public-api.txt` pins the `MachineStateView` constructor. | R08: the public-API fixture diff is part of the change. The new value is an added constructor parameter with a default, so existing callers stay valid. A no-observer-effect determinism test compares save and trace SHA with and without a reader. |
| Q-5 | The counter had no oracle. | R19: a property test checks that the sum of deltas equals the engine-trace completions across W003/W005, including batch, offline and restore. |
| Q-6 | `DisplayPreferencesStoreTests` encode v1 rules. | R19 and R20: listed for re-baseline, with new v2 tests. |
| Q-7 | The guidance probes were unproven. | R02: a `ShellGeometryProbe` task with a self-test on synthetic layouts. The baseline JSON is written to `reviews/` for diffing. |
| Q-8 | The StandalonePlayer scope was too thin. | R19: the allocation tests, a hub and back-stack smoke test, card open/close and a preferences round trip are tagged. Editor-only helpers sit under `#if UNITY_EDITOR`. |
| Q-9 | Risk of a 0-test Play Mode run. | R19: every task requires a recompile before the run, fails on a total of 0, and records the count. |
| Q-10 | Test naming. | R19: new tests go in namespaces `.F006` and allocation tests use an `F006` prefix. |
| Q-11 | Pinch is not adb-injectable. | R19: pinch stays covered by Play Mode only. Emulator zoom evidence comes from the button or the settings path. |
| Q-12 | The trials need seeds. | R18: a seed list and a pass/fail oracle script per trial task. |
| U-1 | An icon-only hub entry has no tooltip on touch. | **Owner (2026-11-11): icon plus a short visible text label.** FR-020 and the contract are updated. |
| U-2 | A 30 % card at 200 % text hides Upgrade. | **Owner: the card may grow to about 45 % at large text.** The first view is name, state and Upgrade, with the shortfall inside the button. A test asserts that Upgrade is visible without scrolling at 200 % with worst-case copy. |
| U-3 | Phone landscape at 640×360 and 200 % text was unspecified. | R10/R11: landscape puts the persistent layer in left and right columns. The card is a fixed side column of about 40 % width with Upgrade pinned. The 640×360 at 200 % case is added to the tests and to the mockups. |
| U-4 | Double alarm between attention indicators and badges. | R09/R11: the hub dot covers **system** attention only (Contracts, Research, Prestige, Overdrive). Production attention, including store-full, uses one indicator in the world and the strip. The Market flag is dropped. Priority: production attention is shown in the strip, and system attention only as the hub dot. |
| U-5 | Mixed card, hub and build sequences were undefined. | R11: opening the hub closes the card or build card and restores the camera. The card and the build card are mutually exclusive. `BackOrderTests` gets the mixed sequences. |
| U-6 | Keyboard order was undefined. | R10: the order is persistent layer, then world locations in `SortKeyOf` order, then card or hub. Focus returns to the invoking location. |
| U-7 | The build preview could be hidden, and a ghost is weak for CVD players. | R13: the SC-002 visibility rule extends to the build card. A text line "Selected: <type> at location N" is added. |
| U-8 | Removing the RP caption. | R11: the RP value sits in the Research card status line. A checklist test for FR-009. |
| U-9 | Missing states and stale-action placement. | R10/R11: shared empty, loading and error templates are added to `SurfaceConsistencyTests`. A stale action shows an inline card line. Notices render above sheets. |
| U-10 | Mockups cannot prove UX. | Accepted with the owner's gate unchanged (mockups only, OD-4). R03 adds a 640×360 sheet. The emulator UI checkpoint is the P3 batch acceptance (FR-045 per batch) before the owner accepts the UX batches, with no separate prototype. |
| U-11 | No named scaffold. | R11: a shared `PanelScaffold` (header with back, primary and secondary button classes) governed by Art Bible §11 (UI and font rules). |
| AD-1 | An isometric Tilemap is not whole-pixel. | R06: a rectangular Grid with 70×54 cells (cellSize 70/24 × 54/24 world units), matching the F004 floor repeat. The deck-edge style is decided in the P0 mockups. A seam test covers the float cell size. |
| AD-2 | Lanes cannot run between plates, and a shared trunk makes pips unattributable. | R07 rewritten: per-row trunk lanes run in the 19 px row gap, and each location has a short branch from its plate's front edge to the trunk. Pips appear **only on branches**, so they are attributable. Trunks are static. The geometry is proven on the 3×4, 4×3, 6, 20 and L layouts in mockups and tests. |
| AD-3 | Moving the badge anchor up worsens the overlap. The anchor is part of the frozen S3 = b decision. | R17: A-03 is measured first. Candidate anchors (centred x = 40, lower or left offset, a pointer tail) are checked against the 54 px row band, the selection band and the overlap proof. **The owner decides (OD-12)** in P2 before the v1.1 sign-off. |
| AD-4 | A fast 10 fps pace breaks the Assembler flash limit. Lamp flicker breaks L-1 and §6.4. | R12: pace is capped per machine family by its measured flash rate (the Assembler stays at 8 fps or below). There is no lamp flicker; lamps are static and use steel or teal tones, never `lit_*`. `check_flash` runs on body + accent + FX composites at every pace. A pace change takes effect at the next loop start (frame 0), keeping the shared-clock frame formula per pace. |
| AD-5 | Palette allowances and §9.4 tone set. | R17: a per-family allowance table. Active-link, pip and spark colours avoid `lit_*`. §9.4 is re-run on the new backdrop and added to SC-005. §5 "no background illustration" is superseded in v1.1. |
| AD-6 | The world-layer budget had no numbers, and every atlas adds a SetPass. | R06/R16/R17: the static families (ground, link, edge, prop, foundation, deposit) are merged into **one world atlas**, and FX get one atlas. Numeric caps per location are in contract §5. The SetPass delta is budgeted explicitly against budget #8. |
| AD-7 | The texture estimate was underived, and the depot variant count was inconsistent. | R17: a sprite-by-sprite table with padding and page rounding is part of the v1.1 draft. The depot has 4 fill states × 3 stage variants = 12 sprites. |
| AD-8 | The prop rule cannot be satisfied inside the lattice. | R06: the prop-eligible mask is the margin ring plus empty or locked cells, with a maximum prop height of 24 art px. The free area is measured in a test. |
| AD-9 | Bounds versus fit slack. Foundations must stay flat. The padlock and digit must stay. | R06: the depot sits inside the existing margin band, and n_fit is measured per class against today's values (a parity test). Foundations are flat, with no plinth wall. Locked locations keep the §8.5 padlock and digit on the hatch tile. |
| AD-10 | Mockups cannot show activity. Layout constants could drift. | R03: 6-frame animated strips (loop, pip, completion pop) and a reduced-motion variant are added. The compositor reads geometry from one shared JSON exported by the layout tests. Palette and flash checks run on the mockup inputs. |
| AD-11 | Route conflicts with §12. | R17: ground, hatch, edge, prop, depot and deposit go through the Blender rig-v2. Lanes, pips and FX are scripted. ComfyUI is used as a concept reference only. |
| AD-12 | Stage deltas are marginal at fit-all. | R15: a minimum measurable delta per stage (deck-lit area share and grayscale difference at n_min). The stage steps at 4 and 8 of 12 are documented. |
| G-1 | A batch factor would overstate partial batches. | R08: the count is +1 per completed cycle, and the unit is named `CompletedCycles`. |
| G-2 | The bottleneck is a steady-state rate ratio, not live stock. | R09: the ranking is over resources that currently block at least one MissingInput machine, ordered by coverage, then affected count, then content order. If no machine is MissingInput, no bottleneck is shown. The copy says "Short on <resource>", not "bottleneck". Mismatch cases are tested. |
| G-3 | Coverage is 0 without demand, and auto-sold resources need handling. | R09: an explicit `HasDemand` filter. Auto-sold blocking surfaces through the existing corrective action. Tests added. |
| G-4 | The stage function divides by zero when t = 0. | R15: t ≤ 0 maps to Early. |
| G-5 | Offline catch-up produces a burst of pops. | R08/R12: the counter baseline resets after offline reconciliation, and offline deltas produce no effect. |
| S-1 | v2 parser precedence was undefined. | R20 rewritten: v2 is read from `display-v2.json` only. If it is absent, `display-v1.json` is read through the existing v1 parser, then written as v2 on the next change, and v1 is left in place. A malformed v2 is invalid and falls back to the defaults; there is no silent v1 fallback once v2 exists. The v2 parser keeps the duplicate, unknown-key and value-end rules. `showZoomButtons` is rejected in v1. |
| S-2 | Future versions are overwritten. | Accepted as documented (R20). The stale v1 file is not deleted. |
| S-3 | Temp file name collision. | R20: the temp file is named `display-v2.json.tmp`. |
| W-1 | There was no World Bible and no gate. | **Owner: minimal World Bible v0.1, a neutral starter site.** New P0 task, frozen before the direction briefs. The owner approves it with OD-9. Briefs cite its version (new FR-047, SC-013). |
| W-2 | Layout composition had no owner. | R22: a `WorldCompositionSettings` presentation ScriptableObject (not gameplay content, so FR-001 holds) owned by the World Designer and edited through Unity tooling. It holds the margin, prop density per stage, prop kind weights, the depot anchor rule, hatch and edge rules, and the vein placement. The builder reads it. The World Designer runs a conformance review. |
| W-3 | Mining had no spatial intent. | **Owner: add a vein marker.** R23: a new F006-DEPOSIT family. The vein sits on a margin cell chosen by the composition settings. It shows activity while manual mining is in progress (or a static highlight under reduced motion). It is **not** a hit target in F006; the screen control stays the input (FR-010). It does not change mining yield or pacing, and Economy confirms no pacing effect. |
| W-4 | Copy and names had no owner. | R22: the hub label, banner copy, stage names and site name are owned by the World Designer for tone; UI/UX owns length and fit. The World Designer is added to A-10 and to the review waves. |
| W-5 | Region identity was implicit. | **Owner: a neutral starter industrial platform on a dark base.** The deck and base are a swappable ground family keyed by region id (one region today). |
| W-6 | Acceptance for world fit was missing. | New SC-013: a World Designer checklist with zero conflicts and no unowned world-facing string. |
| W-7 | The wave table had no World Designer row. | Plan review waves updated: plan review (done), P0 with the Art Director, P2 per family, and P4. |

## Owner decisions taken in this review (2026-11-11)

1. Hub entry: icon plus a short visible text label (U-1).
2. Machine card in phone portrait: up to about 45 % of the safe height at large text (U-2).
3. World Bible: minimal v0.1, a neutral starter industrial platform on a dark base, with swappable ground (W-1, W-5).
4. Manual mining: add an ore-vein marker in the world (W-3).

**Still pending:**
- OD-9: direction and label wording, at P0.
- OD-10: Art Bible v1.1 sign-off.
- OD-11: the counter, confirmed against the revised R08 design at the next owner touchpoint.
- OD-12: the A-03 anchor, from measured candidates.

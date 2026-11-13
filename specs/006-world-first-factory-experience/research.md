# Research: World-First Factory Experience (Feature 006)

**Date**: 2026-11-11 (revision 2 after the plan reviews) | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Reviews**: [plan-reviews.md](reviews/plan-reviews.md)

**Format.** Each entry gives the decision, the rationale, and the alternatives that were considered. **Fact** marks repository evidence (file:line or document). **Inference** marks the parent's reasoning. A finding ID in brackets, such as [A-1], points to the plan-review disposition that changed the entry.

---

## R01 — Delivery order and gates

**Decision.** The work runs in five ordered phases.

| Phase | Content | Gate at the end |
|---|---|---|
| **P0 Baseline, world and direction** | Geometry, interaction and label probes and the baseline (R02). World Bible v0.1 (R22). At least two rendered directions with animation strips (R03). Owner decision. | Owner approves World Bible v0.1 and the direction (OD-9). No production asset and no UI rebuild before that. |
| **P1 Enabling refactor** | No-behaviour-change split of the slot-grid view (R05). `ShellTab` shim and extraction of the stack/sheet logic from `RunShellDocument` (R11). World-layout seam with the explicit-cell constructor and test layouts (R04). Sorting layers through Unity tooling (R06) [A-3]. Additive `CompletedCycles` read value (R08, OD-11) [A-8]. `WorldCompositionSettings` asset (R22). | Existing suites green. Re-baseline limited to the recorded file list. No scene diff. Settings-noise exception recorded for TagManager only. |
| **P2 World art first** (owner) | Art Bible v1.1 draft with an amendment list, budgets and texture table (R17). Families: ground, foundation, depot, link, prop, edge, deposit, FX. Tilemap deck that replaces the F004 floor directly (no P1 floor migration [A-8]). Depot and link view. Vein marker (R23). Effect pool (R12). A-03 anchor decision (OD-12). | Art Director and Technical Art per family. World Designer conformance. Owner signs v1.1 (OD-10). SC-005 re-run. B4a. |
| **P3 Interaction and UX** | Presentation model (R08), attention (R09), card and details (R10), hub and back stack (R11), build card (R13), milestones (R14), stages (R15), preferences v2 (R20), #555. | UI/UX review per surface. Manual real-input checklist. Play-Tester per batch. B4b. |
| **P4 Verification and acceptance** | Full suites and the player suite, re-retargeted emulator scenarios, trials (R18), screenshot matrix, owner acceptance. | Binding criteria (SC-005, -007, -008, -009, -011, -012, -013) pass. Owner verdicts recorded. |

**Rationale.**
- The owner chose one feature with art first.
- The layout seam, the sorting layers and the counter are prerequisites of P2 [A-8].
- The World Designer requires a frozen setting before the briefs [W-1].

**Alternatives.**
- UX first, or splitting into F007: both declined by the owner.
- Interleaving the phases: rejected, because art would be redone around layout changes.

## R02 — Baseline measurement method (FR-046)

**Decision.**

- **Screenshot matrix.** Captured on the current build (`origin/main` at the start of P0) for every `ShellScreens.All()` class, plus the emulator at 1080x2400 in both orientations. States: no panel, machine sheet open, Contracts open, build sheet open, offline summary. Text scale 100 % and 200 %.
- **`ShellGeometryProbe`.** A Play Mode component with a self-test on synthetic layouts [Q-7]. It computes:
  - the area shares from UI Toolkit `worldBound`, as `RunHudLayoutTests` already does: the world rect is `TryGetUnobstructedScreenRect`, and the chrome is the union of the persistent elements within the safe area;
  - the persistent label count, meaning visible `Label`s with text.

  It writes `reviews/baseline/<class>.json` so that results before and after F006 can be diffed.
- **Interaction counts.** A deterministic scripted journey dispatches panel events and `Click` through the real shell. Batch UI Toolkit drops device pointer events, so the journey does not use device input [Q-1] (fact: `docs/agents/workstation-pitfalls.md:36`). A **manual real-input checklist** on the Windows player covers the hub, card, build card and back actions in P3 and P4 [Q-1].

**Rationale.** The method is deterministic and re-runnable, and it reuses the existing capture and layout test patterns.

**Alternatives.** Pixel segmentation (fragile); manual counting (not reproducible).

## R03 — Visual direction mockups (FR-040, FR-041; owner: mockups only)

**Decision.**

- **Compositor.** A deterministic `mockup` command in `tools/art-pipeline` (Pillow, pinned). It places real F004 sprites and concept sprites at the exact device-pixel scale per screen class.
- **Geometry source.** Geometry comes from **one shared JSON** that the R04 layout tests export, so the mockups cannot drift from Unity [AD-10].
- **Gates on inputs.** The palette and `check_flash` gates run on the mockup inputs [AD-10].

**Contents per direction (at least two).**

- **Sheets:** phone portrait 1080x2400, phone landscape 2400x1080 **and 640x360-class at 200 %** [U-3], tablet 1536x2048.
- **Each sheet shows:**
  - an active factory with at least 8 machines in mixed states;
  - depot, trunks and branches (R07), and the vein marker (R23);
  - the docked card with camera framing;
  - the hub open, with its text label (U-1);
  - the stage triptych;
  - a 200 % text variant.
- **Animated strips** (GIF or APNG, 6 frames): machine loop, branch pip, completion pop. Plus a reduced-motion still [AD-10].

**Review.** `reviews/direction.md` compares the directions against `Core-Idea.md`, the spec and World Bible v0.1, and gives a recommendation.

**Rationale.** These are owner-chosen mockups at real scale, extended to cover activity, which carries the direction [AD-10].

**Risk.** Text layout is approximated. UX fit is proven in the P3 batch checkpoints on the emulator and Windows [U-10].

## R04 — World-layout seam (FR-037, FR-038, US7)

**Fact.**
- The 12-slot assumption lives only in presentation: `SlotGridView.cs:232`, `:260`, `:718`.
- `CameraScaleModel` uses only `Extent(columns)` (`:184`, `:195`, `:301`, `:316`) and adds `HalfCellMargin` itself.
- `ScaleLayout.Columns` and `ScaleLayout.Key` (`:29-47`), `RunCameraFraming.FrameSlot/FrameContent(columns)`, and the bootstrap (`:852`, `:1183`) and `RunShellDocument` (`:633`, `:672`) all key on `columns` [A-2].
- The pitch of 70 is tuned to the tablet n_min fit (`SlotGridView.cs:222-229`).

**Decision.**

- **`FactoryWorldLayout`.** An immutable concrete class in `Unity/Presentation/World/`. Its members:
  - `LocationCount`, `LayoutId` (string identity);
  - **`Bounds` = exactly today's `Extent`** (plates plus the badge band). `Margin` is a separate value that the camera model keeps adding as today, so the margin is not counted twice [A-2];
  - `AnchorOf(id)`, `FootprintOf(id)`, `HitShapeOf(id)`, `SortKeyOf(id)`;
  - **`TryHit(world, out id)`** [A-7];
  - `DeckCells` (all locations, R06);
  - `DepotCell` and `DepotAnchor`;
  - `TrunkOf(row)` and `BranchOf(id)` (R07);
  - `DepositCell` (R23).
- **Builders.** `LatticeLayoutBuilder(locationCount, orientation, WorldCompositionSettings)` reproduces today's 3 and 4 column lattice. **An explicit-cells constructor** serves `TestLayouts` (6, 20, L) in the test assemblies [A-7].
- **Camera.** `ScaleLayout.Columns` and `Key` become `LayoutId` plus `Bounds`. `CameraScaleModel` and `RunCameraFraming` consume `Bounds` and `FootprintOf`. `FactoryCameraController` stays the sole writer and holds no layout math [A-2].
- **Parity.** A test checks `LargestFittingScale` against the current values for every screen class [A-2].
- **Identity.** `BuildingLocationId(int)` equals the slot index today. Machine identity stays the instance id.

**Rationale.** This is the smallest seam with exact parity, and it unblocks US7.

**Alternatives.**
- An interface with strategies: there is no second runtime variant.
- Generalising in place: the file keeps growing (F004 A-4).

## R05 — Splitting the slot-grid view

**Decision.** The split is a **pure move plus a visibility change** [A-6].

- `git mv Assets/IdleFactory/Unity/Presentation/SlotGridView.cs` (with its `.meta`, keeping the GUID) into `Presentation/World/`.
- `SlotVisualModel.cs`, `SlotLattice.cs`, `FloorRange.cs`, `RunZoomSettings.cs` and `SlotRenderers.cs` are extracted. `SlotRenderers` changes from private nested to internal.
- `RunZoomSettings` is serialized by field name inside the bootstrap. No `.unity`, `.prefab` or `.asset` file references the type (fact), so the scene YAML is unaffected.

**Verification.**
- All suites pass with only `using` edits.
- A no-scene-diff check passes.
- The settings-noise check passes.

## R06 — Environment: platform deck, base, Grid and sorting layers

**Decision.** This is the default for the mockups. The direction is chosen at OD-9.

- **Grid** [AD-1, A-4].
  - The `Grid` is **Rectangle**, with cell size 70×54 art px (70/24 × 54/24 world units). This is the F004 floor repeat and the lattice pitch.
  - Cell origins sit on whole art pixels. A seam test covers the float cell size.
  - It is not an isometric Tilemap: the half-steps of 17.5 px would break whole-pixel placement.
- **Deck coverage** [A-4].
  - `Bounds` and `DeckCells` cover **all** locations plus the existing margin band. Only the tile art changes with unlock: deck versus hatch. The camera scale never jumps on unlock.
  - The deck-edge style (diamond-clipped edge tiles versus rectangular tiles with a 2:1 edge trim) is decided in the P0 mockups [AD-1].
- **Location tiles** [AD-9].
  - Locked locations use a hatch tile and **keep the §8.5 padlock and level digit**.
  - Empty unlocked locations show an outline foundation.
  - Occupied locations show a **flat** solid foundation: the same 70×35 diamond hit and selection shape, with no wall or plinth.
- **Prop mask** [AD-8].
  - Props may only go in the margin ring and on empty or locked cells, at most 24 art px tall, by a deterministic cell-hash rule weighted per stage (R15, R22).
  - Props always draw under machines. This is recorded and acceptable given the mask [A-9].
- **Sorting layers** [A-3].
  - The order is `WorldBase`, `WorldDeck`, `WorldLinks`, then `Default` (slots and machines), then `Overlay`, then `Effects`.
  - The layers are created in P1 through Unity tooling (TagManager), with a recorded settings-noise exception, and checked by a guard test that fails if any layer is missing. `SortingLayer.NameToID` silently returns 0 for a missing layer (fact).
- **Atlases and material** [AD-6, A-9].
  - Static families share **one world atlas**: ground, foundation, link, edge, prop, depot and deposit. FX get one atlas.
  - The Tilemap uses the same sprite material as the slot sprites.
  - An Android IL2CPP and Player check covers the `tilemap` module.
- **Budget** [AD-6].
  - The deck Tilemap replaces the F004 floor pool.
  - The world atlas adds at most one SetPass net of the removed floor atlas, and FX add one more while active.
  - Budget #8 is recorded per the F004 method. Numeric caps are in contract §5.

**Alternatives.**
- Pooled sprite floor: the draw-call problem already recorded (TA-2).
- Baked background: does not scale with layouts.
- Isometric Tilemap: not whole-pixel.

## R07 — Central store depiction (FR-013, owner: links to a central store)

**Fact.** Plates touch at their side apexes (pitch 70 equals plate width). The gap between rows is 19 px. There is no vertical corridor between plates [AD-2].

**Decision** [AD-2].

- **Trunks.** One **trunk** lane per row runs in the row gap behind the row's plates toward the depot side. It is static: it never animates and is never attributed.
- **Branches.** Each **occupied** location has a **branch**: a short lane segment from its plate's rear edge to its row trunk.
- **Depot.** The depot occupies `DepotCell` in the margin band at the trunks' end.
- **Pips.** Pips appear **only on branches**, so every pip belongs to one machine. A pip runs while the machine is in the Running phase. Its pace comes from `ActivityPace` (R08), within the R12 caps.
- **Depot states.** The depot shows 4 fill states × 3 stage variants (R15), 12 sprites in all [AD-7]. "Full" uses the existing storage-full semantics.
- **Reduced motion.** No pips. Branches use a static "active" tile.
- **Proof.** The geometry is proven on the 3×4, 4×3, 6, 20 and L layouts in the mockups and in `DepotLinkModelTests`.

**Constraints.**
- There are never links between machines.
- A pip never represents a unit count.

## R08 — Machine presentation model and the completion count

**Fact** [A-1].
- `RunSimulator.Stage()` rebuilds the per-machine arrays from `RunState` on every `Advance()` (`RunSimulator.cs:511-518`, `853`). Only `_completedNow` and the last-completion arrays exist.
- A faulted advance returns before `Commit()` (`:437-461`).
- `MachineStateView` is built in `RunSession.RefreshViews` (`RunSession.cs:830-856`). That is its single constructor caller.
- `RunSimulator` instances also exist in `OfflineSegmenter`, `PrestigeTransition`, `V1Import` and the bootstrap.

**Decision.**

- **Completion count.**
  - The simulator fills a per-advance `int` completion count per machine in `CompletionPass`, **+1 per completed cycle** (also for a partial batch, units < batch). This count is never the batch factor [G-1].
  - The count is exposed only after a successful `Commit()`. A faulted advance exposes zero.
  - `RunSession` accumulates the counts into a session-owned `long` per `InstanceId`. It prunes the entry when the machine is removed, resets it on replace, and starts from an empty map on session construction, restore and prestige.
  - `RefreshViews` surfaces the value as `MachineStateView.CompletedCycles`, through an added constructor parameter with default `0`, so the existing single caller compiles unchanged until it is updated [Q-4].
  - The value is not saved, and no economy, offline or prestige code reads it.
  - **Offline reconciliation** completions do not reach the presentation as deltas. The model resets its baseline after an offline return, and the offline summary already reports them [G-5].
  - The public-API fixture (`core-public-api.txt`) diff is part of the change [Q-4].
- **`MachinePresentationModel`.** A pure C# model, refreshed on a revision change into a buffer sized from `LocationCount`. Per machine it holds:
  - `VisualState`, from `SlotGridModel.StateFor`, the single precedence;
  - `UpgradeAvailable`;
  - `CompletionsDelta`: the counter difference, capped for display; it is 0 on a baseline reset;
  - `ActivityPace`: Slow, Normal or Fast, from `CycleTicks`, capped per family by R12.
- **Tests.**
  - A faulted advance yields no count.
  - Multiple completions in one advance.
  - Remove and replace.
  - Prestige.
  - A property test: the sum of deltas equals the engine-trace completions over W003/W005, including batch, offline and restore [Q-5].
  - A no-observer-effect test: save and trace SHA are identical with and without a reader [Q-4].

**Owner visibility.** OD-11 is confirmed against this revised design at the next owner touchpoint.

**Alternatives.**
- Deriving the completion from the progress wrap: lossy.
- A counter in the simulator: not possible, because the simulator is stateless across advances (fact).
- An event stream: allocates.

## R09 — Shared cause and the "short on" signal (FR-026, FR-028)

**Decision** [G-2, G-3, U-4].

1. **Shared cause.** MissingInput machines grouped by blocking resource. A group of 2 or more machines is a shared cause.
2. **"Short on" signal.** This is the single production-level signal, phrased in copy as "Short on <resource>", never as "bottleneck". It is derived as follows:
   - Take the resources that currently block **at least one MissingInput machine**.
   - Filter to resources with `HasDemand`, so that "no demand" never reads as 0 %.
   - Order by `SupplyCoveragePpm` ascending, then by affected count descending, then by content order.
   - If no machine is MissingInput, no signal is shown.

   Tests cover:
   - an auto-sold input with coverage of 1,000,000 or more but stock 0, which is still shown, because the machine is blocked;
   - coverage below 1,000,000 with nobody blocked, which shows no signal;
   - no demand;
   - ties.
3. **Store full.** Any StorageFull machine produces the depot "full" state and production attention.
4. **Where signals show.**
   - Production attention (the short-on signal, shared causes, store full) shows **in one place in the world**: a marker at the trunk junction of the affected rows, or at the depot for store full. It also shows **in one strip indicator** (resource icon and count). Tapping either selects the first affected location.
   - **Several shortage resources at once**: one world marker per resource, at most 2 visible, ranked by the shortage order. The strip shows the top resource plus "+N" [analysis D8].
   - The hub dot shows **system** attention only: Contracts, Research, Prestige and Overdrive. Market has no hub flag [U-4].
   - Per-machine badges stay as in F004.
5. **Review.** Game Systems reviewed the derivation (Low tier) and accepted it with these conditions applied.

## R10 — Contextual machine card and details (US3)

**Decision.**

- **One instance.** There is one `MachineCard` UI Toolkit instance, **docked only** [A-5].
  - **Phone portrait:** a bottom band. It is at most 30 % of the safe height at 100–140 % text, **may grow at large text up to a hard cap of 45 %** (owner, U-2).
  - **Phone landscape:** a fixed **side column of about 40 % width** with Upgrade pinned [U-3].
  - **Tablet and desktop:** a side panel.
- **Camera.** The band or column is known before the camera reframes. Framing stays selection-driven through `OpenSheet(band, footprint)` (fact: `bootstrap:1310-1333`) [A-5].
- **First view.** Name and level, the state line, and **Upgrade with the cost and shortfall inside the button**. Below that come the cause line with the resource icon and the Details chevron. A test asserts that Upgrade is visible without scrolling at 200 % with worst-case copy on every class [U-2].
- **Stale actions.** A stale action shows an inline card line with the rejection copy and never executes [U-9].
- **Details.** Contains all F003–F005 machine-panel capabilities: rates, progress, batch note, blockers with corrective actions, switch on/off, Items and boosts, and Remove behind `RequestConfirmation`.
- **Keyboard order.** Persistent layer, then world locations in `SortKeyOf` order, then the card. Focus returns to the invoking location on close [U-6].

**Alternatives.** Floating cards (circular with the unobstructed rect) [A-5]; world-space buttons; radial menus.

## R11 — Hub navigation, the back stack and migration

**Decision.**

- **Hub entry.** **Icon plus a short visible text label** (owner, U-1). The wording is chosen at OD-9 by the World Designer for tone and UI/UX for fit. It is never "Systems". It survives 200 % text and carries the system-attention dot.
- **`HubPanel`.** Built on the shared `PanelScaffold`: a header with back, primary and secondary button classes, and empty, loading and error templates, governed by Art Bible §11 [U-11, U-9].
  - Cards: Market, Contracts, Items, Research (with the RP value in its status line [U-8]), Industry (additive registry id), Overdrive, Prestige and Settings.
  - System views open through `ShellPanelRegistry` unchanged.
- **Mutual exclusion** [U-5].
  - Opening the hub closes a machine card or build card and restores the camera.
  - The machine card and the build card are mutually exclusive.
- **`ShellNavigationStack`.** Frames, in order: Dialog, PinnedValue, Card or BuildCard or Details, Hub or SystemView or Settings.
  - Back and Escape pop one frame per frame-tick.
  - Focus returns to the invoker.
  - Notices render above sheets [U-9].
- **Migration** [A-5].
  1. Extract the stack and sheet logic from `RunShellDocument` (2652 lines) into `ShellNavigationStack` and a `ShellSheetHost`, as a pure move.
  2. Keep `ShellTab` as an internal shim. `SelectTab` maps to opening the hub and system view, so the 45 dependent files keep compiling. `StartupStatusPanel` (`:667`) continues to call `HandleBack`, which delegates to the stack.
  3. Remove the tab bar from the UXML in P3.
  4. Re-baseline the tests by file (R19).
- **Badge feed.** `ContractsShellFeed` re-points from `SetContractsBadge` to the hub card flag (fact: a single call site).

## R12 — Activity, effects and the shared clock

**Decision** [AD-4, A-10].

- **Clock.** All motion uses `PresentationAnimationClock` with frame `floor(t·fps) mod n`.
- **Pace.**
  - `ActivityPace` selects 6, 8 or 10 fps, **capped per machine family by its measured flash rate**: the Assembler stays at ≤ 8 fps as conservative headroom (3 flashes/s in the F004 spike; 0 alone and 2 with the completion pop in production, T522; Art Bible 21.9, P2-F3).
  - A pace change takes effect at the next loop start (frame 0).
  - `check_flash` runs on body + accent + FX composites at every pace.
- **Lamps.** No lamp flicker. Lamps are static, with a `copper_2` head and a `steel_3` cap; no `teal_*`, never `lit_*` (Art Bible 21.2, 21.8 item 5, 21.9; P2-F4). L-1 and §6.4 stay unamended, except for the effect-sprite allowance listed in the v1.1 amendments (R17).
- **Effects.** Completion pop, spark (Smelter, Press), steam (Smelter), build dust and upgrade glint.
- **Pool.**
  - **8 renderers = 3 active + 5 for draining tails.** A cancelled effect fades its remaining frames instead of popping, and a pop lasts 6 frames, so a cap of 3 can churn without reallocating.
  - Coalescing timestamps are sized from `LocationCount`.
  - Offline arrivals produce **no** effect [G-5].
  - The window is 250 ms per location.
  - A completion pop takes priority over decorative effects.
- **Reduced motion.** No effects. Loops show frame 0. Completion shows as a static 1 s highlight on the card or depot.
- **Allocations.** None in steady state.

## R13 — Building flow

**Decision.**

- **Build card.** It is docked like the machine card and mutually exclusive with it. It shows the machine types with icon, cost and affordability.
- **Highlights.** Available locations get an outline highlight.
- **Preview.** A ghost preview (frame 0, 60 % alpha) **plus a text line "Selected: <type> at location N"** [U-7].
- **Visibility.** The SC-002 rule applies: the preview location is fully visible [U-7].
- **Actions.** Confirm and Cancel sit side by side at 44 pt. A shortfall disables Confirm and shows the line "Need $X more".
- **Success.** Build dust, then the foundation appears, then the build card hands over to the new machine's card.

## R14 — Milestone presentation

**Decision.**

- **Detection.** `MilestoneWatcher` detects a factory-level increase (with the newly unlocked locations) and a prestige completion (a run-id change) by diffing snapshots.
- **Banner.** A non-blocking banner of at most 2.5 s, with copy owned by the World Designer and fit owned by UI/UX (R22). It comes with a deck reveal and a 2 s foundation highlight, and is static under reduced motion.
- **Offline.** Offline milestones are listed in the offline summary only.

## R15 — Development stages

**Decision.**

- **Stage.** `stage = t ≤ 0 ? Early : clamp(floor(3·u/t), 0, 2)` [G-4]. Here u is the unlocked-location count at the current factory level and t is the location count at the maximum level. With 12 locations the steps fall at 4 and 8 [AD-12].
- **Secondary densities.** Active/unlocked and mean/max machine level, each in 3 bins.
- **Per stage:**
  - deck-lit area, where unlocked cells use deck art and the others hatch;
  - edges: railing, then wall, then gantry;
  - prop weights;
  - depot variant.
- **Readability.** **Minimum measurable delta per stage:** the deck-lit area share differs by at least 25 percentage points between adjacent stages, and a grayscale difference is visible at n_min, as a test on the composited triptych [AD-12].
- **Prestige.** The stage resets with the run (owner).

## R16 — Performance

**Decision.**

- **Workload.** W006-V is W004-V plus a full deck at the established stage, 12 branches with pips, the vein, 3 effects and the card open.
- **Measurements.** B4a after P2, B4b after P3. The F004 frozen budgets #4, #5, #6, #7, #8, #10 and #11 apply. The SetPass delta is reported per added atlas [AD-6].
- **Emulator.** Emulator FPS stays exploratory.
- **Leases.** `unity,emulator,gpu`.

## R17 — Art Bible v1.1 (additive) and A-03

**Decision.**

- **Version.** `F004-PAB v1.1.0`, a minor release.
- **Families.** F006-GROUND, -FOUNDATION, -DEPOT (12 sprites), -LINK (trunk, branch, active branch, pip), -PROP, -EDGE, -DEPOSIT (R23), -FX, and -UI only if UI Toolkit cannot express it.
- **Explicit amendment list** [AD-5, AD-4]:
  - §5 supersedes "no background illustration" and adds a per-family allowance table with no new id.
  - The active link, pip and spark avoid `lit_*`. The `res_*` uses are allowed by id.
  - §6.4 allows FX sprites on the shared clock.
  - §6.5 adds effect caps and the per-family pace cap.
  - §8.5 keeps the padlock and digit on the hatch.
  - §9.4 is re-run on the new backdrop, which is part of SC-005.
  - §2.2 adds a numeric world-layer budget.
  - §12 adds the routes.
- **Sprite table.** A sprite-by-sprite texture table with padding and page rounding sits in the draft. The world atlas has at most one 1024² page [AD-7].
- **Routes** [AD-11]:
  - Blender rig-v2 for ground, hatch, edge, prop, depot and deposit;
  - scripted for lanes, pips and FX;
  - ComfyUI for concept reference only.
- **A-03** [AD-3]. The anchor (59,33) is part of the owner's S3 = b decision.
  - Candidates (x = 40 centred, a lower or left offset, a pointer tail) are measured against the 54 px row band, the selection band and the overlap proof.
  - The **owner decides (OD-12)** before the v1.1 sign-off.
- **Sign-off.** The v1.1 sign-off (OD-10) cites World Bible v0.1.

## R18 — Evaluation methods

**Decision.**
- **SC-005.** Independent blind naming plus the owner's timed trial with `tools/art-pipeline/trials/timed_trial.py`. Includes §9.4 on the new backdrop and the portrait 720x1280 at n_min 2.
- **SC-004.** 10 seeded mixed-state captures.
- **SC-006.** 10 shuffled triptychs.
- **SC-013.** A World Designer checklist.
- **Seeds.** Every trial task gets a **seed list and a pass/fail oracle script** [Q-12].
- **Independence.** Reviewers are fresh sessions, and producers never review their own work.

## R19 — Test strategy and re-baselining

**New tests.** Namespaces `IdleFactory.Tests.*.F006`; the allocation classes are prefixed `F006` [Q-10].

- **Edit Mode:**
  - layout parity, validation, `TryHit` and `LargestFittingScale` parity;
  - `ShellGeometryProbeTests`;
  - the presentation model;
  - the `CompletedCycles` lifecycle and property tests;
  - attention, including the G-2 and G-3 cases;
  - the stage function;
  - the milestone watcher;
  - the navigation stack;
  - the effect pool;
  - `DisplayPreferencesV2Tests`: v1 read, migration, a malformed v2, atomic write, and the temp file name [Q-6];
  - the sorting-layer guard;
  - `NoFixedSlotCountGuardTests`, with the existing 12-slot tests exempted explicitly.
- **Play Mode:**
  - card, build card, hub and back order, including the mixed sequences;
  - keyboard traversal;
  - persistent layer, including the zoom matrix (touch/pointer × preference × class) and 640x360 at 200 %;
  - layout independence through the real camera;
  - deck, depot and link views;
  - reduced motion with zero moving decorative pixels;
  - `F006Allocation*`.
- **`[Category("StandalonePlayer")]`** [Q-8]:
  - the allocation tests;
  - the W006-V 600-frame run;
  - a hub and back-stack smoke test;
  - card open and close;
  - a preferences round trip.

  Editor-only helpers sit under `#if UNITY_EDITOR`.
- **Every run** recompiles first, fails on a total of 0, and records the count [Q-9].

**Re-baseline**, recorded in `reviews/test-rebaseline.md` with an old→new diff per file [Q-2]:

- **Six tabs and `HandleBack`:**
  - `RunHudLayoutTests` (L171–186);
  - `RunKeyboardFocusTests`;
  - `RunPanelCompositionTests`;
  - `GoalsLinePanelTests`;
  - `UiReviewFixesTests`;
  - `UiIconSurfaceRig`;
  - `RunTextScaleTests`;
  - `OreVeinPanelTests`;
  - `ContractsAutoDeliverPanelTests`;
  - the panel tests that use the `ShellTab`/`TabButton` helpers (about 35 files; a compatibility helper goes in the shell test support);
  - `AndroidBackGestureTests`.
- **Zoom buttons:** `NavigationInputTests`, `RunDeviceProfileTests`, `RunTextScaleTests`, `PixelCrispnessTests`, `DevToolsOverlayPlacementTests`.
- **12 slots and columns:** `SlotGridLatticeTests`, `SlotGridTests`, `CameraScaleModelTests`, `ScreenClassScaleTableTests`, `StateCueSetTests`, `F004AssetContractTests`.
- **Preferences:** `DisplayPreferencesStoreTests`.
- **Core API:** `core-public-api.txt`.

**Emulator** [Q-3, Q-11]:
- `android-emulator-journey.ps1`: the Default, F003, F004 and F005 scenarios move from bottom-bar fraction taps to a hub-entry tap helper. F006 is added to `$knownScenarios`.
- Pinch stays Play Mode only. Emulator zoom evidence uses the button or settings path.

## R20 — Display preferences v2 (FR-008a)

**Decision** [S-1, S-2, S-3, A-11].

- **File.** `display-v2.json` = `{"version":2,"reducedMotion":…,"showZoomButtons":bool}`.
- **Read order.**
  1. If `display-v2.json` exists, read it with the v2 parser. That parser keeps the strict token walk, the duplicate and unknown-key rejection, the value-end rules and the 4096-byte cap.
  2. A malformed or unknown-version v2 is invalid: it falls back to the defaults and emits the once-per-session diagnostic. There is no silent v1 fallback once v2 exists.
  3. If `display-v2.json` is absent, read `display-v1.json` with the unchanged v1 parser, which rejects `showZoomButtons`. Then `showZoomButtons = false`.
- **Write.** The first change writes v2 atomically, with the temp file `display-v2.json.tmp`. The v1 file is left in place.
- **Accepted, documented behaviour:**
  - A downgrade to an older build reads v1 and so loses the zoom choice.
  - A future version is overwritten.
- **Settings sheet.** Gains "Show zoom buttons". Pointer profiles always show the zoom controls.
- **Review.** Security reviewed this design and accepted it with the conditions applied.

## R21 — Roles, tiers and resources

- **Roles.**
  - Mandatory: PO, Architect, Test/QA, UI/UX, Art Director, Technical Art, Performance, Play-Tester (per batch), and **World Designer** (P0 World Bible, P2 conformance, P4) [W-4, W-7].
  - Light reviews at Low tier: Game Systems (R09, R15, the R23 no-pacing check), Security (R20), and **DevOps** (the TagManager settings change, A-3).
  - Omitted: Sound (A-8).
- **Tiers.** Default Medium (`sonnet`). R04, R05, R08 and R11 run at Medium with `high` reasoning, because they cross boundaries. High tier (`opus`) only on a recorded risk.
- **Leases.** Unity, emulator and GPU per AGENTS.md. P2 art needs `gpu`. The mockup compositor needs no GPU unless concept renders are made.

## R22 — World Bible v0.1 and world composition (new, World Designer)

**Decision** [W-1, W-2, W-4, W-5; owner: minimal v0.1, neutral site].

- **World Bible v0.1.** `docs/world/world-bible-v0.1.md`, owned by the World Designer, at most a page or two. It covers:
  - the site concept, a **neutral starter industrial platform on a dark base**;
  - the site name and tagline;
  - mood words;
  - naming and copy rules (tone, vocabulary, forbidden terms such as "Systems" for the hub);
  - the **swappable ground rule**: ground, deck and base art is a family keyed by region id, and one region exists today;
  - a note that later regions place deposits.

  It is frozen before the P0 direction briefs. The owner approves it together with OD-9. Every F006 brief, mockup and world-facing string cites its version (FR-047).
- **`WorldCompositionSettings`.** A presentation `ScriptableObject` (authored tuning, **not** gameplay content, so FR-001 holds). It holds:
  - the margin width in cells;
  - the depot cell rule per orientation;
  - the deposit cell rule;
  - prop density per stage and the prop-kind weights;
  - the hatch and edge rules.

  It is read by `LatticeLayoutBuilder`. It is edited only through Unity tooling (Inspector, MCP or batch editor script), never as hand-edited YAML. The World Designer owns its values and the Architect owns its shape.
- **World-facing strings.** The hub label, banner copy, stage names and site name are owned by the World Designer for tone and by UI/UX for length and fit, and are listed in `reviews/world-copy.md`.

**Alternatives.**
- F002 gameplay content: it would change the content schema, against FR-001.
- Code constants: no owner and no data path [W-2].

## R23 — Ore-vein marker (owner: add a vein marker)

**Decision.**

- **Family.** A new F006-DEPOSIT family: a vein sprite on `DepositCell`, a margin cell chosen by `WorldCompositionSettings`. In portrait it sits near the manual-mining control's side, never inside a hit shape or a badge keep-out.
- **Mining activity.** While manual mining is in progress (the existing mining hold or progress state), the vein shows a short 6-frame "chip" loop on the shared clock. Under reduced motion it shows a static highlight.
- **No hit target.** In F006 the vein is **not a hit target**. The mining control stays the input (FR-010), which avoids new gesture conflicts.
- **No pacing change.** Mining yield, rate and pacing are unchanged. Game Systems confirms that there is no pacing effect.
- **Composition.** The World Bible notes that later regions own real deposit placement. The vein grows no further than the stage rule allows: stage-variant dressing only.

**Alternatives.** HUD-only mining (not chosen by the owner); a tappable vein as an alternative input (deferred to avoid input conflicts; a future candidate).

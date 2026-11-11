# Research: World-First Factory Experience (Feature 006)

**Date**: 2026-11-11 | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

Each entry records the decision, why it was chosen, and the alternatives considered.

- **Fact** marks repository evidence (file:line or document).
- **Inference** marks the parent's reasoning.

Discovery and feasibility findings are in [reviews/discovery.md](reviews/discovery.md). Owner decisions are in the spec's Clarifications section (session 2026-11-11).

---

## R01 — Delivery order and gates

**Decision.** F006 stays one feature delivered in five ordered phases:

| Phase | Content | Gate at the end |
|---|---|---|
| **P0 Baseline and direction** | Current-state captures and baseline measurements (FR-046); ranked problem list; at least two rendered directions; owner direction decision | Owner direction record (SC-012 part 1). No production asset and no UI rebuild starts before it. |
| **P1 Enabling refactor** | No-behaviour-change split of the slot-grid view (R05); world-layout seam (R04); test layouts for US7; unified back stack (R11) | Existing suites green with no re-baseline beyond file moves |
| **P2 World art** (owner: art first) | Art Bible v1.1 additive families (R17); ground, deck, foundations, depot and links (R06, R07); activity and effect art (R12); integration on the seam | Art Director + Technical Art per family; SC-005 re-run on the new backdrop |
| **P3 Interaction and UX** | Machine presentation model (R08, R09); contextual card and details (R10); hub navigation (R11); building flow (R13); progression and milestones (R14, R15) | UI/UX review per surface; Play-Tester per batch |
| **P4 Verification and acceptance** | Full suites, B4 performance (R16), readability trials (R18), screenshot matrix, owner acceptance | SC-005 to SC-012 |

**Rationale.**

- The owner chose one feature with art first (Clarification Q5).
- The world-layout seam must exist before the environment, or the environment would hard-code 12 slots and break FR-037.
- The direction gate precedes everything (FR-040).

**Alternatives.**

- UX first on F004 art (rejected by the owner).
- Splitting the art into F007 (rejected by the owner).
- Interleaving art and UX per story (rejected: the art would be redone around UX layout changes).

## R02 — Baseline measurement method (FR-046)

**Decision.** Before any change, record the current build (`origin/main` at the start of P0) in `reviews/baseline.md`:

- **Screenshot matrix.** Every `ShellScreens.All()` class, plus emulator 1080x2400 portrait and landscape. States: no panel, machine sheet open, Contracts open, build sheet open, offline summary. Text at 100 % and 200 %.
- **Area shares (SC-001, SC-002).** Computed from UI Toolkit layout geometry in a Play Mode probe, not from pixel colours. The world rect is the camera's unobstructed rect; chrome is the union of persistent element world-bounds within the safe area.
- **Interaction counts (SC-003).** Taken from a scripted Play Mode journey that drives the real shell through `RuntimePanelHitTest` and the Input System test fixtures. Each pointer down and up pair or key press counts as one interaction.
- **Persistent text labels (SC-010).** The count of visible `Label` elements with non-empty text in the persistent layer at 100 % text.

**Rationale.** These are deterministic, re-runnable after F006, and need no human judgement. The F004 capture rigs (`RenderCaptureHarness`, the shell rig) already produce screen-class captures.

**Alternatives.**

- Pixel segmentation of screenshots: fragile against art changes.
- Manual counting: not reproducible.

## R03 — Visual direction mockups (FR-040, FR-041; owner: mockups only)

**Decision.**

- **Tool.** Mockups are produced by a deterministic compositor in `tools/art-pipeline` (new `mockup` command, Pillow, pinned through `uv.lock`). It places real F004 sprites and new concept sprites from the approved routes on the lattice at the exact device-pixel scale of each screen class (n from `CameraScaleModel` rules).
- **Interface layer.** The compositor draws the interface from a small layout description: panel rects in points, the pixel font and the body font, and Art Bible UI tokens.
- **Directions.** At least two directions: the Art Director recommends **C platform deck** dressed with foundations, plus one of **A walled hall** or a variant of C.
- **Sheets per direction.** Phone portrait 1080x2400, phone landscape 2400x1080, tablet 1536x2048. Each sheet shows:
  - an active factory with at least 8 machines in mixed states, linked to the central store (FR-013);
  - the machine card next to a selected machine, with camera framing;
  - the hub open;
  - one development-stage triptych (early, partial, established) for the progression screen;
  - one 200 % text variant (FR-041).
- **Comparison.** Each sheet set is compared against `Core-Idea.md` and the spec user stories in `reviews/direction.md`, with a recommendation.

**Rationale.**

- The owner wants mockups without an in-engine prototype.
- Compositing real sprites at real scale satisfies "no placeholder rectangles".
- A scripted compositor keeps the mockups reproducible and lets the later in-engine result be compared against them.

**Alternatives.**

- Hand-painted mockups: no reproducibility, and the owner has no art tooling.
- ComfyUI full-scene generation: does not respect the lattice, the palette or pixel scale.
- An in-engine prototype: declined by the owner.

**Risk.** The compositor's interface rendering approximates UI Toolkit text layout. **Mitigation:** the mockups are design intent; final acceptance uses real builds (FR-042).

## R04 — World-layout seam (FR-037, FR-038, US7)

**Fact.**

- The 12-slot assumption sits only in presentation: `SlotGridLayout.SlotCount = 12` (`SlotGridView.cs:232`), `ColumnsFor` returns 3 or 4 (`:260`), `SetColumns` accepts only 3 or 4 (`:718`).
- Callers: `CameraScaleModel` (`:184-195`, `:301`, `:316`), `RunCameraFraming` (`:79-82`), `RunShellDocument` (`:633`, `:672`), `CoreGameFoundationBootstrap` (`:852`).
- The domain `SlotCount` comes from content (`FactoryLevelDef.SlotCount`).

**Decision.**

- **`FactoryWorldLayout`.** Introduce an immutable world-layout description, a plain C# class in `Unity/Presentation/World/`. It is built from the authored location count and the screen orientation, and exposes:
  - `LocationCount`;
  - `Bounds` (content extent including the deck margin);
  - `AnchorOf(locationId)`, `FootprintOf(locationId)`, `HitShapeOf(locationId)`, `SortKeyOf(locationId)`;
  - `DepotAnchor` and `LinkPathOf(locationId)` (R07);
  - `DeckCells` (R06).
- **Builders.** `LatticeLayoutBuilder` reproduces today's 3- and 4-column lattice exactly, so the shipped game is unchanged. `TestLayouts` (Edit Mode and Play Mode test assembly only) builds 6-, 20- and L-shaped arrangements.
- **Consumers.** Camera framing, fit-all, pan limits, hit-testing, the floor and deck, cue overlay anchors and card focus consume the layout instead of `(columns, index)`.
- **Location identity.** The id is the existing slot index wrapped in a `BuildingLocationId` value type. Machine identity stays the existing instance id. Position is looked up, never used as identity.
- **Type shape.** The layout is a concrete class, not an interface, because there is one production builder (constitution principle V). The test arrangements are data produced by a second builder, not a second implementation.

**Rationale.**

- This is the smallest seam the Architect review proposed.
- It keeps saves keyed by slot index; a future free-placement feature maps slot to position behind this seam.

**Alternatives.**

- An interface with a strategy per layout kind: no second runtime variant yet.
- Generalising `SlotGridLayout` in place: leaves the 53 KB file growing (F004 finding A-4).

## R05 — Splitting the slot-grid view (no behaviour change)

**Decision.** Split `SlotGridView.cs` (1155 lines) into cohesive files under `Unity/Presentation/World/`, with no behaviour change:

| New file | Content |
|---|---|
| `SlotVisualModel.cs` | `SlotCondition`, `SlotVisual`, `SlotGridModel` (`Describe`, `BadgeFor`, `StateFor`) |
| `SlotLattice.cs` | `SlotGridLayout`, which becomes the lattice builder input of R04 |
| `FloorRange.cs` | `FloorRange` |
| `RunZoomSettings.cs` | Kept serialized with the same type name and namespace, so the scene YAML is untouched |
| `SlotRenderers.cs` | The pooled per-slot renderer set |
| `SlotGridView.cs` | The MonoBehaviour orchestration only |

- The `MonoBehaviour` class name, namespace, assembly and serialized fields stay identical. The `.meta` GUID of `SlotGridView.cs` is kept, so scene references do not change.
- Verification: all Edit Mode and Play Mode suites pass with zero test edits other than `using` changes. The settings-noise and scene-diff checks show no serialized change.

**Rationale.** F004 Architect A-4. A pure move first makes the later feature diffs reviewable.

**Alternatives.** Building F006 on the monolith (rejected by the Architect review).

## R06 — Environment: platform deck on a dark industrial base

**Decision.** This is the default for the mockups. The owner's direction decision in P0 can replace it.

- **Ground layer.** A static deck drawn with **Unity Tilemap** (`com.unity.modules.tilemap` is already in `Packages/manifest.json`). It has one `TilemapRenderer` per layer in chunk mode, on new sorting layers `WorldGround` and `WorldDeck` below the existing slot layers. The deck covers the layout's `DeckCells`, which are the unlocked locations plus a margin. Locked locations show a darker "hatch" tile. Outside the deck is the dark base: `ui_bg` with sparse base tiles that replace the F004 floor-dash family in the world view.
- **Foundation.** One foundation sprite per occupied location, drawn by the slot renderer set. It replaces the plate under an occupied machine, which keeps the per-slot layer count at or below 5.
- **Edges and decoration.** Edge, corner and T-junction tiles. Decoration props (crates, vents, lamps) at most at one prop per 3 deck cells. They are placed by a deterministic rule from the layout (cell hash), never inside badge or marker keep-out boxes (Art Bible §3.4).
- **Development growth (R15).** The deck extent follows the unlocked locations. Prop density and lamp count follow the stage.
- **Budgets.**
  - The Tilemap renders the deck as one or two batches. This replaces the F004 floor pool's separate SetPass (TA-2) instead of adding one.
  - Ground atlas at most 512x512 within budget #7.
  - Texture growth at most 1.5 MB against budget #6, which has about 2–3 MB headroom per the Architect review.

**Rationale.**

- Tilemap is the engine's batched static-ground path, is already installed, needs no new package and suits a future free-building grid.
- A deck that grows with unlocked locations makes progression literal (Art Director recommendation C).

**Alternatives.**

- Pooled `SpriteRenderer`s for the ground: more draw calls and more allocations to manage.
- One baked background image per layout: does not scale to N locations and is wasteful in memory.
- An outdoor yard: the palette cap would force a major Art Bible version.

## R07 — Central store depiction (FR-013, owner: links to a central store)

**Decision.**

- **Depot.** A depot structure placed by the layout at `DepotAnchor`: the lattice's front corner in portrait and its side in landscape. It is outside every hit area of a location and has its own hit area, which opens the Market inventory.
- **Links.** Every occupied location has a static link (a lane of link tiles on the deck) to the depot along the lattice lanes. `LinkPathOf` is computed by the layout from lattice adjacency. The paths are straight segments and L-turns only, and are deterministic.
- **Link activity.** A link shows activity with a small "pip" sprite that moves along the lane while its machine is in the Running phase. The pip's pace is proportional to the machine's cycle rate, clamped to the Art Bible animation limits. It never represents unit counts (FR-002). A stopped machine's link is still.
- **Depot fill.** The depot's appearance shows the store fill level in up to 4 discrete states derived from the existing store fill ratio. "Full" uses the existing storage-full cue semantics.
- **Reduced motion.** Pips are hidden. The link uses a static "active" tile variant, so activity stays visible without motion (FR-031).

**Rationale.**

- This is truthful to the global-store simulation.
- The links stay visual only and carry no gameplay meaning, so real conveyors can later replace them.

**Alternatives.**

- Decorative links between machines: rejected by the owner.
- No links: rejected by the owner.

## R08 — Machine presentation model and the completion signal

**Fact.**

- `RunSession.CopyMachineStates(Span<MachineStateView>)` is allocation-free and provides `Phase`, `Blocker`, `ProgressPpm` and `BatchFactor`.
- Per-machine completion exists only as the aggregate `RunSimulator.LastCompletions` and `TryGetLastCompletion`.

**Decision.**

- **Additive read model.** Add a monotonic, non-persisted `CompletedCycles` counter (long) to `MachineStateView`, maintained by the simulator per instance. It starts at 0 on session construction or restore. It is a read model only: no save field, no balance effect, no change to simulation results. Edit Mode tests prove that the trace and save fixtures stay byte-identical (SC-007).
- **Derived model.** `MachinePresentationModel` is a pure C# class in `Unity/Presentation/World/`. Each frame that the session revision changes, it fills a reused array of `MachinePresentation`. For each machine it computes:
  - the F004 `MachineVisualState` (existing `StateFor`, the single precedence);
  - `UpgradeAvailable` (`NextUpgrade.Available && Affordable`);
  - `CompletionsSinceLastFrame` (the counter delta, capped for display);
  - the `ActivityPace`, from `MachineRates.CycleTicks` bucketed into 3 animation paces.
- **Read pattern.** The model reads the snapshot fields only on revision change. An unchanged revision does no work and allocates nothing (the F001/T063 pattern).

**Rationale.**

- The spec requires that completion feedback never overstates output (FR-029).
- A progress-wrap derivation misses completions in batch, offline and multi-cycle ticks.
- The counter is the smallest exact signal. A-7 allows it, and the Architect recommends it.

**Alternatives.**

- Deriving from the `ProgressPpm` wrap: lossy.
- An event stream from the simulator: allocates, and adds a second observer mechanism.

**Owner visibility.** This is the one Core change in F006. It is listed in the plan's owner decisions for confirmation at the plan review.

## R09 — Shared cause and most important bottleneck (FR-026, FR-028)

**Decision.** `AttentionModel` is a pure C# class, recomputed on revision change.

1. **Shared cause.** Group machines whose blocker is `MissingInput` by `MachineBlockerInfo.ResourceId`. A group with at least 2 machines is a *shared cause*.
2. **Most important bottleneck.** Among resources with `ResourceRates.SupplyCoveragePpm < 1,000,000` that at least one active machine consumes, pick:
   - (a) the lowest coverage;
   - on a tie, (b) the most machines currently missing that resource;
   - on a further tie, (c) the content order of the resource.

   If no resource is under-covered, there is no bottleneck. Storage-full machines are handled by the store attention below, not ranked.
3. **Store full.** If any machine is `StorageFull`, the depot shows the full state and the store attention indicator.
4. **Where they show:**
   - A *shared cause* or a *bottleneck* shows one attention marker in the world on the depot link junction of the consuming machines, with the resource icon.
   - The persistent attention indicator shows the resource icon and the count of affected machines.
   - Tapping either opens the card of the first affected machine (FR-026).
   - Per-machine badges stay as in F004 (the single precedence rule). A running machine has no badge.
5. **Pure mapping.** No new gameplay rule: every input is an existing read value. Game Systems/Economy reviews the ranking at the plan review (Low tier) because it is a definition over existing values (spec FR-028).

**Rationale.** It is truthful, deterministic and allocation-free with reused buffers. It answers "what is the root cause" without new simulation.

**Alternatives.**

- A dependency-graph root cause through recipe chains: the global store makes upstream attribution speculative, so a chain-aware analysis waits for the conveyor features.
- Ranking by Cash impact: needs economy reasoning outside presentation.

## R10 — Contextual machine card and details (US3)

**Decision.**

- **Card.** One `MachineCard` UI Toolkit element, instantiated once and reused. It is positioned from the selected location's screen rect only when the selection or the camera changes, not every frame. Content, per FR-016:
  - name and level;
  - a state line (existing `RunRejectionCopy` and blocker wording);
  - a cause line with the resource icon;
  - the Upgrade button with cost and affordability;
  - a "Details" chevron.

  Upgrade updates the card in place and keeps focus on Upgrade (F004 N-3 behaviour preserved).
- **Placement per class.** Phone portrait: the card docks at the bottom with a compact height target of at most 30 % of the safe height at 100 % text. The camera keeps the machine above it through the existing `FactoryCameraController.OpenSheet(band, slotWorldRect)`. Phone landscape, tablet and desktop: the card docks beside the machine on the side away from the hub, or as a side panel when the space is narrower than the card.
- **Details.** "Details" expands to the existing `MachinePanelPresenter` content, reorganised (rates, progress, batch note, blockers with corrective actions, switch on/off, Items and boosts, Remove), as a sheet or side panel. Remove sits in Details behind `RequestConfirmation` (FR-019, A-5).
- **Close.** Tapping empty world, Back, Escape or the close control closes the card. Tapping another machine switches selection. The camera restores through `CloseSheet` (FR-017).
- **Accessibility.** Keyboard focus order moves from machine to machine, then into the card's actions. The card is a focus group. At 200 % text the state line and Upgrade stay in the first view, and the rest scrolls (spec edge case).

**Rationale.**

- One instance means bounded layout cost and no per-machine UI Toolkit (Architect condition).
- Reusing the presenter keeps every current capability (FR-018).

**Alternatives.**

- World-space sprite buttons: no text scaling, poor accessibility.
- A radial menu: rejected by UI/UX (44 pt at edges, focus order).

## R11 — Hub navigation and the unified back stack (US6, FR-020, FR-021)

**Decision.**

- **Hub entry.** The six-tab bar is removed from the persistent layer. A single hub entry takes its place: icon-only, with an accessible name. The name is chosen at the direction gate and must not be "Systems". The entry carries an aggregate attention dot.
- **Hub view.** The entry opens `HubPanel`: a sheet in phone portrait, a side panel elsewhere. It lists cards for Market, Contracts, Items, Research, Industry, Overdrive, Prestige and Settings, each with an icon, a one-line status from existing data, and an attention indicator.
- **System views.** Tapping a card opens the existing panel through `ShellPanelRegistry` unchanged. Industry gets its own card, which scrolls Research to the Industry section, or a registry id `Industry` that hosts `IndustryPanel` directly. The plan picks the registry id (cleaner Back behaviour), which is additive to `ShellPanelIds`.
- **Attention sources (A-6, existing data only):**

  | System | Attention when |
  |---|---|
  | Contracts | claimable or expiring offer |
  | Research | affordable node |
  | Prestige | ready |
  | Overdrive | ready (already shown by its persistent control) |
  | Market | the store-full attention |

- **`ShellTab`.** It becomes internal navigation state and is no longer a visible tab bar. The F005 badge feed (`ContractsShellFeed`) is re-pointed to the hub card.
- **Back stack.** `ShellNavigationStack` replaces the branching in `RunShellDocument.HandleBack`. It is an ordered stack of frames: dialog, pinned value, card, details, hub or system view. Back and Escape pop exactly one frame per frame-tick (idempotent within a frame), and focus returns to the invoker. A new panel registers a frame; it does not add a branch (F004 gotcha in the wiki page `run-shell-and-panels`).

**Rationale.**

- The owner chose a hub; UI/UX direction A.
- A stack removes the per-panel branching flagged in discovery.

**Alternatives.**

- A persistent icon rail: not chosen.
- Keeping the `HandleBack` branches: grows with every panel.

## R12 — Activity, effects and the shared clock (FR-029, FR-034, US5)

**Decision.**

- **Clock.** All motion uses the existing `PresentationAnimationClock` (integer milliseconds, `FrameAt`). There is no Animator and no per-object `Update`.
- **Activity.** Running machines keep the F004 6-frame loop at 8 fps. `ActivityPace` (R08) selects one of 3 loop speeds within the Art Bible limits: slow 6, normal 8, fast 10 fps. The flash limit is checked per composite.
- **Effect types (new art):**
  - completion pop: a resource icon rising one cell, 6 frames;
  - spark (Smelter, Press);
  - steam puff (Smelter);
  - build dust;
  - upgrade glint;
  - milestone banner sweep (UI).
- **Pooling and caps.** Effects come from a fixed pool of 8 renderers, with **at most 3 simultaneous world effects**. Completions arriving in the same 250 ms window on one machine coalesce into one pop. When more than 3 effects are requested, the oldest is cancelled; a completion pop has priority over decorative sparks.
- **Reduced motion.** Effects are not spawned. Loops freeze on frame 0. Completion shows as a static 1-second highlight of the output icon on the card or depot instead of motion. State remains fully identifiable (FR-031, SC-009).
- **Allocations.** None in steady state. The pool is pre-allocated at `Configure`.

**Rationale.** The caps and coalescing implement the noise limits (Art Bible §6.5) and the edge case for bursts after an offline return.

**Alternatives.**

- Unity `ParticleSystem`: shader-variant and overdraw risk against budgets #8 and #11, and it does not respect the pixel grid.
- `Animator` controllers: rejected in F004 R05.

## R13 — Building flow (US5, FR-032, FR-033)

**Decision.**

1. Tapping an empty unlocked location opens a compact build card listing the machine types with icon, cost and affordability. The existing `BuildSheetPresenter` data is reused.
2. While the card is open, available locations get an outline highlight and locked ones keep the hatch. A selected type shows a translucent preview: the machine's frame 0 at 60 % alpha in the palette-allowed ghost style (Art Bible §6), at the tapped location.
3. Confirm and Cancel sit side by side, both 44 pt. Back cancels.
4. Insufficient Cash shows a disabled Confirm and a shortfall line: "Need $X more", from the existing price and Cash values.
5. On success, the build dust effect and the foundation appear, and the card closes into the machine card of the new machine.

**Rationale.** It is contextual, keeps the current slot rules, and the preview works for any layout location (US7).

**Alternatives.**

- Drag-to-place from a palette: assumes free placement, which belongs to a future feature.
- Keeping the build sheet full-width: contradicts US3 and US6.

## R14 — Milestone presentation (FR-035)

**Decision.**

- `MilestoneWatcher` (pure C#) compares the previous and current snapshot on revision change and detects:
  - factory level increase, which also means newly unlocked locations;
  - prestige completion, detected by the run id changing after `PromotePrestige`.
- It raises a non-blocking banner (the UI layer, at most 2.5 s, dismissible, no input capture outside the banner), a deck-extension reveal of the new deck cells, and new foundations highlighted for 2 s.
- No reward is shown that the game did not grant. On an offline return, milestones that happened offline are listed in the offline summary instead of animating (existing summary data).

**Rationale.** It uses existing state only. A non-blocking presentation respects short sessions.

**Alternatives.** A modal celebration: interrupts play and would collide with the offline summary.

## R15 — Development stages (FR-036, owner: factory level + machines)

**Decision.**

- **Primary stage index.** `stage = clamp(floor(3 × unlockedLocations / totalLocations), 0, 2)` → early, partial, established.
  - `unlockedLocations` comes from the current factory level's slot count; `totalLocations` from the maximum factory level's slot count. Both are authored content.
  - A full unlock maps to established. The clamp keeps a zero-location configuration valid.
- **Secondary density.** `activeMachines / unlockedLocations` and `mean machine level / max level`, each bucketed into 3 bins. They drive prop and lamp density and the depot dressing within a stage.
- **What changes per stage:**
  - deck extent (always tied to unlocked locations);
  - edge structures: railing, then wall segments, then gantry;
  - prop density;
  - lamp count;
  - depot size variant.
- **Reset.** Prestige resets the stage, because the inputs reset (owner).

**Rationale.**

- The rule is monotonic within a run and derived only from authored, tracked values.
- It is explainable in one sentence for SC-006 trials.

**Alternatives.**

- Stage from Factory Value: an economy figure, so it needs a Game Systems definition.
- Stage from prestige count: excluded by the owner.

## R16 — Performance budgets and the B4 measurement

**Decision.**

- **Workload.** W004-V (F004 visual worst case) is extended to **W006-V**. It adds:
  - the full deck at the established stage;
  - all 12 links with pips;
  - 3 concurrent effects;
  - the card open.

  It is measured twice:
  - after P2 (B4a, art only);
  - after P3 (B4b, art plus UX).
- **Gates.** The F004 frozen budgets apply unchanged:
  - #5 zero steady-state allocations: Edit and Play Mode allocation tests, and a 600-frame Windows-player run;
  - #6 texture memory at most 8 MB;
  - #7 atlas pages at most 1024²;
  - #8 SetPass recorded and held, with the deck replacing the floor's extra pass;
  - #10 APK at most 150 MB;
  - #11 shader variants at most 490;
  - #4 Windows frame p95.
- **Emulator FPS rows** stay exploratory, as in F004 (emulator verdicts invalid; owner acceptance on emulator and Windows, A-9).
- **Method.** Leases `unity,emulator,gpu`, the same as F004 T444.

**Rationale.** The spec requires no regression (SC-008) and uses F004's proven method.

## R17 — Art Bible v1.1 (additive) and A-03

**Decision.**

- **Version.** Art Bible `F004-PAB v1.1.0`, a minor release (§16). It adds the families:

  | ID | Families |
  |---|---|
  | F006-GROUND | deck, base, hatch, edge, corner and T-junction tiles |
  | F006-FOUNDATION | foundations |
  | F006-DEPOT | 4 fill states |
  | F006-LINK | lane tiles: straight, L-turn, active variant, pip |
  | F006-PROP | props |
  | F006-EDGE | railing, wall, gantry |
  | F006-FX | the effect sheets of R12 |
  | F006-UI | hub, card and banner chrome sprites, only if UI Toolkit styling cannot express them |

- **Palette.** Reuse of existing ids only. No new hue. Family allowances are added in §5. `res_*` use needs an id-level allowance edit, which is minor.
- **Layer budget.** The per-slot layer limit stays at 5. The deck, links and props are counted in a new "world layer" budget recorded in §2.2 (Tilemap chunks). That budget needs an Art Director and owner sign-off, as part of the v1.1 sign-off.
- **A-03.** The badge overlap is fixed by moving the badge anchor up, within the plate's own row band (Art Director recommendation). The anchor change is minor, and SC-001 and SC-002 are re-run (SC-005).
- **Routes per family.** Blender for the depot, edges and props, which are multi-angle 3D shapes. Scripted drawing for tiles, lanes, pips and effects. ComfyUI only where the Art Director picks it, for props. All routes end in the deterministic pixel pass with provenance (F004 pipeline).

**Rationale.**

- Additive families keep the F004 style decisions frozen (FR-004).
- A palette increase would force a major version, which the owner did not ask for.

## R18 — Evaluation methods (SC-004, SC-005, SC-006, SC-012)

**Decision.**

- **SC-005.** Re-run the F004 SC-001 blind naming by an independent agent session (grayscale plus three CVD filters on the new backdrop, including portrait 720x1280 at n_min 2). Re-run the SC-002 owner timed trial with `tools/art-pipeline/trials/timed_trial.py`.
- **SC-004** (guidance). The same harness with a new trial set: 10 seeded factory captures with mixed states. The owner answers "which machines are stopped and why" within 3.0 s, and names the shared resource within 5.0 s.
- **SC-006** (guidance). An independent agent orders shuffled triptychs (10 trials, layouts and machine mixes varied).
- **SC-012.** `reviews/final-acceptance.md` with before/after sheets per class and state, and an owner verdict per surface.
- **Producers never review their own output.** Reviewers are fresh sessions.

## R19 — Test strategy and re-baselining

**Decision.**

- **Edit Mode:**
  - `FactoryWorldLayout` (lattice parity with today's `SlotGridLayout` for 3 and 4 columns, plus the 6, 20 and L test layouts);
  - `MachinePresentationModel` (state mapping, upgrade availability, counter delta across batch and restore);
  - `AttentionModel` (grouping, ranking, ties, no-bottleneck);
  - the development-stage function;
  - `MilestoneWatcher`;
  - `ShellNavigationStack`;
  - the effect pool caps and coalescing;
  - the `CompletedCycles` invariants (trace and save fixtures byte-identical);
  - the display-preference v2 reader (R20).
- **Play Mode:**
  - card open, close and restore with the camera on every `ShellScreens.All()` class at 100, 150 and 200 % text;
  - hub reachability and attention;
  - back-stack order with Android Back and Escape;
  - the build flow with enough and too little Cash;
  - reduced motion (zero moving decorative pixels);
  - allocation gates;
  - US7 test layouts through the real view;
  - the baseline and final geometry probes (R02).
- **Tagged `StandalonePlayer`.** The 600-frame W006-V run and a restyled-scene smoke test.
- **Emulator journey.** A new `-Scenario F006`: select, upgrade, hub to Contracts and back, build, rotation with the card open.
- **Re-baseline.** Tests asserting the six-tab bar or `HandleBack` branches (`RunHudLayoutTests`, shell rig tests, F004 and F005 emulator scenarios, `AndroidBackGestureTests` expectations) are re-baselined with a recorded old-to-new diff (`reviews/test-rebaseline.md`), never deleted silently (F004 R15 rule).

## R20 — Zoom-button preference and display preferences v2 (FR-008a)

**Fact.** `display-v1.json` is `{version:1, reducedMotion}`, read by a strict reader. A future version 2 currently reads as invalid, falls back, and is overwritten (F004 Security S-1).

**Decision.**

- **Schema.** Add `display-v2` with `{version:2, reducedMotion, showZoomButtons: bool}`. The reader accepts v1 (maps `showZoomButtons=false` on touch) and v2.
- **Migration.** v1 migrates to v2 on the first change. A v3 or unknown version falls back without data loss beyond these two presentation choices.
- **Settings sheet.** It gains "Show zoom buttons".
- **Pointer profiles.** They always show zoom controls.
- **Review.** A Security/Privacy Low-tier review of the reader change at the plan review. It is local presentation data and not the save.

**Alternatives.** Always showing zoom on touch: contradicts the owner's answer to Q2.

## R21 — Roles, tiers and resources

- **Roles.**
  - Mandatory: PO, Architect, Test/QA, UI/UX, Art Director, Technical Art, Performance, Play-Tester (per batch).
  - Light reviews at Low tier: Game Systems (R09 ranking, R15 stage function) and Security/Privacy (R20 reader).
  - Omitted: DevOps (no build or pipeline change beyond the existing scripts) and Sound (audio out of scope).
- **Tiers.** Default Medium (`sonnet`). R04, R05, R08 and R11 are cross-boundary and run Medium at `high` reasoning. High tier (`opus`) only on a recorded unresolved risk.
- **Leases.** Unity, emulator and GPU leases per AGENTS.md. Art batches in P2 need `gpu`. Mockup compositing (R03) needs no GPU unless new concept art is rendered.

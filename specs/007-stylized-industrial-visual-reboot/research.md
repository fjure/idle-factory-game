# Research: Visual Direction Reboot — Stylized Industrial 2.5D (Feature 007)

**Date**: 2026-11-27 · **Spec**: [spec.md](./spec.md) · **Plan**: [plan.md](./plan.md)

**Evidence labels**: every number below carries a label.

- **measured**: cites a committed record.
- **derived**: arithmetic on measured or stated values, shown in the text.
- **hypothesis**: unverified. The named task measures it.

Hypotheses are not evidence (constitution III, Art Bible §0 convention).

---

## R01 — Milestones, gates and when tasks are generated

**Decision.** F007 runs as six gated milestones:

| Milestone | Scope |
|---|---|
| M1 | Audit |
| M2 | Briefs, shared foundations, three full prototypes, comparison |
| M3 | Owner decision |
| M4 | Art Bible v2, World Bible v0.3 production rules, pipeline v2, budgets |
| M5 | Incremental production |
| M6 | Final acceptance |

`$speckit-tasks` now generates tasks for **M1–M3 only**, plus the World Bible v0.3 mood task, which gates M2 scoring (FR-007a). M4–M6 appear in `tasks.md` as a placeholder phase. After the M3 record exists, `$speckit-tasks` is re-run, followed by `$speckit-analyze`, the ledger refresh and `$speckit-maqa-sync` (FR-004).

**Rationale.**

- The production tasks depend on the chosen direction, so tasks written now would encode a guess.
- The constitution requires `analyze` after stable tasks, and tasks for M4–M6 are not stable before M3.
- F006 used the same pattern for its direction gate (OD-9b, research R01 of F006).

**Alternatives considered.**

- *Generate all tasks now with three conditional branches.* Rejected. It triples the ledger, and two thirds of it would be dead after M3.
- *Separate features for exploration and production.* Rejected. The owner frames one feature, and the success criteria span both halves (SC-001 compares the final build against the F006 baseline).

## R02 — The F006 closeout running in parallel

**Decision.** F007 neither reads nor writes F006 task, review or Issue state. These are the touch points:

- **Moved carries.** AD-1 (world upgrade marker), AD-4 (deck edge and void) and AD-7 (store-full marker scale and cap) are listed in the M1 problem register as F007 items. The F006 parent records the move in F006 `reviews/p3b-followups.md` during its own closeout, not in F007.
- **PERF-9.** The M1 device baseline (R16) measures the F006 look on the physical phone. Its record links back to F006 PERF-9. The F006 parent closes PERF-9 by citing it.
- **Merge order.** M5 family merges wait for the F006 T549 verdict to be on `main` (FR-002). M1–M3 work does not wait.
- **Shared resources.** The F006 trials (T546) and the F007 captures both use the emulator and Unity leases. These are coordinated through `scripts/resource-lease.ps1` and never run in parallel.

**Rationale.** Keeping the two features apart prevents a cross-feature ledger race (AGENTS.md: GitHub Issues are the status authority per feature).

## R03 — Audit method (M1, US1, FR-006)

**Decision.** M1 is one audit record, `reviews/audit-f007.md`, with four parts.

1. **Captures.** The F006 look on `main` is captured with the existing capture rig (`W006-V` and the T562 trial seeds) for the four FR-011 classes and both factory states. The factory states are described in R08.
2. **Metrics on the baseline.** The audit measures the SC-004 machine area and the SC-006 dead-area share, by the methods in R18. It also measures the per-family texture memory from the Android build report, and the counts of renderers, SetPass calls and variants from the B4b method in F006 T544.
3. **Constraint dispositions.** A table lists every F004 rule (§2, §3, §5, §6, §7, §8, §10, §14 and §16 of `F004-PAB v1.1.2`). Each rule gets a verdict per direction (preserve, relax, replace or remove) and a reason. The Art Director authors the table; the Technical Artist and UI/UX co-sign the rows they own.
4. **Problem register.** Problems are ranked and given evidence links. The register includes AD-1, AD-4 and AD-7, plus the six problems in the spec's "Current visual state" section.

The physical-device baseline (R16) is part of M1.

## R04 — Direction A: Enhanced Pixel Art

**Decision (prototype definition).** Direction A keeps a recognizable pixel identity but drops the constraints the audit marks as causes:

- **Density.** A higher density (**D48**: 48 art px per cell, twice D24) at integer scaling. On the emulator class, the `n_min` rule gives `n_min = ceil(24 · 2.625 / 48 − 0.01) = 2` (derived), and 1 at the smaller classes. Integer zoom levels are kept (hypothesis that this fits, verified in M2).
- **Palette.** An extended palette of up to 96 colours with per-material ramps of 5–6 tones.
- **Shading.** Hand-placed or render-derived highlights and ambient occlusion, quantised to the palette. Ordered dithering is allowed only on large flat surfaces.
- **Outline.** Selective outlines instead of a uniform `ink_0` outline.
- **Light.** The 2D Light envelope L2 of the Art Bible is adopted for the dusk mood.
- **Pipeline.** The Blender rig plus the existing pixel pass, re-parameterised for density and palette.

**Rationale.** Direction A answers the owner's question "is pixel art salvageable?" at a fair level of effort. D48 doubles the linear detail, which is enough to draw drills, furnaces and press rams. The pixel pass already exists, so A costs the least new tooling.

**Known cost.**

- 4× the texel count of D24 for the same canvas (derived).
- Under RGBA32 point-filtered art, the 1 MiB page cap allows a 512 × 512 page, so a 160 × 128 machine frame (the D48 equivalent of 80 × 64) fits **9** frames per page with padding 2 (derived: 3 × 3; a fourth row of 128 px plus padding exceeds 512).

**Alternatives.**

- *D32.* Rejected: too little gain to answer the question.
- *D64.* Rejected for A: it approaches B's cost while keeping the pixel limitations.

**Plan review amendments (2026-11-27, `reviews/plan-reviews.md`).**

- **Light (AD-3).** The A brief states the cap of Point Light2D per screen (at most 12, as in L2) and the order of operations: palette-baked ramp first, then an additive Light2D accent. The resulting variant delta is reported in `performance-m2.md`.
- **Page plan (TA-9).** 9 frames per 512² page.

## R05 — Direction B: Stylized 3D-to-2D

**Decision (prototype definition).** Detailed stylized 3D models are rendered from the existing orthographic isometric 2:1 rig into high-resolution sprites:

- **Rig.** `rig-v3.blend` is derived from `rig-v2.blend` with the same camera, so the projection is unchanged. It has a three-light dusk setup: a warm key, a cool fill and a rim.
- **Renderer.** Cycles with ambient occlusion and contact shadows. Filmic or AgX tone mapping is avoided, and the Standard view transform is kept, so colours stay predictable across families (hypothesis, checked by the Art Director in M2).
- **Shadows.** Shadows are baked into a separate soft-alpha shadow sprite, so the shadows of neighbouring locations can blend.
- **Materials.** Stylized PBR: base colour, roughness, edge wear through a curvature mask, and hand-painted accents. Textures are procedural in Blender, or ComfyUI-generated tileables under R14.
- **Resolution.** Prototype resolution is **128 art px per cell** at 1×, with bilinear filtering and mipmaps for zoom-out. This is derived as follows:
  - In the F006 phone portrait capture `f01-grid12-p100.png`, a location plate is about 210 device px wide. That is the 70-art-px lattice pitch at n = 3 (measured on the capture: about 175 px at the 900 px display width, scale factor 1.2).
  - With a ≥ 1.5× area (SC-004, about 1.22× linear) and a denser pitch (FR-005), a machine covers roughly 260–320 device px.
  - 128 px per cell gives machine frames of about 256–320 px, which is close to 1:1 on the reference phone. On the tablet and when zoomed in, the art is mildly upscaled.
  - This is a hypothesis. M2 compares 128 against 192 on the phone for sharpness against memory.
- **Animation.** Rendered frame sequences, 12–16 frames per loop, played on the shared `PresentationAnimationClock`. Moving parts (drill head, press ram, assembler arm) can optionally be rendered as separate overlay sprites, so a loop needs fewer full frames.
- **State light.** A separate emissive mask sprite per machine (lamps, furnace glow). It is driven by state, so a stopped machine's lights can switch off without re-rendering the body (FR-027).

**Canvas (corrected, TA-2).** At 128 px per F006 art cell (24 art px), the F006 machine canvas of 80 × 64 art px scales by 128 / 24 = 5.33 to about **427 × 341 px** per frame (derived).

**Memory estimate (corrected, TA-1, TA-2, PERF-3; derived, a hypothesis until it is measured from the M2 build report).** ASTC stores 128 bits per block. That is 0.444 B/px at 6×6 and 0.25 B/px at 8×8. One frame of 427 × 341 px is 145,607 px. Stage 0 for four machines with 14 frames each:

| Layer | Pixels | ASTC 6×6 | RGBA32 |
|---|---|---|---|
| Beauty, 56 frames | 8.15 M | 3.62 MB | 32.6 MB |
| Emissive mask, 56 frames (full resolution) | 8.15 M | 3.62 MB | 32.6 MB |
| Shadow, 1 still per machine, R8 | 0.58 M | 0.58 MB (R8) | — |
| Total with mips (+33 %) | | **≈ 10.4 MB** | ≈ 88 MB |

M2 adds the depot, the environment and one stage-1 machine, for roughly 13–16 MB. M5 with 2–3 stages per machine lands at roughly 25–30 MB.

Levers, each to be measured in M2:

- an emissive mask at half resolution or as a single channel;
- moving parts as overlay sprites, so a loop needs fewer full frames;
- ASTC 8×8 on large, calm surfaces;
- tight sprite meshes.

**Consequence.** Direction B cannot fit the current 8 MB world-art budget. It needs compression and a new memory budget (FR-032) in any case. The M2 comparison reports B's real figure from the build report. Testing 192 px per cell is limited to **one** machine (2.25× the texels).

**Rationale.** This is the owner's preferred candidate. It reuses the existing Blender toolchain, camera and projection. It produces material depth, AO and lighting offline at no runtime cost, and it keeps the URP 2D renderer and the sprite-based presentation architecture unchanged.

**Risks.**

- Sprite memory grows quickly.
- Animation is limited to pre-rendered frames.
- Compressed textures on Android are unverified against the load hang (R13).

**Plan review amendments (2026-11-27, `reviews/plan-reviews.md`).**

- **View transform (AD-9).** Standard stays. Emissive light comes only through the emissive mask, without bloom. One asset is rendered once under AgX as a recorded comparison parameter; it does not change the route.

## R06 — Direction C: Hybrid 2.5D

**Decision (prototype definition).**

- The floor and environment stay as 2D sprites in the B style, at lower detail where possible.
- The four machines and the depot are real-time 3D meshes with a stylized lit shader, using the same models as B where practical. This reuse lowers the cost of building three prototypes.
- A single real-time directional light plus baked ambient gives the dusk mood.
- The UI Toolkit UI is unchanged.

**Renderer constraint (fact, URP 17).** The project uses the URP **2D Renderer**. 3D meshes drawn by the 2D Renderer get no 3D lighting. Mixing 2D-Renderer and Universal-Renderer cameras in one camera stack is not supported. That leaves three ways to build C:

| Route | What it is | Assessment |
|---|---|---|
| C1 | The world camera switches to the **Universal Renderer** (Forward+). Sprites are drawn as transparent sorted renderers in that renderer, and meshes get real lighting. | A renderer change touches the F004 URP foundation: parity tests, variants and SetPass. Recommended for the prototype, because it is the only route that renders 3D meshes with real lighting at screen resolution. |
| C2 | Meshes render into a RenderTexture through a second camera (Universal Renderer), which is composited as a sprite. | Costs an extra target and fill rate, and adds a resolution trade-off. |
| C3 | Meshes use Sprite-Lit or custom shaders inside the 2D Renderer, lit only by 2D lights. | No 3D light direction or shadows. This is closer to B, at more cost (reworded, ARCH-13). |

**Rationale.** C exists to test whether real-time machinery adds enough life and flexibility to justify its cost:

- free rotation later;
- dynamic lights;
- any zoom without resolution limits;
- no per-frame sprite memory.

It must be built honestly, which means C1. Like all M2 work, the C1 renderer change is confined to the development-build look selection (R07). Release builds stay on the 2D Renderer until M3 decides.

**Feasibility gate (hypothesis).** A one-machine C1 spike runs first in M2 (T6xx "C feasibility"). It must show four things:

- sprite sorting against meshes is correct;
- the cue overlay and hit-testing are unchanged;
- shader variants are reported;
- the frame time on the phone is within 2× the F006 baseline.

If the spike fails, the owner is told before the full C prototype is built. Per Q2, C is still built to full parity unless the owner decides otherwise on that evidence.

**Plan review amendments (2026-11-27, `reviews/plan-reviews.md`).**

- **Route for C1 (ARCH-2, decision).** C uses a **dev-only second URP asset** with a Universal Renderer (Forward, not Forward+). DevTools sets it through `QualitySettings.renderPipeline` before the first frame. The shipped URP asset keeps exactly one 2D renderer, so `RenderPipelineConfigurationTests` stay scoped to it. The release check asserts that the variant count (490) is unchanged and that no `UniversalRendererData` is in the release build.
- **Sorting (ARCH-3).** Before the full C prototype, the spike decides between two options: depth-placed sprites, or meshes in the transparent queue with sorting orders. Exit criteria: correct order of floor, mesh, shadow, marker, effect, selection frame and indicator in a test scene with overhang.
- **Fair C brief (AD-1).** C gets:
  - a defined ramp or toon shader with 3 bands and a rim;
  - a contact-shadow blob per machine;
  - at most 4 warm real-time point lights for lamps, or emissive plus light probes;
  - a directional key in the dusk direction.

  C reuses B's models. The brief records how a model flaw (shared with B) is told apart from a rendering flaw (C only).
- **Spike gate with absolute budgets (PERF-6).** The gate runs on the Pixel, in the one-machine scene with W007-D conditions:
  - frame time p95 ≤ 16.7 ms and GPU time ≤ 8 ms at 60 Hz (hypothesis thresholds, which the owner can adjust);
  - variant and SetPass deltas reported;
  - the URP asset settings listed: depth and opaque texture off, MSAA, HDR and post off, at most 1 shadow cascade or baked blob shadows only, SRP Batcher compatibility of the sprite shader.
- **Settings review (OPS-5).** `check-settings-noise.ps1` does not cover GraphicsSettings or QualitySettings. Every commit that touches a URP asset or the quality settings is reviewed by hand with its diff.

## R07 — How the prototype is isolated: the look profile

**Decision.** A presentation-only **look profile** selects the art set used by the world views:

- the sprite catalog, the indicator glyph set, the environment family and the camera policy;
- for C, the mesh prefab set and the world-camera renderer asset.

The profiles are:

- `F006` (current, default);
- `F007-A`, `F007-B` and `F007-C` (prototype);
- later, `F007` (production).

**Where the switch lives.** The profile is chosen through `IdleFactory.DevTools`, which compiles only with `IDLEFACTORY_DEV` (`PlatformBuildCommands.DevToolsDefine`). Release builds have no switch and always use the profile named in the shipped presentation settings. Nothing is persisted in the save. In a development build, the choice may persist in the display-preferences file, which already holds presentation-only preferences (F006 FR-003).

**Where the art lives.** Prototype assets are in `Assets/IdleFactory/Art/F007/Prototype/{A,B,C}/` and their own atlases. They are referenced only from prototype profile assets, which only the DevTools assembly references. This keeps them out of release builds, which the M2 build-report check verifies (hypothesis: Unity includes only referenced assets; verified by the build report).

**Rationale.** This reuses the existing seams:

- the `PresentationSpriteCatalog` registry;
- `FactoryWorldLayout` and `WorldCompositionSettings`;
- `DeckPlan`;
- the effect pool;
- the attention markers.

No simulation path changes (FR-009).

**After M3.** The production look becomes a profile that ships as the default (FR-035a: families go live as they merge). Prototype profiles and assets are deleted, or archived outside `Assets/` under `art-source/f007/prototype/` (A-8).

**Alternatives.**

- *Branches per direction.* Rejected: comparison captures must come from one build at one commit.
- *Scene copies.* Rejected: scenes are edited only through Unity tooling, and three scene copies would triple the serialized-change review.

**Plan review amendments (2026-11-27, `reviews/plan-reviews.md`).**

- **Isolation mechanism (ARCH-1).** Prototype profiles and assets are referenced by a **dev-only scene** (or prefab) with a DevTools holder component. `PlatformBuildCommands.Run` adds that scene to the build only when `IDLEFACTORY_DEV` is among the `extraScriptingDefines`. There are no `Resources/` paths and no StreamingAssets.
- **Release check (QA-3).** `scripts/check-release-build.ps1` is extended to check `BuildReport.packedAssets` by path and GUID. It fails on:
  - any source under `Art/F007/Prototype/`;
  - any `LookProfile` with `isPrototype`;
  - any `UniversalRendererData`;
  - prototype atlas names in the built data.

  Positive control: the dev build must contain them. An Edit Mode guard checks that no asset or assembly outside DevTools references prototype assets.
- **Ownership and initialization order (ARCH-6).** The bootstrap owns **one** `LookProfileResolver` instance and passes it to the views, the camera settings and the effect pool. DevTools injects an `ILookProfileSource` at `BeforeSceneLoad`, mirroring `DefaultTimeScaleSource`. There are no new statics, and there is one switch path (rebuild plus refit).
- **Requirement tables (ARCH-5).** Every profile carries its own requirement table (keys, sizes, pivots). The existing static registry stays the table of the `F006` profile.
- **Invalid dev selection (ARCH-11).** An unknown profile id in the preferences file falls back to the shipped default with a log entry. Release builds ignore the key, and a test covers both.
- **Cleanup after M3 (ARCH-12).** The cleanup covers:
  - the prototype assets and profiles;
  - the dev scene;
  - the second URP asset;
  - per-profile tests of rejected looks;
  - the test-player build inputs.

  The release check runs again afterwards.

## R08 — Comparison capture protocol (FR-011, FR-012, FR-013)

**Decision.**

- **Factory states.** Two seeded fixtures are reused from the F006 trial seeds:
  - **Partial**: 5 occupied locations, stage 1, mixed running and one missing-input state.
  - **Dense**: 12 occupied locations, the latest stage, mixed states including storage full, paused, missing input and an upgrade available.
- **Classes.** These are the four FR-011 classes:

  | Key | Class |
  |---|---|
  | emuP | 1080 × 2400 at 420 dpi |
  | emuL | 2400 × 1080 |
  | tabL | 2560 × 1600 Windows-player tablet class, as in the F006 final captures |
  | phoneP-card | 720 × 1280 at 320 dpi, machine card open, at 100 % and 200 % text |

- **Framing.** All four looks use the same camera rule: fit all locations into the unobstructed rectangle, with the card-open framing for phoneP-card. They do not use identical world coordinates, because B and C may propose a denser geometry (FR-005). Each direction is captured twice:
  - **"Same geometry"**: the F006 lattice spacing, for a strict like-for-like comparison.
  - **"Proposed geometry"**: the direction's denser arrangement.

  The comparison sheet therefore shows the F006 look and A, B and C, each in both geometries.
- **Source.** Captures come from real builds: the Android development APK on the emulator and the phone, and the Windows player. The existing capture rigs and `scripts/android-emulator-journey.ps1` are used. Each capture records the commit, profile, seed, class, text scale and provenance.
- **Motion.** One short clip (an APNG strip, as in F006) per direction shows the animated process.

**Rationale.** Equal rules make the comparison fair. The second geometry shows what densification buys, without mixing that gain into the style judgement.

**Plan review amendments (2026-11-27, `reviews/plan-reviews.md`).**

- **Sidecars (QA-9).** The seed ids and the commit of the F006 trial fixtures are pinned in a fixture list (`reviews/comparison/fixtures.json`). The sidecar fields are fixed: commit, profile, geometry, class, text scale, seed, factory state, build id and the measured framing rectangle. The sheet generator rejects sidecars that do not match their file name or commit. One cell is captured twice as a determinism check.
- **640 × 360 (UX-5, FR-011a).** A reduced check per direction: dense state only, at 100 % and 200 % text, proposed geometry only. It is listed in the M3 brief and is a binding class in M6.

## R09 — Location geometry and densification (FR-005, SC-004)

**Fact.**

- On-screen location spacing comes from the lattice constants in `SlotGridLayout` (70 × 54 art px lattice) and from the `LatticeLayoutBuilder` columns: 3 in portrait, 4 in landscape.
- The location count is the constant 12 in `SlotGridLayout` (`SlotLattice.cs:20`). `LatticeLayoutBuilder.ShippedLocationCount` reuses it, and test layouts pass other counts (ARCH-9; corrected).
- Selection, hit-testing and cue anchoring all go through `FactoryWorldLayout` and `WorldTapResolver`.

**Decision.**

- The lattice pitch and the plate and deck dressing become per-look-profile layout parameters. They are a presentation asset, extending `WorldCompositionSettings`, not constants.
- A direction may propose a denser pitch and machine-group arrangements, such as bays on a shared slab. Grouping never follows the production chain (WD-4). Every location keeps:
  - its id;
  - its unlock order;
  - a non-overlapping hit area of at least 44 pt;
  - an unambiguous cue anchor.

The M3 decision fixes the production pitch and arrangement.

**Guard.** The existing layout-independence tests (6, 20 and L-shape layouts) run against every profile.

**Plan review amendments (2026-11-27, `reviews/plan-reviews.md`).**

- **Pitch as data (ARCH-4).** T610 turns `PivotOfCell` and `DeckTileRect` (currently static with `const` pitch, 16 call sites) into instance methods of `FactoryWorldLayout`. It adds the pitch to the equality and `IdentitySuffix` of `LayoutComposition`, so the camera refits. The pitch is expressed in **world units**.
- **Grouping (WD-4).** Shared foundations group locations only by site zone or density. Grouping is identical for all machine types and unlock orders, never follows the item chain, and carries no direction marks. Every location keeps its own visible foundation and indicator anchor ("bays on one slab").

## R10 — State indicators built into the machine (FR-027, FR-027b, FR-028, FR-029)

**Decision.**

- **Anchor.** Each machine artwork defines an **indicator anchor**: a lamp or beacon position in the machine manifest. The indicator is drawn there as a small overlay sprite with a per-state shape glyph. The shapes carry over from the S3 = b set (circle-slash, triangle and similar), because their blind-naming results are measured (F004 S3, F006 SC-005).
- **Size.** The prototype size target is about 60 % of the F006 badge's on-screen area. This is a hypothesis: M2 measures blind naming at that size and adjusts.
- **Machine behaviour.** Behaviour carries state together with the indicator:
  - running: animated, work lights on;
  - stopped: frame 0 or a dedicated still, work lights off through the emissive mask;
  - paused: lights dimmed;
  - storage full: a full-hopper visual where the art allows it.
- **No second precedence rule.** The state comes from the existing badge precedence.
- **No colour-only cue.** Shape plus position plus motion keep FR-028.
- **Upgrade marker (AD-1).** It becomes a small world glyph in the same indicator family, outlined and placed off the machine's top silhouette.
- **Store-full marker (AD-7).** It is drawn once at the depot, not per machine. Per machine, a storage-full state is shown by the indicator.

**Evaluation.** The F004/F006 blind-naming protocol (grayscale plus three colour-vision filters) and the owner's timed trial run on the M2 dense captures for every direction (SC-008, SC-009 guidance at M2, binding at M6).

**Plan review amendments (2026-11-27, `reviews/plan-reviews.md`).**

- **Glyph floor (UX-1, AD-7).** The 60 % figure applies only to the salience of the lamp housing. The **glyph** keeps the F004 floor: at least 24 pt at `n_min` on every required class (including 720 × 1280 and 640 × 360), with strokes of at least 2 art px or the equivalent. A per-class pt-size check is part of the tests.
- **Normative carrier (UX-2).** The glyph alone carries the state normatively. Lights and motion are secondary, because they disappear with reduced motion or on machines without an emissive area.
- **Contrast (UX-3).** The glyph sits on its own small plate or carries an outline, with a luminance-contrast floor (≥ 3:1 for the glyph against the plate, checked per direction). The upgrade glyph (AD-1) uses the same plate family.
- **Keep-out per look (AD-7).** The F004 overlay keep-out (§3.4) assumes overhang 0. It is derived again per look, and the indicator is tested in a dense-cluster capture.
- **Upgrade marker (AD-8).** It may move to the side when the denser pitch would collide with the neighbour above. Captured in the proposed geometry.

## R11 — Build-out stages (FR-027a)

**Decision.**

- Each machine type has 2–3 stages, selected by `level ≥ threshold`. The thresholds live in an authored presentation asset (`MachineBuildOutStages`), keyed by machine definition id.
- Threshold values come from the existing level data. Defaults are proposed by Game Systems/Economy (Low tier), for example the levels at which a machine's batch factor or rate tier changes, if such breakpoints exist in content. The proposal is reviewed for "no pacing change".
- The stage is a pure function of `(definition id, level)`. It is not persisted and not part of the simulation.
- The art adds parts on a fixed base silhouette, so the hit area and indicator anchor stay stable across stages. Silhouette identity across types is checked for every stage (SC-003).
- **M2 scope**: two stages of one machine type per direction. **M5 scope**: the full set.

**Plan review amendments (2026-11-27, `reviews/plan-reviews.md`).**

- **Fixture (GS-3).** The M2 stage-1 fixture uses a level that a real player can reach.
- **Pending owner decision O-3.** Default thresholds 10 / 25 / 50 from the existing speed milestones (`Assets/IdleFactory/Content/Iron/iron-factory-content.json`, `milestones`). Because the stage is a pure function of the current level, it drops back with prestige.

## R12 — Animation, effects and flash

**Decision.**

- All animation stays on the shared `PresentationAnimationClock` (F004 R05, F006 R12): frame = `floor(t · fps) mod n`.
- Reduced motion freezes the clock to frame 0 for machines and environment. Effects are suppressed and static state stays readable (FR-030, SC-010).
- The F004 flash check (relative-luminance change over the opaque area, at most 3 per second) is colour-model independent, so it carries over to B and C unchanged. For C, a Play Mode capture runs the same check on rendered frames.
- Offline-arrival and prestige rules from F006 R12 are unchanged (FR-031).

## R13 — Texture formats, filtering and the Android page cap

**Facts (measured).**

- A 2 MiB RGBA32 atlas page hung Android loading in the reference emulator. Pages of 1 MiB or less load in about 3 s (wiki `f006-android-load-hang-atlas-page-cap`).
- The cause was not identified, so the rule is empirical.

**Decision.**

- **A** keeps RGBA32, point filtering, no mipmaps and pages of at most 1 MiB.
- **B and C** sprites use **ASTC** on Android: 6×6 as the default, 4×4 for small high-detail parts. They use bilinear filtering with mipmaps.
- **Windows player**: DXT5 or BC7.
- **Page-cap re-measurement.** The cap is re-measured for compressed pages in a dedicated M2 measurement, both on the emulator and on the phone:
  - a byte-matched sweep in ASTC 6×6: pages of 1448² (≈ 0.9 MiB) and 2048² (≈ 1.8 MiB). A texel-matched control holds the texel count and varies the bytes, through RGBA32 versus ASTC at the same size (TA-1, TA-3);
  - load time and the hang signature from the F006 journey `isLoading` gate.

  Until that measurement passes, compressed pages also stay at 1 MiB or less of GPU memory (FR-032).

**Hypothesis.** The hang relates to the byte size of a single upload or read rather than to texel count. An ASTC page of about 0.9 MiB is expected to load like a 1 MiB RGBA32 page. The M2 sweep verifies or refutes this; the cause stays unknown until then.

**Plan review amendments (2026-11-27, `reviews/plan-reviews.md`).**

- **The phone is authoritative (PERF-2, TA-3).** For each device, record whether ASTC is supported natively (extension list through `adb`). The emulator may decode in software and then does not predict the phone. Each page variant is loaded cold 3 times, and the maximum is reported, using the `isLoading` gate. `AtlasPageBudgetTests` stays strict at 1 MiB until the phone passes (QA-11).

## R14 — Art pipeline v2 (FR-019, FR-034)

**Decision.** `tools/art-pipeline` is extended, not replaced.

- **Route.** A new route `render-hd` covers the steps below.
  1. **Render.** Blender `rig-v3.blend` renders with the committed `render.py` parameters (samples, seed, view transform and passes: beauty, shadow, emissive mask, optional normal).
  2. **Post.** Trim to canvas and pivot. Write the soft-alpha shadow sprite. Write the frame strip and the manifest (canvas, pivot, footprint, indicator anchor, effect anchors, stage, frames, fps).
  3. **Checks.**
     - canvas and pivot;
     - frame count;
     - flash;
     - silhouette IoU at the phone fit-all scale;
     - indicator-anchor-inside-silhouette;
     - contrast of the indicator over its own machine;
     - colour-space sanity (no out-of-range values);
     - texture-memory estimate per family against the budget.

     The palette, alpha-0/255 and outline checks become **direction-specific**: on for A, off for B and C.
- **Reproducibility.** Blender regeneration is accepted within a tolerance: per-pixel ΔE ≤ 2 on at least 99 % of opaque pixels, and an SSIM threshold, replacing the "0.5 % of opaque pixels" rule. This is a hypothesis, confirmed in M4 on two re-renders.
- **Provenance.** Fields as in F004 §15, with these additions:
  - `look`, the look profile;
  - `renderPasses`;
  - `textureFormat`;
  - `generatedTextures[]` (model, licence, prompt, seed) for ComfyUI material textures.
- **Generative AI.** ComfyUI (Z-Image-Turbo, Apache-2.0) may be used for:
  - concept images in the direction briefs, labelled "concept, not in-engine evidence";
  - tileable material textures for Blender.

  Generated outputs are corrected or approved by the Art Director before use. New models are downloaded only with owner approval. Paid hosted providers are not used without owner approval (A-10).
- **Originality.** Briefs cite the reference games for principles only, such as "readable silhouettes", "visible process" or "industrial layering". The Art Director's review checklist includes "no recognizable element of a reference game" (FR-007).
- **Interactive versus final.** Following the owner's hybrid route (memory 2026-11-03), modelling and look development iterate through the Blender MCP and ComfyUI MCP. Final renders run once through the headless batch command, which is the only source of shipped pixels.

**Unity import.** A new `F007SpriteImportPostprocessor` covers per-look rules:

- filtering, mipmaps and compression per platform;
- PPU per look, so that 1 cell stays 1 world unit;
- Sprite Atlas v2 per family with the page plan.

The postprocessor and the manifest-driven integration (`F004ArtIntegration` pattern) are extended, not forked.

**Plan review amendments (2026-11-27, `reviews/plan-reviews.md`).**

- **Alignment (TA-4).** All passes are trimmed to the **union bbox**, with one shared pivot. A check verifies that the emissive mask is a subset of the beauty alpha.
- **Shadow and emissive (TA-5).** Shadows are R8 or A8, at ASTC 4×4 or uncompressed, because ASTC 6×6 bands soft gradients. They use a multiply blend on their own sorting layer under the neighbouring bodies. The emissive pass uses additive blending. The materials and the SetPass delta enter the estimate.
- **Reproducibility (TA-6).** The render runs on the CPU with a fixed seed, no denoiser (or a deterministic one), and pinned Blender and OIDN versions. The SSIM threshold is set in M2 from a two-render test.
- **Filtering (TA-7, M2 brief).** Edges are dilated or premultiplied. Padding is at least 2^(mip levels), or the mip levels are limited. Tight sprite meshes are used, and overdraw is measured.
- **Mesh export (TA-8).** Checks for unit and scale, pivot, triangle count, material and variant counts, and the shadow-caster flag. The silhouette IoU for C needs a Unity render step.
- **Originality (AD-10).** A prompt lint rejects game and artist names. Every prompt and seed is logged. Reference screenshots are never used as img2img or ControlNet inputs.

## R15 — Camera and scaling per look

**Decision.**

- **M2** keeps the existing integer camera model for all looks. For B and C, `n` maps to an equivalent world scale through the look's PPU. This changes no camera code in the prototype, and the scale steps are coarse but acceptable for captures.
- **M4** decides the production camera policy for the chosen direction:
  - **A** keeps integer scaling.
  - **B and C** may adopt continuous zoom with readable minimum and maximum bounds.

  `CameraScaleModel` and the crispness tests are then re-baselined explicitly, never deleted silently. The F004 wiki rule "re-baseline with an old-to-new diff" applies.
- **Composition (FR-023, SC-006).** The world boundary becomes authored art:
  - surrounding ground;
  - background structures, such as a distant plant silhouette and storage yard;
  - a sky or haze band, in the dusk mood.

  The black void disappears, which addresses AD-4. Fit-all framing keeps the HUD-aware unobstructed rectangle (F006 C1). The dead-area metric in R18 counts only pixels that are neither world art nor UI.

**Plan review amendments (2026-11-27, `reviews/plan-reviews.md`).**

- **Background rules (WD-3).** The background is anonymous, silhouette-only, unlit and unreachable. It has no signage or text, no named buildings, no power lines, pylons, cooling towers or grid hardware, no vehicles, people or landmarks, and nothing that reads as a second depot. The sky is only a mood band, with no sun, moon or day cycle. There are no gates, roads or exits beyond the site. The ground family stays keyed to `starter-platform`.
- **Hit size (ARCH-10).** The hit-size formula in `CameraScaleModel` (`:281`) becomes profile-aware. The camera keeps its single writer.

## R16 — Physical device protocol (Q3, SC-011)

**Decision.**

- **Device record.** The phone is a Google Pixel 11 Pro with Android 17. No tablet is available (owner, 2026-11-27). The first M1 task reads the SoC and GPU, resolution, density and refresh rate from the device and records them in `reviews/devices-f007.md`. Tablet evidence stays emulated: the Windows-player tablet class for captures, with no physical tablet performance.
- **Lease.** The phone shares the host's single ADB server with the emulator. Unity CLI runs with an Android target kill that server ("Killing ADB server", AGENTS.md). Phone work therefore holds the **`emulator`** lease, which `scripts/resource-lease.ps1:18` already documents as "emulator-5554 and anything that talks to its adb server" (OPS-1). It also holds `unity` when a build runs, and `gpu` for every performance capture (`-Resource unity,emulator,gpu`, OPS-6). A separate `phone` lease resource is not added. This is a documentation change to AGENTS.md and the workstation pitfalls, owned by a DevOps Low-tier task.
- **Measurement.** The same ABA method as F004 B2 and F006 B4: frame pacing via SurfaceFlinger timestats or `dumpsys gfxinfo`, PSS memory, texture memory from the build report, and a 10-minute sustained run that records frequency and thermal status where the device exposes them.
- **Input.** Measurements use profiler-free development builds (memory: profile only with `-noProfilerAutoconnect`). Input goes through `adb` only, never host input injection (memory 2026-11-17).

**Plan review amendments (2026-11-27, `reviews/plan-reviews.md`).**

- **Serial (OPS-2).** When the emulator and the phone are both connected, every run passes `-Serial`: the phone's serial for phone runs, `emulator-5554` for emulator runs. The journey script already stops safely when more than one device is connected.
- **Device protocol (PERF-7).** `devices-f007.md` records, per run: thermal state at start, charging state, battery level, brightness, fixed refresh rate (no adaptive 120 Hz), Game Mode, and battery saver off. Runs start from a fixed cool-down state, with ABAB order. Phone runs and emulator runs never share a lease window while the emulator loads the host GPU.
- **Dev builds (PERF-9).** Every M2 number is labelled "dev build". The overlay and DevTools update are switched off during runs. SC-011 is measured on release builds (M5 and M6). At M2, one release-config spot check runs for B, as far as isolation allows.

## R17 — Performance budgets (FR-032)

**Decision.** M2 reports each direction's cost against the F004/F006 frozen budgets:

| Budget | Current value |
|---|---|
| Emulator FPS | 60 |
| Present-interval p95 | 19 ms |
| Steady-state allocations | 0 |
| World-art texture | 8 MB |
| Atlas page | 1 MiB |
| SetPass | ≤ 0.86 (F006 same-state delta reading) |
| Shader variants | 490 |
| APK | 150 MB |

M2 adds phone numbers for each budget.

At M4, the Performance Engineer drafts **budget set v2** for the chosen direction. The expected deltas are:

- world-art texture memory (derived for B: about 10–20 MB compressed with environment and stages);
- the atlas page rule (R13);
- variants (C1 or Light2D);
- SetPass.

The owner approves budget set v2, and it is recorded in Art Bible v2. M5 measures every family against v2 (FR-035).

**Workloads.** W006-V is reused. W007-D (dense, all 12 running with effects) is the worst case.

**Plan review amendments (2026-11-27, `reviews/plan-reviews.md`).**

- **GPU metrics (PERF-1).** Measure GPU time per frame (Perfetto, or the `gfxinfo` GPU columns). Take one overdraw capture per direction at W007-D and report the pixels drawn per frame.
- **On-device pass counts (PERF-4).** Take draw and pass counts on the device with the Frame Debugger or Perfetto. The Editor SetPass reading is labelled "estimated".
- **Contents of budget set v2 (PERF-5, fixed for the M4 task):**
  - FPS target, p95, p99, and the number of intervals ≥ 33 ms and ≥ 50 ms (phone and emulator);
  - 0 allocations per frame;
  - GPU world-art memory per format, and the phone PSS delta;
  - the page rule (size, format, mips);
  - the APK delta;
  - variants and SetPass;
  - GPU ms and overdraw ratio;
  - cold load time to the first frame;
  - 10-minute drift in frame time, plus thermal status;
  - maximum resident sprite memory while zooming.
- **W007-D (PERF-8).** 12 machines running with effects, a fixed seed, the three zoom stops, one build-out stage (M2; more in M5) and a fixed camera path. A 30 s warm-up is discarded, then 60 s are sampled. The sustained run is a 10-minute loop.
- **Allocations (PERF-10).** The steady state starts after the warm-up and never includes a profile switch.

## R18 — Evaluation methods

| Criterion | Method | Who |
|---|---|---|
| SC-001 | Side-by-side sheets with matched state, class and framing; owner verdict per class | Owner |
| SC-002 | Rubric of 10 dimensions (FR-037), 1–5 scale. Anchors (1 = F006-like, 3 = competent mobile, 5 = premium reference quality) are written before M2 scoring. Scored on F006, A, B and C at M2, and on the final build at M6 | Art Director plus one independent reviewer (UI/UX or World Designer), separate sessions |
| SC-003 | Shuffled grayscale silhouettes at phone fit-all scale, 10 trials per type, plus pairwise IoU | Independent agent session |
| SC-004 | Median machine bounding area (opaque pixels of body sprites or meshes) at default framing, emuP and tabL, against the M1 baseline | Script on captures |
| SC-005 | 10 region prompts on dense captures | Independent reviewer |
| SC-006 | Per-pixel classification of the capture into world art, UI (from the UI Toolkit layout rectangles) and clear colour. The dead share is clear colour within the safe area. The clear colour is a reserved key colour in capture builds | Script on captures |
| SC-007 | World Designer checklist against World Bible v0.3 | World Designer |
| SC-008 / SC-009 | F004/F006 blind naming and timed trial tools (`tools/art-pipeline/trials`) | Independent session; owner |
| SC-010 | Frame differencing of a 10 s capture with reduced motion on; flash check | Script |
| SC-011 | R16 and R17 | Performance Engineer |

**Plan review amendments (2026-11-27, `reviews/plan-reviews.md`).**

- **SC-002 rubric (AD-4, AD-5, AD-6, PO-4).** The rubric scores **8 visual dimensions**:
  - silhouettes;
  - materials;
  - environment coherence;
  - composition;
  - density and clutter;
  - state readability;
  - animation;
  - consistency across classes.

  "Performance" is measured (SC-011). "Before and after" is the owner's verdict (SC-001). The anchors 1, 3 and 5 are written per dimension from reference criteria. F006 is **scored**, not defined as anchor 1. Before scoring, the reviewers calibrate on two sample captures. A spread of more than 1 point between reviewers is discussed and recorded. Blinding is impossible; the mitigation is matched framing and shuffled order.
- **SC-006 (UX-6, PO-6).** Measured per class and per factory state. The UI rectangles come from the UI Toolkit layout, and empty areas hidden by the card count as UI. The criterion is only met together with SC-004 on the same captures, and with background art that is less salient than the machines (FR-025).
- **SC-004 (PO-5).** The body area excludes the shadow sprite. Overhang beyond the location cell is reported separately.
- **SC-010 (QA-7).** Two checks: an effect-spawn counter against the simulation events, and with reduced motion on, a frozen clock with a pixel-identical comparison over 10 s.
- **Scripts (QA-8).** The dead-area, area and IoU scripts are deliverables with their own pytest and known-answer fixtures. The key colour must not appear in any art (a check). The F006 baseline values are frozen in M1, together with the script version.
- **Readability baseline (PO-3).** SC-008 and SC-009 compare against F006 `readability-f006.md` (T546). If T546 is not on `main` when T630 starts, T641 reproduces the F006 baseline with the same tools.
- **M2 UX check per direction (UX-7).** In the dense state, select the front, back and edge machine, open the card, and check that no cue or selection frame is covered. Recorded as pass or fail in `directions/*.md`.

## R19 — Test strategy

- **Edit Mode**:
  - look-profile resolution and validation (every profile resolves every required key, fails closed);
  - build-out stage function;
  - layout pitch parameters and layout independence (6, 12, 20, L) per profile;
  - indicator anchor inside the location's hit area;
  - manifest validation.
- **Play Mode**:
  - real-scene profile switch in development builds, with no simulation-state difference (a determinism parity test runs the same inputs under F006 and each prototype profile, and compares snapshots);
  - tap and selection under overhang;
  - reduced motion;
  - card framing with larger art;
  - effect sync.
- **StandalonePlayer** (tagged): an allocation test of the steady path per profile.
- **Art pipeline**: pytest for the new checks and the `render-hd` route, through `scripts/test-art-pipeline.ps1`.
- **Device**: the emulator journey for each look in development builds, plus the phone measurement scripts.
- **Re-baselining**: every test that asserts F004 constants for the shipped look moves behind the F006 profile, or is re-baselined at M4/M5 with an explicit old-to-new list. Examples are D24, palette, crispness, IoU and badge size. Tests are never silently deleted.

**Plan review amendments (2026-11-27, `reviews/plan-reviews.md`).**

- **Parity test (QA-1, QA-2, ARCH-7).**
  - **What is compared.** A hash per tick of the full authoritative simulation state: balances, per-machine state and progress, cycle counters, depot fill, contracts and RNG state.
  - **Inputs.** The same seed, a fixed time source, and scripted inputs (build, upgrade, Overdrive, prestige, an offline gap). The taps run through `WorldTapResolver` and must produce identical location-id sequences for every profile.
  - **Profiles.** Under `F006` and every other profile.
  - **Extra cases.** A profile switch in the middle of a run, compared against a run without the switch. Save and checkpoint bytes must be identical.
  - **Negative control.** A planted, state-changing test profile must fail.
  - **Guard.** `Look/` uses no simulation write API.
- **DevTools tests (QA-4).** Tests that need `IDLEFACTORY_DEV` run in a defined runner path (temporary `csc.rsp` plus `-assemblyNames`, per workstation-pitfalls). A gate fails on 0 discovered tests. Profile-agnostic cases (resolution, layout) also run in the normal suites under `F006`.
- **Re-baselining (QA-5).** `reviews/rebaseline-map.md` maps old test ids to new ones. A script (T638) compares `[Test]` and `[TestCase]` ids against the `main` baseline: every missing id must appear in the map. Re-baselining candidates include `CameraScaleModelTests`, `SlotGridLatticeTests`, `PixelCrispnessTests`, `StateCueSetTests`, `AtlasPageBudgetTests`, `PresentationSpriteCatalogTests`, the F006 layout, marker and stage tests, `DevToolsOverlay*Tests` and `tools/art-pipeline/tests/test_checks.py`. The candidate list in the QA report is binding as a starting point.
- **Edge-case tests (QA-6), per profile in Play Mode:**
  - profile switch with the card open at 200 % text in phone portrait;
  - reduced motion during a running effect;
  - an overhang tap into the neighbour zone on the dense layout;
  - offline return, Overdrive, prestige and stage change (no catch-up);
  - empty and locked locations without colour;
  - card framing with large art.
- **Overhang (UX-4).** `OverhangTapTests` asserts, on the dense layout per class and at both text scales, that a tap on an opaque pixel of the front-most machine selects that machine, that the selection frame stays visible, and that hit-shape separation is measured in pt at `n_min`. There is also a cue-anchor-to-owner test for grouped bays.
- **Hit areas (QA-12).** A 44 pt and no-overlap check per profile, with a deliberately overlapping layout as the negative case.
- **Named suites (UX-8, QA-10).** These F006 suites must pass unchanged under every profile:
  - `CardCameraTests`;
  - `TextScaleLayoutTests`;
  - `RunHudLayoutTests`;
  - `F006/LayoutIndependenceTests`;
  - `F006/ReducedMotionWorldTests`;
  - focus and keyboard order.

  The SC-012 evidence is the result XML of Edit Mode, Play Mode and the tagged player tests, with a Play Mode count above 0, plus the existing SaveV1 to V3 fixtures.
- **Allocations (QA-11).** The profile is a parameter of the tagged allocation test.
- **Suite duration.** The full Play Mode run already takes about 25–30 minutes. The profile matrix applies only to the tests named above, not to the whole suite.

## R20 — Roles, tiers, lane and resources

- **Lane**: MAQA, as in F004 and F006. Parent merges only through `scripts/maqa-merge.ps1`.
- **Task IDs**: **T600–T699** are reserved for F007. `gh issue list --label maqa --state all` shows no T6xx title (checked 2026-11-27). The tasks for M4–M6 continue in the same range after M3.
- **Activated roles**:

  | Role | Tier |
  |---|---|
  | Game Designer/PO | Medium |
  | Software Architect | Medium; High for the look-profile seam and C1 renderer decision |
  | Test/QA | Medium |
  | UI/UX Reviewer | Medium |
  | Art Director | Medium; High for the rubric anchors and the M3 recommendation |
  | Technical Artist | Medium |
  | 3D and Render Artist | Medium |
  | Asset Artist | Medium |
  | World Designer | Medium |
  | Performance Engineer | Medium |
  | Gameplay and UI/UX Developers | Medium |
  | Play-Tester | Medium; once on M2, then per M5 core-loop batch |
  | DevOps | Low; lease docs and renderer settings review |
  | Game Systems/Economy | Low; stage thresholds |

- **Omitted**:
  - Security/Privacy: no trust boundary and no persisted data. The dev-only profile choice goes in the existing preferences file, which is already reviewed.
  - Sound and Composer: audio is out of scope.
  - Online Services: no remote boundary.
- **Resources**: Unity, emulator (covers the phone, R16) and GPU leases per the AGENTS.md rules. Blender and ComfyUI MCP sessions hold `gpu`.

**Plan review amendments (2026-11-27, `reviews/plan-reviews.md`).**

- **Labels and batch gate (OPS-7).** M1 and M2 Issues carry `resource:unity`, `resource:emulator` and `resource:gpu` as needed. Every batch ends with `scripts/test-unity.ps1 -StandaloneWindowsPlayer` (AGENTS.md).

## R21 — World Bible v0.3 (FR-007a)

**Decision.** The World Designer authors `F006-WB v0.3`:

- the mood changes to the shared dusk (§3);
- the site concept wording drops "on a dark base" (§1);
- the ground-family migration note (§5);
- the infrastructure purpose rules for FR-024, including the visual distinction between cosmetic infrastructure and depot trunks.

The site name, naming rules and the depot/trunk/no-machine-link rule are unchanged. The World Designer decides MINOR or MAJOR under §7 with a written reason (spec A-7). The owner approves before M2 captures are scored. The Art Director confirms the mood can be rendered in all three directions before the v0.3 text is frozen.

**Plan review amendments (2026-11-27, `reviews/plan-reviews.md`).**

- **Scope of v0.3 (WD-1, WD-8).** v0.3 covers:
  - new mood words for §3 (replacing "dim-lit" and "night shift");
  - removing "dark base" from §1 and §5, with a migration table;
  - an explicit statement that no string, the site name and the tagline do not change;
  - the background rules (R15);
  - the grouping rules (R09);
  - stage legibility (open deck, walled yard, gantry line) in the dusk look, required in every direction brief.
- **Infrastructure rules (FR-024):**
  - only trunks end at the depot, one machine per trunk;
  - cosmetic runs never connect two machines and end at ground, wall, support, tank, boundary or depot housing;
  - cosmetic runs use their own vocabulary (thinner, no flow marks, no arrows, no motion), are mounted on walls and frames, and never lie flat between locations;
  - no parallel run along a trunk; crossings only as a clearly higher overhead run;
  - one line of purpose per element in the brief.

  The SC-007 test question is: "Can this be traced from machine X to machine Y?" The answer must be no.
- **Forbidden terms (WD-6).** Pylons, signage, roads and vehicles are added to the forbidden list.
- **Measurable mood anchors (AD-2).** Swatches for ground, sky and lamp; key and fill hues; a luminance band for the ground; and a minimum luminance for the void (no pixel below X). They are checked on every capture.
- **Version (WD-5, WD-7).** Recommendation: **MINOR**, but non-additive. v0.3 says that it supersedes v0.2 §1, §3 and §5, carries the migration table, and adds a one-line clarification to §7 that a mood or ground change counts as MINOR with migration. The World Designer decides and records the reason.

## R22 — Art Bible v2 (FR-017, FR-018, FR-033)

**Decision.**

- **What it is.** After M3, the Art Director authors `F007-AB v2.0`, a new document `docs/art/F007-art-bible-v2.md`. It supersedes `F004-PAB v1.1.2` section by section, through a supersession table as F004 did for F003.
- **Rule coverage.** Every FR-018 topic has a rule and a checker or named review.
- **Migration.** A migration table assigns each F004/F006 family one of: replaced in F007 M5 (with a task id), kept (with a reason), or retired.
- **Approval.** The owner signs it. Production starts only after the sign-off (FR-003).
- **During M2.** The prototype is governed by the direction briefs, which are explicitly non-shipping. The F004 bible keeps governing shipped assets until v2 is signed (FR-017).

## Coverage of the wiki gaps

The gaps recorded in spec "Wiki recovery" are closed as follows:

| Gap | Closed by |
|---|---|
| High-resolution and compressed sprites | R05, R13 |
| Lit sprites and 2D lights | R04 (A adopts L2), R17 (variants) |
| Real-time 3D with the 2D renderer | R06 |
| Baked AO and shadows | R05, R14 |
| Page cap with compression | R13 (measured in M2) |
| F006 final acceptance | R02 (F006 closeout ingest, T550) |

Each closure is a hypothesis or a design decision until its M2 measurement is ingested.

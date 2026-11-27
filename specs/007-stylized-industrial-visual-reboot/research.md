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

- **Density.** A higher density (**D48**: 48 art px per cell, twice D24) at integer scaling. On the reference phone, the `n_min` rule then needs n ≥ 1, which keeps integer zoom levels (derived: `n_min = ceil(24 · 2.625 / 48 − 0.01) = 2`, and 1 at the smaller classes; hypothesis that this fits, verified in M2).
- **Palette.** An extended palette of up to 96 colours with per-material ramps of 5–6 tones.
- **Shading.** Hand-placed or render-derived highlights and ambient occlusion, quantised to the palette. Ordered dithering is allowed only on large flat surfaces.
- **Outline.** Selective outlines instead of a uniform `ink_0` outline.
- **Light.** The 2D Light envelope L2 of the Art Bible is adopted for the dusk mood.
- **Pipeline.** The Blender rig plus the existing pixel pass, re-parameterised for density and palette.

**Rationale.** Direction A answers the owner's question "is pixel art salvageable?" at a fair level of effort. D48 doubles the linear detail, which is enough to draw drills, furnaces and press rams. The pixel pass already exists, so A costs the least new tooling.

**Known cost.**

- 4× the texel count of D24 for the same canvas (derived).
- Under RGBA32 point-filtered art, the 1 MiB page cap allows a 512 × 512 page, so a 160 × 128 machine frame (the D48 equivalent of 80 × 64) fits 12 frames per page (derived: 3 × 4 cells of 160 × 128 in 512 × 512).

**Alternatives.**

- *D32.* Rejected: too little gain to answer the question.
- *D64.* Rejected for A: it approaches B's cost while keeping the pixel limitations.

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

**Memory estimate (derived, hypothesis until measured in M2).** One machine frame of 288 × 224 px is 64,512 px. Four machine types with 14 frames each is 56 frames, or 3.61 M px:

| Format | Bytes per px | Total |
|---|---|---|
| RGBA32 | 4 | 14.4 MB, above the current 8 MB world-art budget |
| ASTC 6×6 | 0.89 | 3.2 MB |
| ASTC 8×8 | 0.5 | 1.8 MB |

M5 adds 2–3 build-out stages (FR-027a). At ASTC 6×6 that is about 6.5–9.6 MB, before environment art. A compressed texture format and a revised memory budget are therefore required (FR-032).

**Rationale.** This is the owner's preferred candidate. It reuses the existing Blender toolchain, camera and projection. It produces material depth, AO and lighting offline at no runtime cost, and it keeps the URP 2D renderer and the sprite-based presentation architecture unchanged.

**Risks.**

- Sprite memory grows quickly.
- Animation is limited to pre-rendered frames.
- Compressed textures on Android are unverified against the load hang (R13).

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
| C3 | Meshes use a custom unlit "fake-lit" shader inside the 2D Renderer. | No real lights or shadows. This is close to B with more cost. |

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

## R09 — Location geometry and densification (FR-005, SC-004)

**Fact.**

- On-screen location spacing comes from the lattice constants in `SlotGridLayout` (70 × 54 art px lattice) and from the `LatticeLayoutBuilder` columns: 3 in portrait, 4 in landscape.
- `FactoryWorldLayout` is the only place that knows the location count (F006 SC-011).
- Selection, hit-testing and cue anchoring all go through `FactoryWorldLayout` and `WorldTapResolver`.

**Decision.**

- The lattice pitch and the plate and deck dressing become per-look-profile layout parameters. They are a presentation asset, extending `WorldCompositionSettings`, not constants.
- A direction may propose a denser pitch and machine-group arrangements, for example pairing Miner with Smelter around a shared foundation. Every location keeps:
  - its id;
  - its unlock order;
  - a non-overlapping hit area of at least 44 pt;
  - an unambiguous cue anchor.

The M3 decision fixes the production pitch and arrangement.

**Guard.** The existing layout-independence tests (6, 20 and L-shape layouts) run against every profile.

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

## R11 — Build-out stages (FR-027a)

**Decision.**

- Each machine type has 2–3 stages, selected by `level ≥ threshold`. The thresholds live in an authored presentation asset (`MachineBuildOutStages`), keyed by machine definition id.
- Threshold values come from the existing level data. Defaults are proposed by Game Systems/Economy (Low tier), for example the levels at which a machine's batch factor or rate tier changes, if such breakpoints exist in content. The proposal is reviewed for "no pacing change".
- The stage is a pure function of `(definition id, level)`. It is not persisted and not part of the simulation.
- The art adds parts on a fixed base silhouette, so the hit area and indicator anchor stay stable across stages. Silhouette identity across types is checked for every stage (SC-003).
- **M2 scope**: two stages of one machine type per direction. **M5 scope**: the full set.

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
  - pages of 1024² and 2048² in ASTC 6×6 (≈ 0.9 MiB and 3.6 MiB);
  - load time and the hang signature from the F006 journey `isLoading` gate.

  Until that measurement passes, compressed pages also stay at 1 MiB or less of GPU memory (FR-032).

**Hypothesis.** The hang relates to the byte size of a single upload or read rather than to texel count. An ASTC 1024² page (≈ 0.9 MiB) is expected to load like a 1 MiB RGBA32 page. The M2 measurement verifies this.

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

## R16 — Physical device protocol (Q3, SC-011)

**Decision.**

- **Device record.** The phone is a Google Pixel 11 Pro with Android 17. No tablet is available (owner, 2026-11-27). The first M1 task reads the SoC and GPU, resolution, density and refresh rate from the device and records them in `reviews/devices-f007.md`. Tablet evidence stays emulated: the Windows-player tablet class for captures, with no physical tablet performance.
- **Lease.** The phone shares the host's single ADB server with the emulator. Unity CLI runs with an Android target kill that server ("Killing ADB server", AGENTS.md). Phone work therefore holds the **`emulator`** lease, which this plan redefines as "the host ADB server and any device on it". It also holds `unity` when a build runs. A separate `phone` lease resource is not added. This is a documentation change to AGENTS.md and the workstation pitfalls, owned by a DevOps Low-tier task.
- **Measurement.** The same ABA method as F004 B2 and F006 B4: frame pacing via SurfaceFlinger timestats or `dumpsys gfxinfo`, PSS memory, texture memory from the build report, and a 10-minute sustained run that records frequency and thermal status where the device exposes them.
- **Input.** Measurements use profiler-free development builds (memory: profile only with `-noProfilerAutoconnect`). Input goes through `adb` only, never host input injection (memory 2026-11-17).

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

## R21 — World Bible v0.3 (FR-007a)

**Decision.** The World Designer authors `F006-WB v0.3`:

- the mood changes to the shared dusk (§3);
- the site concept wording drops "on a dark base" (§1);
- the ground-family migration note (§5);
- the infrastructure purpose rules for FR-024, including the visual distinction between cosmetic infrastructure and depot trunks.

The site name, naming rules and the depot/trunk/no-machine-link rule are unchanged. The World Designer decides MINOR or MAJOR under §7 with a written reason (spec A-7). The owner approves before M2 captures are scored. The Art Director confirms the mood can be rendered in all three directions before the v0.3 text is frozen.

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

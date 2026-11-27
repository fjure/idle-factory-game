# Feature Specification: Visual Direction Reboot — Stylized Industrial 2.5D

**Feature Branch**: `feat/007-stylized-industrial-visual-reboot`

**Created**: 2026-11-27

**Status**: Draft

**Input**: Owner brief "Feature 007 – Visual Direction Reboot: Stylized Industrial 2.5D" (2026-11-27). In short, the current game looks like a basic pixel-art prototype, not a polished, atmospheric industrial factory game. The owner asks for three directions to be compared in real Unity rendering: A Enhanced Pixel Art, B Stylized 3D-to-2D (the preferred candidate), and C Hybrid 2.5D. The owner then approves one direction. Only after that approval do the art rules and the pipeline get revised and the production assets replace the current ones incrementally. Gameplay, economy, saves and the 12-location model are unchanged.

## Context and authority

- **Source of truth**: `main` at `ac49f6c8` (2026-11-27). Feature numbering checked: `specs/` holds 001–006, and no `007` branch or folder exists locally or on `origin`.
- **Governing documents**:
  - `.specify/memory/constitution.md` v1.0.0;
  - `Core-Idea.md`;
  - Art Bible `F004-PAB v1.1.2` (`docs/art/F004-art-bible-v1.md`);
  - World Bible `F006-WB v0.2` (`docs/world/world-bible-v0.2.md`);
  - Feature 004 (`specs/004-visual-identity/`);
  - Feature 006 (`specs/006-world-first-factory-experience/`).
- **What this feature is**: a visual direction and presentation overhaul, with visual appearance treated as a first-class product requirement. It is not a gameplay change. It is also explicitly not a polish pass on the current D24 pixel-art style. The owner asks whether that style suits the product at all.

### Feature 006 status (verified 2026-11-27)

Feature 006 is **not complete** on `main`. Its review and test tasks T531–T545, T547, T548 and T560–T563 are merged; the `tasks.md` checkboxes are not yet mirrored. In `reviews/final-acceptance.md`, all ten lenses return ACCEPT WITH CONDITIONS with no blocker. These parent and owner steps are still open:

- **T530 / T546**: the SC-004, SC-005 and SC-006 readability trials. `reviews/readability-f006.md` does not exist yet.
- **T549**: the owner's visual acceptance per surface and screen class.
- **T550**: checkbox mirroring, wiki ingest, learn review and wiki lint.
- **T551**: the hook gate, `$speckit-converge` and the stale-worktree report.
- **Post-P4 carries** recorded for later Issues:
  - C-68 deep profile and a physical-device measurement;
  - AD-1 world upgrade marker;
  - AD-4 deck edge and black void;
  - AD-7 store-full marker cap.

The owner decided (Q1, 2026-11-27) that F006 closes in parallel with the F007 audit and prototype. T549 judges the F006 look as an interim state. AD-1, AD-4 and AD-7 move to F007, PERF-9 is closed by F007's M1 device baseline, and C-68 stays with F006 (FR-002).

### Current visual state (repository evidence)

Evidence:

- the F006 final captures `specs/006-world-first-factory-experience/reviews/uiux-f006-final/` (`f01-grid12-p100.png` emulator portrait, `j-tablet-G12.png` tablet);
- `reviews/final-acceptance.md`;
- Art Bible `F004-PAB v1.1.2`.

Problems found:

1. **Large unused screen areas.** The phone portrait capture shows a black band of about 22 % of the screen height above the deck and about 18 % below it. The tablet shows a black band above the deck and an unused right margin. The deck ends in a hard edge against the void (F006 carry AD-4).
2. **Machines read as small icons.** Each machine is drawn on an 80 × 64 art-pixel canvas, with a body inside a 68 × 30 footprint box (Art Bible §3.2). Every machine shares the same plate, the same height band (bodies start at canvas row 34) and the same three-tone ramp. At fit-all, a machine covers roughly 2–3 % of the portrait screen.
3. **A row-and-column grid of plates, not a facility.** Twelve identical diamond plates sit in a rectangular lattice. Straight dashed trunks run to one depot. Props are few and far between. Nothing gives the site structure, height or depth.
4. **Flat materials and flat light.** Lighting B gives each face one flat tone: top lightest, left mid, right darkest. The rules allow no gradients, no dithering, no baked ambient occlusion and no cast shadows. They allow at most 10 machine colours per sprite from a palette capped at 48 colours. The result is that steel, copper and concrete barely differ.
5. **Indicators outweigh machinery.** On the 12-machine capture, the white upgrade chevrons above each location are the highest-contrast shapes on screen (F006 carry AD-1). The red badges dominated the F004 captures in the same way.
6. **Little sense of process.** Machine loops are 6 frames at 8 fps inside the body silhouette. No machine part moves in a way that explains its job.

### What exists and is reused

- **Unchanged**: the authoritative simulation and read models, which give machine phase, blocker, cycle progress, completed cycles, rates, supply coverage, the upgrade preview and the development stage.
- **Reused presentation architecture**:
  - the layout seam (`FactoryWorldLayout`, `DeckPlan`, `LatticeLayoutBuilder`, building location ids, which are not tied to 12);
  - the machine presentation model;
  - the effect pool on the shared animation clock;
  - attention markers;
  - the depot and link models;
  - development stages;
  - the single camera writer and panel-aware framing;
  - the UI Toolkit shell, the machine card and the hub;
  - the display-preferences store with reduced motion.
- **Reused production tooling**:
  - Blender 5.2.1 with `rig-v2.blend` (orthographic, true isometric 2:1);
  - ComfyUI with Z-Image-Turbo;
  - the `tools/art-pipeline` checks;
  - provenance records;
  - the manifest-driven Unity importer;
  - the atlas page cap tests;
  - the emulator journey and capture rigs;
  - the readability trial tooling.
- **Current rendering foundation**: URP 17.6.0 with the 2D Renderer, an Unlit default sprite material and Gamma colour space. HDR, post-processing and 2D lights are off. Integer camera scaling uses `n_min`. Atlas pages are at most 512 × 512 RGBA32, uncompressed and point-filtered.

### Wiki recovery

`$speckit-wiki-query` ran on 2026-11-27, and coverage is **partial**.

**Covered:**

- the F004 visual decisions and Art Bible history ([visual-identity-art-direction](../../wiki/pages/visual-identity-art-direction.md));
- the pipeline routes and the pixel pass ([art-production-pipeline](../../wiki/pages/art-production-pipeline.md));
- the URP 2D parity settings, including the warning that 2D lights need `m_Strip2DUnusedVariants` = 0 and push shader variants above the frozen 490 ([urp-rendering-foundation](../../wiki/pages/urp-rendering-foundation.md));
- the integer camera scale and the points-versus-pixels gotcha ([pixel-camera-scale](../../wiki/pages/pixel-camera-scale.md));
- the F006 direction decision OD-9b and the dead band above the lattice already observed at P0 ([f006-p0-direction-decision](../../wiki/pages/f006-p0-direction-decision.md));
- the Android load hang caused by a 2 MiB atlas page, which led to the empirical 1 MiB page cap ([f006-android-load-hang-atlas-page-cap](../../wiki/pages/f006-android-load-hang-atlas-page-cap.md));
- the frozen B2 budgets ([f004-b2-frozen-budgets](../../wiki/pages/f004-b2-frozen-budgets.md));
- the F006 world census and effect pool ([f006-batch-5-world-and-effects](../../wiki/pages/f006-batch-5-world-and-effects.md)).

**Coverage gaps the plan's research must close:**

- high-resolution or non-pixel sprite rendering and its import settings (filtering, compression formats such as ASTC or ETC2, mipmaps, texture memory on Android);
- lit sprites, normal or mask maps, and 2D light cost on the target devices;
- real-time 3D meshes alongside the 2D renderer, including sorting and camera interplay;
- baked AO and shadow workflows in the Blender rig;
- whether the 1 MiB atlas page hang also applies to compressed textures;
- the F006 final-acceptance outcomes, which are not yet ingested because T550 is open.

## Clarifications

### Session 2026-11-27

- Q1 (F006 closeout versus F007): May the F007 audit and prototype run while F006 closes in parallel, with T549 judging the current look as an interim state? → **Answer: Yes (option A).** F006 closes in parallel. Its T549 owner acceptance judges the F006 look as an interim state that F007 is expected to replace. The F006 visual carries AD-1 (world upgrade marker), AD-4 (deck edge and black void) and AD-7 (store-full marker scale and cap) move to F007 and are not built separately in F006. PERF-9 (no physical-device data) is closed by F007's M1 audit, which measures the F006 look on the physical device (Q3). C-68 (deep profile of the unattributed steady-state allocations) stays an F006 post-P4 Issue.
- Q2 (prototype fidelity per direction): Must Directions A, B and C each be built to full prototype parity? → **Answer: Yes (option A).** All three directions are built to full parity: four machines, the depot, environment, infrastructure, states, animation, the selected-machine UI and lighting (FR-008). There is no early elimination by reduced-fidelity screening.
- Q3 (performance evidence hardware): Is a physical device available? → **Answer: Yes (option A).** A physical Android phone is available, and optionally a tablet. Prototype and final performance evidence include measurements on it, in addition to the emulator and the Windows player. The plan records the device model, OS version, GPU and screen class, and the device-use rules (exclusive use and a lease, as for the emulator).

## User Scenarios & Testing *(mandatory)*

The "user" of the decision stories (US1–US3, US7) is the owner, who decides the product's look. The "user" of the production stories (US4–US6, US8) is the player.

### User Story 1 — An honest current-state visual audit (Priority: P1)

Before anything is designed, the team records what the game actually looks like on `main` and why. The audit covers screenshots on every screen class, rendering settings, scene composition, the asset inventory, Art Bible constraints and the presentation architecture. It ends with a ranked problem list and a disposition of every F004 art constraint: preserve, relax, replace or remove, each with a reason.

**Why this priority**: Every later choice is judged against this baseline. Without it, the comparison and the "clearly different" success criterion cannot be measured.

**Independent Test**: Review the audit record. It contains:

- current captures from real builds for each required screen class and factory state;
- the ranked problem list with evidence links;
- a constraint table that covers D24 density, the 48-colour palette, no gradients, no dithering, hard outlines, flat shading, integer scaling, canvas and silhouette rules, Lighting B, the frame rules and the atlas and filter rules.

**Acceptance Scenarios**:

1. **Given** `main` at the audit commit, **When** the audit runs, **Then** captures of the current F006 look exist for each screen class in FR-011, in both factory states (FR-012), taken from real builds.
2. **Given** the F004 constraint list, **When** the audit is complete, **Then** each constraint has a disposition (preserve, relax, replace or remove) per direction A, B and C, with the reason and the evidence or hypothesis behind it.
3. **Given** the F006 carries moved to F007 (AD-1, AD-4, AD-7), **When** the audit is complete, **Then** each one appears in the ranked problem list. The F006 look's frame rate, pacing and memory on the physical phone are recorded as the device baseline (PERF-9).

---

### User Story 2 — Three directions compared side by side in the real game (Priority: P1)

The owner sees the current look and the three candidate directions side by side, in actual Unity rendering. Every look shows the same factory state, layout, camera framing and screen size. Each direction shows a detailed Miner, Smelter, Press, Assembler and Depot. It also shows a coherent industrial floor and surroundings, infrastructure that organizes the site, recognizable production states, at least one animated production process, and one selected machine with its contextual UI. A comparison of strengths, weaknesses, mobile performance, memory, production effort, maintainability and fit with future free building accompanies the screenshots.

**Why this priority**: The owner requires real in-engine evidence before deciding, and rejects concept art as a substitute. This is the first implementation milestone.

**Independent Test**: Open the comparison record. For each required screen class and both factory states, it holds four captures (current, A, B, C) made from real Unity builds at equal framing and resolution. It also holds the comparison table and the measured or estimated performance and memory figures, each labelled measured or estimated.

**Acceptance Scenarios**:

1. **Given** the prototype build, **When** captures are taken, **Then** all four looks render the same seeded game state with equal camera framing and screen dimensions, on every required screen class and in both factory states.
2. **Given** any direction, **When** its machines are viewed, **Then** the four machine types differ clearly in silhouette, height profile, proportion and animation (FR-020), and each communicates its industrial purpose (FR-021).
3. **Given** the prototype, **When** a machine is selected, **Then** its existing contextual card opens, and the machine stays visible and readable beside it on the compact mobile layout.
4. **Given** the prototype, **When** the factory runs, **Then** the simulation results, the save data and the state transitions are identical to the unmodified game for the same inputs (FR-001).
5. **Given** the prototype build, **When** a player-facing build is produced, **Then** no prototype direction is visible to players unless it is selected in a development build.

---

### User Story 3 — The owner chooses the direction, with a real alternative to "none" (Priority: P1)

The owner receives a decision brief. It holds the real screenshots, performance estimates and measurements, strengths and weaknesses, a recommended direction, and the proposed changes to the Art Bible and World Bible. The owner approves one direction, or asks for a revision. No large-scale asset production starts before the approval.

**Why this priority**: Visual quality is subjective, and the owner is the final authority (constitution VII). Producing assets before the decision would waste the most expensive work in the feature.

**Independent Test**: The decision record names the approved direction, the date and the owner. It lists any conditions, and it links the captures the owner saw. No production-family asset task is dispatched before that record exists.

**Acceptance Scenarios**:

1. **Given** the comparison record, **When** the owner decides, **Then** the decision, the conditions and the rejected alternatives with their reasons are recorded.
2. **Given** that no candidate meets the desired quality, **When** the owner says so, **Then** the design is revised and re-prototyped. The technically easiest candidate is not selected by default.
3. **Given** a direction that needs a modified projection, **When** it is proposed, **Then** the trade-offs are documented, and the change needs separate explicit owner approval.

---

### User Story 4 — Distinctive, convincing industrial machines (Priority: P1)

In the shipped game, the player recognizes each machine at a glance by what it does:

- the Miner visibly drills or cuts into the ground;
- the Smelter is a furnace with vents, exhaust and heat light;
- the Press has a heavy pressing assembly that moves;
- the Assembler has tooling, arms or a conveyor-like mechanism.

The machines are larger on screen, more detailed and made of recognizable materials (steel, copper, concrete, iron). They differ in silhouette, height and footprint artwork.

**Why this priority**: Machine appearance is the owner's primary priority, and it is the most visible gap.

**Independent Test**: Grayscale silhouette captures of the four machines in the shipped look, shown without labels to an independent reviewer, are named correctly (SC-003). The owner's comparison against the F006 captures records a clear improvement (SC-001).

**Acceptance Scenarios**:

1. **Given** the shipped machines, **When** they are shown as silhouettes only, **Then** an independent reviewer names each type correctly (SC-003).
2. **Given** any machine at the default fit-all framing on phone portrait, **When** it is measured, **Then** its on-screen area is larger than its F006 baseline by the factor agreed at the direction decision (SC-004).
3. **Given** the machine art, **When** it is reviewed, **Then** steel, copper, concrete and iron are recognizably different materials (SC-005).

---

### User Story 5 — A coherent industrial site instead of isolated platforms (Priority: P1)

The factory reads as one built place. It has concrete and metal floors, foundations and structural supports, pipes and conduits, tanks and containers, railings and walkways, beams and frames, and machine-specific surroundings. Every decorative element has a plausible industrial purpose. The site fills the screen with intent: no large black void and no hard edge into nothing. Important machinery is never hidden.

**Why this priority**: The owner calls the "twelve objects on isolated platforms" impression one of the core failures.

**Independent Test**: At fit-all on each required screen class, measure the share of the safe area that is neither world nor UI (SC-006), and run the environment-coherence review (SC-007). Confirm that no decoration covers a machine, cue, marker or hit area (FR-026).

**Acceptance Scenarios**:

1. **Given** fit-all on any required screen class, **When** the screen is measured, **Then** unused non-world, non-UI area stays within SC-006.
2. **Given** decorative infrastructure, **When** it is reviewed, **Then** each element has a stated industrial purpose. None implies item transport between machines that the simulation does not perform, and cosmetic infrastructure is visually distinct from functional depot trunks (FR-024).
3. **Given** a partially developed and a dense established factory, **When** both are captured, **Then** the site reads as one facility at both densities, and growth is visible between them.

---

### User Story 6 — Machines feel alive, and their state is readable without noise (Priority: P2)

Machines show active production, completion, paused, missing input, full output, construction and upgrade. The primary carriers are the machinery's own behaviour (moving parts, heat, exhaust, lights). Restrained indicators back them up. Problem machines stay identifiable without relying on colour. Reduced motion stops all non-essential motion, and every effect follows the authoritative simulation.

**Why this priority**: It adds life and clarity. It depends on the machine and environment art (US4, US5), so it follows them.

**Independent Test**: Repeat the F004/F006 readability trials on the new look. These are blind naming in grayscale and three colour-vision filters, and the owner's timed trial (SC-008, SC-009). Run the reduced-motion and simulation-sync tests (SC-010).

**Acceptance Scenarios**:

1. **Given** a machine in each state, **When** it is viewed in grayscale and under the three colour-vision filters, **Then** its state is named correctly (SC-008).
2. **Given** a factory with several problem machines, **When** the screen is viewed, **Then** state indicators do not outweigh the machinery visually (FR-029). The Art Director's salience review records this.
3. **Given** reduced motion on, **When** the factory runs, **Then** no decorative or machine animation plays, and state stays readable from static cues.
4. **Given** a paused, offline-returned or blocked machine, **When** it is shown, **Then** no animation suggests production that the simulation did not perform.

---

### User Story 7 — Revised, versioned art rules and a reproducible pipeline (Priority: P2)

After the decision, the approved visual rules are written down as a new, owner-approved Art Bible version. The World Bible gets a new version if the site concept changes. A reproducible pipeline produces every asset family. The pipeline covers:

- source models and materials;
- the lighting rig and the camera and projection;
- animation and sprite export;
- Unity import settings and atlas generation;
- manifests and provenance;
- automated validation and visual review.

**Why this priority**: The production assets in US4–US6 need it. Without written rules, the new look drifts across families.

**Independent Test**: Regenerate one approved asset from its committed source and record. The output matches within the tolerance the new Art Bible defines. Its provenance record is complete, and the automated checks pass.

**Acceptance Scenarios**:

1. **Given** the approved direction, **When** the Art Bible is revised, **Then** every changed rule is marked, versioned (expected to be a major version, FR-033) and approved by the owner, with a migration decision for existing assets.
2. **Given** an asset produced by the new pipeline, **When** its record is checked, **Then** it states source, licence, tools and versions, parameters, transformations, the brief, and an independent reviewer.
3. **Given** generative AI used in an asset, **When** the asset is reviewed, **Then** it has artistic review and correction before shipping, and its licence permits commercial use.

---

### User Story 8 — Incremental replacement with final owner acceptance (Priority: P1)

The approved assets replace the current ones one family at a time. Every family has its own regression checks and screenshot validation, so the game is always shippable between families. The feature ends with owner visual acceptance per surface and screen class, with before/after sheets and mobile performance within the agreed budgets.

**Why this priority**: This is the outcome the owner pays for. Incremental delivery keeps risk small and evidence reviewable.

**Independent Test**: For each replaced family, a before/after sheet, the regression suites and the screenshot checks exist on the merge commit. The final acceptance record carries an owner verdict per surface and screen class.

**Acceptance Scenarios**:

1. **Given** a family replaced, **When** it merges, **Then** all suites pass and before/after captures for the affected surfaces are recorded.
2. **Given** the completed presentation, **When** final acceptance runs, **Then** the owner records accept or reject per surface and screen class, and the success criteria are reported.

---

### Edge Cases

- **Mixed families mid-production.** Some families are in the new look and some in the old (US8). The mixed state must stay readable and functional. If it looks incoherent, the plan orders families to minimize the mixed period, or ships them behind the development-build selection until a coherent set exists.
- **Panel open on small layouts.** With the machine card open on the compact phone layout at 200 % text, the selected machine must stay fully visible. Larger machine art must not push it under the panel.
- **Overhang.** Taller machine artwork may overhang neighbouring locations. Hit areas and cue anchors must stay unambiguous: a tap selects the machine whose location was tapped, and a cue reads as belonging to its own machine (F004 A-03).
- **Locations not shown as machines.** Empty and locked locations must stay distinguishable from occupied ones and from decoration, by shape and value and not by colour alone.
- **Offline return, Overdrive, prestige reset, stage changes.** Visuals must reflect the authoritative state immediately after each of these. There must be no catch-up animation that implies production that did not happen.
- **Low-memory or slow devices.** Loading the higher-resolution art must not hang or crash. The F006 Android load hang on a 2 MiB atlas page is a known precedent. Higher-resolution textures must be tested on the Android emulator before any family merges.
- **Layout counts other than 12.** Test layouts must keep working (F006 SC-011). This covers smaller, larger and non-rectangular arrangements.
- **Reduced motion while an effect is in flight.** The effect stops or goes static at once, and the state stays readable.
- **Owner rejects all candidates.** US3 scenario 2 applies, and the feature loops back to US2 with a revised brief.

## Requirements *(mandatory)*

### Gates and scope

- **FR-001**: The feature MUST NOT change production, economy, balancing, content definitions, research, prestige, unlock rules, offline progress, deterministic simulation results or the save format. It MUST NOT add recipes, resource types, logistics mechanics, free-form building, regions or progression systems. Every visual MUST derive from existing authoritative state, without changing it.
- **FR-002**: F007 M1 and M2 MAY run while F006 closes (Q1). F007 MUST own the F006 carries AD-1, AD-4 and AD-7: the world upgrade marker, the deck edge and void, and the store-full marker scale and cap. The F006 closeout MUST NOT build them separately. F007 M1 MUST record the physical-device baseline of the F006 look, which closes PERF-9. C-68 stays with F006. F007 work MUST NOT write F006 artifacts, and F007 M5 production MUST NOT merge before F006 T549 has recorded its interim verdict. That keeps the F006 acceptance evidence tied to the F006 look.
- **FR-003**: The feature MUST proceed through gated milestones in this order:
  - M1, the audit (US1);
  - M2, the direction exploration and in-engine prototype (US2);
  - M3, the owner direction decision (US3);
  - M4, the Art Bible, World Bible and pipeline revision (US7);
  - M5, incremental production and integration (US4, US5, US6, US8);
  - M6, final visual acceptance (US8).

  No asset-family production task for M5 may be dispatched before the M3 decision record exists. No M5 family may merge before the M4 rules it depends on are approved.
- **FR-004**: The tasks for M4–M6 depend on the chosen direction. They MUST be generated or regenerated after the M3 decision, and `$speckit-analyze` re-run, before they are dispatched.
- **FR-005**: Any change to slot geometry, interactive footprints, hit areas, placement behaviour or the 12-location model requires an explicit owner decision. Artwork MAY differ in visual footprint and height per machine type, within the overlap rules of FR-026.

### Audit and comparison (M1, M2)

- **FR-006**: The audit MUST record:
  - the current captures (FR-011, FR-012);
  - the rendering settings;
  - the scene composition;
  - the asset inventory with per-family texture memory;
  - the presentation architecture seams that a new look can reuse;
  - a ranked problem list with evidence;
  - a per-direction disposition (preserve, relax, replace, remove) of each F004 art constraint, with reasons.
- **FR-007**: Exploration MUST produce, for each of Directions A, B and C, a style brief with material, lighting and colour references. Concept images MAY support the briefs, but MUST NOT be presented as in-engine evidence. Reference games inform principles only. Assets, art styles and recognizable elements of Factorio, Satisfactory, Captain of Industry or any other game MUST NOT be copied, and every direction MUST keep an original visual identity.
- **FR-008**: The in-engine prototype MUST show, for each of Directions A, B and C, all at full parity (Q2):
  - a detailed Miner, Smelter, Press, Assembler and Depot;
  - a coherent floor and surroundings;
  - visible infrastructure that organizes the site;
  - recognizable production states;
  - at least one animated production process;
  - one selected machine with its existing contextual UI;
  - the direction's lighting, shading and atmosphere.
- **FR-009**: The prototype MUST reuse the existing presentation architecture wherever possible, and MUST NOT change simulation code paths. In a development build, the prototype directions MUST be selectable without affecting saves. They MUST NOT be visible to players in a release build.
- **FR-010**: Direction C MUST be evaluated as a hybrid: 2D floor and environment, detailed pre-rendered or real-time 3D machinery, controlled depth and lighting, and the existing UI Toolkit interface. The evaluation MUST state which machinery is real-time 3D, if any, and what that costs.
- **FR-011**: Comparison captures MUST cover these screen classes, each at equal framing and resolution across all four looks:
  - mobile portrait (emulator 1080 × 2400);
  - mobile landscape (2400 × 1080);
  - tablet (an established tablet class from the F006 capture matrix);
  - a compact mobile layout with the machine card open (the 720 × 1280 class at `n_min` or its successor).
- **FR-012**: Comparison captures MUST show the same seeded game state per factory state. There are two factory states: a partially developed factory (fewer than half the locations occupied, an early development stage) and a dense established factory (all locations occupied, mixed states, the latest stage).
- **FR-013**: Every comparison and acceptance capture MUST come from actual Unity rendering of a real build or Play Mode capture rig. Concept art MUST NOT substitute for in-engine evidence.
- **FR-014**: The comparison MUST report, per direction, labelled as measured or estimated:
  - frame rate and frame pacing on the physical phone (Q3), and on the reference emulator class for comparison with the F004/F006 records;
  - texture and atlas memory;
  - APK size impact;
  - draw-call and pass cost;
  - asset production effort per machine family;
  - maintainability;
  - compatibility with future free factory building, real conveyors and further regions.
- **FR-015**: The projection MUST stay isometric unless the owner approves a change after a documented trade-off analysis (US3 scenario 3).

### Decision and rules (M3, M4)

- **FR-016**: The M3 decision brief MUST follow constitution VII. It contains context, constraints, two to four realistic options, trade-offs, a recommendation, labelled assumptions, and the smallest set of questions needed. The owner's decision and its conditions MUST be recorded in the feature's reviews, with a matching wiki ingest.
- **FR-017**: Changes to Art Bible `F004-PAB` and World Bible `F006-WB` MUST be proposed as new versions under their semver rules, marked rule by rule, and approved by the owner. They MUST NOT be bypassed silently. Until the new versions are approved, the current ones govern shipped assets.
- **FR-018**: The revised Art Bible MUST define, at least:
  - the projection and camera, density or resolution, and the scaling and filtering policy;
  - the material, lighting and shadow model;
  - the colour policy;
  - the outline policy;
  - the silhouette differentiation rule;
  - the animation frame rules and the flash limit;
  - the state-cue and indicator salience rule;
  - the canvas, pivot and sorting conventions;
  - atlas, compression and memory budgets;
  - a checker or named review for every rule.

  An F004 rule may be removed only with an explicit replacement or a recorded reason.
- **FR-019**: The revised pipeline MUST define a reproducible workflow for:
  - source models and materials;
  - the lighting rig and the camera and projection;
  - animation and sprite export;
  - Unity import settings, atlas generation and asset manifests;
  - provenance;
  - automated validation;
  - visual review.

  It MUST state the regeneration tolerance. It MUST reuse the existing Blender, ComfyUI, `tools/art-pipeline` and importer tooling where practical, and record why anything is replaced.

### Machines, environment, feedback (M5)

- **FR-020**: Within each direction, the four machine types MUST have clearly different silhouettes, height profiles, proportions and animation characteristics. Four similar boxes with different colours or minor decoration do not meet this requirement.
- **FR-021**: Each machine MUST visually communicate its industrial purpose, per the brief: the Miner extracts, the Smelter applies heat, the Press presses with a moving assembly, and the Assembler builds with tooling or arms. These descriptions are artistic guidance and MUST NOT create new mechanics.
- **FR-022**: Machines MUST be larger and more prominent on screen than in F006, by the factor approved at M3 (SC-004), on every screen class. All existing touch-target, readability and fit rules still apply.
- **FR-023**: The environment MUST present one coherent industrial site. It covers:
  - floors;
  - foundations and supports;
  - pipes and conduits;
  - tanks and containers;
  - railings and walkways;
  - beams and frames;
  - machine-specific surroundings;
  - an intentional world boundary and background that replace the current black void.

  Decorative elements MUST have a plausible industrial purpose and MUST NOT be scattered randomly.
- **FR-024**: Decorative infrastructure MUST NOT imply physical transport between machines that the simulation does not perform. The current rule stays: machines link only to the depot (World Bible OD-2, F006 FR-013). Cosmetic infrastructure MUST be visually distinguishable from the functional depot trunks. Any change to the no-machine-to-machine-link rule requires a World Bible version and an owner decision.
- **FR-025**: Visual hierarchy MUST keep interactive machinery, the depot, state cues, markers, the selection frame and hit areas above, and unobscured by, decoration. The camera's fit and focus rules MUST keep the selected machine fully visible with panels open.
- **FR-026**: Machine artwork that overhangs neighbouring locations MUST NOT change which machine a tap selects. It MUST NOT cover another machine's cue or marker, or make a cue read as belonging to another machine.
- **FR-027**: Machines MUST communicate each of these states: active production, production completion, paused, missing input, full output storage, construction and upgrade. Each state is carried first by the machinery's own behaviour and lighting, and backed by a restrained indicator. The existing badge precedence stays the single source of state, and no second precedence rule is allowed.
- **FR-028**: Problem machines MUST stay identifiable without colour: by shape, motion state, or indicator glyph. They MUST pass the grayscale and colour-vision trials (SC-008).
- **FR-029**: State and upgrade indicators MUST NOT be the dominant shapes of the composition with all locations occupied. The Art Director's salience review MUST confirm this on the dense factory captures.
- **FR-030**: Reduced motion MUST stop all decorative and machine animation and leave readable static state cues. When reduced motion is off, motion MUST stay within the revised Art Bible's flash and motion limits.
- **FR-031**: Every visual effect MUST stay synchronized with the authoritative simulation. Effects for offline arrivals and prestige resets MUST follow the existing F006 rules: no catch-up production animation.

### Quality, performance and process

- **FR-032**: Mobile performance budgets MUST be agreed before M5 starts: a revision of the F004 frozen budgets, owner-approved and recorded in the Art Bible. They cover frame rate and pacing, steady-state allocations, texture and atlas memory with the page-size rule, APK size, draw passes and shader variants. Higher-resolution art MUST NOT ship on an assumption that the 1 MiB page cap no longer applies. The cap is re-measured for the chosen texture format, or it stays.
- **FR-033**: A change of density, palette cap, gradient, dither, outline or shading rules is expected to be a **major** Art Bible version, under F004-PAB §16. It MUST come with a migration decision for every existing asset family: replaced in F007, kept, or retired.
- **FR-034**: Every new or changed visual asset MUST follow the revised pipeline: a brief, provenance, deterministic or tolerance-bounded checks, independent Art Director and Technical Art review, and the rule that producers never review their own output. Generated content MUST have a licence that permits commercial use, and MUST be artistically reviewed and corrected before it ships.
- **FR-035**: Every asset family replacement MUST merge with:
  - passing Edit Mode, Play Mode and Windows test-player suites;
  - a successful emulator journey;
  - before/after screenshots of the affected surfaces;
  - a budget check against FR-032.
- **FR-036**: The feature MUST keep the F006 interaction functionality: selection, card, detail view, building, hub, offline summary, 100–200 % text, 44 pt touch targets, keyboard and pointer input, and layout independence from 12. Responsive and accessibility tests MUST pass on the new look.
- **FR-037**: The visual acceptance process MUST evaluate:
  - machine silhouette differentiation;
  - industrial material readability;
  - environmental coherence;
  - camera composition;
  - visual density and clutter;
  - state readability;
  - animation quality;
  - consistency across screen classes;
  - performance on mobile hardware;
  - the before/after comparison.

  The method is the rubric in SC-002, with independent reviewers and the owner verdict.
- **FR-038**: Role gates. Mandatory for every non-trivial M1–M6 deliverable, as applicable:
  - Game Designer/Product Owner, Software Architect and Test/QA;
  - Art Director (all visual assets and the Art Bible);
  - Technical Art (assets, import, atlases, shaders, rendering settings);
  - UI/UX (player-visible interaction and cue salience);
  - World Designer (site composition, infrastructure purpose, World Bible);
  - Performance (render, memory and APK budgets).

  DevOps is mandatory for rendering-pipeline settings changes. Security/Privacy and Game Systems/Economy are expected to be omitted, because no trust boundary, persisted data or economy changes. The plan records the omission and its reason. Play-Tester runs once on the M2 prototype and once per M5 batch that changes the player-visible core loop. Its report goes to the owner.
- **FR-039**: Meaningful decisions MUST be ingested into the wiki: the constraint dispositions, the direction decision, the new budgets, the pipeline changes and the import settings. The mandatory `after_implement` hooks MUST run per batch.

### Key Entities

- **Visual direction**: a candidate look (A, B, C, or a revised one). It has a style brief, constraint dispositions, prototype assets, captures, measured and estimated costs, and a status: explored, prototyped, rejected or approved.
- **Comparison capture set**: captures of the four looks for one screen class and one factory state. Each capture carries the build commit, the seed, the framing, the resolution and its provenance.
- **Constraint disposition**: one F004 art rule, its per-direction verdict (preserve, relax, replace or remove), the reason, and the evidence or hypothesis.
- **Direction decision record**: the owner's choice, its conditions, the rejected alternatives with reasons, the date, and links to the evidence seen.
- **Art Bible version and World Bible version**: the versioned rule sets that govern shipped assets, with change markers and a migration decision.
- **Asset family**: the unit of incremental replacement. Examples are machines, depot, floor and foundations, infrastructure, effects and indicators. Each family has a brief, a manifest, provenance, checks, review records and before/after captures.
- **Performance budget set**: the agreed limits for the new look, with their measurement method and workload.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001 — Clearly different and clearly better** *(binding)*. In the owner's final before/after review, the owner sees the F006 baseline and the final captures side by side, with matched states, framing and screen classes. The owner records that the new look is substantially improved on every required screen class. A result that looks essentially like the F006 game fails this criterion, whatever its technical success.
- **SC-002 — Visual quality rubric** *(binding)*. The Art Director and at least one other independent reviewer score the approved direction's prototype and the final build on the ten FR-037 dimensions, on a 1–5 scale. The rubric anchors are defined before M2 captures are scored. Every dimension averages at least 4, with no dimension below 3, and the F006 baseline is scored with the same rubric for comparison. The owner may override a rubric pass. A rubric failure blocks acceptance unless the owner records an exception.
- **SC-003 — Machines are recognizable from shape** *(binding)*. An independent reviewer sees grayscale silhouettes of the four machine types, shuffled and unlabelled, at the phone portrait fit-all scale. The reviewer names each type correctly in at least 9 of 10 trials per type. Any pair of machine silhouettes overlaps by no more than the limit the revised Art Bible sets. That limit is no looser than the F004 limit of 0.70.
- **SC-004 — Machines are more prominent** *(binding)*. At the default fit-all framing, the median on-screen area of a machine is at least 1.5 times its F006 baseline on phone portrait and on tablet. The owner may raise this factor at M3. The required readability and touch-target floors still hold.
- **SC-005 — Materials read** *(guidance)*. On the dense-factory captures, an independent reviewer correctly identifies steel, copper, concrete and iron surfaces when prompted with a region, in at least 8 of 10 prompts.
- **SC-006 — No dead screen** *(binding)*. At fit-all with no panel open, the share of the safe screen area that is neither world artwork nor interface is at most 10 % on every required screen class, in both factory states. In the F006 phone portrait baseline it is roughly 40 %, measured in M1 by the same method.
- **SC-007 — One coherent site** *(binding)*. The World Designer's conformance review of the final site finds that every decorative element has a stated industrial purpose. It finds no element that implies machine-to-machine transport, and no decoration covering a machine, cue, marker or hit area.
- **SC-008 — State readability does not regress** *(binding)*. Blind naming of every machine state, in grayscale and under three colour-vision filters, on the final look meets or beats the F006 SC-005 results, with no state named below its F006 rate.
- **SC-009 — Problems at a glance** *(guidance)*. In the owner's timed trial on seeded mixed-state captures, the owner names the stopped machines and their problem category within 3.0 s in at least 9 of 10 trials, the same threshold as F006 SC-004.
- **SC-010 — Motion behaves** *(binding)*. With reduced motion on, no frame-to-frame change occurs in decorative or machine art over a 10-second capture of a running factory. With it off, no capture exceeds the revised flash limit. No effect plays for production the simulation did not perform.
- **SC-011 — Performance within agreed budgets** *(binding)*. The final build meets every FR-032 budget on the physical phone (Q3), on the reference emulator classes and on the Windows player, measured with the established ABA method. The phone figures include frame rate, frame pacing and memory, and a sustained-load check over at least 10 minutes for thermal throttling. The tablet is measured too, if one is available.
- **SC-012 — Nothing else changed** *(binding)*. All existing Edit Mode, Play Mode and Windows test-player suites pass. The determinism and save round-trip tests produce identical results. Saves from `main` load unchanged, and the emulator journey succeeds.
- **SC-013 — Decided before produced** *(binding)*. The M3 decision record predates the dispatch of every M5 asset-family task. Every shipped asset cites an approved Art Bible version and has a complete provenance record.
- **SC-014 — Owner acceptance** *(binding)*. The final acceptance record contains an owner accept verdict for every surface and required screen class. A reject reopens that surface.

## Assumptions

- **A-1 Foundation**: Work starts from `main` `ac49f6c8`. F006's world-layout seam, effect pool, markers, stages, camera framing and shell are reused. F006 closes in parallel (Q1).
- **A-2 Preferred, not presumed**: Direction B is the owner's preferred candidate. The comparison treats all three directions on equal terms, and the recommendation follows the evidence.
- **A-3 Projection**: Isometric stays the starting point for all three directions. The 2:1 ratio and the existing Blender rig camera are the default for B and C.
- **A-4 Rendering pipeline**: URP 17.6.0 and Unity `6000.6.0f1` stay. Settings changes (lighting, colour space, HDR, post-processing, 2D lights, sprite material, compression) are allowed when justified by the prototype and approved through the plan and the budgets. A change of Unity version or render pipeline is out of scope.
- **A-5 12 locations**: The 12-location model and building rules stay authoritative. Artwork may vary in visual footprint and height (FR-005).
- **A-6 Logistics depiction**: Infrastructure that organizes the site — pipes, conduits, walkways, frames, utility runs feeding the depot trunks — is allowed. Machine-to-machine links stay forbidden (FR-024), unless a later owner decision changes World Bible OD-2.
- **A-7 World Bible**: "A neutral starter industrial platform on a dark base", the mood "dim-lit night shift" and the naming rules stay unless the approved direction needs a change. Replacing the starter ground or the dark base needs a new World Bible version (§5 swappable-ground rule).
- **A-8 Prototype isolation**: Prototype directions live in a development-build selection and dedicated asset paths. They are not shipped to players and do not touch saves. Rejected prototype assets are removed, or archived as non-shipping, after M3.
- **A-9 Devices** (Q3): Performance evidence uses a physical Android phone, and a tablet if one is available, as well as the Android emulator (1080 × 2400 @ 420 dpi) and the Windows player. The emulator and Windows player stay the capture rigs for the screen-class matrix. The plan records the device identity and how its exclusive use is coordinated.
- **A-14 F006 in parallel** (Q1): F007 does not edit F006 specs, reviews or task records. The F006 closeout does not build the carries moved to F007 (FR-002).
- **A-10 Generative tools**: ComfyUI and other generators may support concept exploration and asset creation, under the existing licence, provenance and cost rules. Models are downloaded only with owner approval. Hosted paid providers need prior owner approval.
- **A-11 Audio**: Sound is not in scope. Audio feedback stays a future candidate.
- **A-12 Task IDs**: F007 reserves a task ID range that collides with no existing Issue title (checked with `gh issue list --label maqa --state all`) before task generation.
- **A-13 Discovery lenses**: Product Owner, UI/UX and Art Director discovery perspectives are folded into this specification from the F006 final reviews and the owner brief. Independent discovery and plan reviews run at the clarify and plan phases before M1 tasks are dispatched.

## Future feature candidates (documented, not in scope)

| Candidate | Why it is separate |
|---|---|
| Free 2D placement and rotation | New placement and occupancy rules and a save schema change. The new art must support it (FR-014), but F007 does not build it |
| Real conveyors and machine-to-machine logistics | A new flow simulation. F007 art must leave room for visible belts later |
| Additional regions and ground families | A new world structure under the World Bible swappable-ground rule |
| Audio feedback and ambience | Not in the brief |
| Day/night or global lighting changes | A presentation feature with readability re-validation; only if the approved direction does not include it |

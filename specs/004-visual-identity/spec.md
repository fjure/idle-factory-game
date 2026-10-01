# Feature Specification: Visual Identity — 2.5D Pixel-Art Restyle

**Feature Branch**: `feat/004-visual-identity`

**Created**: 2026-10-01

**Status**: Draft

**Input**: User description: "Feature 004: Visual Identity — 2.5D pixel-art/cartoon restyle of the Feature 003 factory, with the URP 2D Renderer migration as phase 0." Full input: [`Spec-004-Draft.md`](../../Spec-004-Draft.md) plus the discovery and feasibility reviews of 2026-10-01 (Game Designer/Product Owner, Art Director, UI/UX, Software Architect with DevOps lens, Technical Artist, Performance Engineer).

## Context and authority

Feature 003 delivers the first complete Iron factory and prestige loop with **placeholder art** governed by the provisional Art Bible `F003-PAB v0.1` (`docs/art/F003-provisional-art-bible-v0.1.md`). Feature 004 changes how the game **looks**, not how it plays: it replaces every Feature 003 placeholder with a coherent 2.5D pixel-art/cartoon look and first moves the game onto the owner-selected rendering foundation. Gameplay rules, economy, content data and saved games are unchanged.

Owner decisions recorded before this specification (2026-09-30 and 2026-10-01):

- The final visual style is **2.5D and pixel-art/cartoon-like**. `F003-PAB v0.1` was a development placeholder and is superseded by a new, independently approved Art Bible.
- **Rendering foundation**: the project moves from Unity's Built-in Render Pipeline (deprecated by Unity from 6.5) to URP with the 2D Renderer. This happens as **phase 0 of this feature**, before the style spike, and only after Feature 003's scene, camera and settings issues have merged.
- Feature 003 finishes with its placeholder art. Feature 003 task T110 makes the sprite catalog resolution-agnostic (validated by world size and pivot, optional frame list) so that this feature can swap art without changing the catalog contract.
- The per-machine **status badges are to be reconsidered**: the owner suggests that a machine that is full or otherwise not working changes its own look (texture, color, lights) so the player sees immediately where something is wrong. Color must never be the only cue.

Wiki recovery (`/speckit-wiki-query`, 2026-10-01) returned **partial** coverage: presentation boundary, UI Toolkit pitfalls, performance targets and emulator procedures are recorded ([unity-presentation-boundary](../../wiki/pages/unity-presentation-boundary.md), [ui-toolkit-hud-layout-pitfalls](../../wiki/pages/ui-toolkit-hud-layout-pitfalls.md), [mobile-performance-targets](../../wiki/pages/mobile-performance-targets.md), [frame-rate-target](../../wiki/pages/frame-rate-target.md), [android-emulator-validation](../../wiki/pages/android-emulator-validation.md)). **Coverage gaps**: no wiki page covers art direction, the Art Bible, the render pipeline, or Feature 003's zoom and readability rules; these are taken from `docs/art/` and `specs/003-first-factory-prestige/` directly.

## Clarifications

### Session 2026-10-01

- Q: Integer pixel scaling leaves unused space on phones in portrait and no headroom in landscape. Which zoom trade-off applies? → A: Whole-number scales only, including fit-all; unused space is margin (FR-011).
- Q: May assets produced with generative image models ship? → A: Yes (combined with the per-family rule): use the 3D render pipeline where it suits an asset, generative models otherwise; generated assets ship after pixel pass, touch-up, Art Director approval and complete provenance with licenses (FR-021, FR-021a).
- Q: How does the reduced-motion preference work? → A: It follows the operating-system setting by default and can be overridden by an in-game switch stored in a separate local display-preferences file, never in the save (FR-020).
- Q: Who performs the readability evaluations? → A: An independent agent reviewer evaluates the shuffled grayscale and color-vision sheets; the owner performs the timed at-a-glance trials and the final visual acceptance (SC-001, SC-002).
- Q: Where does the reduced-motion control live? → A: A gear icon in the HUD opens a new "Settings" sheet that hosts it (and future settings); the HUD layout is re-verified on every screen class (FR-020, FR-020a).
- Owner context: the owner has no 3D or asset-production skills; agents choose the tool per use case and perform all production and touch-up; the owner decides the style and accepts results visually (FR-021a).

## User Scenarios & Testing *(mandatory)*

### User Story 1 — See at a glance which machine is in trouble (Priority: P1)

A player looks at their factory and immediately sees which machines are working and which are stuck (missing input, storage full, waiting, paused or switched off), without reading text and without relying on color.

**Why this priority**: Diagnosing the bottleneck is the game's core recurring question ("What is limiting my factory?"). The restyle must make this faster, not slower, and it answers the owner's concern that permanent badges on every machine look cluttered.

**Independent Test**: Show a 12-slot factory with every machine state present, in grayscale and in simulated protanopia, deuteranopia and tritanopia, at the smallest and at the fit-all zoom; reviewers identify every state.

**Acceptance Scenarios**:

1. **Given** a factory where one machine is blocked by a full output and all others run, **When** the player views the factory at fit-all zoom, **Then** the blocked machine is visually distinct from the running ones through a change on the machine itself plus a shape cue (icon or pattern), not through color alone.
2. **Given** every machine state (running, paused, waiting, missing input, storage full, inactive) and every slot condition (empty, locked with its level, occupied, selected), **When** a shuffled sheet is shown in grayscale and in each of the three color-vision-deficiency simulations at the smallest allowed zoom, **Then** the independent agent reviewer names every tile with zero confusions.
3. **Given** a machine that runs normally, **When** the player views it, **Then** no problem cue is shown on it.
4. **Given** a machine in a problem state, **When** the player selects it, **Then** the machine panel names the state in text.

---

### User Story 2 — A restyled, readable factory (Priority: P1)

The player sees a charming, chunky 2.5D pixel-art factory: four distinct machines, slot plates, floor, Iron resources and item tiers, all in one consistent style and all readable on every supported screen.

**Why this priority**: This is the visible value of the feature; every other story builds on the new asset set.

**Independent Test**: Load the Feature 003 factory with the new art on each supported screen class and compare machine identification, slot readability and layout against the acceptance checklist.

**Acceptance Scenarios**:

1. **Given** each of the four machines shown as a silhouette only, **When** the independent agent reviewer views them at the smallest allowed zoom, **Then** it identifies every machine correctly, and no two silhouettes exceed the overlap limit carried over from Feature 003 (IoU ≤ 0.70).
2. **Given** a fully occupied 12-slot factory, **When** it is shown, **Then** no machine hides a neighbor's state cue, selection frame or slot label.
3. **Given** any Feature 003 presentation key, **When** the game starts, **Then** it resolves to restyled art and no placeholder remains in the shipped set.

---

### User Story 3 — Crisp pixels at every zoom and on every screen (Priority: P1)

The player pans and zooms the factory on a phone, tablet or desktop, in portrait or landscape, and the pixel art always stays crisp: no blurred, shimmering or unevenly sized pixels, and all 12 slots can still be brought into view.

**Why this priority**: Pixel art that shimmers or blurs looks broken; this is a quality bar of the chosen style and it interacts with Feature 003's "all 12 slots visible" rule.

**Independent Test**: On each supported screen class and orientation, zoom through every allowed zoom level and fit-all, capture frames at rest and during a slow pan, and inspect pixel sizes.

**Acceptance Scenarios**:

1. **Given** any supported screen class and orientation, **When** the camera is at rest at any allowed zoom level, **Then** every art pixel is drawn as an equal whole number of screen pixels.
2. **Given** a 12-slot factory with no sheet open, **When** the player uses fit-all, **Then** all 12 slots are visible on every supported screen class, including rotated phones.
3. **Given** a pinch or wheel zoom, **When** the gesture ends, **Then** the view settles on an allowed zoom level.
4. **Given** a bottom sheet is open on a phone, **When** a slot is selected, **Then** the selected slot stays fully visible above the sheet.

---

### User Story 4 — A factory that feels alive, with reduced motion respected (Priority: P2)

Running machines animate (moving parts, lights, smoke), idle and stopped machines are still. Players who prefer reduced motion get a still factory that is equally readable.

**Why this priority**: Animation makes the idle loop rewarding to watch, but it is decorative; state must never depend on it.

**Independent Test**: Run a factory with mixed states, observe animation per state, then enable reduced motion and repeat the state-identification test.

**Acceptance Scenarios**:

1. **Given** a running machine, **When** it is visible, **Then** it plays its running loop; **Given** a paused, inactive or blocked machine, **Then** it is still.
2. **Given** reduced motion is enabled, **When** the factory is shown, **Then** no looping animation plays and every state remains identifiable by the User Story 1 test.
3. **Given** any animation, **When** it plays, **Then** nothing flashes more than three times per second.

---

### User Story 5 — Restyled interface icons (Priority: P3)

The HUD, sheets and panels use icons and frames in the same pixel-art style, while text stays in a clean, readable font.

**Why this priority**: Consistency polish; the world restyle delivers most of the value.

**Independent Test**: Walk every Feature 003 UI surface and check that each icon is restyled, legible and meets the contrast, text-scaling and touch-target rules.

**Acceptance Scenarios**:

1. **Given** any Feature 003 UI surface, **When** it is shown, **Then** all of its icons use the new style and no surface mixes old and new icon styles.
2. **Given** text scaling at 200 %, **When** any surface is shown, **Then** no text is clipped and body text keeps at least 4.5:1 contrast.

---

### User Story 6 — Owner decides the style on evidence, and every asset is reproducible (Priority: P1)

Before production starts, the owner compares concrete style candidates side by side in the running game and decides. Afterwards every shipped asset can be traced to its sources, tools, brief and Art Bible version and regenerated.

**Why this priority**: A wrong style decision multiplies production cost; unclear provenance is a legal and maintenance risk for a single-owner project.

**Independent Test**: Review the style-spike evidence page and the recorded owner decision; pick any shipped sprite and follow its provenance record to regenerate it.

**Acceptance Scenarios**:

1. **Given** the style spike is complete, **When** the owner reviews it, **Then** each candidate is shown in the game on a phone, a tablet and the emulator, with the pass/fail result of every readability gate, and the owner's choice is recorded before the Art Bible is frozen.
2. **Given** any shipped sprite, **When** its provenance record is followed, **Then** it names the source files, tool and model versions, brief, Art Bible version and license, and the asset can be regenerated within the recorded tolerance.
3. **Given** an asset produced with generative tools, **When** it is proposed for shipping, **Then** it is only accepted under the owner's AI-art policy (FR-021).

---

### User Story 7 — Rendering foundation switch without regression (Priority: P1, phase 0)

Before any restyle work, the game moves onto the new rendering foundation. The player notices nothing: the placeholder factory looks and behaves as before and performs within budget.

**Why this priority**: It is the base for the style spike (lighting, pixel-accurate camera) and must be isolated so that any visual or performance change can be attributed to it alone.

**Independent Test**: Compare the placeholder factory before and after the switch: automated test suites, screenshots at fixed resolutions, the Android emulator journey and performance measurements.

**Acceptance Scenarios**:

1. **Given** the Feature 003 placeholder factory, **When** the switch is complete, **Then** all existing automated test suites pass unchanged, and screenshots at the fixed test resolutions match the pre-switch baseline within a recorded tolerance.
2. **Given** the Android emulator journey, **When** it runs after the switch, **Then** it passes with the same steps as before.
3. **Given** the pre-switch performance baseline, **When** the same workload is measured after the switch in the same session, **Then** the regression stays within the budget set in the plan.

---

### Edge Cases

- **Smallest zoom on a phone in landscape**: Feature 003's fit-all is exactly 32 points per cell there, so there is no headroom; if the HUD grows, fit-all must still show all 12 slots (FR-011).
- **Fractional device pixel ratios** (for example 1.68 or 2.625 device pixels per point): crisp pixels are measured in device pixels, not points; letterboxing or cropping must never hide slots, HUD or touch targets.
- **Tall machines** that extend above their slot footprint: taps on the overhang select the slot behind it, never the tall machine; the overhang never hides another slot's cue.
- **Rotation during a pinch**: the gesture is cancelled and the view re-fits.
- **Screen size not yet known** (layout not resolved, rotation in progress): the last valid zoom level is kept until layout resolves.
- **Missing or failed sprite, atlas or animation frame**: the Feature 003 placeholder for that key is shown, one diagnostic is logged, the slot stays tappable, and state cues still appear.
- **Asset loaded late**: placeholder first, then the swap, with no layout shift.
- **Display-preferences file missing or corrupt**: the game starts with "follow system", logs one diagnostic without paths, and never touches the save.
- **Reduced motion switched while machines run**: loops stop at the next frame; nothing else changes.
- **Dithering or texture**: never carries meaning.
- **Locked slot** with its unlocking level, **empty slot** and **selected slot**: each has a restyled look and stays identifiable in grayscale.
- **Saved games**: loading a Feature 003 save shows the same factory in the new style; nothing in the save changes.

## Requirements *(mandatory)*

### Functional Requirements

**Scope and non-regression**

- **FR-001**: The feature MUST NOT change gameplay rules, economy values, content data, save format or saved games. All Feature 003 gameplay, content and save tests MUST pass unchanged.
- **FR-002**: Every Feature 003 presentation key MUST keep its identifier. New art is attached to existing keys; keys are never renamed or removed.
- **FR-003**: The restyled set MUST cover every Feature 003 visual: the four machines with every state, slot plates (empty, locked with level, occupied, selected), floor/background, Iron resources, the three item tiers, all machine state cues and markers, and all UI icons. No placeholder remains in the shipped set.

**Phase 0: rendering foundation**

- **FR-004**: Before the style spike, the game MUST run on the owner-selected rendering foundation (see Context) with the placeholder art, with no player-visible change: existing suites pass unchanged, fixed-resolution screenshots match the pre-switch baseline within a recorded tolerance, and the Android emulator journey passes.
- **FR-005**: Phase 0 MUST start only after Feature 003's scene, camera and project-settings work has merged, and MUST be revertible as one self-contained change.
- **FR-006**: A performance baseline of the placeholder factory on the previous foundation (B1) MUST exist before phase 0 changes anything; phase 0 is measured against it (B2) in the same session.

**Style decision and Art Bible**

- **FR-007**: A style spike MUST compare at least two machines (including the pair at highest silhouette-confusion risk), the four slot plates, all state cues and two resource icons, across the candidate projections and production approaches, shown in the running game on a phone, a tablet and the emulator. It MUST compare at least two state-presentation variants: (a) the machine body carries the state plus exception-only icons, and (b) redesigned pixel badges.
- **FR-008**: The owner MUST record a decision on projection, pixel density, state-presentation variant and lighting approach before the Art Bible is frozen. The Art Director recommends; the Game Designer/Product Owner checks readability; producers of spike assets do not review their own output.
- **FR-009**: A new versioned Art Bible MUST supersede `F003-PAB v0.1` explicitly, section by section, and MUST be independently approved before production. It defines at least: projection and light direction, pixel density and canvas/pivot rules, a machine-readable project palette with license, outline/shading/dithering/animation rules, the state-cue model, contrast and color-vision rules, noise limits, UI and font rules, tool and version pins, and provenance rules. A change of projection, density or palette requires a new major version and a migration decision.

**Readability and state cues**

- **FR-010**: Every machine state and slot condition MUST be identifiable without color and without animation, at the smallest allowed zoom and at fit-all, on every supported screen class. Problem states (missing input, storage full, waiting) MUST be marked on or at the machine with a distinct shape cue per state; normal running MUST show no problem cue. Text labels for states MUST be available in the machine panel.
- **FR-011**: With no sheet open, fit-all MUST show all 12 slots on every Feature 003 screen class and orientation, and the resulting zoom MUST be an allowed crisp zoom level. Only whole-number pixel scales are allowed, including for fit-all; screen space that the largest fitting scale leaves unused is shown as margin, never filled by a fractional scale (owner decision 2026-10-01). If a layout change would make the 12 slots no longer fit at the smallest allowed scale on any supported screen class, that layout change is rejected or the zoom rule is revisited by an explicit owner decision.
- **FR-012**: The allowed zoom levels and the minimum zoom of this feature MUST explicitly supersede Feature 003's zoom steps (Z16/Z24/Z32/Z64) and their readability table; the corresponding Feature 003 tests are re-baselined, not deleted silently.
- **FR-013**: Machine silhouettes MUST stay pairwise distinguishable (IoU ≤ 0.70 on the alpha mask at full size and at the smallest zoom), with cast shadows excluded.
- **FR-014**: Non-text graphics MUST keep at least 3:1 contrast against their surface and text and glyphs at least 4.5:1, including under the darkest lighting the Art Bible allows.
- **FR-015**: Machine art taller than its slot footprint MUST NOT capture taps outside the footprint and MUST NOT hide another slot's state cue, marker, selection frame or label; state cues and the selection frame draw above all machine art.

**Crisp rendering and interaction**

- **FR-016**: At rest, every art pixel MUST be drawn as an equal whole number of device pixels at every allowed zoom level on the reference devices, with no shimmer during slow panning. The interface layer keeps its own scaling and is not bound to the art pixel grid.
- **FR-017**: Pinch and wheel zoom MUST settle on an allowed zoom level when the gesture ends; visible zoom buttons step through the same levels.
- **FR-018**: Slot hit areas remain the slot footprint and at least 44×44 UI points at every allowed zoom level; Feature 003 touch, keyboard-focus, rotation and safe-area behavior carries forward unchanged (Feature 003 FR-032, FR-033).

**Animation and motion**

- **FR-019**: Running machines MUST show a looping animation; paused, inactive and blocked machines MUST be still. Animation never carries meaning and never flashes more than three times per second.
- **FR-020**: When the player's reduced-motion preference is on, no looping animation plays and all states remain identifiable. The preference follows the operating-system setting where available; an in-game switch can override it (on, off, or follow system). The override is stored in a separate local display-preferences file, never in the save; a missing, unreadable or invalid preferences file falls back to "follow system" without an error dialog and is rewritten on the next change.

**Production, provenance and policy**

- **FR-020a**: A gear icon in the HUD opens a "Settings" sheet (owner decision 2026-10-01) that hosts the reduced-motion control. The icon and sheet follow Feature 003 rules: at least 44×44 UI points, keyboard reachable with visible focus and focus return on close, Escape/Android Back closes the sheet, rotation cancels nothing unsaved, usable at 200 % text, and the HUD layout passes the overlap and clipping tests on every supported screen class and orientation.
- **FR-021**: Every shipped asset MUST have a provenance record naming its source files, tools and versions (and for generative tools: model, add-ons, prompt, seed and workflow), brief, Art Bible version, license and every manual edit. Generated output is never shipped without the pixel-grid and palette pass and manual review. **AI-art policy (owner decision 2026-10-01)**: assets whose source was produced by generative image models MAY ship, after the pixel-grid and palette pass, touch-up, independent Art Director approval and a complete provenance record that includes the model and add-on licenses. A model or add-on whose license does not permit commercial use MUST NOT be used for shipped assets; unknown training-data provenance is recorded per model as an accepted risk.
- **FR-021a**: The production approach is chosen **per asset family** by the Art Director (agent role) and recorded in the brief with its reason: a 3D render pipeline where it suits the asset (consistent perspective and lighting across states, coherent animation frames, many states of one object), generative image models where it does not (small illustrative icons, backgrounds, concepts), and deterministic scripted drawing where exact shapes matter (state cues). The owner does not produce or edit assets; all production and touch-up are performed by agent roles with tools and recorded scripted steps, and the owner's role is the style decision (FR-008) and visual acceptance.
- **FR-022**: Every asset family MUST follow brief → production → independent Art Director review → Technical Art integration → in-game validation. Asset briefs extend the Feature 003 brief format with projection, canvas and footprint, pivot, frames per state, palette version, production approach and generator block.
- **FR-023**: Every shipped sprite MUST pass automated checks: palette compliance, canvas size, pivot, clean alpha, outline presence, frame count, and the importer and catalog settings the plan defines.
- **FR-024**: If a sprite, frame or animation for a key cannot be loaded, the game MUST show that key's Feature 003 placeholder, log one diagnostic without file paths, keep the slot interactive and keep its state cues.

**Performance and evidence**

- **FR-025**: The 60 FPS Android-emulator development target MUST hold on the Feature 003 producing 12-slot workload (W003) and on a visual worst case (all 12 slots occupied and animating, all cues visible, at the smallest zoom and at fit-all), measured against a same-session control and against the baselines B1 (previous foundation), B2 (new foundation, placeholder art) and B3 (new art).
- **FR-026**: Steady-state presentation MUST NOT allocate memory per frame, and presentation cost MUST NOT grow with machine count beyond the 12 slots; reduced motion removes animation cost.
- **FR-027**: Texture memory of the restyled world art and the growth of the Android package MUST stay within caps set in the plan from the B1/B2 measurements; results are labelled as emulator evidence, and the missing physical-device evidence is stated.

**Knowledge**

- **FR-028**: The approved Art Bible decisions, the rendering foundation decision and the zoom-rule change MUST be recorded in the project wiki, closing the coverage gaps named in the Context section.

### Key Entities

- **Art Bible (versioned)**: the approved rules for projection, density, palette, outline, shading, animation, state cues, readability, UI, tools and provenance; supersedes `F003-PAB v0.1`.
- **Style-spike record**: candidates, evidence per device, gate results and the owner's decision.
- **Asset brief**: per asset family; ID, footprint, canvas, pivot, states and frames, palette version, production approach, Art Bible version.
- **Asset provenance record**: per shipped asset; sources, tool and model versions, generator parameters, manual edits, license, reviewer.
- **Presentation key**: the stable Feature 003 identifier that maps game content to art (one sprite or an ordered frame list).
- **State-cue set**: the visual treatment and icons for each machine state and slot condition.
- **Display preferences**: local, per-device presentation settings (reduced-motion override); separate from the save and from content; versioned and validated on load.
- **Performance baseline set**: workloads (W003, visual worst case), devices, metrics and the B1/B2/B3 results.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An independent agent reviewer (not the producer or Art Director of the assets) names every machine state, marker and slot condition with **zero confusions** on shuffled sheets in grayscale and in all three color-vision-deficiency simulations, at the smallest allowed zoom and at fit-all; the owner confirms the sheets visually.
- **SC-002**: In a timed trial of 10 randomized 12-slot factory screenshots (one stuck machine each, shown in random order on a phone-sized view), the owner points out the stuck machine within **2 seconds** in at least **9 of 10** trials. The owner knows the art, so this is a recognizability check, not a naive-player study.
- **SC-003**: **100 %** of Feature 003 presentation keys show restyled art; **0** placeholders remain in the shipped set.
- **SC-004**: **100 %** of shipped sprites pass the automated asset checks, and **100 %** have a complete provenance record.
- **SC-005**: On every supported screen class and orientation, fit-all shows **all 12 slots**, and no mixed pixel sizes or shimmer are visible at any allowed zoom level on the reference devices.
- **SC-006**: All Feature 003 gameplay, content, save and UI test suites pass **unchanged in behavior** after phase 0 and after the restyle; re-baselined zoom tests are listed explicitly.
- **SC-007**: The Android-emulator development target of **60 FPS** holds on W003 and on the visual worst case (same-session control), with **zero** per-frame presentation allocations in steady state.
- **SC-008**: Phase 0 changes the placeholder factory's screenshots only within the recorded tolerance, and its measured regression against B1 stays within the plan's budget.
- **SC-009**: The owner's style decision is recorded **before** any production asset is created.

## Assumptions

- The reference devices are the Feature 003 screen classes and the Android emulator used for Feature 003 evidence; physical-device performance remains an open gap and is not claimed.
- The pixel density proposed in the draft (32 art pixels per cell, machines up to 2 × 3 cells tall) is a working hypothesis to be confirmed or replaced by the style spike (FR-008) and FR-011.
- The recommended projection is a 3/4 top-down view; an isometric view is a spike candidate. Machines do not rotate.
- Lighting is baked into the art by default; dynamic 2D lights are limited to accents unless the spike shows a measured benefit.
- Body text stays in a clean UI font; a pixel font is allowed only for headings and numbers that pass the minimum-size and contrast rules.
- Tool choices, pixels per unit, the camera implementation and atlas layout are decided in the plan. The initial per-family tool routing is recorded in `Spec-004-Draft.md` section 5.1a and is confirmed or adjusted by the style spike.
- Because no human performs pixel editing, touch-up is done through recorded scripted edits (and, where needed, generative inpainting); GUI pixel editors are optional and not required by any workflow.
- Local tools only; hosted generation services are out of scope.
- Out of scope: new machines, regions or content; player-selectable art styles or an isometric mode; runtime 3D; music and sound; gameplay or economy tuning.
- Dependencies: Feature 003 completion (including T110's resolution-agnostic catalog and the B1 performance baseline on the W003 workload); workstation leases for Unity and the emulator.

---
description: "Dependency-ordered work for the Visual Direction Reboot, milestones M1–M3 (M4–M6 are regenerated after the M3 decision)"
---

# Tasks: Visual Direction Reboot — Stylized Industrial 2.5D

**Input**: the approved artifacts in `specs/007-stylized-industrial-visual-reboot/`.

**Prerequisites**:
- `spec.md`, clarified 2026-11-27. This includes Q1–Q3, five clarify answers and the plan-review owner decisions O-1 to O-3.
- `plan.md`, with owner decisions OD7-1 to OD7-15. OD7-10 to OD7-12 are pending.
- `research.md` R01–R22, including the plan-review amendments.
- `data-model.md`.
- The contracts `contracts/look-profile.md`, `contracts/art-pipeline-v2.md` and `contracts/evidence-and-decision.md`.
- `quickstart.md`.
- `reviews/plan-reviews.md`.
- `component-diagram.md` and `software-architecture.md`.
- The constitution.

**Scope**: M1 (audit), M2 (foundations, three full-parity prototypes and the comparison) and M3 (owner decision) only. M4–M6 appear as a placeholder phase. Their tasks are generated after the M3 decision record exists, followed by `$speckit-analyze`, the ledger refresh and `$speckit-maqa-sync` (FR-004, research R01).

**Tests**: Every implementation Issue ends with its named tests green.

- Run `scripts/test-unity.ps1 -TestFile <file>` first, then the suite of its ledger unit.
- Art tooling runs through `scripts/test-art-pipeline.ps1`. Exit 3 is a blocker, never a pass.
- Tests that need `IDLEFACTORY_DEV` run through the DevTools runner path from T609, with a gate that fails on 0 discovered tests (QA-4).
- Test work belongs to each task. It is never a separate red-only Issue.
- No test is deleted or re-baselined without an entry in `reviews/rebaseline-map.md`, checked by the T638 script (QA-5).

**Implementation lane**: MAQA, as in F004 and F006.

- Each task is one MAQA Issue unless it is marked *Parent* or *Owner*.
- Issues are created only by an owner-approved `$speckit-maqa-sync` run.
- Every accepted dependency is merged to `origin/main` before its dependents start.
- After QA PASS the parent merges only through `scripts/maqa-merge.ps1 -Issue <n>`.
- Every batch ends with `scripts/test-unity.ps1 -StandaloneWindowsPlayer` (needs `unity,emulator`), the `after_implement` hooks, the hook gate and the stale-worktree report.

**Roles and skills**:
- Role sections come from `docs/agents/roles-and-reviews.md`. Workstation rules come from `docs/agents/workstation-pitfalls.md`.
- Claude Code workers may use these `unity:*` skills as knowledge only, after reading `docs/agents/unity-plugin-skills.md`:
  - `unity:manage-sprite-atlas` for import and atlases;
  - `unity:sprite-editor` for pivots;
  - `unity:validate-urp-render-graph-renderer-feature` and `unity:urp-postprocessing` for the C1 renderer;
  - `unity:ui-uitk` for card checks.
- Model tiers follow `AGENTS.md`: Medium (`sonnet`) by default.
  - T609 and T625 run at Medium with `high` reasoning, and the architect review of T609 is High (`opus`).
  - The Art Director's rubric and recommendation work in T608 and T635 runs at High.
  - DevOps (T602) runs at Low (`haiku`).

**Shared resources**:
- `unity`, `emulator` and `gpu` are leased through `scripts/resource-lease.ps1` with owner `issue-<n>`.
- The `emulator` lease also covers the Pixel 11 Pro on the shared ADB server (`resource-lease.ps1:18`).
- Every run with two devices attached passes `-Serial`.
- Pixel and emulator performance captures need `unity,emulator,gpu`.
- Blender and ComfyUI work needs `gpu`. Interactive MCP work also takes the `gpu` lease, under the session name.
- Issues carry the matching `resource:*` labels.

**Task IDs**: T600–T699 are reserved for F007. No `maqa` Issue title uses T6xx (checked 2026-11-27). This file uses T600–T644. M4–M6 continue after T644.

**Scene and settings rule**:
- Scenes, prefabs, `.asset` files, URP assets and `ProjectSettings/` change only through Unity tooling (MCP, the Inspector, or a temporary `-executeMethod` editor script). They are never edited as hand-written YAML.
- Changes to the URP pipeline or quality settings are reviewed by hand with their diff (OPS-5).
- The shipped default look stays `F006` throughout M1–M3. Release builds never contain prototype assets (look-profile rule 4).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: may run concurrently after its dependencies, on disjoint paths and worktrees. A Unity or GPU lease still serializes the runs.
- **[US#]**: the user story in `spec.md`.
- Setup, foundational and closeout tasks carry no story label.
- Every task names its role and lenses, tier, paths, validation, resources and the requirements it covers.
- *Parent* and *Owner* tasks have no MAQA Issue. The parent ticks them after verification.

## Issue mapping

| Task | Issue | Resources | Notes |
|---|---|---|---|
| T600, T601, T606, T639, T642, T643, T644, T636, T637, T640, T641 | *parent* or *owner* | varies | Gates, device record, reviews, owner checkpoints, decision |
| T602 | new | none | DevOps docs and art-path triggers |
| T603 | new | unity, emulator | Baseline captures and metric scripts |
| T604 | new | unity, emulator, gpu | Pixel baseline |
| T605 | new | none | Audit record |
| T607 | new | none | World Bible v0.3 (World Designer) |
| T608 | new | none | Rubric anchors |
| T609–T613, T615, T638 | new | unity (T613: gpu) | Foundations |
| T614 | new | unity, emulator, gpu | ASTC page-cap sweep |
| T616, T620, T624 | new | gpu (concepts only) | Direction briefs |
| T617–T619, T621–T623, T625–T628 | new | unity, gpu | Prototype art and integration |
| T629 | new | unity, emulator | Capture matrix |
| T630, T631 | new | none | Trials and rubric (independent sessions) |
| T632 | new | unity, emulator, gpu | Performance M2 |
| T633, T634 | new | unity, emulator | Play-Tester and UX/World reviews |
| T635 | new | none | Decision brief |

---

## Phase 1: Setup and gates (no story)

- [ ] T600 *Parent* Re-verify the MAQA gates on `origin/main`:
  - the constitution has no placeholders;
  - `.specify/branch-convention.yml` is present;
  - the wiki is initialised;
  - the labels `maqa`, `maqa:in-progress`, `maqa:in-review`, `maqa:blocked` and `resource:unity|emulator|gpu` exist;
  - `maqa-config.yml` holds the real Unity commands;
  - `$speckit-analyze` has no open CRITICAL finding;
  - the orchestration ledger validates for `specs/007-stylized-industrial-visual-reboot/tasks.md`.

  Record the result in `specs/007-stylized-industrial-visual-reboot/reviews/gates.md`. **Covers**: constitution VIII; plan "Implementation lane".
- [ ] T601 *Parent* Record the Pixel 11 Pro in `specs/007-stylized-industrial-visual-reboot/reviews/devices-f007.md`, reading the values through `adb -s <serial>`:
  - model, SoC and GPU, Android 17 build;
  - resolution, density and refresh-rate modes;
  - whether ASTC is supported (`GL_KHR_texture_compression_astc_ldr`).

  Then install and start a profiler-free development APK of `main` (`-noProfilerAutoconnect`). **Resources**: unity, emulator. **Covers**: OD7-9; research R16.
- [ ] T602 DevOps (Software Architect lens; Low, `haiku`). Changes:
  - `AGENTS.md` "Shared workstation resources" and `docs/agents/workstation-pitfalls.md` "Emulator and adb":
    - name the physical phone under the `emulator` lease;
    - require `-Serial` when two devices are attached;
    - require `unity,emulator,gpu` for phone performance runs;
    - adb starts from the main checkout, for the phone too.
  - Extend the art-path arrays in `scripts/check-after-implement-hooks.ps1:157` and `scripts/test-art-pipeline.ps1:46`, plus the doc lines `check-after-implement-hooks.ps1:20`, `test-art-pipeline.ps1:27` and `test-unity.ps1:31`, to `Assets/IdleFactory/Art/F004/`, `Art/F006/` and `Art/F007/`.
  - Add the manual diff-review rule for URP and quality settings to `docs/agents/workstation-pitfalls.md`.
  - Create the empty skeleton `specs/007-stylized-industrial-visual-reboot/reviews/{comparison,rubric,trials-f007,directions}/` (with `.gitkeep`).

  **Validation**: `scripts/test-art-pipeline.ps1` still exits 0. A dry run of `check-after-implement-hooks.ps1 -FeatureDir specs/006-world-first-factory-experience` now detects `Art/F006/` changes. **Resources**: none. **Covers**: OPS-1, OPS-2, OPS-3, OPS-5, OPS-6, OPS-8; research R16, R20.
- [ ] T638 Test/QA Engineer as implementer of test tooling (Software Architect lens; Medium). Two deliverables:
  - `tools/test-ids/compare_test_ids.py`, with pytest in `tools/test-ids/tests/`. It lists every `[Test]` and `[TestCase]` id under `Assets/IdleFactory/Tests/` at a given ref, compares against `origin/main`, and fails unless every missing or renamed id appears in `specs/007-stylized-industrial-visual-reboot/reviews/rebaseline-map.md`.
  - Create the map with its header and the QA-5 candidate list (from `reviews/plan-reviews.md`) as "not yet moved".
  - Wire the script into `scripts/test-unity.ps1` as an optional `-CheckTestIds` step, documented in `docs/agents/workstation-pitfalls.md`.

  **Validation**: pytest green; a planted deleted test id fails. **Resources**: none. **Covers**: QA-5; research R19; FR-036.

## Phase 2: M1 — Current-state visual audit (US1, P1)

**Goal**: an evidence-based audit of the F006 look, including the Pixel baseline.

**Independent test**: open `reviews/audit-f007.md`. It has captures for every class and state, the frozen baseline metrics, a disposition per F004 rule and direction, and a ranked problem register that includes AD-1, AD-4 and AD-7.

- [ ] T603 [US1] Technical Artist (Test/QA lens; Medium), after T602. Five deliverables:
  1. **Metric scripts** in `tools/art-pipeline/metrics/`, each with pytest known-answer fixtures in `tools/art-pipeline/tests/metrics/`:
     - `dead_area.py`: the SC-006 share of key-colour pixels inside the safe area outside the UI rectangles. The UI rectangles come from a JSON layout dump; areas hidden by the card count as UI.
     - `machine_area.py`: the SC-004 median body area, excluding the shadow, with overhang reported separately.
     - `capture_iou.py`: the SC-003 silhouette IoU on captures.
  2. **Capture builds.** A reserved clear key colour for capture builds only. The key colour is never in art (QA-8), which a check enforces.
  3. **Fixture list** `specs/007-stylized-industrial-visual-reboot/reviews/comparison/fixtures.json`. It pins the seed ids and commits of the F006 trial fixtures for the *partial* state (5 occupied, stage 1, mixed running and one missing-input) and the *dense* state (12 occupied, latest stage, mixed states including storage full, paused, missing input and upgrade available) (R08, QA-9).
  4. **Captures.** Capture the F006 look for emuP 1080×2400, emuL 2400×1080, tabL 2560×1600 (Windows player), phoneP-card 720×1280 at 100 % and 200 % text, and 640×360 at 100 % and 200 % text, each in both states. They go to `reviews/comparison/F006/` with JSON sidecars per `contracts/evidence-and-decision.md`.
  5. **Frozen values.** Freeze the baseline values, together with the script version, in `reviews/comparison/F006/baseline-metrics.json`.

  **Validation**:
  - pytest green;
  - one cell captured twice is pixel-identical (determinism);
  - `scripts/test-art-pipeline.ps1` exits 0.

  **Resources**: unity, emulator. **Covers**: FR-006, FR-011, FR-011a, FR-012, FR-013; SC-003, SC-004, SC-006; QA-8, QA-9.
- [ ] T604 [P] [US1] Performance Engineer (Test/QA lens; Medium), after T601 and T602. Pixel baseline of the F006 look under the device protocol from research R16 (cool-down start, fixed refresh rate, charging state, brightness, Game Mode, battery saver off; ABAB order):
  - frame pacing (p95, p99, intervals ≥ 33 ms and ≥ 50 ms);
  - GPU frame time;
  - one overdraw capture;
  - PSS;
  - cold load time to the first frame;
  - a 10-minute sustained run on W006-V with thermal status;
  - an emulator control run in a separate lease window.

  Record it in `specs/007-stylized-industrial-visual-reboot/reviews/baseline-device.md`, with every number labelled "dev build" and its method. This closes F006 PERF-9; F006's parent cites it. **Resources**: unity, emulator, gpu. **Covers**: FR-014, FR-032; SC-011; PERF-1, PERF-7, PERF-9; OD7-1.
- [ ] T605 [US1] Art Director (author) with Technical Artist and UI/UX co-signing their rows (Medium), after T603. Write `specs/007-stylized-industrial-visual-reboot/reviews/audit-f007.md`:
  - **Index**: the baseline captures and metrics from T603 and T604.
  - **Render inventory**: rendering settings (URP 2D, Gamma, Unlit sprites, HDR and post off, integer camera) and scene composition.
  - **Asset inventory**: per family, with texture memory from the Android build report.
  - **Reusable seams**: the presentation-architecture seams per `plan.md` "Dependencies".
  - **Constraint dispositions**: one row per rule of `docs/art/F004-art-bible-v1.md` §2, §3, §5, §6, §7, §8, §10, §14 and §16, with preserve, relax, replace or remove for each of A, B and C, plus the reason and its evidence or hypothesis.
  - **Problem register**: ranked, with evidence links, including AD-1, AD-4 and AD-7 and the six problems in the spec's "Current visual state".

  **Validation**: every F004 rule id appears in the table; every problem links a capture or record. **Resources**: none. **Covers**: FR-006; US1 scenarios 1–3.
- [ ] T606 *Parent* Audit review: independent read-only Art Director (second session), Technical Artist, UI/UX Reviewer and Performance Engineer on the T605 commit. Findings go to `reviews/audit-review.md` and are fixed by the T605 author. **Covers**: constitution IX.

## Phase 3: M2 foundations (blocking for US2)

**Purpose**: shared seams, tooling, rules and measurements that all three directions need. No direction asset is produced before T609, T610, T611, T613 and T615 are merged and T639 has passed.

- [ ] T607 World Designer (Art Director lens; Medium), after T605. Author `docs/world/world-bible-v0.3.md` per research R21 and its amendments:
  - **Mood**: the shared dusk mood with new §3 mood words, plus measurable anchors (ground, sky and lamp swatches, key and fill hues, a ground luminance band, a minimum void luminance).
  - **Dark base**: remove "dark base" from §1 and §5, with a migration table for the existing ground family.
  - **Unchanged**: state that the site name, tagline and all strings stay as they are.
  - **Infrastructure rules** (FR-024): only trunks end at the depot; cosmetic runs never connect two machines; cosmetic runs use their own vocabulary with no flow marks, arrows or motion; nothing runs parallel to a trunk; every element has a purpose line. The trace-test question is "can this be traced from machine X to machine Y?", and the answer must be no.
  - **Background rules**: anonymous, silhouette only, no signage, power hardware, vehicles, people, roads or exits; the sky is a mood band only.
  - **Grouping rules**: never by the production chain.
  - **Stage legibility**: open deck, walled yard and gantry line stay legible.
  - **Forbidden terms**: extend the list (pylons, signage, roads, vehicles).
  - **Version**: record MINOR (non-additive) or MAJOR with the reason, and add the §7 clarification.

  The Art Director confirms in writing that the mood is renderable in A, B and C. Owner approval (OD7-10) is recorded in the document before T631 scores. **Resources**: none. **Covers**: FR-007a, FR-023, FR-024; SC-007; WD-1 to WD-8, AD-2; OD7-4, OD7-10.
- [ ] T608 [P] Art Director with UI/UX Reviewer (High tier for the Art Director), after T605. Write `specs/007-stylized-industrial-visual-reboot/reviews/rubric-anchors.md`:
  - anchors 1, 3 and 5 for each of the 8 visual dimensions (silhouettes, materials, environment coherence, composition, density and clutter, state readability, animation, consistency across classes), written from reference criteria;
  - F006 is not an anchor;
  - calibration on two sample captures;
  - the disagreement rule (a spread of more than 1 point is discussed and recorded);
  - the blinding limitation and its mitigation (matched framing, shuffled order);
  - the JSON schema for `reviews/rubric/*.json` per `data-model.md` RubricScore.

  The owner approves before any score is taken (OD7-11). **Resources**: none. **Covers**: SC-002, FR-037; AD-4, AD-5, AD-6, PO-4.
- [ ] T609 Gameplay Developer (Software Architect High review, Test/QA lens; Medium, high reasoning), after T602 and T638. The look-profile seam per `contracts/look-profile.md`.
  - **Runtime types** in `Assets/IdleFactory/Unity/Presentation/Look/`: `LookProfile.cs` (fields per `data-model.md`, including `requirementTable`), `LookProfileResolver.cs`, `ILookProfileSource.cs` and `LookProfileRequirementTable.cs`.
    - The bootstrap (`Assets/IdleFactory/Unity/Bootstrap/CoreGameFoundationBootstrap.cs`) owns one resolver instance and passes it to the world views, the camera settings and the effect pool. No new statics (ARCH-6).
    - The existing static registry in `PresentationSpriteCatalog.cs` becomes the `F006` profile's table (ARCH-5).
  - **Dev-only parts**:
    - `DevTools` injects `ILookProfileSource` at `BeforeSceneLoad`, mirroring `DefaultTimeScaleSource`, and adds a profile selector to `DevToolsOverlay.cs`.
    - A dev-only holder scene `Assets/IdleFactory/DevTools/Scenes/F007LookProfiles.unity`, created through Unity tooling, holds the prototype profile references. `Assets/IdleFactory/Editor/PlatformBuildCommands.cs` adds it to the build scenes only when `IDLEFACTORY_DEV` is in `extraScriptingDefines` (ARCH-1).
    - An unknown persisted id falls back to the shipped default with a log entry. Release builds ignore the key (ARCH-11).
  - **Shipped asset**: the `F006` profile at `Assets/IdleFactory/Art/F007/Profiles/F006.asset`, created through Unity tooling. It reproduces today's look exactly.
  - **DevTools test runner**: a path with a temporary `csc.rsp` and `-assemblyNames`, plus a non-zero-count gate (QA-4), scripted in `scripts/test-unity.ps1 -DevTools`.
  - **Tests**:
    - `Assets/IdleFactory/Tests/EditMode/F007/LookProfileResolutionTests.cs`: fail closed on a missing key or a wrong size; F006 parity of every resolved sprite.
    - `Assets/IdleFactory/Tests/PlayMode/F007/LookProfileDeterminismParityTests.cs` (R19 amendment):
      - a per-tick hash of the full authoritative state;
      - a fixed time source;
      - scripted build, upgrade, Overdrive, prestige and offline-gap inputs;
      - taps through `WorldTapResolver`, with identical id sequences;
      - a mid-run switch case;
      - identical save bytes;
      - a negative control with a planted mutating profile;
      - a guard that `Look/` uses no simulation write API.
    - `LookProfileInvalidSelectionTests.cs`.
    - The DevTools-assembly variants run under the new runner.
  - **Validation**:
    - the full suites have the same pass set as the base;
    - no visible change in the F006 look (`F006/BaselineMatrixTests` green);
    - `scripts/check-settings-noise.ps1 -Commit <sha>`;
    - the release build check from T615, once it lands, passes on this commit.
  - **Resources**: unity.
  - **Covers**: FR-001, FR-009, FR-035a; look-profile rules 1–5; ARCH-1, ARCH-5, ARCH-6, ARCH-7, ARCH-11; QA-1, QA-2, QA-4.
- [ ] T610 Gameplay Developer (Software Architect and Test/QA lenses; Medium, high reasoning), after T609. Pitch as instance data.
  - **Pitch conversion**: move `PivotOfCell` and `DeckTileRect` from static with `const` pitch (`Assets/IdleFactory/Unity/Presentation/World/SlotLattice.cs:20-41`, `FactoryWorldLayout.cs:477-493`) to instance methods of `FactoryWorldLayout`. Migrate all 16 call sites.
  - **`LayoutPitchSettings`**: add it to `WorldCompositionSettings.cs` with `pitchWorld` in world units, an optional `groupRule` and `minHitPoints` ≥ 44. Include the pitch in `LayoutComposition` equality and `IdentitySuffix` (`WorldCompositionSettings.cs:84-110`), so the camera refits (ARCH-4).
  - **Hit size**: make `CameraScaleModel.cs:281` profile-aware, keeping the single camera writer (ARCH-10).
  - **Tests**:
    - `Tests/EditMode/F007/LayoutPitchTests.cs`: F006 parity at the default pitch; a denser pitch keeps ids, unlock order, ≥ 44 pt and no overlap; a deliberately overlapping layout fails validation (QA-12).
    - `Tests/PlayMode/F007/OverhangTapTests.cs` on the dense layout per class and at 100 %/200 % text: a tap on an opaque pixel of the front-most machine selects that machine; the selection frame stays visible; hit-shape separation is measured in pt at `n_min`; a cue-anchor-to-owner check for grouped bays (UX-4).
    - The existing `F006/LayoutIndependenceTests`, `WorldLayoutParityTests` and `WorldTapPriorityTests` run unchanged.
  - **Resources**: unity.
  - **Covers**: FR-005, FR-026; look-profile rules 6–7; ARCH-4, ARCH-9, ARCH-10; UX-4; QA-12.
- [ ] T611 [P] Gameplay Developer for the view, plus Asset Artist on the scripted route for the glyphs (UI/UX Reviewer, Art Director and Test/QA lenses; Medium), after T609.
  - **View**: an indicator view at `indicatorAnchor` (`Assets/IdleFactory/Unity/Presentation/World/MachineIndicatorView.cs`). It is driven only by the existing badge precedence (no second precedence rule) and shows no indicator on running machines.
  - **Glyph family**: plated or outlined, adapted from the S3 = b shapes, under `tools/art-pipeline/` on the scripted route, with the upgrade glyph on the same plate family (AD-1).
  - **Glyph rules**:
    - at least 24 pt at `n_min` on every class, including 720×1280 and 640×360;
    - strokes of at least 2 art px or the equivalent;
    - contrast of at least 3:1 between glyph and plate (UX-1, UX-3).
  - **Placement**: keep-out derived per look (AD-7). The upgrade glyph may move to the side when the denser pitch collides (AD-8).
  - **Tests**:
    - `Tests/EditMode/F007/IndicatorGlyphSizeTests.cs` (pt per class);
    - pipeline pytest for contrast;
    - `Tests/PlayMode/F007/IndicatorAnchorTests.cs` (anchor inside the body and the hit area; no overlap with a neighbour's indicator on the dense layout).
  - **Resources**: unity.
  - **Covers**: FR-027, FR-027b, FR-028, FR-029; R10; UX-1, UX-2, UX-3, AD-7, AD-8.
- [ ] T612 [P] Gameplay Developer (Game Systems/Economy Low review, Test/QA lens; Medium), after T609. Two pure presentation functions.
  - **`MachineBuildOutStages`** (`Assets/IdleFactory/Unity/Presentation/World/MachineBuildOutStages.cs` plus its asset): "Exactly 2 ascending machine levels (default 10 and 25; the second may move to 50 after Game Systems review), giving stages 0–2". It follows the current level and drops back on prestige.
  - **`FacilityFinishTiers`** (`FacilityFinishTiers.cs` plus its asset): "1–3 ascending prestige counts, giving 2–4 tiers including tier 0". It reads the persisted `PrestigeCount` only, never writes it, and survives resets.
  - **Composition**: stage × tier combine orthogonally (FR-027a, FR-027c).
  - **Game Systems review**: records in the Issue that both threshold sets come from existing data (the `milestones` in `Assets/IdleFactory/Content/Iron/iron-factory-content.json`, and the prestige count). It confirms "no reward, no currency, no unlock, no pacing", and that the fixture levels and prestige counts are reachable by a real player (GS-3).
  - **Tests**: `Tests/EditMode/F007/BuildOutStageTests.cs` and `FacilityFinishTierTests.cs` (boundaries; the drop-back after prestige for stages; persistence of tiers across prestige; no write to state).
  - **Resources**: unity.
  - **Covers**: FR-027a, FR-027c; R11; GS-1, GS-2, GS-3; OD7-15.
- [ ] T613 Technical Artist with 3D and Render Artist (Art Director and Test/QA lenses; Medium), after T602. Art pipeline v2 prototype per `contracts/art-pipeline-v2.md`.
  - **Rig and route**:
    - `tools/art-pipeline/blender/rig-v3.blend`, derived from `rig-v2.blend`, with the same camera and a three-light dusk set from the WB v0.3 anchors (it may start before T607 merges, then retunes);
    - `render.py` parameters;
    - the `render-hd` route: passes `beauty`, `shadow`, `emissiveMask` and optional `normal`; union-bbox trim with one pivot; dilate or premultiply; a manifest row per `data-model.md` MachineArtEntry.
  - **Checks**: canvas, pivot and frames; flash; IoU; indicator anchor; indicator contrast; texture-memory estimate; pass alignment (emissive ⊆ beauty alpha); key-colour absence; reproducibility (CPU, fixed seed, denoiser off, pinned Blender and OIDN; ΔE ≤ 2 on ≥ 99 %, plus an SSIM threshold set from a two-render test); prompt lint for generated inputs.
  - **Provenance**: the v2 additions.
  - **Unity import**: `Assets/IdleFactory/Editor/Art/F007SpriteImportPostprocessor.cs` with PPU per look, filtering, mips, ASTC/BC7 per platform, padding ≥ 2^mips or limited mips, tight meshes, and Sprite Atlas v2 per family with pages ≤ 1 MiB. Extend `AtlasPageBudgetTests` per look, staying strict at 1 MiB.
  - **Mesh export** (C): the route and its checks (TA-8).
  - **Tests**: pytest for every new check (known-answer fixtures); an Edit Mode importer test.
  - **Validation**: `scripts/test-art-pipeline.ps1` exits 0. A re-render of the reference asset reproduces within tolerance.
  - **Resources**: unity, gpu.
  - **Covers**: FR-019, FR-034; R05, R14; TA-4 to TA-8, AD-9, AD-10.
- [ ] T614 [P] Performance Engineer with Technical Artist (Test/QA lens; Medium), after T601 and T613. ASTC page-cap sweep per research R13 and its amendment:
  - an isolated dev APK with test atlases;
  - byte-matched ASTC 6×6 pages of 1448² (≈ 0.9 MiB) and 2048² (≈ 1.8 MiB), plus a texel-matched RGBA32 versus ASTC control;
  - 3 cold loads each on the **Pixel** (authoritative) and on the emulator, reporting the maximum, with the `isLoading` gate;
  - record whether the emulator supports ASTC natively.

  Record it in `specs/007-stylized-industrial-visual-reboot/reviews/page-cap-astc.md` with a pass or fail verdict per size. `AtlasPageBudgetTests` stays at 1 MiB unless the Pixel passes and the owner accepts a change at M4. **Resources**: unity, emulator, gpu. **Covers**: FR-032; R13; TA-1, TA-3, PERF-2, QA-11.
- [ ] T615 [P] Technical Artist with DevOps (Test/QA lens; Medium), after T609. Three deliverables:
  1. **Capture driver**: drives the look, geometry, class, text scale, state and seed, and writes PNGs plus JSON sidecars (fields per research R08 amendment).
  2. **Sheet generator** `tools/art-pipeline/comparison/make_sheets.py`:
     - rows F006, A, B, C; columns *same* and *proposed* geometry;
     - rejects sidecars that do not match their file name or commit;
     - lists missing cells against the 70-cell matrix plus the 640×360 reduced check.
  3. **Release isolation** in `scripts/check-release-build.ps1`:
     - checks `BuildReport.packedAssets` by path and GUID;
     - fails on `Art/F007/Prototype/`, any `LookProfile` with `isPrototype`, any `UniversalRendererData`, and prototype atlas names in the built data;
     - positive control: the dev build must contain the holder scene;
     - asserts the release variant count is unchanged (490);
     - an Edit Mode guard `Tests/EditMode/F007/PrototypeReferenceGuardTests.cs` checks that no asset or assembly outside DevTools references prototype paths, and that none are under `Resources/` or `StreamingAssets/`.

  **Tests**: pytest for the sheet generator. A release-build check run on a release APK and the Windows build, plus the dev-build control. **Resources**: unity. **Covers**: FR-009, FR-011, FR-011a, FR-012, FR-013; look-profile rule 4; QA-3, QA-9; ARCH-1, ARCH-2.
- [ ] T639 *Owner* Checkpoint 1 (O-2), after T607, T616, T620 and T624. The owner reviews World Bible v0.3 and the three briefs, and gives go or adjust per direction. This is not a ranking. A brief changes only on the owner's explicit word. Record it in `specs/007-stylized-industrial-visual-reboot/reviews/owner-checkpoints.md`. **Covers**: FR-016a; OD7-14.

## Phase 4: M2 — Three directions in the real game (US2, P1)

**Goal**: each direction reaches full parity in one development build, switchable through the look profile:

- Miner, Smelter, Press, Assembler and Depot;
- environment, infrastructure and world boundary;
- states and the integrated indicator;
- at least one animated process;
- stage 0–1 of one machine type;
- finish tiers 0–1 on one machine type plus a site patch;
- the card on a selected machine;
- the dusk mood.

**Independent test**: in a dev APK, switch between `F006`, `F007-A`, `F007-B` and `F007-C`. Every profile resolves (fail closed). The parity test is green. The release build check passes.

Rules that apply to every direction task:

- assets go only to `art-source/f007/prototype/<A|B|C>/` and `Assets/IdleFactory/Art/F007/Prototype/<A|B|C>/`;
- the brief is the governing document; nothing is shipping;
- provenance is per `contracts/art-pipeline-v2.md` with `artBibleVersion: "none (prototype brief)"`;
- the Art Director reviews independently and producers never review their own output;
- the Blender MCP or ComfyUI MCP is used for iteration only after a verified real tool call in the session, and final pixels come only from the headless batch.

### Direction A — Enhanced Pixel Art

- [ ] T616 [P] [US2] Art Director (World Designer lens; Medium), after T605 and T607. Write `specs/007-stylized-industrial-visual-reboot/reviews/directions/A.md` per research R04 and its amendment:
  - D48, at most 96 colours, ramps of 5–6 tones, selective outlines, limited ordered dither on large flat areas;
  - L2 Light2D capped at 12 per screen, palette ramp first and then the additive accent;
  - page plan of 9 frames per 512² page;
  - material, lighting and colour references (concept images labelled `CONCEPT — not in-engine evidence`; prompt lint applies);
  - machine purpose sketches per FR-021;
  - the infrastructure purpose list;
  - stage legibility.

  **Resources**: gpu, only if concepts are generated. **Covers**: FR-007, FR-008, FR-020, FR-021; AD-3, TA-9.
- [ ] T617 [US2] Asset Artist (Art Director, Technical Artist and Test/QA lenses; Medium), after T609, T611, T613, T616 and T639. The **first machine** in A (Miner, stage 0, run loop, indicator anchor), produced on the pixel route at D48, imported and wired into the `F007-A` profile. It is the input for checkpoint 2. **Tests**: profile resolution for the A keys present; flash ≤ 3/s; pipeline checks. **Resources**: unity, gpu. **Covers**: FR-008, FR-020, FR-021.
- [ ] T618 [US2] Asset Artist (same lenses; Medium), after T617 and T642. The remaining A machines (Smelter, Press, Assembler), the Depot with fill states EMPTY/LOW/HIGH/FULL, and stage 1 plus finish tier 1 of one machine type. **Tests**:
  - silhouette IoU ≤ 0.70 for every pair at every stage;
  - `LookProfileResolutionTests` covers all A machine keys.

  **Resources**: unity, gpu. **Covers**: FR-008, FR-020, FR-021, FR-027a, FR-027c; SC-003.
- [ ] T619 [US2] Asset Artist for the art, Technical Artist for integration (Art Director, World Designer and Test/QA lenses; Medium), after T618 and T610. Covers:
  - A environment: floors, foundations, infrastructure per the WB v0.3 rules, world boundary and background;
  - the proposed denser pitch as an `F007-A` `LayoutPitchSettings`;
  - effects on the shared clock (one process effect per machine type, plus completion);
  - the site patch for finish tier 1.

  **Tests**:
  - `ReducedMotionWorldTests` and the profile-parameterised allocation test (`[Category("StandalonePlayer")]`) under `F007-A`;
  - parity test green;
  - the UX-7 interaction check in `directions/A.md`.

  **Resources**: unity, gpu. **Covers**: FR-008, FR-023, FR-024, FR-025, FR-030, FR-031; SC-007, SC-010.

### Direction B — Stylized 3D-to-2D

- [ ] T620 [P] [US2] Art Director (World Designer lens; Medium), after T605 and T607. Write `reviews/directions/B.md` per research R05 and its amendments:
  - 128 px per world unit, canvas about 427×341;
  - stylized PBR, edge wear and AO;
  - soft shadows in R8 with multiply blend;
  - emissive mask without bloom, Standard view transform, a one-asset AgX comparison;
  - 12–16 frames, with moving parts as overlay sprites where that saves frames;
  - the memory levers;
  - a one-machine 192 px test;
  - references, purpose sketches, infrastructure list and stage legibility, as for A.

  **Resources**: gpu, only if concepts are generated. **Covers**: FR-007, FR-008, FR-020, FR-021; TA-1, TA-2, AD-9.
- [ ] T621 [US2] 3D and Render Artist (Art Director, Technical Artist and Test/QA lenses; Medium), after T609, T611, T613, T620 and T639. The **first machine** in B (Miner; model in `art-source/f007/prototype/B/`; render-hd passes; import; `F007-B` profile). Input for checkpoint 2. **Tests**: pipeline checks, including reproducibility and alignment. **Resources**: unity, gpu. **Covers**: FR-008, FR-020, FR-021.
- [ ] T622 [US2] 3D and Render Artist (same lenses; Medium), after T621 and T642. The remaining B machines, the Depot with fill states, stage 1 and finish tier 1 of one type, and the 192 px test for one machine. **Tests**: IoU per pair and stage; resolution tests. **Resources**: unity, gpu. **Covers**: FR-008, FR-020, FR-021, FR-027a, FR-027c; SC-003.
- [ ] T623 [US2] 3D and Render Artist for the art, Technical Artist for integration (Art Director, World Designer and Test/QA lenses; Medium), after T622 and T610. B environment, infrastructure, boundary and background, the proposed pitch, effects and the finish-tier site patch, with the same tests as T619 under `F007-B`. **Resources**: unity, gpu. **Covers**: as T619.

### Direction C — Hybrid 2.5D

- [ ] T624 [P] [US2] Art Director (World Designer lens; Medium), after T605 and T607. Write `reviews/directions/C.md` per research R06 and its amendments, making it a fair brief (AD-1):
  - a ramp or toon shader with 3 bands and a rim;
  - a contact-shadow blob;
  - at most 4 warm real-time lamp lights, or emissive plus probes;
  - a dusk-direction key light;
  - C reuses B's models where practical, with a rule for telling a model flaw apart from a rendering flaw;
  - 2D floor and environment in the B style;
  - references, purpose sketches, infrastructure list and stage legibility.

  **Resources**: none. **Covers**: FR-007, FR-008, FR-010.
- [ ] T625 [US2] Technical Artist with Gameplay Developer (Software Architect High review, Performance and Test/QA lenses; Medium, high reasoning), after T609, T611 and T624. **C1 feasibility spike**:
  - **Pipeline**: a dev-only second URP asset `Assets/IdleFactory/DevTools/Rendering/F007C_UniversalURP.asset`, created through Unity tooling. It uses a Universal Renderer (Forward), with depth and opaque textures off, MSAA, HDR and post off, and at most 1 shadow cascade or blob shadows only. DevTools assigns it through `QualitySettings.renderPipeline` before the first frame. The shipped URP asset keeps one 2D renderer (`RenderPipelineConfigurationTests` unchanged).
  - **Scene**: one machine mesh plus the 2D floor.
  - **Sorting decision**: depth-placed sprites versus transparent-queue meshes with sorting orders. Order tests cover floor, mesh, shadow, marker, effect, selection frame and indicator with overhang (`Tests/PlayMode/F007/C1SortingTests.cs`).
  - **Gate on the Pixel**, W007-D one-machine scene: frame p95 ≤ 16.7 ms and GPU ≤ 8 ms at 60 Hz (hypothesis thresholds); variant and SetPass deltas.
  - **Isolation**: the release build check from T615 passes, with no `UniversalRendererData` in release.
  - **Review**: the URP and quality diff is reviewed by hand.

  The result goes to `reviews/directions/C-spike.md` and to the owner (T643). **Resources**: unity, emulator, gpu. **Covers**: FR-010, FR-014; R06; ARCH-2, ARCH-3, ARCH-13; PERF-6; OPS-5.
- [ ] T626 [US2] 3D and Render Artist with Technical Artist (Art Director and Test/QA lenses; Medium), after T625, T643 and T613. The **first machine** in C (Miner mesh, the toon shader, the `F007-C` profile with mesh prefab set and renderer), unless the owner stopped or adjusted C at T643. Input for checkpoint 2 (C may join checkpoint 2 later than A and B). **Tests**: mesh-export checks; a Unity-render silhouette IoU step. **Resources**: unity, gpu. **Covers**: FR-008, FR-010, FR-020, FR-021.
- [ ] T627 [US2] 3D and Render Artist (same lenses; Medium), after T626 and T642. The remaining C machines, the Depot with fill states, stage 1 and finish tier 1 of one type. **Tests**: IoU through the render step; resolution tests. **Resources**: unity, gpu. **Covers**: as T622.
- [ ] T628 [US2] Technical Artist with Asset Artist (Art Director, World Designer and Test/QA lenses; Medium), after T627 and T610. C environment (2D), infrastructure, boundary, proposed pitch, effects, finish-tier patch and lamp lights, with the same tests as T619 under `F007-C`, plus `C1SortingTests` on the full scene. **Resources**: unity, gpu. **Covers**: as T619, plus FR-010.

### Owner checkpoint 2 and C1 gate

- [ ] T642 *Owner* Checkpoint 2 (O-2), after T617 and T621, with T626 joining when it is available. The owner sees the first machine of each direction in-engine on the Pixel and gives go or adjust per direction. Record it in `reviews/owner-checkpoints.md`. **Covers**: FR-016a.
- [ ] T643 *Owner* Checkpoint 3 (O-2), after T625. The owner reads `C-spike.md` and decides to continue, adjust or stop C. If C is stopped, the M3 brief records this, and T626–T628 are closed as not planned only on the owner's explicit word (OD7-2 stands otherwise). **Covers**: FR-016a; OD7-2, OD7-14.

### Comparison

- [ ] T629 [US2] Technical Artist (Test/QA lens; Medium), after T619, T623 and T628. Run the full capture matrix with the T615 driver:
  - 5 class variants × 2 states × 7 look and geometry columns (70 cells);
  - the 640×360 reduced check per direction (dense state, 100 % and 200 % text, proposed geometry);
  - one APNG strip per direction of the animated process.

  Output goes to `reviews/comparison/`. **Validation**: the sheet generator reports 0 missing cells; one cell re-captured is identical. **Resources**: unity, emulator. **Covers**: FR-011, FR-011a, FR-012, FR-013; SC-004, SC-006 measurements; QA-9.
- [ ] T630 [P] [US2] Independent trial sessions (Test/QA lens; Medium), after T629, plus *Owner* for the timed trial. Precondition: F006 `reviews/readability-f006.md` (T546) is on `main`; otherwise T641 runs first.
  - Silhouette naming per direction: grayscale, shuffled, 10 trials per type (SC-003).
  - Blind state naming in grayscale and three CVD filters, including static frames with reduced motion on and one stopped machine among running machines (SC-008).
  - Material prompts (SC-005).
  - The owner's timed trial on mixed-state captures (SC-009).

  Use `tools/art-pipeline/trials`. Output goes to `reviews/trials-f007/`. **Resources**: none. **Covers**: SC-003, SC-005, SC-008, SC-009; UX-2; PO-3.
- [ ] T631 [P] [US2] Rubric reviewers (Art Director, plus one of UI/UX or World Designer; separate sessions; High tier for the Art Director), after T629, T608 (approved) and T607 (approved). Score F006, A, B and C on the 8 dimensions against `rubric-anchors.md`, writing `reviews/rubric/<reviewer>-<look>.json`. A spread of more than 1 point is discussed and recorded. **Resources**: none. **Covers**: SC-002; FR-037.
- [ ] T632 [P] [US2] Performance Engineer (Test/QA lens; Medium), after T629. Measure per direction on the Pixel (authoritative) and the emulator, plus a Windows control, under the device protocol, ABAB, at W007-D:
  - **W007-D**: 12 running machines with effects, fixed seed, three zoom stops, one stage, fixed camera path; 30 s warm-up, 60 s sample; a 10-minute sustained run.
  - **Metrics**:
    - FPS, p95, p99, intervals ≥ 33 ms and ≥ 50 ms;
    - GPU ms and an overdraw capture;
    - on-device draw and pass counts;
    - PSS and world-art GPU memory from the build report (including mips, shadow and emissive layers);
    - APK delta, variants, cold load, sustained drift and thermal status.
  - **Spot check**: one release-config check for B.

  Every number is labelled "dev build", "measured" or "estimated". Output: `reviews/performance-m2.md`, with each direction against the frozen budgets and a draft of the budget set v2 items (PERF-5). **Resources**: unity, emulator, gpu. **Covers**: FR-014, FR-032; SC-011; PERF-1, PERF-3, PERF-4, PERF-5, PERF-8, PERF-9.
- [ ] T633 [P] [US2] Play-Tester (Medium), after T629. A player session per direction on the dev APK, on the emulator (adb screenshots only, no host input injection). It reports clarity, feedback, readability and feel, labelled as inference, to `reviews/playtest-f007-m2.md` for the owner. **Resources**: unity, emulator. **Covers**: FR-038; constitution IX.
- [ ] T634 [P] [US2] UI/UX Reviewer and World Designer (read-only; Medium), after T629.
  - **UI/UX**: the per-direction interaction check (select the front, back and edge machine in the dense state, open the card, no covered cue or frame; UX-7), the 640×360 reduced check (FR-011a), indicator salience against machinery (FR-029), the card framing at 200 % text, and the named F006 suites green under each profile (UX-8).
  - **World Designer**: the WB v0.3 conformance and trace test (SC-007).

  Output: `reviews/uiux-f007-m2.md` and `reviews/world-conformance-f007-m2.md`. **Resources**: unity, emulator. **Covers**: FR-025, FR-026, FR-029, FR-036; SC-007; UX-7, UX-8.
- [ ] T641 *Parent* Fallback, run only if F006 T546 is not on `main` when T630 starts. An independent session reproduces the F006 readability baseline (blind naming plus the owner's timed trial on F006 captures from T603) with the same tools, in `reviews/trials-f007/f006-baseline.md`. **Covers**: SC-008, SC-009; PO-3.

## Phase 5: M3 — Owner chooses the direction (US3, P1)

**Goal**: a recorded owner decision based on real evidence. "Revise" is a real option.

**Independent test**: `reviews/direction-decision.md` exists, names the chosen direction (or "revise"), the conditions, the geometry and the machine-area factor, links the evidence, and predates every M5 Issue.

- [ ] T644 *Owner* Hands-on play (O-1), after T629. The owner plays each direction on the Pixel through a profiler-free dev APK with the look switch. The parent prepares the APK and holds `unity,emulator`. Impressions go to `reviews/owner-checkpoints.md`. **Covers**: FR-016a; OD7-13.
- [ ] T635 [US3] Art Director (recommendation; High) with the parent (assembly), after T630, T631, T632, T633, T634 and T644. Write `specs/007-stylized-industrial-visual-reboot/reviews/direction-brief.md` per `contracts/evidence-and-decision.md`:
  - context and constraints;
  - the options A, B, C and "revise", each with strengths and weaknesses, rubric and trial results, measured and estimated performance and memory, production effort, maintainability, and fit for free building, conveyors and regions;
  - a recommendation, with labelled assumptions and risks;
  - the implied Art Bible v2 and WB changes;
  - the proposed production geometry and machine-area factor (≥ 1.5);
  - the smallest set of owner questions.

  **Resources**: none. **Covers**: FR-014, FR-015, FR-016; US3 scenarios 1–3.
- [ ] T636 *Owner* Decision (OD7-12), after T635. The parent records the owner's words verbatim in `reviews/direction-decision.md` (data-model DirectionDecisionRecord), and ingests it into the wiki. **Covers**: FR-016; SC-013.
- [ ] T637 *Parent* After T636:
  - the cleanup plan for rejected looks (assets, profiles, the dev scene, the second URP asset if C is rejected, per-profile tests, and the test-player build inputs, followed by a release check; ARCH-12);
  - regenerate `tasks.md` for M4–M6 within T645–T699;
  - `$speckit-analyze`, the ledger refresh and `$speckit-maqa-sync`.

  **Covers**: FR-003, FR-004; R01.
- [ ] T640 *Parent* Conditional, only if T636 is "revise". Write a revised brief with scope and budget agreed with the owner, then re-run the affected T616–T635 steps as new task ids, and record the loop in the decision file. **Covers**: US3 scenario 2; PO-7.

## Phase 6: M4–M6 placeholder (US4–US8)

The tasks for these stories are generated by T637 after the M3 decision. The plan fixes these anchors in advance:

- Art Bible `F007-AB v2.0` (R22), with a migration table and owner sign-off before any production;
- budget set v2 with the PERF-5 contents, approved by the owner;
- pipeline v2 freeze and its reproducibility tolerance;
- the production camera policy (R15);
- families replaced one at a time, live on merge (FR-035a), each with a before/after sheet, suites, journey and budget check;
- the M5 family merges wait for F006 T549's verdict on `main` (FR-002);
- M6 reruns the capture matrix, the trials, the rubric and the performance runs on release builds, and the owner gives a verdict per surface and class, with 640×360 binding (SC-001, SC-002, SC-011, SC-014).

## Batch closeout (every batch, parent)

After each batch's merges, the parent runs:

1. `scripts/maqa-sync-task-checkboxes.ps1 -TasksFile specs/007-stylized-industrial-visual-reboot/tasks.md`;
2. `$speckit-wiki-ingest specs/007-stylized-industrial-visual-reboot`;
3. `$speckit-learn-review`;
4. `$speckit-wiki-lint`;
5. `scripts/check-after-implement-hooks.ps1 -FeatureDir specs/007-stylized-industrial-visual-reboot`;
6. `scripts/maqa-stale-worktrees.ps1`.

Then the parent hands off to a fresh session per AGENTS.md.

---

## Dependencies and execution order

### Gates (strict)

- T600 → every Issue.
- T602 and T638 → all code and test tasks.
- T605 → T607, T608 and the briefs (T616, T620, T624).
- T607, T616, T620 and T624 → T639 (checkpoint 1) → first machines (T617, T621).
- T609 → T610, T611, T612, T615 and T625.
- T613 → all asset tasks.
- T617 and T621 → T642 (checkpoint 2) → T618 and T622.
- T625 → T643 (C1 gate) → T626 → T627 → T628.
- T619, T623 and T628 → T629 → T630, T631, T632, T633, T634 and T644 → T635 → T636 → T637 (or T640).
- T546 of F006 on `main`, or T641 → T630.
- WB v0.3 approval (T607) and rubric approval (T608) → T631.

### Proposed batches

| Batch | Tasks | Resources |
|---|---|---|
| B0 | T600, T601, T602, T638 | unity, emulator; no contention |
| B1 (M1) | T603, T604, then T605 and T606 | unity, emulator, gpu in turn |
| B2 (foundations) | T607, T608 (docs, parallel); T609 → T610, T611, T612, T615; T613; T614 | unity serial; gpu for T613 and T614 |
| B3 (briefs) | T616, T620, T624 → T639 | none or gpu |
| B4 (first machines and spike) | T617, T621, T625 → T642, T643 | gpu and unity in turn |
| B5 (full prototypes) | T618, T619, T622, T623, T626, T627, T628 | gpu-bound, serialized per lease; disjoint paths per direction |
| B6 (comparison) | T629 → T630, T631, T632, T633, T634, T644 → T635 → T636 → T637 | emulator and gpu in turn |

### Parallel examples

- **B2**: T611 and T612 run in parallel after T609 (different files; one `unity` lease at a time for test runs). T607 and T608 are docs-only and run in parallel with any code task.
- **B3**: the three briefs T616, T620 and T624 run in parallel (different files).
- **B5**: A, B and C art run in parallel on disjoint paths (`Prototype/A`, `B`, `C`). Renders serialize on `gpu`, and Unity imports serialize on `unity`.
- **B6**: trials (T630), rubric (T631) and the UX/World review (T634) are read-only and run in parallel. T632 and T633 need the emulator lease in turn.

## Implementation strategy

- **MVP of this phase**: US1 (M1 audit). It is useful on its own, because it fixes the baseline and closes F006 PERF-9.
- **Next**: the M2 foundations, which keep the F006 look unchanged and are verifiable by parity and release checks. Then the directions in checkpointed increments, each stopping at an owner checkpoint.
- **Then**: M3 ends this task set. Production (US4–US8) starts only after T637 regenerates the tasks.

# Implementation Plan: Visual Direction Reboot — Stylized Industrial 2.5D

**Branch**: `feat/007-stylized-industrial-visual-reboot` | **Date**: 2026-11-27 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/007-stylized-industrial-visual-reboot/spec.md`, clarified 2026-11-27 with 3 plus 5 owner answers.

## Summary

F007 replaces the current D24 pixel-art presentation with a richer, stylized industrial look. It changes no gameplay, economy, content or save state.

The approach is evidence first:

- **M1** audits the F006 look, including a physical-phone baseline.
- **M2** builds three full-parity in-engine prototypes in one development build. They are switched through a presentation-only **look profile** (`contracts/look-profile.md`) and share one dusk lighting mood (World Bible v0.3):
  - **A** Enhanced Pixel Art at D48;
  - **B** Blender-rendered high-resolution sprites;
  - **C** hybrid real-time 3D machinery on the Universal Renderer.
- M2 then compares the three with the current look through a capture matrix, independent trials, a quality rubric, and phone, emulator and Windows performance.
- **M3** is the owner's decision.
- **M4** writes Art Bible v2, the pipeline v2 freeze and budget set v2.
- **M5** replaces the asset families incrementally; they go live as they merge.
- **M6** is the final acceptance.

Tasks are generated now for M1–M3 only. M4–M6 are generated after the decision (research R01).

## Technical Context

**Language/Version**: C# for Unity `6000.6.0f1` (.NET Standard 2.1). Python, as pinned in `tools/art-pipeline/pyproject.toml`, for the pipeline, checks, trial and measurement scripts.

**Primary Dependencies**: Existing.

- URP 17.6.0, with the 2D Renderer, plus the Universal Renderer for the C1 prototype only (R06).
- UI Toolkit, Input System and Unity Test Framework.
- Blender 5.2.1 with `rig-v3.blend`, derived from `rig-v2`.
- ComfyUI with Z-Image-Turbo, for concepts and material textures only.

**No new Unity package.** A package change would need a separate plan decision (AGENTS.md).

**Storage**: No save or content change.

- Presentation assets:
  - `LookProfile`;
  - `MachineBuildOutStages`;
  - `LayoutPitchSettings`, which extends `WorldCompositionSettings`.
- The development-build profile selection may sit in the display-preferences file.
- Art lives in:
  - `art-source/f007/`;
  - `Assets/IdleFactory/Art/F007/Prototype/{A,B,C}/` (M2);
  - `Assets/IdleFactory/Art/F007/` (M5).

**Testing**:

- Edit Mode, Play Mode, and the tagged `StandalonePlayer` tests;
- the art-pipeline pytest suite;
- the emulator journey per look;
- the phone measurement scripts;
- independent trials and the rubric;
- owner trials (R18, R19).

**Target Platform**: Android ARM64 IL2CPP on the physical phone (Google Pixel 11 Pro, Android 17), plus the emulator at 1080x2400 and 420 dpi, plus the Windows Standalone player. No physical tablet exists. The tablet class is captured through the Windows player's tablet resolution.

**Project Type**: A single Unity 2D game repository with offline art tooling.

**Performance Goals**: M2 measures against the F004/F006 frozen budgets (R17). M4 sets budget set v2, which the owner approves. Budget set v2 covers:

- texture and atlas memory;
- the page rule, re-measured for ASTC (R13);
- variants and SetPass;
- 0 steady-state allocations;
- phone frame pacing, plus a 10-minute sustained run.

**Constraints**:

- FR-001, and the 12-location model (densified on screen only, FR-005).
- One camera writer. The integer camera model stays in M2.
- Machines link only to the depot (OD-2).
- Scene and prefab changes go through Unity tooling only.
- Resource leases. The `emulator` lease also covers the phone (R16).
- Prototype assets are excluded from release builds.

**Scale/Scope (M1–M3)**:

- 3 directions × (4 machines + depot + environment + indicators + effects + 2 stages of 1 machine type).
- A capture matrix of 70 cells. It is derived from 5 class variants (phoneP-card counts twice, at 100 % and 200 % text) × 2 factory states × 7 look and geometry columns (F006 in the same geometry only; A, B and C each in both geometries).
- One renderer spike (C1).
- About 20 new or extended test files.

## Constitution Check

*GATE: passed before Phase 0. Rechecked after Phase 1 design.*

| Principle | Evidence | Result |
|---|---|---|
| I. Correct observable behaviour | FR-001 is guarded by the determinism parity test across profiles. Every SC has a method (R18). Hypotheses are labelled and named for measurement. | PASS |
| II. Domain architecture, explicit state | All new state is authored presentation data, and an ownership table exists (data-model). Nothing enters the domain or the save. The build-out stage is a pure function of level. | PASS |
| III. Deterministic, data-driven | Profiles, pitch and stage thresholds are authored assets. Renders are seeded and reproducible within a stated tolerance. The capture fixtures are seeded. | PASS |
| IV. Tests protect behaviour | Contract tests per rule (look-profile). Pipeline checks. An explicit re-baseline list. No silent deletion (R19). | PASS |
| V. Simplicity | One seam (look profile) reuses the catalog, layout, effects and camera. There is no per-direction branch and no scene copies. See Complexity Tracking for the Universal Renderer prototype. | PASS (one justified item) |
| VI. Measured performance | The phone, emulator and Windows ABA. The page cap is re-measured before relaxing it. Budget set v2 is approved before M5. | PASS |
| VII. Human authority | Q1–Q3 and the five clarify answers are recorded. M3 is owner-only. WB v0.3, Art Bible v2 and budget v2 need owner approval. The geometry is approved in principle, and its details are decided at M3. | PASS |
| VIII. Traceability | FR/SC → R → contract → test (table below). Tasks for M4–M6 are regenerated and analysed after M3. | PASS |
| IX. Independent review | Producers never review their own work. The rubric and trials run in separate sessions. Plan reviews are listed below. | PASS |
| X. Safe delivery, durable knowledge | Prototype isolation, versioned bibles, provenance, wiki ingest after the plan and per batch. | PASS |

## Owner decisions

| ID | Decision (2026-11-27) | Source |
|---|---|---|
| OD7-1 | F006 closes in parallel. AD-1, AD-4 and AD-7 move to F007. PERF-9 is closed by the M1 phone baseline. C-68 stays with F006. | Spec Q1 |
| OD7-2 | All three directions are built at full prototype parity. | Spec Q2 |
| OD7-3 | A physical phone provides the performance evidence. | Spec Q3 |
| OD7-4 | One shared dusk mood, through World Bible v0.3. | Clarify 1 |
| OD7-5 | Families go live as they merge; a temporary style mix is accepted. | Clarify 2 |
| OD7-6 | A short upgrade effect plus 2–3 build-out stages per machine type, from existing levels. | Clarify 3 |
| OD7-7 | A small integrated status indicator replaces the free-standing badges. | Clarify 4 |
| OD7-8 | Denser on-screen location geometry is approved in principle. The concrete geometry is decided at M3. | Clarify 5 |
| OD7-9 | Phone: **Google Pixel 11 Pro, Android 17**. No tablet is available (owner, 2026-11-27). The SoC, GPU, resolution and refresh rate are read from the device in T601. | R16 |
| OD7-10 | *Pending (M2)*: World Bible v0.3 approval, before M2 scoring. | R21 |
| OD7-11 | *Pending (M2)*: approval of the rubric anchors, before scoring. | R18 |
| OD7-12 | *Pending (M3)*: the direction, the geometry, the machine-area factor and their conditions. | FR-016 |

## Implementation lane

- **Lane: MAQA**, as in F004 and F006.
- **Gates.** The bootstrap gates verified for F006 still hold on `main` `ac49f6c8`. They are re-verified by the parent before `$speckit-maqa-sync`: labels, `maqa-config.yml` commands, constitution and wiki.
- **Task IDs.** The range T600–T699 is reserved; no existing Issue title uses it (R20).
- **Batch end.** Each batch ends with the `after_implement` hooks (`$speckit-wiki-ingest`, `$speckit-learn-review`) and the hook gate.
- **M2 and the Play-Tester.** M2 changes no core loop. The Play-Tester runs once on the prototype build to report on feel, and the report goes to the owner.

## Dependencies

| Dependency | Status | Use |
|---|---|---|
| F006 on `main` (`ac49f6c8`) | Closing in parallel (OD7-1) | Layout seam, effects, markers, stages, shell, capture rigs, trial seeds |
| F006 T549 verdict | Pending | Gate for M5 merges only (FR-002) |
| `IdleFactory.DevTools` (`IDLEFACTORY_DEV`) | Exists | Hosts the prototype profile switch and the references (R07) |
| `PresentationSpriteCatalog` registry, `FactoryWorldLayout`, `LatticeLayoutBuilder`, `WorldTapResolver`, `EffectPool`, `PresentationAnimationClock` | Exist | Reused behind the look profile |
| `tools/art-pipeline`, `rig-v2.blend`, `F004ArtIntegration`, `F006SpriteImportPostprocessor`, `AtlasPageBudgetTests` | Exist | Extended (R14) |
| `scripts/test-unity.ps1` and `scripts/check-after-implement-hooks.ps1` art-path triggers | Cover `Art/F004/` only | A DevOps task adds `Art/F006/` and `Art/F007/` (contracts/art-pipeline-v2.md) |
| Blender MCP, ComfyUI MCP, Unity MCP | Blender and ComfyUI MCP are listed in the planning session, but no real tool call has been verified. Unity MCP refused the connection (ECONNREFUSED) | Verified by a real call per assignment; the CLI fallback is recorded (AGENTS.md, `docs/integrations/mcp-readiness.md`) |

## Design

### Components

- **Look profile seam** (`Assets/IdleFactory/Unity/Presentation/Look/`): `LookProfile`, `LookProfileResolver` and the required-key validation. World views ask the resolver instead of fixed catalogs. Profile selection lives in DevTools (development builds only).
- **Layout pitch**: `LayoutPitchSettings` feeds `LatticeLayoutBuilder` and `FactoryWorldLayout` without changing location identity (R09).
- **Machine view v2**: body, shadow, emissive mask and overlay parts per `MachineArtEntry`, plus an indicator view at the `indicatorAnchor` (R10). For C, a mesh view with the same API.
- **Build-out stage**: a pure `stage(level)` from `MachineBuildOutStages` (R11).
- **Environment**: world-boundary and background art per profile, replacing the black void (R15, AD-4).
- **Pipeline**: the `render-hd` and `mesh-export` routes, the checks and provenance (contracts/art-pipeline-v2.md), and `F007SpriteImportPostprocessor`.
- **Evidence tooling**:
  - the comparison capture driver and the sheet generator;
  - the dead-area and machine-area scripts;
  - the rubric JSON schema;
  - the phone measurement scripts (R08, R16, R18).

### Data flow

The data flow is unchanged from F006:

`RunSession` snapshot → presentation models (machine, attention, stage, depot) → world views.

The look profile chooses only the art, layout pitch and camera policy that the views apply. No path flows from presentation back to the simulation.

### Art and technical art

Directions are briefed in `reviews/directions/*.md` against World Bible v0.3, which supplies the mood. Assets follow the pipeline v2 prototype rules. The Art Director reviews every family, and the Technical Artist reviews import, atlases and passes. Interactive modelling uses the Blender MCP; final renders come from the headless batch.

### Performance

R16 and R17. The C1 feasibility spike measures phone frame time before the full C prototype is built. The page cap is re-measured for ASTC.

### Test strategy

See R19. The determinism parity test across profiles is the FR-001 guard. All suites stay green with `F006` as the shipped default throughout M1–M3.

### Requirement traceability (M1–M3 scope; M4–M6 at regeneration)

| Requirement | Research | Contract or record | Test or evidence |
|---|---|---|---|
| FR-001, FR-009 | R07 | look-profile rules 1, 4 | `LookProfileDeterminismParityTests`, `ReleaseBuildExcludesPrototypeAssets` |
| FR-002 | R02 | — | F006 closeout record; merge-gate check before M5 |
| FR-003, FR-004 | R01 | — | Analyze after regeneration; SC-013 git check |
| FR-005, FR-026 | R09 | look-profile rules 6, 7 | `LookProfileLayoutIndependenceTests`, `OverhangTapTests` |
| FR-006 | R03 | evidence: `audit-f007.md` | Audit review (Art Director, Technical Art, UI/UX) |
| FR-007, FR-007a | R04–R06, R21 | `directions/*.md`, WB v0.3 | World Designer and Art Director reviews; owner approval |
| FR-008, FR-010 | R04–R06 | look-profile required keys | `LookProfileResolutionTests`; prototype review |
| FR-011–FR-013 | R08 | evidence: capture rules | Sheet generator (0 missing cells) |
| FR-014, FR-032 | R13, R16, R17 | `performance-m2.md` | Phone, emulator and Windows ABA; page-cap measurement |
| FR-015 | R04–R06 | — | All three use isometric 2:1 |
| FR-016 | — | evidence: decision brief | Owner record |
| FR-020, FR-021, FR-027a | R11, R14 | art-pipeline-v2 checks | IoU check, `MachineBuildOutStageTests` |
| FR-027, FR-027b, FR-028, FR-029 | R10 | indicator keys | Blind naming, timed trial, salience review |
| FR-030, FR-031 | R12 | look-profile rule 10 | Reduced-motion tests, flash check |
| FR-034 | R14 | art-pipeline-v2 provenance | Pipeline refusal tests |
| FR-037, SC-002 | R18 | `rubric-anchors.md` | Rubric JSON |
| FR-038 | R20 | — | Review records |

### Review waves and role disposition

1. **Plan reviews** (before tasks; read-only, separate sessions):
   - Software Architect (High): look-profile seam, C1 renderer;
   - Art Director: direction definitions R04–R06 and rubric approach;
   - Technical Artist: R13, R14;
   - Performance Engineer: R16, R17;
   - UI/UX: R10, R15;
   - World Designer: R21;
   - Game Designer/PO: scope and milestones;
   - Test/QA: R18, R19;
   - Game Systems (Low): R11;
   - DevOps (Low): the R16 lease wording and the art-path triggers.

   Findings are recorded in `reviews/plan-reviews.md`.
2. **M1 batch**: Art Director, Technical Artist, UI/UX and Performance on the audit.
3. **M2 batches**:
   - per direction: Art Director, Technical Artist, Software Architect (seam) and Test/QA;
   - once for the whole M2: UI/UX, World Designer, Performance and Play-Tester.
4. **M3**: owner.

### Proposed work breakdown (input to `$speckit-tasks`, M1–M3)

**Phase 0 — Gates and set-up (parent).**

- T600: re-verify the MAQA gates.
- T601: record the Pixel 11 Pro identity (OD7-9), with the SoC, GPU, resolution, density and refresh rate read through `adb`. Check that a profiler-free development APK installs and starts.
- T602: DevOps, Low tier. Extend the lease wording (the `emulator` lease covers the phone) in AGENTS.md and `docs/agents/workstation-pitfalls.md`, and extend the art-path triggers to `Art/F006/` and `Art/F007/`.

**M1 — Audit.**

- T603: baseline captures and metrics scripts (dead area, machine area) on F006.
- T604: phone baseline, ABA plus sustained run (PERF-9).
- T605: audit record, constraint dispositions and problem register.
- T606: audit review.

**M2 — Foundations** (sequential before the directions).

- T607: World Bible v0.3, authored by the World Designer and approved by the owner (OD7-10).
- T608: rubric anchors, approved by the owner (OD7-11).
- T609: look-profile seam, resolver and DevTools switch, with the tests from contracts/look-profile.md (High-tier architect review).
- T610: `LayoutPitchSettings` and the overhang tap rules.
- T611: indicator view at the anchor, plus the scripted indicator glyph family, adapted from the S3 = b shapes.
- T612: `MachineBuildOutStages` (with the Game Systems review).
- T613: pipeline v2 prototype: `rig-v3`, the `render-hd` route, the checks and `F007SpriteImportPostprocessor`.
- T614: ASTC page-cap re-measurement on the emulator and the phone.
- T615: comparison capture driver, sheet generator and release-isolation build check.

**M2 — Directions** (parallel by direction once foundations merge, subject to the `gpu` and `unity` leases).

| Direction | Brief | Machines and depot | Environment, effects, stage art | Integration and review |
|---|---|---|---|---|
| A | T616 | T617 | T618 | T619 |
| B | T620 | T621 | T622 | T623 |
| C | T624 | T625: C1 feasibility spike (owner informed of the result), then T626 machines | T627 | T628 |

**M2 — Comparison.**

- T629: capture the full matrix.
- T630: independent trials (silhouettes, blind naming, materials) and the owner timed trial.
- T631: rubric scoring by two reviewers.
- T632: `performance-m2.md`.
- T633: Play-Tester on the prototype build.
- T634: UI/UX and World Designer reviews.

**M3 — Decision.**

- T635: decision brief.
- T636: owner decision record.
- T637: post-decision cleanup plan, regeneration of the M4–M6 tasks, and `analyze`.

**Closeout per batch.** Parent hooks, as in AGENTS.md.

## Project Structure

### Documentation (this feature)

```text
specs/007-stylized-industrial-visual-reboot/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── look-profile.md
│   ├── art-pipeline-v2.md
│   └── evidence-and-decision.md
├── checklists/requirements.md
├── reviews/              # created by M1–M6 tasks (see contracts/evidence-and-decision.md)
└── tasks.md              # $speckit-tasks (M1–M3 now; M4–M6 after M3)
```

### Source code (repository root)

```text
Assets/IdleFactory/
├── Unity/Presentation/Look/          # LookProfile, LookProfileResolver, required keys (new)
├── Unity/Presentation/World/         # existing seam; LayoutPitchSettings, indicator and stage views (extended)
├── DevTools/                         # profile switch, prototype profile references (extended; IDLEFACTORY_DEV only)
├── Editor/Art/                       # F007SpriteImportPostprocessor, atlas settings per look (new or extended)
├── Art/F007/Prototype/{A,B,C}/       # M2 prototype assets (removed or archived after M3)
├── Art/F007/                         # M5 production assets
└── Tests/{EditMode,PlayMode}/F007/   # contract tests
tools/art-pipeline/                   # render-hd and mesh-export routes, checks, capture and metrics scripts, trials
art-source/f007/                      # briefs, Blender sources, rig-v3, render params, provenance
docs/world/world-bible-v0.3.md        # M2 (FR-007a)
docs/art/F007-art-bible-v2.md         # M4
```

**Structure decision**: extend the existing Unity 2D project and art tooling in place. There is no new assembly except the `Look` folder inside `IdleFactory.Unity`, so asmdef boundaries are unchanged. The prototype references stay inside `IdleFactory.DevTools`.

## Complexity Tracking

| Item | Why needed | Simpler alternative rejected because |
|---|---|---|
| Universal Renderer world camera for the C prototype (C1) | The only way to render real-time lit 3D machinery at screen resolution in URP 17. The 2D Renderer gives meshes no 3D lighting, and the camera stack cannot mix renderer types (R06). It is confined to a dev-only profile. | C2 (RenderTexture) adds a fill-rate and resolution trade-off. C3 (fake-lit inside the 2D Renderer) would not test the hybrid honestly. Skipping C conflicts with OD7-2. |
| Three full prototypes | Owner decision OD7-2 | Reduced-fidelity screening was offered and declined |

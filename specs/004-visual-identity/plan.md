# Implementation Plan: Visual Identity — 2.5D Pixel-Art Restyle

**Branch**: `feat/004-visual-identity` | **Date**: 2026-10-01 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/004-visual-identity/spec.md`

## Summary

Replace every Feature 003 placeholder with a coherent 2.5D pixel-art/cartoon look without changing gameplay, content or saves. Phase 0 moves the project to URP 17.6.0 with the 2D Renderer, with parity gates against Built-in (research R01). A style spike in the running game, with an owner decision, fixes projection, art density, state-cue variant and lighting before the Art Bible v1.0 freeze (R03, R06). Production follows a per-family route chosen by agents: Blender for machines, slots and floor; ComfyUI for resources, items, illustrative icons and backgrounds; scripted drawing for state cues and glyphs. Every route ends in one deterministic pixel pass with automated checks and provenance (R07–R09). Unity integration adds an integer device-pixel camera scale (R02), row-ordered sorting with an overlay layer for cues (R04), a flipbook animation clock with reduced motion (R05, R11), and an atlas-based import (R10). Performance is measured B1 → B2 → B3 (R12).

## Technical Context

**Language/Version**: C# for Unity `6000.6.0f1` (.NET Standard 2.1 profile); Python (version pinned in `tools/art-pipeline/pyproject.toml`) for offline art tooling; Blender Python for render scripts.

**Primary Dependencies**:
- New: `com.unity.render-pipelines.universal` `17.6.0` (built into the editor; brings `render-pipelines.core`, `shadergraph`, `universal-config` 17.6.0).
- Offline tooling only, not shipped: Pillow (pinned via `uv.lock`), Blender 5.2.1, ComfyUI v0.38.0 with Z-Image-Turbo (installed) and the pixel-art LoRA (download pending owner approval).
- Existing: Input System 1.20.0, UI Toolkit, Unity Test Framework 1.8.0.

**Storage**: No save or content change. New local file `persistentDataPath/IdleFactory/Preferences/display-v1.json` (R11). Art sources in `art-source/f004/`, final sprites in `Assets/IdleFactory/Art/F004/`.

**Testing**: Edit Mode (camera scale model and screen-class table, catalog fallback and cue mapping, preferences reader, render-pipeline configuration, asset contract and importer settings); Play Mode (re-baselined zoom and slot-grid tests, crispness, overlay/overhang, reduced motion, fallback); `[Category("StandalonePlayer")]` smoke on the restyled scene; Android emulator journey and FPS; pipeline checks (`tools/art-pipeline checks`) as CI-runnable Python; independent agent review of grayscale/CVD sheets; owner timed trial.

**Target Platform**: Windows editor and Standalone player (required evidence); Android ARM64 IL2CPP Development player on the API 36 x86_64 AVD (emulator-only evidence, GLES3 build path). iOS deferred (Issue #39).

**Project Type**: Single Unity 2D game repository plus an offline art-tooling folder.

**Performance Goals**: Emulator ≥ 60 FPS average on W003 and W004-V, p95 ≤ 18 ms, 0 intervals ≥ 50 ms; 0 steady-state GC allocations per frame; world-art texture memory ≤ 8 MB; phase-0 regression ≤ +10 % guest CPU and ≤ +10 MB APK vs B1 (provisional, frozen after B1/B2, R12).

**Constraints**: Core untouched; no gameplay/content/save change; F003 keys never renamed; whole-number scales only; generated art only with commercial-use licenses and complete provenance; no GUI pixel editing in any workflow; single Unity and emulator lease; GPU shared by Blender, ComfyUI and the emulator.

**Scale/Scope**: 4 machines × (6-frame running loop + still variants per state), 4 slot plates, floor tiles, 4 Iron resources, 3 item tiers, ~10 state-cue icons and markers, ~55 UI icons; 12 slots per factory.

## Constitution Check

*GATE: passed before Phase 0 research and rechecked after Phase 1 design.*

| Principle | Evidence | Result |
|---|---|---|
| I. Correct observable behavior | Every FR maps to a contract rule and a named test (traceability below). The density/readability conflict found in research (R03) is made an executable gate, not guessed away. | PASS |
| II. Domain architecture, explicit state | Presentation-only; ownership table in data-model; one writer each for camera, catalog, cue mapping and preferences; Core never reads presentation data. | PASS |
| III. Deterministic, data-driven | Scale model is a pure function; cue mapping and animation are data; the pixel pass is deterministic; no gameplay values involved. | PASS |
| IV. Tests protect behavior | Edit/Play/Player/emulator coverage per contract; re-baselined tests listed explicitly (R15); pipeline checks. | PASS |
| V. Simplicity | Custom camera model reuses the existing controller instead of a second camera system; flipbook instead of Animator; no new runtime package beyond URP; tooling kept outside `Assets/`. | PASS (see Complexity Tracking) |
| VI. Measured performance | Workloads W003/W004-V, metrics, B1→B2→B3 order, provisional budgets frozen after measurement. | PASS |
| VII. Human authority | Owner decisions recorded (spec Clarifications); style spike ends in an owner decision; LoRA download needs approval. | PASS |
| VIII. Traceability | FR/SC → design → contract → evidence table below; tasks and analysis follow. | PASS |
| IX. Independent review | Review waves and role disposition below; producers never review their own assets; independent agent reviewer for sheets. | PASS |
| X. Safe delivery, durable knowledge | Phase 0 is one revertible commit; preferences file fails safe; provenance per asset; wiki ingest of Art Bible and rendering decisions (FR-028). | PASS |

Post-design recheck: no violations. Two Complexity Tracking entries.

## Owner decisions

| ID | Decision (2026-10-01) | Source |
|---|---|---|
| OD-1 | 2.5D pixel-art/cartoon style; F003-PAB v0.1 is a placeholder | Spec Context |
| OD-2 | URP 2D Renderer as phase 0 of this feature, after F003's scene/settings work | Spec Context |
| OD-3 | T110 resolution-agnostic catalog (merged, #253) | Spec Context |
| OD-4 | Whole-number scales only, unused space is margin | Clarification Q1 |
| OD-5 | Generated art may ship; tool per asset family chosen by agents; owner does not produce art | Clarification Q2 |
| OD-6 | Reduced motion: OS default with in-game override in a separate file | Clarification |
| OD-7 | Independent agent reviewer for sheets; owner for timed trial and acceptance | Clarification |

**Pending owner decisions** (raised at the right time, not now): the style-spike outcome (projection, `D`, cue variant, lighting; FR-008); the pixel-art LoRA download (R14).

## Design

### Components

| Component | Path | Change |
|---|---|---|
| Rendering foundation | `Assets/IdleFactory/Rendering/`, `ProjectSettings/{Graphics,Quality}Settings.asset`, scene camera | New (phase 0) |
| Camera scale model | `Assets/IdleFactory/Unity/Presentation/CameraScaleModel.cs` (pure), `FactoryCameraController.cs` | New model; controller delegates size/position/snap |
| Catalog | `Assets/IdleFactory/Unity/Presentation/PresentationSpriteCatalog.cs` (from T110) | Fallback key, additive `F004-` keys |
| State-cue mapping | `Assets/IdleFactory/Unity/Presentation/StateCueSet.cs` (asset) and `SlotGridView.cs` | New data; view consumes it |
| Animation clock | `Assets/IdleFactory/Unity/Presentation/PresentationAnimationClock.cs` | New |
| Display preferences | `Assets/IdleFactory/Unity/Persistence/DisplayPreferencesStore.cs`, OS signal in `Unity/Bootstrap/ApplicationLifecycleAdapter.cs` | New store; adapter extension |
| Importer | `Assets/IdleFactory/Editor/Art/F004SpriteImportPostprocessor.cs` | New (editor only) |
| Art tooling | `tools/art-pipeline/` (`pixel_pass`, `checks`, `cues`, `blender/`, `comfy/`, `palette/`) | New, offline |
| Art sources | `art-source/f004/` | New |
| Art Bible, briefs, integration record | `docs/art/F004-art-bible-v1.md`, `docs/art/F004-asset-briefs-v1.md`, `docs/art/F004-technical-art-integration.md` | New |

### Data flow

Simulation → existing allocation-free machine presentation states → `SlotGridView` → state-cue set → catalog keys → sprites/frames; animation clock → frame index (frozen by effective reduced motion); camera scale model ← screen pixels, `pxPerPoint`, unobstructed rect, Art Bible policy. Nothing flows back into Core.

### Art and technical art

Brief → route production → pixel pass and checks → independent Art Director review → Technical Art import via manifest and catalog → in-game validation ([art-pipeline](./contracts/art-pipeline.md)). Feature 003 placeholders remain as fallbacks until acceptance, then are archived by explicit decision.

### Performance

See research R12: B1 (Built-in, W003) → B2 (URP, same art, same session) → B3 (new art, W003 + W004-V). Budgets are provisional until `reviews/performance-budgets.md` freezes them after B2. A miss triggers a Performance Engineer review with options; no readability reduction without an owner decision.

### Test strategy

| Level | Scope | Suites (new or re-baselined) |
|---|---|---|
| Edit Mode | Pipeline configuration; scale model and screen-class table; catalog fallback, additive keys; cue mapping; preferences reader; importer settings and asset contract | `RenderPipelineConfigurationTests`, `CameraScaleModelTests`, `ScreenClassScaleTableTests`, `PresentationCatalogFallbackTests`, `StateCueSetTests`, `DisplayPreferencesStoreTests`, `F004AssetContractTests` |
| Play Mode | Crispness; overlay/overhang; tap on overhang; reduced motion; fallback; re-baselined zoom and slot grid | `PixelCrispnessTests`, `StateCueOverlayTests`, `ReducedMotionTests`, `ArtFallbackTests`, `ZoomReadabilityTests`*, `SlotGridTests`* |
| StandalonePlayer | Restyled journey smoke and allocation check | `RestyledSceneSmokePlayerTests` |
| Android emulator | Journey, crispness screenshots, W003/W004-V FPS | `scripts/android-emulator-journey.ps1` steps |
| Pipeline (Python) | Palette, canvas, pivot, alpha, outline, frames, IoU, contrast, flash, sheet generation, reproducibility | `tools/art-pipeline` checks + unit tests |
| Human/agent | Independent sheet naming (agent); timed trial and acceptance (owner); Art Director and UI/UX reviews | `reviews/readability-*.md`, `reviews/style-spike.md` |

\* re-baselined, each change listed in the task evidence (R15).

### Requirement traceability

| Requirement | Design / evidence |
|---|---|
| FR-001, FR-002, SC-006 | No Core/content/save paths in scope; catalog additive-key rule; unchanged suites |
| FR-003, SC-003 | Briefs cover the full inventory; `F004AssetContractTests` |
| FR-004–FR-006, US7, SC-008 | [render-foundation](./contracts/render-foundation.md); R01, R12, R13 |
| FR-007, FR-008, SC-009 | Style spike, `reviews/style-spike.md`; R03, R06 |
| FR-009 | `docs/art/F004-art-bible-v1.md` with explicit supersession table |
| FR-010, FR-013–FR-015, US1, US2, SC-001, SC-002 | [presentation-and-state-cues](./contracts/presentation-and-state-cues.md); pipeline IoU/contrast checks; sheets and timed trial |
| FR-011, FR-012, FR-016–FR-018, US3, SC-005 | [camera-scale](./contracts/camera-scale.md); R02, R03, R15 |
| FR-019, FR-020, US4 | Animation clock and preferences store; R05, R11 |
| FR-021, FR-021a, FR-022, FR-023, US6, SC-004 | [art-pipeline](./contracts/art-pipeline.md); R07–R10 |
| FR-024 | Catalog fallback; `ArtFallbackTests` |
| FR-025–FR-027, SC-007 | R12; performance reviews |
| FR-028 | Wiki ingest after plan and after implementation |
| US5 | UI icon briefs and atlas; UI/UX review |

### Review waves and role disposition

- **Plan review wave** (`max_parallel: 3`): (1) Software Architect, Test/QA, Technical Artist; (2) Art Director, UI/UX Developer + independent UI/UX Reviewer, Performance Engineer; (3) Game Designer/PO on acceptance, DevOps on phase 0 and tooling, Security/Privacy on the preferences file. Recorded in `reviews/plan-reviews.md`.
- **Activated roles**: Game Designer/PO, Architect, Test/QA, UI/UX, Art Director, Asset Artist, **3D and Render Artist** (activated: Blender route for machines), Technical Artist, Performance, DevOps (pipeline and build change), Security/Privacy (local file only).
- **Omitted**: Game Systems/Economy (no economy change, FR-001); Online Services (no remote boundary); Sound Designer and Composer (out of scope).

### Proposed work breakdown (input to `$speckit-tasks`)

[U] = `unity` lease (plus `emulator` when the target may be Android); [E] = emulator lease; ∥ = disjoint paths.

1. **Pre-phase-0 baselines** [U][E]: B1 (if not recorded by F003), screenshot baselines at R13 resolutions.
2. **Phase 0 URP switch** [U][E]: change set, configuration test, gates, B2, frozen budgets (after F003 scene/settings Issues and 1).
3. ∥ **Art tooling foundation** (no Unity): `tools/art-pipeline` uv project, pixel pass, checks, cues, palette v0, Blender rig template and render script, Comfy workflows; Python unit tests.
4. ∥ **Camera scale model** (pure C#, Edit Mode): `CameraScaleModel`, screen-class table test.
5. **Style spike** (after 2, 3, 4; LoRA approval for the generative route): spike assets per candidate, in-game evidence [U][E], sheets, agent naming, owner timed trial, owner decision record.
6. **Art Bible v1.0 and briefs** (after 5): Art Director author, independent review, owner sign-off.
7. **Presentation integration** [U] (after 2, 4, 6): camera controller integration and re-baselined zoom/slot tests; cue mapping and overlay layer; animation clock; preferences store and UI control; catalog fallback; importer postprocessor.
8. **Production per family** ∥ (after 6): machines (Blender), slots and floor (Blender), resources and items (Comfy), cues and markers (scripted), UI icons (scripted/Comfy) — each followed by independent Art review.
9. **Technical Art import** [U] per approved family (after 7 and 8).
10. **Verification** [U][E]: full suites, Windows player, emulator journey, B3, readability sheets and timed trial, UI/UX review, Art and Technical Art reviews, Performance review.
11. **Knowledge**: wiki ingest (Art Bible, rendering foundation, zoom rule), learn review, placeholder archive decision.

## Project Structure

### Documentation (this feature)

```text
specs/004-visual-identity/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/ (render-foundation.md, camera-scale.md, presentation-and-state-cues.md, art-pipeline.md)
├── checklists/requirements.md
├── reviews/ (discovery.md; later plan-reviews.md, style-spike.md, performance-*.md, readability-*.md, ui-ux-review.md, art-direction-review.md, final-acceptance.md)
└── tasks.md            # $speckit-tasks
```

### Source code (repository root)

```text
Packages/manifest.json, Packages/packages-lock.json            # + URP 17.6.0 (phase 0)
ProjectSettings/GraphicsSettings.asset, QualitySettings.asset  # pipeline assignment (phase 0)
Assets/IdleFactory/Rendering/                                  # URP pipeline + 2D Renderer assets
Assets/IdleFactory/Unity/Presentation/{CameraScaleModel,FactoryCameraController,PresentationSpriteCatalog,StateCueSet,SlotGridView,PresentationAnimationClock}.cs
Assets/IdleFactory/Unity/Persistence/DisplayPreferencesStore.cs
Assets/IdleFactory/Unity/Bootstrap/ApplicationLifecycleAdapter.cs  # OS reduced-motion signal
Assets/IdleFactory/Editor/Art/F004SpriteImportPostprocessor.cs
Assets/IdleFactory/Art/F004/                                   # final PNGs, atlases, import-manifest.json
Assets/IdleFactory/Scenes/CoreGameFoundation.unity             # camera component (phase 0), Unity tooling only
Assets/IdleFactory/Tests/{EditMode,PlayMode}/                  # suites listed above
tools/art-pipeline/                                            # offline tooling (uv, Blender, Comfy, palette)
art-source/f004/                                               # sources, raw outputs, provenance
docs/art/F004-art-bible-v1.md, F004-asset-briefs-v1.md, F004-technical-art-integration.md
scripts/android-emulator-journey.ps1                           # + F004 steps
```

**Structure Decision**: Keep the existing assembly layout; all runtime changes stay in `IdleFactory.Unity` presentation, persistence and editor code. Offline art tooling and sources live outside `Assets/` so Unity never imports them.

## Complexity Tracking

| Addition | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| URP package and pipeline assets | Owner decision; Built-in deprecated; enables 2D lighting for the spike | Staying on Built-in contradicts OD-2 |
| Offline Python tooling (`uv`, Pillow) beside the Unity project | Deterministic pixel pass, checks and scripted drawing that agents can run and reproduce | GUI editors need a human; C# editor tooling would tie art production to the single Unity lease |

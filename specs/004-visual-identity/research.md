# Research: Visual Identity — 2.5D Pixel-Art Restyle (Feature 004)

**Date**: 2026-10-01
**Inputs**: [spec.md](./spec.md), [`Spec-004-Draft.md`](../../Spec-004-Draft.md), the discovery and feasibility reviews of 2026-10-01 (Game Designer/PO, Art Director, UI/UX, Software Architect + DevOps, Technical Artist, Performance Engineer; summarized in [reviews/discovery.md](./reviews/discovery.md)), `docs/art/F003-provisional-art-bible-v0.1.md`, the Unity 6000.6.0f1 installation, and the wiki pages named in the spec.

Each entry: **Decision**, **Rationale**, **Alternatives considered**. "Verified" marks facts checked against the repository or the local Unity installation on 2026-10-01.

## R01 — Rendering foundation (phase 0)

**Decision**: Adopt the Universal Render Pipeline **17.6.0** (built into Unity 6000.6.0f1; dependencies `render-pipelines.core` 17.6.0, `shadergraph` 17.6.0, `universal-config` 17.6.0 — verified in `Editor/Data/Resources/PackageManager/BuiltInPackages`) with one URP pipeline asset and one **2D Renderer** data asset, assigned in `GraphicsSettings` and in **every** quality level. Parity settings for phase 0:
- default sprite material type **Unlit** (code-created `SpriteRenderer`s otherwise get Sprite-Lit and change the look);
- color space stays **Gamma** (`m_ActiveColorSpace: 0`, verified) so palette values stay exact;
- HDR, post-processing, depth and opaque textures off; no 2D lights in phase 0;
- the camera gets its URP additional camera data through Unity tooling (scene edit, unity lease).

Phase 0 is one self-contained change (manifest, lock file, pipeline assets, `GraphicsSettings`, `QualitySettings`, scene camera component) and is reverted as one commit. An Edit Mode test asserts that the pipeline asset is assigned in `GraphicsSettings` and in every quality level, because `scripts/check-settings-noise.ps1` inspects only `ProjectSettings.asset` and `EditorSettings.asset` (verified) and cannot tell a deliberate `QualitySettings` change from re-serialization noise.

**Rationale**: Owner decision (spec Context). Built-in RP is deprecated from Unity 6.5 (Unity render pipeline strategy 2026). The cost is lowest now: 0 custom shaders, 0 materials, 1 scene (verified). Unlit + Gamma isolates the pipeline change so B2 can be compared with B1.

**Alternatives considered**: Stay on Built-in with the legacy 2D Pixel Perfect package (rejected: deprecated pipeline). A separate feature for the migration (rejected by the owner). Linear color space (rejected: shifts palette values and every screenshot baseline).

## R02 — Crisp pixels: integer device-pixel camera

**Decision**: Extend `FactoryCameraController` with an **integer scale model** instead of using URP's `PixelPerfectCamera` component (present in URP 17.6.0 `Runtime/2D`, verified).
- `scale n` = device pixels per art pixel, `n ∈ {1, 2, 3, …}`; `orthographicSize = cameraPixelHeight / (2 · n · D)`, where `D` is the art density in art pixels per cell (1 cell = 1 world unit, unchanged).
- The camera position snaps to `1 / (n · D)` world units after every pan step and at rest.
- Pinch and wheel move a continuous preview scale; on gesture end the scale snaps to the nearest allowed `n` (spec FR-017). Zoom buttons step `n ± 1`.
- `cameraPixelHeight` is the camera's pixel rect in **physical pixels** (never UI points), so fractional device pixel ratios affect only the physical cell size, never pixel integrality.
- Fit-all uses the HUD-aware unobstructed rectangle that already exists (wiki: ui-toolkit-hud-layout-pitfalls) and selects the **largest** `n` at which all 12 slots plus a half-cell margin fit; unused space stays margin (spec FR-011, owner decision Q1 = A).

**Rationale**: The `PixelPerfectCamera` component drives `orthographicSize` from a reference resolution, conflicting with the controller's pan and zoom math (`PanByScreenDelta`, `ZoomAtWorldPoint`, verified) and with the HUD-aware framing. Its render-texture upscale letterboxes or crops on phone aspect ratios and adds a full-screen blit (Performance review). A controller-owned integer model keeps one writer of camera state.

**Alternatives considered**: `PixelPerfectCamera` with upscale render texture (rejected above; kept as a measured fallback only if the custom model shows artifacts on a device). Point-filtered non-integer scaling (rejected by the owner, Q1 = A).

## R03 — Art density and the readability floor (open, resolved by the style spike)

**Finding**: The Feature 003 readability floor is expressed in **UI panel points** (Z24: a cell is at least 24 pt; F003-PAB §8), while integer scaling is expressed in **device pixels**. With `ScaleWithScreenSize` 1280×720, match 0.5, the reference phone runs at about 1.68 px/pt (wiki, verified page). Worst case, phone landscape: fit-all is 32 pt per cell (F003-PAB §8.2) ≈ 53.8 px per cell at 1.68 px/pt.

| Density `D` (art px per cell) | Largest `n` at fit-all | Cell size at fit-all | Meets 24 pt floor? |
|---|---|---|---|
| 32 | ⌊53.8 / 32⌋ = 1 | 32 px ≈ 19 pt | **No** |
| 24 | ⌊53.8 / 24⌋ = 2 | 48 px ≈ 28.6 pt | Yes |
| 16 | ⌊53.8 / 16⌋ = 3 | 48 px ≈ 28.6 pt | Yes |

**Decision**: `D` is **not** fixed by the plan. The style spike (spec FR-007/FR-008) must pick `D` and the readability floor such that, for every supported screen class at its **physical** resolution and pixel-per-point ratio, `n_fit ≥ n_min`, where `n_min` is the smallest scale that meets the floor. The spike evaluates `D ∈ {16, 24, 32}`; 24 is the leading candidate (machines 48×72 art px at 2×3 cells). The computation runs as an Edit Mode test over a table of the supported screen classes and device ratios, so a later HUD or density change that breaks the rule fails a test (spec FR-011, Edge Cases).

**Rationale**: The draft's 32 art px per cell does not satisfy the owner's Q1 = A on low pixel-per-point phones in landscape. Making the rule executable prevents a silent regression.

**Alternatives considered**: Fix `D = 32` and accept 19 pt cells (rejected: violates the readability floor). Scale state cues independently of the world (kept: cues and markers are separate overlays and may use their own integer factor, but machine identification still needs the floor).

## R04 — Sorting and overlap of tall machines

**Decision**: No transparency-sort axis. `sortingOrder` is derived from the slot row (front rows draw later); each machine is one Sorting Group. State cues, markers and the selection frame live on a separate, higher sorting layer above all machine art. Hit testing stays footprint-only (`GridWorldMapper`, unchanged). The machine canvas may overhang upward by at most 1 cell, which fits the 1-cell gap between slot rows (slot pitch 160 px vs 128 px slot in F003 units, verified in F003-PAB §2).

**Rationale**: The slot grid is fixed (at most 12 slots), so row order is exact. URP's 2D Renderer ignores `GraphicsSettings.m_TransparencySortAxis` (currently `0,0,1`, verified); a custom axis would move into renderer data without benefit.

**Alternatives considered**: Custom Y sort axis in the 2D Renderer data (rejected: unnecessary for a fixed grid).

## R05 — Animation

**Decision**: A presentation-layer **flipbook**: catalog entries carry ordered frames and a frames-per-second value (T110 contract). One shared animation clock in the presentation layer computes `frame = ⌊t · fps⌋ mod frameCount` for every animated machine in a single update; no `Animator`, no per-machine `Update`. Running loops default to 6 frames at 8 fps; idle, paused, inactive and problem states use a single still frame. Reduced motion forces frame 0. The flash limit (≤ 3 Hz) is checked by the asset pipeline for any frame-to-frame luminance alternation.

**Rationale**: Zero allocations per frame, trivially freezable, data-driven by the catalog (Technical Artist and Performance reviews).

**Alternatives considered**: `Animator` with clips (rejected: allocations, per-instance state, heavier for 12 sprites).

## R06 — State presentation variants for the spike

**Decision**: The presentation maps each machine state to a **cue triple** `(bodyVariant, cueIcon, labelPolicy)` defined in content-independent presentation data (see data-model). Two spike variants:
- **(a) body + exception icons**: running = animated loop, lights on; paused / inactive = still, desaturated body variant plus a small pause / power glyph; problem states (missing input, output full, waiting) = still body with a warning-light variant plus a per-state pixel icon bubble above the machine; running shows no cue.
- **(b) pixel badges**: the F003 badge model redrawn as pixel art, one badge per machine.
Both keep a distinct shape per state, so the grayscale and CVD tests (spec SC-001) apply unchanged. Markers (boosted, Overdrive) remain separate small glyphs in both variants.

**Rationale**: Owner request (status badges look cluttered) and the discovery finding that 4-letter labels are unreadable at the smallest zoom. A data-driven triple lets the owner's choice be applied without code changes.

**Alternatives considered**: Color tint only (rejected: color-only cue, violates spec FR-010).

## R07 — Asset production tooling and repository layout

**Decision**:
- `tools/art-pipeline/` is a `uv` project (`pyproject.toml`, `uv.lock`) pinning Python and Pillow. It contains `pixel_pass` (nearest-neighbor downscale to the art grid, palette quantization without dithering, alpha threshold 0/255, 1-px outline check, trim/pad to canvas and pivot, frame sheets, manifest), `checks` (palette, canvas, pivot, alpha, outline, frame count, silhouette IoU, contrast, flash rate, grayscale/CVD sheet generation) and `cues` (deterministic scripted drawing of state icons, markers and simple UI glyphs).
- `tools/art-pipeline/blender/`: a versioned template `.blend` (orthographic 3/4 camera, top-left key light, toon or flat materials, filter size 0, transparent film, color management **Standard**, Look None) and `render.py`, run as `blender -b <template> --factory-startup --python-exit-code 1 -P render.py -- <asset>`. Blender **5.2.1** pinned (verified in `docs/integrations/mcp-readiness.md`); no add-ons. Blender MCP is used only interactively for modeling; the batch path executes only committed scripts.
- `tools/art-pipeline/comfy/`: committed workflow JSON files; ComfyUI v0.38.0, Z-Image-Turbo (Apache-2.0, installed) and, after owner approval of the download, the pixel-art LoRA `tarn59/pixel_art_style_lora_z_image_turbo` (Apache-2.0).
- Source art lives **outside `Assets/`** in `art-source/f004/<family>/<asset>/` (models, raw renders or generations, provenance JSON). Unity never imports `.blend` files. Only final PNGs and the import manifest go to `Assets/IdleFactory/Art/F004/`.
- Size policy: no Git LFS initially; a source file over 10 MB needs an explicit decision.

**Rationale**: Reproducible, scriptable by agents (owner has no art skills, spec FR-021a), deterministic final pass. Keeping `.blend` out of `Assets/` avoids Unity's Blender importer dependency.

**Alternatives considered**: GUI pixel editors (Pixelorama, Aseprite) as part of the workflow (rejected: no human editor, spec Assumptions). Aseprite importer package (rejected: extra dependency without need).

## R08 — Tool routing per asset family

**Decision**: As in `Spec-004-Draft.md` §5.1a, recorded per brief (spec FR-021a): machines, slot plates and floor tiles → Blender; resources, item tiers, illustrative UI icons, background, concepts → ComfyUI; state cues, markers, selection frame, simple UI glyphs → scripted drawing. The style spike may move a family to another route with a recorded reason.

**Rationale**: Blender wins where frame coherence and shared perspective/light matter; generation wins for small illustrative objects; exact shapes and CVD rules need determinism.

**Alternatives considered**: ComfyUI for everything (rejected: no frame coherence, perspective drift). Blender for everything (rejected: high cost for small icons).

## R09 — Reproducibility definition

**Decision**: "Regenerable within the recorded tolerance" (spec US6) means: rerunning the recorded route with the pinned tool versions produces raw output that, after the deterministic pixel pass, is **pixel-identical** for scripted routes and differs in at most **0.5 % of opaque pixels** for Blender (EEVEE/driver variance) and generative routes with the same seed. Raw renders and generations are committed as intermediates, so the pixel pass alone is always exactly reproducible.

**Rationale**: EEVEE output and GPU diffusion are not byte-stable across drivers (Architect and Technical Artist reviews). Committing intermediates gives an exact fallback.

**Alternatives considered**: Byte-identical regeneration for all routes (rejected: not achievable).

## R10 — Unity import, atlases and catalog

**Decision**: Pixels per unit = `D`, so 1 cell = 1 world unit (unchanged). Point filter, no mipmaps, clamp, **uncompressed RGBA32** for world art, Sprite Atlas (v2) per family with 2 px padding and rotation disabled; UI icons in a separate atlas consumed by UI Toolkit. An `AssetPostprocessor` applies pivots, PPU and import settings from `art-source`-generated `Assets/IdleFactory/Art/F004/import-manifest.json`; an Edit Mode test validates importer settings, catalog resolution and the golden key list. F004 adds keys only additively (spec FR-002); F003 placeholders stay as the fallback set until acceptance (spec FR-024).

**Rationale**: Block compression smears hard pixel edges and palette colors (Technical Artist review); the world set is small (estimate ≈ 1.1 MB at D = 32, less at 24).

**Alternatives considered**: ASTC/ETC2 for world art (rejected), individual sprites without atlas (rejected: breaks batching).

## R11 — Display preferences and reduced motion

**Decision**: `persistentDataPath/IdleFactory/Preferences/display-v1.json` with `{ "version": 1, "reducedMotion": "followSystem" | "on" | "off" }`, read by a strict reader; missing, unreadable or invalid → `followSystem`, one diagnostic without paths, rewritten on the next change (spec FR-020). Written atomically (temp file + replace). OS detection behind one presentation-layer interface: Android reads the system animator duration scale (0 means reduce) through the Android lifecycle adapter; Windows reads the client-area-animation setting; any platform or failure without a signal reports "not reduced". This file is never part of the save and never read by Core.

**Rationale**: Clarification Q (2026-10-01). Separating it from the save keeps spec FR-001.

**Alternatives considered**: `PlayerPrefs` (rejected: opaque storage, harder to validate and test); storing in Save V2 (rejected by the owner).

## R12 — Performance baselines, workloads and budgets

**Decision**:
- **B1**: Feature 003 on Built-in with W003 (committed Save V2 fixtures), emulator (T065 protocol, profiler-free build, same-session 30 FPS control) and Windows player. Taken from Feature 003 evidence if recorded on the same host and protocol; otherwise re-measured before phase 0.
- **B2**: after phase 0, same art, same session as a repeat B1 control.
- **B3**: restyled art, W003 plus **W004-V** (visual worst case: 12 occupied slots, all animating where the state allows, every cue and marker visible, at the smallest allowed scale and at fit-all), built as a Play Mode fixture.
- Metrics: emulator average FPS and p50/p95/p99 present interval, guest CPU, PSS, texture memory, draw calls/batches, GC allocations per frame, APK size, shader variant count, Windows host frame ms p95.
- **Provisional budgets** (frozen in `reviews/performance-budgets.md` after B1/B2, Performance Engineer): ≥ 60 FPS average, p95 ≤ 18 ms, 0 intervals ≥ 50 ms on the emulator; 0 steady-state GC allocations per frame; world-art texture memory ≤ 8 MB; atlas page ≤ 2048²; phase-0 guest CPU regression ≤ +10 % and APK growth ≤ +10 MB against B1.

**Rationale**: Constitution VI; Performance review. Emulator results remain exploratory; the physical-device gap is stated (spec FR-027).

**Alternatives considered**: Numeric budgets fixed now (rejected: no B1/B2 data yet).

## R13 — Phase-0 screenshot parity

**Decision**: Before phase 0, capture baseline frames of the placeholder factory in Play Mode at 640×480 and at the Feature 003 screen classes (portrait and landscape phones, tablet, 1280×720, 1920×1080). After phase 0, compare: at most **0.1 % of pixels** may differ, and no pixel by more than **2/255 per channel**. The tolerance owner is Test/QA; any larger difference blocks phase 0.

**Rationale**: Gamma color space and Unlit sprites should render nearly identically; a tiny tolerance absorbs blending differences.

**Alternatives considered**: Exact equality (rejected: likely false failures); visual inspection only (rejected: not repeatable).

## R14 — External downloads and approvals

**Decision**: The pixel-art LoRA download needs the owner's approval before the spike's generative route runs; the SDXL comparison is deferred unless the spike needs it. Each model's license and training-data note is recorded in its provenance (spec FR-021). Workstation leases apply: Unity and emulator via `scripts/resource-lease.ps1`; the GPU is shared by Blender, ComfyUI and the host-GPU emulator, so generation and render batches never run during performance measurements.

**Rationale**: AGENTS.md download rule; measurement discipline (wiki: android-emulator-profiling).

## R15 — Superseding Feature 003 zoom rules and tests

**Decision**: Feature 003's Z16/Z24/Z32/Z64 table and the F003-PAB readability table are superseded by the new Art Bible's scale model (R02, R03). `ZoomReadabilityTests` and `SlotGridTests` expectations are re-baselined in the same task that introduces the integer scale model, listing each changed expectation in the task evidence (spec FR-012, SC-006).

**Rationale**: Prevent silent deletion of a guarding test.

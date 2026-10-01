# Research: Visual Identity — 2.5D Pixel-Art Restyle (Feature 004)

**Date**: 2026-10-01
**Inputs**: [spec.md](./spec.md), [`Spec-004-Draft.md`](../../Spec-004-Draft.md), the discovery and feasibility reviews of 2026-10-01 (Game Designer/PO, Art Director, UI/UX, Software Architect + DevOps, Technical Artist, Performance Engineer; summarized in [reviews/discovery.md](./reviews/discovery.md)), `docs/art/F003-provisional-art-bible-v0.1.md`, the Unity 6000.6.0f1 installation, and the wiki pages named in the spec.

Each entry: **Decision**, **Rationale**, **Alternatives considered**. "Verified" marks facts checked against the repository or the local Unity installation on 2026-10-01.

## R01 — Rendering foundation (phase 0)

**Decision**: Adopt the Universal Render Pipeline **17.6.0** (built into Unity 6000.6.0f1; dependencies `render-pipelines.core` 17.6.0, `shadergraph` 17.6.0, `universal-config` 17.6.0 — verified in `Editor/Data/Resources/PackageManager/BuiltInPackages`) with one URP pipeline asset and one **2D Renderer** data asset, assigned in `GraphicsSettings` and in **every** quality level. Parity settings for phase 0:
- default sprite material type **Unlit** (code-created `SpriteRenderer`s otherwise get Sprite-Lit and change the look);
- color space stays **Gamma** (`m_ActiveColorSpace: 0`, verified) so palette values stay exact;
- HDR, post-processing, depth and opaque textures off; no 2D lights in phase 0;
- the camera gets its URP additional camera data (renderer index 0, the 2D Renderer) through Unity tooling (scene edit, unity lease);
- the URP global settings asset (`UniversalRenderPipelineGlobalSettings`) is created and registered in `GraphicsSettings`;
- the 2D Renderer's transparency-sort setting is recorded (default; the slot grid sorts by row, R04).

Phase 0 changes **no test expectation** (re-baselining belongs to R15). "Same pass set" means the same test-name and count manifest before and after, not only pass/fail, so a silently lost suite (the known 0-test Play Mode pitfall) fails the gate. A revert dry-run on a scratch branch proves the rollback restores the Built-in state. Phase-0 gates also include an IL2CPP Android GLES3 build under the `unity,emulator` leases, one clean `Library` reimport, a committed `packages-lock.json`, recorded stripped shader-variant counts, and `scripts/check-settings-noise.ps1 -Commit` on the phase-0 commit. The Sprite Atlas v2 packer-mode change (`EditorSettings.asset` `m_SpritePackerMode`, not inspected by the noise checker) is recorded as a decision in its Issue and included in the revert assertions.

Phase 0 is one self-contained change (manifest, lock file, pipeline assets, `GraphicsSettings`, `QualitySettings`, scene camera component) and is reverted as one commit. An Edit Mode test asserts that the pipeline asset is assigned in `GraphicsSettings` and in every quality level, because `scripts/check-settings-noise.ps1` inspects only `ProjectSettings.asset` and `EditorSettings.asset` (verified) and cannot tell a deliberate `QualitySettings` change from re-serialization noise.

**Rationale**: Owner decision (spec Context). Built-in RP is deprecated from Unity 6.5 (Unity render pipeline strategy 2026). The cost is lowest now: 0 custom shaders, 0 materials, 1 scene (verified). Unlit + Gamma isolates the pipeline change so B2 can be compared with B1.

**Alternatives considered**: Stay on Built-in with the legacy 2D Pixel Perfect package (rejected: deprecated pipeline). A separate feature for the migration (rejected by the owner). Linear color space (rejected: shifts palette values and every screenshot baseline).

## R02 — Crisp pixels: integer device-pixel camera

**Decision**: Extend `FactoryCameraController` with an **integer scale model** instead of using URP's `PixelPerfectCamera` component (present in URP 17.6.0 `Runtime/2D`, verified).
- `scale n` = device pixels per art pixel, `n ∈ {1, 2, 3, …}`; `orthographicSize = cameraPixelHeight / (2 · n · D)`, where `D` is the art density in art pixels per cell (1 cell = 1 world unit, unchanged).
- The camera position snaps to `1 / (n · D)` world units after every pan step and at rest.
- Pinch and wheel move a continuous preview scale; on gesture end the scale snaps to the nearest allowed `n` (spec FR-017). Zoom buttons step `n ± 1`.
- `cameraPixelHeight` is the camera's pixel rect in **physical pixels** (never UI points), so fractional device pixel ratios affect only the physical cell size, never pixel integrality.
- Fit-all uses the HUD-aware unobstructed rectangle that already exists (wiki: ui-toolkit-hud-layout-pitfalls) and selects the **largest** `n` at which all 12 slots plus a half-cell margin fit; unused space stays margin and the factory is centered in the rectangle (spec FR-011, owner decision Q1 = A).
- The allowed set is `[n_min, maxScale]`. `pxPerPoint` comes from the HUD panel's `scaledPixelsPerPoint`, provided by `FactoryHudDocument` together with the rectangle. For an odd camera pixel height or width, the camera position is offset by half an art pixel so the pixel grid stays aligned.
- **Runtime degrade**: arbitrary desktop windows and continuous `pxPerPoint` make `n_fit < n_min` reachable at runtime even though every supported screen class passes the table test. Then the camera uses the largest `n >= 1` that fits, ignores the floor, and logs `presentation.scale.belowFloor` once per layout; it never uses a fractional scale.
- Feel: snaps are instant (no tween), so reduced motion needs no special case; the world point under the gesture anchor is held during a snap; one mouse-wheel notch is one level; zoom buttons are disabled with a visible reason at the ends of the range and hidden when the range has one level. A live window resize re-fits only when the current `n` no longer fits.
- Bottom sheet: when a sheet opens and the selected slot does not fit the visible band, the camera first pans the slot into the band (limits relaxed by the band), then steps down to the largest `n >= n_min` at which it fits; on sheet close the prior `n` and position return unless the player zoomed or panned meanwhile.

**Rationale**: The `PixelPerfectCamera` component drives `orthographicSize` from a reference resolution, conflicting with the controller's pan and zoom math (`PanByScreenDelta`, `ZoomAtWorldPoint`, verified) and with the HUD-aware framing. Its render-texture upscale letterboxes or crops on phone aspect ratios and adds a full-screen blit (Performance review). A controller-owned integer model keeps one writer of camera state.

**Alternatives considered**: `PixelPerfectCamera` with upscale render texture (rejected above; kept as a measured fallback only if the custom model shows artifacts on a device). Point-filtered non-integer scaling (rejected by the owner, Q1 = A).

## R03 — Art density and the readability floor (open, resolved by the style spike)

**Finding**: The Feature 003 readability floor is expressed in **UI panel points** (Z24: a cell is at least 24 pt; F003-PAB §8), while integer scaling is expressed in **device pixels**. With `ScaleWithScreenSize` 1280×720, match 0.5, the reference phone runs at about 1.68 px/pt (wiki, verified page). Worst case, phone landscape: fit-all is 32 pt per cell (F003-PAB §8.2) ≈ 53.8 px per cell at 1.68 px/pt.

| Density `D` (art px per cell) | Largest `n` at fit-all | Cell size at fit-all | Meets 24 pt floor? |
|---|---|---|---|
| 32 | ⌊53.8 / 32⌋ = 1 | 32 px ≈ 19 pt | **No** |
| 24 | ⌊53.8 / 24⌋ = 2 | 48 px ≈ 28.6 pt | Yes |
| 16 | ⌊53.8 / 16⌋ = 3 | 48 px ≈ 28.6 pt | Yes |

**Decision**: `D` is **not** fixed by the plan. The style spike (spec FR-007/FR-008) must pick `D` and the readability floor such that, for every supported screen class at its **physical** resolution and pixel-per-point ratio, `n_fit ≥ n_min`, where `n_min` is the smallest scale that meets the floor. The spike evaluates `D ∈ {16, 24, 32}`; 24 is the leading candidate (machines 48×72 art px at 2×3 cells). At D = 24 the worst phone has the range `[2, 2]` (no zoom-out below fit-all, about 10 % of the height as margin); the table test asserts `n_fit >= n_min`, the level count and the margin (including 360x640 and 390x844 portrait) per screen class. The computation runs as an Edit Mode test over a table of the supported screen classes and device ratios, and a Play Mode variant drives it from the real HUD rectangle with a negative case (an oversized HUD must fail), so a later HUD or density change that breaks the rule fails a test (spec FR-011, Edge Cases).

**Rationale**: The draft's 32 art px per cell does not satisfy the owner's Q1 = A on low pixel-per-point phones in landscape. Making the rule executable prevents a silent regression.

**Alternatives considered**: Fix `D = 32` and accept 19 pt cells (rejected: violates the readability floor). Scale state cues independently of the world (kept: cues and markers are separate overlays and may use their own integer factor, but machine identification still needs the floor).

## R04 — Sorting and overlap of tall machines

**Decision**: No transparency-sort axis. `sortingOrder` is derived from the slot row (front rows draw later); each machine is one Sorting Group. State cues, markers and the selection frame live on a separate, higher sorting layer above all machine art. Hit testing stays footprint-only (`GridWorldMapper`, unchanged). The gap between slot rows is **0.5 cell** (slot pitch 160 px vs a 128 px slot at 64 px per cell, F003-PAB §2). The machine canvas therefore overhangs upward by at most **0.5 cell** (at D = 24: canvas 48x60 art px for a 2x2-cell footprint), so the overhang stays inside the gap and never covers the slot behind. Overlay elements use fixed anchors relative to the **footprint** (never the sprite bounds): state cue top-right inside the overhang band, marker 1 and marker 2 top-left, selection frame around the footprint. A Play Mode frame with 12 occupied slots and the densest overlay (1 cue + 2 markers + selection) at `n_min` asserts no overlap with the row behind and no clipping against the HUD edge for the top row.

**Rationale**: The slot grid is fixed (at most 12 slots), so row order is exact. URP's 2D Renderer ignores `GraphicsSettings.m_TransparencySortAxis` (currently `0,0,1`, verified); a custom axis would move into renderer data without benefit.

**Alternatives considered**: Custom Y sort axis in the 2D Renderer data (rejected: unnecessary for a fixed grid).

## R05 — Animation

**Decision**: A presentation-layer **flipbook**: catalog entries carry ordered frames and a frames-per-second value (T110 contract). One shared animation clock in the presentation layer computes `frame = ⌊t · fps⌋ mod frameCount`, where `t` is unscaled presentation time in integer milliseconds (never simulation time; paused while the app is suspended; no float precision loss over long sessions); effective reduced motion is resolved by a separate pure function with a matrix test, for every animated machine in a single update; no `Animator`, no per-machine `Update`. Running loops default to 6 frames at 8 fps; idle, paused, inactive and problem states use a single still frame. Reduced motion forces frame 0. The flash limit is measured by the pipeline as the mean relative-luminance change over the opaque area between consecutive frames of the loop at its fps; a change above 0.10 counts as a flash, and at most 3 flashes per second are allowed. Frame 0 of a running loop shows the lights-on state, so a frozen running machine still reads as running.

**Rationale**: Zero allocations per frame, trivially freezable, data-driven by the catalog (Technical Artist and Performance reviews).

**Alternatives considered**: `Animator` with clips (rejected: allocations, per-instance state, heavier for 12 sprites).

## R06 — State presentation variants for the spike

**Decision**: The presentation maps each machine state to a **cue triple** `(bodyVariant, cueIcon, labelPolicy)` defined in content-independent presentation data (see data-model). Two spike variants:
- **(a) body + exception icons**: running = animated loop, lights on; paused / inactive = still, desaturated body variant plus a small pause / power glyph; problem states (missing input, storage full, waiting) = still body with a warning-light variant plus a per-state pixel icon bubble above the machine; running shows no cue.
- **(b) pixel badges**: the F003 badge model redrawn as pixel art, but running machines carry **no** badge (the owner disliked permanent badges; the S3 record says so).
- **Salience rule (both variants)**: problem states (missing input, storage full, waiting) get the warning body variant **and** the cue icon; benign states (paused, inactive) get only the desaturated body and a small glyph. S3 gates on this rule.
Both keep a distinct shape per state, so the grayscale and CVD tests (spec SC-001) apply unchanged. Markers (boosted, Overdrive) remain separate small glyphs in both variants.

**Cue size and labels**: a cue icon is at least 12x12 art px and is drawn with its own integer factor so it is at least 24 pt tall at `n_min`; the body variant (lit vs dim vs warning) must carry the state class without the icon, and the icon names the exact problem. Text labels show only when the label cap height is at least 12 pt at the current scale; the machine panel always names the state (primary text path).

**Rationale**: Owner request (status badges look cluttered) and the discovery finding that 4-letter labels are unreadable at the smallest zoom. A data-driven triple lets the owner's choice be applied without code changes.

**Alternatives considered**: Color tint only (rejected: color-only cue, violates spec FR-010).

## R07 — Asset production tooling and repository layout

**Decision**:
- `tools/art-pipeline/` is a `uv` project (`pyproject.toml`, `uv.lock`) pinning Python and Pillow. It contains `pixel_pass` with a route-specific first step and a fixed order afterwards: (1) Blender renders at final size, or at an integer supersample reduced by mode/box downsampling; generative output gets grid detection and mode reduction (plain nearest-neighbor aliases off-grid diffusion pixels); scripted output is already on the grid; (2) alpha threshold 0/255; (3) palette quantization in CIELAB with a deterministic tie-break and no dithering; (4) outline **generation** (1 px, palette ink); (5) placement on one fixed canvas and pivot per asset, anchored on the footprint and shared by all frames of an animation (never trimmed per frame, which would make loops jitter); (6) frames and manifest, `checks` (palette, canvas, pivot, alpha, outline, frame count, silhouette IoU, contrast, flash rate, grayscale/CVD sheet generation) and `cues` (deterministic scripted drawing of state icons, markers and simple UI glyphs).
- `tools/art-pipeline/blender/`: a versioned template `.blend` (orthographic 3/4 camera, top-left key light, toon or flat materials, filter size 0, transparent film, color management **Standard**, Look None) and `render.py`, run as `blender -b <template> --factory-startup --python-exit-code 1 -P render.py -- <asset>`. Blender **5.2.1** pinned (verified in `docs/integrations/mcp-readiness.md`); no add-ons. Blender MCP is used only interactively for modeling; the batch path executes only committed scripts.
- `tools/art-pipeline/comfy/`: committed workflow JSON files; ComfyUI v0.38.0, Z-Image-Turbo (Apache-2.0, installed) and, after owner approval of the download, the pixel-art LoRA `tarn59/pixel_art_style_lora_z_image_turbo` (Apache-2.0).
- Source art lives **outside `Assets/`** in `art-source/f004/<family>/<asset>/` (models, raw renders or generations, provenance JSON). Unity never imports `.blend` files. Only final PNGs and the import manifest go to `Assets/IdleFactory/Art/F004/`.
- Pins: Python version in `pyproject.toml` (`requires-python`); `uv` version recorded in `docs/integrations/mcp-readiness.md`; rig template versioned by file name (`rig-v<k>.blend`) and hash. Locked-plate digits use scripted glyphs or a bitmap font with a recorded license.
- Size policy: no Git LFS initially; a source file over 10 MB needs an explicit decision.
- Execution: `scripts/test-art-pipeline.ps1` resolves `uv` by absolute path, clears proxy variables for its process, runs `uv run --frozen --offline --project tools/art-pipeline pytest` and `checks`, and reports "tooling unavailable" (not a test failure) when `uv` or the cache is missing. `maqa-config.yml` has only Unity commands, so `scripts/test-unity.ps1` chains the pipeline script when art paths changed, the QA assignment contract names it, and `scripts/check-after-implement-hooks.ps1` verifies a passing pipeline report for the batch.
- Repository size: committed intermediates are only the winning raw outputs; rejected candidates stay in a gitignored `art-source/_rejected/`. Budgets of 25 MB per family and 150 MB total are checked by the pipeline. `.gitignore` gains the archive and rejected paths.

**Rationale**: Reproducible, scriptable by agents (owner has no art skills, spec FR-021a), deterministic final pass. Keeping `.blend` out of `Assets/` avoids Unity's Blender importer dependency.

**Alternatives considered**: GUI pixel editors (Pixelorama, Aseprite) as part of the workflow (rejected: no human editor, spec Assumptions). Aseprite importer package (rejected: extra dependency without need).

## R08 — Tool routing per asset family

**Decision**: As in `Spec-004-Draft.md` §5.1a, recorded per brief (spec FR-021a): machines, slot plates and floor tiles → Blender; resources, item tiers, illustrative UI icons, background, concepts → ComfyUI; state cues, markers, selection frame, simple UI glyphs → scripted drawing. The style spike may move a family to another route with a recorded reason. Against style drift between routes, the Art Bible adds a style-anchor sheet (machines, plates, resources and cues together at 1x), shared outline and shading-ramp rules, and a cross-family cohesion review at 1x before each family is approved. The generative route stays blocked until the owner approves the LoRA download (R14).

**Rationale**: Blender wins where frame coherence and shared perspective/light matter; generation wins for small illustrative objects; exact shapes and CVD rules need determinism.

**Alternatives considered**: ComfyUI for everything (rejected: no frame coherence, perspective drift). Blender for everything (rejected: high cost for small icons).

## R09 — Reproducibility definition

**Decision**: "Regenerable within the recorded tolerance" (spec US6) means: rerunning the recorded route with the pinned tool versions produces raw output that, after the deterministic pixel pass, is **pixel-identical** for scripted routes and differs, **per frame**, in at most **0.5 % of opaque pixels** (symmetric alpha-mask difference plus palette-index mismatches) for Blender and generative routes with the same seed. For determinism the Blender route prefers Workbench, or Cycles on CPU with a fixed seed and no denoiser, over EEVEE; GPU, driver, Blender, ComfyUI, node and torch versions go into provenance. A pipeline test regenerates one asset per route against a golden hash or diff; the first reproducibility check runs in the style spike. Raw renders and generations are committed as intermediates, so the pixel pass alone is always exactly reproducible.

**Rationale**: EEVEE output and GPU diffusion are not byte-stable across drivers (Architect and Technical Artist reviews). Committing intermediates gives an exact fallback.

**Alternatives considered**: Byte-identical regeneration for all routes (rejected: not achievable).

## R10 — Unity import, atlases and catalog

**Decision**: Pixels per unit = `D`, so 1 cell = 1 world unit (unchanged). Point filter, no mipmaps, clamp, **uncompressed RGBA32** for world art, Sprite Atlas (v2) per family with 2 px padding, rotation disabled and a **1024²** page cap. The packed pixels are governed by the **atlas** texture settings, not the PNG importer, so every atlas carries explicit overrides for Default, Standalone and **Android** (Android otherwise defaults to block compression). Enabling Sprite Atlas v2 packing changes `EditorSettings.asset`, which the settings-noise hook watches; it is committed as a recorded decision with `IDLEFACTORY_ALLOW_SETTINGS_NOISE=1`.

UI icons: UI Toolkit scales by a non-integer factor (about 1.68 px/pt), so icons are authored at integer display multiples (16 or 24 art px shown at 24 or 32 pt), point-filtered, referenced as sprites by key through USS or the element's background image, and excluded from the world crispness rule; they are enumerated per surface in the F004 brief, and 200 % text never scales them beyond their bounds.

Fallback: a fallback sprite keeps its own PPU, pivot and world size (an F003 placeholder is 64 PPU, 128x128, bottom-left pivot). It is a failure path only, never shipped; it is exempt from the crispness rule (texel ratio n·D/64 is not integer), and overlays anchor to the footprint so cues stay correct. An `AssetPostprocessor` applies pivots, PPU and import settings from the pipeline-generated `Assets/IdleFactory/Art/F004/import-manifest.json` (schema version, PNG hashes, one PNG per frame), registers the manifest as an import dependency and versions itself, so a manifest edit re-imports and a hand edit fails a test; an Edit Mode test validates importer settings, catalog resolution and the golden key list. F004 adds keys only additively (spec FR-002); F003 placeholders stay as the fallback set until acceptance (spec FR-024).

**Rationale**: Block compression smears hard pixel edges and palette colors (Technical Artist review); the world set is small (estimate ≈ 1.1 MB at D = 32, less at 24).

**Alternatives considered**: ASTC/ETC2 for world art (rejected), individual sprites without atlas (rejected: breaks batching).

## R11 — Display preferences and reduced motion

**Decision**: `persistentDataPath/IdleFactory/Preferences/display-v1.json` with `{ "version": 1, "reducedMotion": "followSystem" | "on" | "off" }`, read by a strict reader; missing, unreadable or invalid → `followSystem`, one diagnostic without paths, rewritten on the next change (spec FR-020). Written atomically (temp file + replace). OS detection behind `IReducedMotionSignal` with platform implementations in their own files (Android: system animator duration scale, 0 means reduce, via JNI with link preservation against IL2CPP stripping; Windows: the client-area-animation setting; other platforms or failure: "not reduced"). `ApplicationLifecycleAdapter` only triggers a re-read on focus return. `DisplayPreferencesStore` lives under `Unity/Persistence/` with a header stating it is not a save; the checkpoint code ignores `Preferences/`.

Reduced motion affects only looping art animation; camera snaps and selection feedback are unchanged. UI semantics: a single-choice radio group "Follow system / On / Off" with a live line such as "Following system: reduced motion on" or "Following system: off"; on a write failure the choice stays applied in memory and an inline "Not saved" note appears (no modal). The host is a new Settings sheet opened by a gear icon in the HUD (owner decision 2026-10-01, plan OD-8); the HUD shell's overlap and clipping tests are re-run on every screen class with the extra icon. This file is never part of the save and never read by Core.

**Rationale**: Clarification Q (2026-10-01). Separating it from the save keeps spec FR-001.

**Alternatives considered**: `PlayerPrefs` (rejected: opaque storage, harder to validate and test); storing in Save V2 (rejected by the owner).

## R12 — Performance baselines, workloads and budgets

**Decision**:
- **B1**: Feature 003 on Built-in with W003 (committed Save V2 fixtures). Feature 003's T122/T124 record too few metrics to be reused, so B1 is **re-measured** from a profiler-free Built-in build whose APK is **archived before phase 0** outside every worktree at `<git-common-dir>/../artifacts/f004/B1-<commit7>-<sha256-8>.apk` (APKs are gitignored). `reviews/baselines.md` records sha256, size, source commit, Unity version, build target and graphics API; the archive is kept until the feature converges, and rebuilding from the recorded commit is the fallback.
- **B2**: after phase 0, same art, measured in one cold-booted session in the order B1 → B2 → B1 (ABA) with a 30 FPS control; the session is valid only if the control reproduces about 30 FPS and the two B1 runs agree within the recorded noise band. Regressions are judged against the paired B1 only.
- **B3**: restyled art, W003 plus **W004-V**: a Play Mode fixture over the W003-S checkpoint with **forced presentation states** (no Core change) so that all 12 slots are occupied, running machines animate and the densest overlay is visible, with camera presets `n_min` and fit-all; its hash is recorded with every run.
- Metrics and sources: emulator average FPS and present-interval histogram (SurfaceFlinger TimeStats; percentiles are bucket lower bounds), guest CPU (`top`, first sample excluded), PSS (`dumpsys meminfo`); draw calls, batches and SetPass from the Windows player or the Frame Debugger on W004-V (device-independent, labelled as such); texture memory as the sum of atlas sizes from the build report plus Profiler texture memory in the Windows player; APK size in bytes against B1; shader variant count from the build report; GC allocations per frame via `GcAllocationCounter` in warmed Edit and Play Mode tests (clock tick, cue refresh, camera snap during pan and zoom, reduced motion on and off) and a 600-frame Windows-player run on W004-V.
- Preflight recorded with every run: leases `unity,emulator,gpu` held, no ComfyUI, Blender or Unity editor process, cold-booted emulator, `adb devices` healthy.
- **Provisional budgets** (frozen in `reviews/performance-budgets.md` after B2, Performance Engineer): emulator ≥ 60 FPS average, no histogram bucket above the 16.7–20 ms bucket for p95, 0 intervals ≥ 50 ms; Windows host frame ms p95 unchanged from W003; 0 steady-state GC allocations per frame; world-art texture memory ≤ 8 MB with atlas pages ≤ 1024²; batch count on W004-V recorded at B2 and held at B3; phase-0 guest CPU regression ≤ max(10 %, 2 percentage points) and APK growth ≤ +10 MB against the paired B1.
- On a miss, options in order: atlas and batching fixes; fewer cue draws; removing Sorting Groups; lower animation fps; fewer overlay elements. Any readability reduction needs an owner decision.

**Rationale**: Constitution VI; Performance review. Emulator results remain exploratory; the physical-device gap is stated (spec FR-027).

**Alternatives considered**: Numeric budgets fixed now (rejected: no B1/B2 data yet).

## R13 — Phase-0 screenshot parity

**Decision**: Before phase 0, capture baseline frames of the placeholder factory in Play Mode at 640×480 and at the Feature 003 screen classes (portrait and landscape phones, tablet, 1280×720, 1920×1080). Method: a deterministic fixture (fixed Save V2 checkpoint, frozen presentation clock, fixed camera), rendering into a RenderTexture of the exact size with MSAA off and reading it back; baselines are committed as PNGs under `Assets/IdleFactory/Tests/PlayMode/Fixtures/Screenshots/F004-B1/` with an Edit Mode check that their importer keeps them readable, uncompressed and out of every atlas (total size recorded); the comparison is a C# test helper. Fractional device ratios are covered by the Edit Mode scale model plus an emulator `adb screencap` check that sampled art-pixel blocks are uniform; the same capture method serves the crispness test (FR-016). After phase 0, compare: at most **0.1 % of pixels** may differ, and no pixel by more than **2/255 per channel**. The tolerance owner is Test/QA; any larger difference blocks phase 0.

**Rationale**: Gamma color space and Unlit sprites should render nearly identically; a tiny tolerance absorbs blending differences.

**Alternatives considered**: Exact equality (rejected: likely false failures); visual inspection only (rejected: not repeatable).

## R14 — External downloads and approvals

**Decision**: The pixel-art LoRA download needs the owner's approval before the spike's generative route runs; the SDXL comparison is deferred unless the spike needs it. Each model's license and training-data note is recorded in its provenance (spec FR-021). Workstation leases apply: Unity and emulator via `scripts/resource-lease.ps1`; a new `gpu` resource is added to the script's known resources, help, `-Status` and AGENTS.md "Shared workstation resources", with a `resource:gpu` MAQA label. The parent acquires it per Issue (owner `issue-<n>`) before SPAWN; interactive Blender MCP or ComfyUI sessions acquire it as a session owner; B1/B2/B3 and emulator FPS runs hold it too. Blender batches run through the headless CLI resolved from `BLENDER_EXE` (default recorded in `docs/integrations/mcp-readiness.md`) with render time recorded per asset. Missing Blender or ComfyUI blocks only art-production Issues, never the pipeline tests on committed intermediates.

**Rationale**: AGENTS.md download rule; measurement discipline (wiki: android-emulator-profiling).

## R15 — Superseding Feature 003 zoom rules and tests

**Decision**: Feature 003's Z16/Z24/Z32/Z64 table and the F003-PAB readability table are superseded by the new Art Bible's scale model (R02, R03). `ZoomReadabilityTests` and `SlotGridTests` expectations are re-baselined in the same task that introduces the integer scale model. Invariants must survive: all 12 slots at fit-all, the selected slot visible above an open sheet, and a minimum cell size expressed through `n_min`; only constants and tables may change, the Z16/Z24/Z32/Z64 constants are removed only in that commit, and the QA report contains the old-to-new assertion diff (spec FR-012, SC-006).

**Rationale**: Prevent silent deletion of a guarding test.

## R16 — Staged style spike and its outcomes

**Decision**: The spike runs in four stages instead of a full candidate matrix (Art Director review AD-1): **S1** projection (3/4 oblique vs isometric 2:1) at D = 24 on the Blender route with the two machines at highest silhouette-confusion risk, the four slot plates and two resources; **S2** density D = 16 vs D = 24 on the winning projection; **S3** cue variant (a) vs (b) on one machine body with scripted cues, independent of projection; **S4** lighting: baked top-left key light with emissive frames (default) vs optional 2D-light accents. Routes are not a decision axis; a route change is a recorded outcome. The palette v0 is a spike input and is tested for budget per group. Spike art is imported through an isolated path `Assets/IdleFactory/Art/F004/Spike/D<k>/` with per-density settings and is deleted or promoted by explicit decision.

Each stage record contains the agents' recommended option with its gate results and a "default if no preference" line (S4 defaults to baked light), plus the per-screen-class zoom-level table, stating plainly where a device has no zoom range (S2 shows this consequence of the whole-number rule to the owner). Outcomes: each stage records gate results (grayscale/CVD naming, cue legibility at `n_min` and fit-all, IoU, palette lock, reproducibility, crispness, fit-all rule) and contact sheets at 1x plus on-device screenshots labelled A/B. The owner picks with a dated reason or rejects; a rejection or an all-fail stage produces a "no decision" record and returns to the Art Director for one bounded new candidate round. No Art Bible freeze and no production asset before a complete record exists (SC-009).

## R17 — Evaluation method for SC-001 and SC-002

**Decision**: SC-001 sheets are generated from **in-game captures** at `n_min` and fit-all (R13 method), shuffled with a recorded seed; the answer key stays outside the reviewer's context, and the reviewer is an independent agent session (not the producer, brief author or spike Art Director) whose transcript reference is recorded in `reviews/readability-*.md`. SC-002 uses a script that builds 10 seeded screenshots, each with exactly one machine in a problem state (missing input, storage full and waiting balanced across trials) plus one or two benign distractors (paused or inactive), and an answer key; shows them in random order, and logs the owner's per-trial time; the result reports 9 of 10 with a per-state breakdown. Both are human or agent gates, reported separately from automated suites.

## R18 — Review independence and owner acceptance

**Decision**: The Art Director who authors briefs, the spike or the Art Bible never approves them: a second Art Director session with fresh context reviews spike gates and the Art Bible; UI/UX Reviewer checks cue legibility; Game Designer/PO checks readability. Provenance records the reviewer session identity and date. Owner acceptance per family is captured in `reviews/final-acceptance.md` with before/after sheets. The Art Bible uses semver: **major** for projection, density, palette changes (removal, hue change, any growth beyond the cap) or a cap change; **minor** for additive colors inside the cap, new families or states; **patch** for wording. It contains a section-level supersession table for F003-PAB (superseded, kept, amended), carrying over the state model and brief format and superseding §8 zoom tiers.

Create the specification for **Feature 004: Visual Identity — 2.5D Pixel-Art / Cartoon Art Direction and Asset Pipeline** for the game **Mobile Idle Factory**.

**Status**: Idea draft, recorded 2026-10-01 from an owner decision. It is the input for a later `/speckit-specify` run. It is not a specification, not an approved Art Bible, and not an asset brief. All numbers below are proposals to be validated in the style spike (section 6).

Feature 001 (Core Game Foundation), Feature 002 (JSON content authoring) and Feature 003 (First Iron Factory and Prestige Loop) define the gameplay. Feature 004 changes how the game **looks**, not how it plays. Simulation, economy, save data and content rules must remain unchanged, and simulation must stay independent from presentation (Core-Idea, Specify-Draft "Visual Representation").

---

# 1. Owner decision

- The game's visual style is **2.5D** and **pixel-art / cartoon-like**.
- The provisional Art Bible `docs/art/F003-provisional-art-bible-v0.1.md` (F003-PAB v0.1: top-down orthographic, flat geometric SVG, no generators) was a **development placeholder**. It stays valid for Feature 003 until Feature 004 replaces it.
- The design topic is handled as its own feature (Feature 004) and approached in detail there.

# 2. Goals

1. A distinctive, charming, readable look: chunky cartoon machines with visible volume, clear silhouettes and lively "working" animation.
2. Every gameplay-relevant state from F003 (slot empty/locked/occupied/selected; machine running, waiting, input missing, output full, paused, off; resources; item tiers) stays at least as readable as with the placeholder art, including grayscale and color-vision-deficiency checks.
3. A **reproducible, versioned asset pipeline**: any asset can be regenerated from repository sources, a recorded tool/model version and a brief.
4. Consistent perspective, lighting, proportions and palette across all current and future asset families (regions, machines, resources, UI).
5. Mobile-first: acceptable memory, atlas and draw-call budget on the supported device classes.

# 3. Non-goals

- No gameplay, economy, balancing, save-format or content-rule changes.
- No real-time 3D rendering at runtime. 3D is only a production tool; the game renders 2D sprites.
- No new regions, machines or content. Feature 004 restyles the existing F003 asset set.
- No music. Sound only if explicitly added to scope later.
- No hosted or cloud generation services by default (local tools first, per AGENTS.md).

# 4. Visual direction — concrete proposals

## 4.1 Projection ("2.5D")

Two candidates, to be decided by the style spike:

| | **A. 3/4 top-down (oblique)** — recommended | **B. Isometric 2:1 (dimetric)** |
|---|---|---|
| Look | Top face plus visible front face, like many cozy/management pixel games | Classic diamond grid, strong "factory builder" feel |
| Grid and slots | Keeps the rectangular F003 slot grid and rectangular touch hit areas | Diamond footprints; hit testing and slot layout must be re-derived |
| Screen efficiency (portrait phone) | High; 3x4 slot layout fits as today | Lower; diamonds waste corners in portrait |
| Sorting | Simple: sort by world Y (Unity custom transparency sort axis) | Needs isometric sorting per cell and height |
| Pixel-art rules | Straight horizontal/vertical lines, easy to draw | Requires strict 2:1 pixel lines |
| Effort | Lower | Higher |

Recommendation: **A (3/4 top-down)**, camera pitch around 45–60° in Blender terms, light from top-left, no rotation of machines (F003 machines have no ports).

## 4.2 Pixel density and zoom (key technical constraint)

Pixel art only looks right when one art pixel maps to a **whole number** of screen pixels. The F003 camera currently allows 16 to 129 points per cell (ppc), with Z16/Z24/Z32/Z64 steps and a worst-case fit-all of 32 ppc (phone landscape).

Proposal:

- **32 art pixels per cell** → slot footprint 64x64 art px. Machines may extend upward for their front face and height: canvas 64 px wide x up to 96 px tall, pivot at the footprint's bottom center.
- **Integer zoom steps** 1x/2x/3x/4x = 32/64/96/128 ppc. The camera minimum rises from 16 to 32 ppc. This still satisfies the "all 12 slots at fit-all" rule (worst case exactly 32 ppc) but removes Z16/Z24, so F003's zoom readability rule and `ZoomReadabilityTests` change. This must be an explicit spec decision in Feature 004.
- Alternative: 16 art px per cell (keeps Z16, very retro, only 32x32 art px per slot; probably too few pixels for four distinct machines plus state cues).
- Android devices often have non-integer device-pixel ratios (e.g. 2.625). Evaluate Unity's **Pixel Perfect Camera** (upscale render texture, integer scale with letterbox/crop) against plain point-filtered scaling on the reference devices and the emulator.

## 4.3 Pixel-art / cartoon style rules (candidates for the new Art Bible)

- **Palette**: one fixed project palette of 32–48 colors (start from a published palette with a verified license, or author our own), plus per-region accent ramps later. Every sprite uses only palette colors; the pipeline enforces this.
- **Outlines**: 1 art-px dark outline around silhouettes (colored "selout" allowed inside), no anti-aliasing against transparency.
- **Shading**: 3–4 tone steps per material, light from top-left, no gradients, dithering only as texture, never for state meaning.
- **Cartoon proportions**: exaggerated, chunky shapes; oversized readable features (smokestack, drill, press head, robot arm). Each machine keeps a unique silhouette. The F003 IoU uniqueness rule (≤ 0.70) is carried over and re-measured on the new masks.
- **Animation**: 4–8 frame loops for "running"; one static pose for "idle/off"; state overlays (badge/marker) stay separate sprites so they are not baked into every frame.
- **State readability**: keep the F003 state model (badge glyph + label + color, markers). Re-draw the badges as pixel art; keep 4.5:1 text contrast, 3:1 non-text contrast and the grayscale/CVD pass rule.
- **UI**: pixel-art icons and frames at integer scale. **Body text stays in a clean, accessible font** (a pixel font only for headings/numbers if it passes readability at minimum size). Touch targets and accessibility rules from F003 remain.

# 5. Tooling solution

Principle: **3D for consistency, AI for exploration and small assets, deterministic scripts for the final pixel pass, humans for touch-up and approval.** All tools are local. Current readiness is in `docs/integrations/mcp-readiness.md`.

## 5.1 Pipeline overview

```
Concept & style exploration          ComfyUI (Z-Image-Turbo + pixel-art LoRA)   → reference only
Machines / buildings / slot plates   Blender 3D model → fixed ortho camera rig → low-res toon render
Resources / items / UI icons         ComfyUI generation or direct pixel drawing
                    ↓
Deterministic pixel pass (repo script): nearest-neighbor downscale, palette lock, alpha cleanup,
outline check, frame/sheet packing, automated rule checks
                    ↓
Manual touch-up (Pixelorama / Aseprite) → Art Director review
                    ↓
Unity import (Technical Artist, Unity MCP): point filter, PPU, pivots, Sprite Atlas, animation
```

## 5.2 Blender (primary for machines and buildings)

- **Why**: one camera rig, one light and one material set guarantee the same perspective, proportions and lighting for every machine, state and animation frame. Re-rendering after an Art Bible change is cheap. This is the "proven need" AGENTS.md requires before activating the **3D and Render Artist** role.
- **Setup**: versioned template `.blend` with the orthographic 3/4 camera, top-left key light, toon materials (EEVEE, Shader-to-RGB + Color Ramp with 3–4 steps, or flat vertex colors), outline via Freestyle or an inverted-hull modifier, render at the final art resolution with filter size 0 (no anti-aliasing), transparent background.
- **Automation**: a repository-owned Python render script, run headless (`blender -b <template> -P render.py -- <asset>`), renders all states and animation frames per asset. Blender MCP (Blender 5.2.1, Blender Lab MCP, ready) is used interactively to build and inspect scenes; the committed script remains the reproducible authority.
- **Safety**: Blender MCP executes generated Python; use it only in the isolated 3D workflow and assigned paths.
- Optional add-on for faster setup: "Blender Pixel Kit" (commercial add-on with pixel cameras and toon materials); evaluate license and price first, it is not required.

## 5.3 ComfyUI (exploration, icons, small assets)

- **Ready now**: local ComfyUI v0.38.0 with Z-Image-Turbo (Apache-2.0) via comfy-mcp.
- **Proposed addition**: the LoRA `tarn59/pixel_art_style_lora_z_image_turbo` (Apache-2.0, trigger phrase "Pixel art style.", runs on the installed base model). Download needs owner approval.
- **Comparison candidate**: SDXL base + `nerijs/pixel-art-xl` LoRA (CreativeML OpenRAIL-M: commercial use allowed with use restrictions; recommends 8x nearest-neighbor downscale). Requires an additional SDXL download.
- **Uses**: mood boards and style candidates for the Art Bible; resource, item and UI icon drafts; optional img2img/ControlNet "cartoon pass" over Blender renders (evaluate in the spike).
- **Rule**: diffusion output is never production art as is. It produces "fake pixels" (irregular pixel grid, stray colors). It always goes through the deterministic pixel pass and manual touch-up, and its provenance (model, LoRA, versions, prompt, seed, workflow JSON) is recorded.
- **Legal note**: LoRA training data is often undocumented. Record this risk per model in the provenance file; the owner decides whether AI-derived art may ship or is reference only.

## 5.4 Deterministic pixel pass (new repository script)

- Python with Pillow, run through `uv` (as the Atlas scripts are).
- Steps: nearest-neighbor downscale to the art grid, quantize to the project palette, binarize alpha, check 1-px outline, trim/pad to canvas and pivot, pack frames into sheets, write a JSON manifest.
- **Automated checks** (become QA evidence): palette compliance, canvas size, pivot, silhouette IoU uniqueness, contrast ratios, grayscale/CVD tile sheets for the naming test.

## 5.5 Manual pixel editing

- **Pixelorama** (free, MIT license) as the default editor for touch-up and hand-drawn icons.
- **Aseprite** (paid license) optional if the owner buys it; its CLI can also export sheets.
- Source files (`.pxo` / `.aseprite`) are committed next to the PNG exports.

## 5.6 Unity integration

- Sprite import: point filter, no mipmaps, lossless or carefully checked compression, one consistent PPU (e.g. 32 PPU = 1 cell), pivots from the manifest.
- **Pixel Perfect Camera**: the project uses the Built-in Render Pipeline (no URP package), so evaluate the `com.unity.2d.pixel-perfect` package. Switching to URP 2D (2D lights, its own Pixel Perfect Camera) is a separate, larger architecture decision for the plan phase, not a default.
- Sorting: custom transparency sort axis on world Y for the 3/4 view.
- Sprite Atlas per asset family; sprite-sheet animation for running loops; state overlays as separate sprites.
- Technical Artist works through Unity MCP (ready, verify per session) with the CLI test runner as fallback.

## 5.7 Roles (AGENTS.md)

| Role | Feature 004 work |
|---|---|
| Art Director | Style spike evaluation, Art Bible v1.0, briefs, reviews; ComfyUI experiments |
| 3D and Render Artist | **Activated**: Blender template, models, render script |
| Asset Artist | Icons, resources, touch-up, ComfyUI production within briefs |
| Technical Artist | Pixel-pass script integration, Unity import, atlases, animation, pixel-perfect camera |
| Game Designer / Product Owner | Readability and "feel" acceptance; zoom-rule change |
| UI/UX Developer + independent UI/UX Reviewer | Pixel UI, fonts, touch targets, accessibility across screen classes |
| Performance Engineer | Atlas memory, draw calls, animation cost on the device classes |
| Test/QA | Automated pixel checks, Edit/Play Mode tests, visual regression evidence |

# 6. Proposed phases

1. **Style spike** (small, time-boxed): 1 machine (for example the Smelter) in both projections (4.1 A/B), each through both pipelines (Blender vs ComfyUI + LoRA), plus 2 resource icons and 1 badge. Shown in Unity at 1x–4x on a phone, a tablet and the emulator. Output: side-by-side comparison and owner decision.
2. **Art Bible v1.0** (replaces F003-PAB v0.1): projection, pixel density, palette, outline/shading/animation rules, readability rules, pipeline and provenance rules. Independent Art Director review plus Game Designer / Product Owner check.
3. **Pipeline tooling**: Blender template and render script, pixel-pass script with automated checks, provenance template, Unity import conventions.
4. **Production** of the F003 asset set: 4 machines (all states and running loops), slot plates (empty, locked, occupied, selected), floor/background, Iron resources, three item tiers, badges and markers, UI icons.
5. **Unity integration and migration**: replace the F003 placeholder sprites through the existing presentation sprite catalog, pixel-perfect camera, sorting, zoom-step change.
6. **Verification**: readability sheets (grayscale/CVD), zoom and screen-class checks, performance budget, independent UI/UX and Art reviews.

# 7. Migration decision (proposal)

- Feature 003 **finishes with the approved placeholder art** so gameplay work is not blocked.
- Feature 004 replaces the placeholders completely. Asset IDs and the presentation contract stay stable where possible, so the swap is a presentation-only change.
- The placeholder sources remain in the repository as development fallback until Feature 004 is accepted, then they are removed or archived by explicit decision.

# 8. Candidate acceptance criteria

- Every F003 state is named correctly by two reviewers from shuffled grayscale and CVD sheets at the minimum and fit-all zoom (zero confusions).
- 100 % of production sprites pass the automated palette, canvas, pivot and outline checks.
- Every production asset can be regenerated from committed sources and recorded tool versions; provenance is complete.
- No sub-pixel shimmering or mixed pixel sizes at any allowed zoom step on the reference devices.
- The atlas memory and draw calls of a full 12-slot factory stay within a budget the Performance Engineer sets in the plan phase.
- Gameplay, save and content tests are unchanged and still pass.

# 9. Open decisions for the owner

1. Projection: 3/4 top-down (recommended) or isometric, after the style spike.
2. Pixel density: 32 art px per cell with a camera minimum of 32 ppc (recommended), or 16 art px per cell.
3. Whether AI-generated (ComfyUI) art may ship after touch-up, or is reference/concept only.
4. Download approval for the Z-Image pixel-art LoRA (and optionally SDXL + pixel-art-xl for comparison).
5. Pixelorama only, or buying Aseprite.
6. Stay on the Built-in Render Pipeline with the Pixel Perfect package, or plan a URP 2D migration.
7. Custom palette or a published one (license check needed).

# 10. Risks

- Integer scaling on Android devices with fractional pixel ratios: letterboxing or slight blur.
- Visual inconsistency and unclear training-data provenance of AI output.
- Changing the F003 zoom rule affects existing camera tests.
- Blender Python execution is a code-execution surface; keep it isolated.
- Animation frames multiply atlas memory on mobile.

# 11. Sources

- Owner decision in the Claude Code session of 2026-09-30/2026-10-01.
- `docs/art/F003-provisional-art-bible-v0.1.md` (sections 2, 4, 8), `docs/integrations/mcp-readiness.md`, `AGENTS.md` (roles, tool boundaries), `Packages/manifest.json` (no URP).
- Z-Image-Turbo model card (Apache-2.0): https://huggingface.co/Tongyi-MAI/Z-Image-Turbo
- Pixel-art LoRA for Z-Image-Turbo (Apache-2.0): https://huggingface.co/tarn59/pixel_art_style_lora_z_image_turbo
- Pixel Art XL LoRA (CreativeML OpenRAIL-M): https://huggingface.co/nerijs/pixel-art-xl
- Blender Pixel Kit add-on: https://superhivemarket.com/products/blender-pixel-kit
- Orthographic/isometric sprite rendering in Blender: https://gamefromscratch.com/creating-orthographic-tiles-in-blender/

# Data Model: Visual Identity (Feature 004)

Feature 004 adds **no gameplay, content or save state** (spec FR-001). All entities below are presentation, production or evidence data. Core (`IdleFactory.Core`) is not changed and never reads any of them.

## Ownership overview

| Entity | Owner (sole writer) | Readers | Persistence | Determinism |
|---|---|---|---|---|
| Art density and scale policy | Art Bible v1.x (authored), applied by `FactoryCameraController` | Camera, layout tests | Serialized presentation asset | Pure function of screen pixels, ratio and policy |
| Presentation catalog entry | Technical Artist (catalog asset via Unity tooling) | `FactoryView`, `SlotGridView`, UI | Catalog ScriptableObject | Static |
| State-cue set | Art Director (authored data) | `SlotGridView` | Presentation asset | Pure mapping |
| Asset brief | Art Director | Producers, reviewers | `docs/art/F004-asset-briefs-v1.md` | — |
| Asset provenance record | Producing agent role | Reviewers, pipeline checks | `art-source/f004/**/provenance.json` | Append-only per revision |
| Import manifest | Pipeline (`tools/art-pipeline`) | `AssetPostprocessor`, Edit Mode tests | `Assets/IdleFactory/Art/F004/import-manifest.json` | Generated, deterministic |
| Display preferences | Display-preferences store (presentation) | Animation clock | `persistentDataPath/IdleFactory/Preferences/display-v1.json` | Validated on load |
| Style-spike record | Art Director (recommendation) + owner (decision) | Art Bible, plan | `specs/004-visual-identity/reviews/style-spike.md` | — |
| Performance baseline set | Performance Engineer | Gates | `specs/004-visual-identity/reviews/performance-*.md` | Same-session controls |

## Art density and scale policy

| Field | Type | Rule |
|---|---|---|
| `artPixelsPerCell` (`D`) | int | One of 16, 24, 32; chosen by the style spike (research R03); equals the sprite PPU |
| `readableCellPoints` | int (UI points) | Minimum cell size for machine identification; initial 24 (F003 Z24) |
| `minScale` (`n_min`) | derived | Smallest integer `n` with `n·D / pxPerPoint ≥ readableCellPoints` |
| `maxScale` | int | Largest allowed `n` (initial 6) |
| `fitAllScale` (`n_fit`) | derived | Largest `n ≤ maxScale` at which 12 slots plus a half-cell margin fit the unobstructed rectangle |

**Invariants**: `n_fit ≥ n_min` on every supported screen class (Edit Mode table test); scale is always an integer at rest; camera position is a multiple of `1 / (n·D)` world units. **Transitions**: gesture → preview scale → snap to the nearest allowed `n` on gesture end; rotation or layout change → keep the last valid `n` until the unobstructed rectangle resolves, then re-fit if the old `n` no longer fits.

## Presentation catalog entry (extends the T110 contract)

| Field | Type | Rule |
|---|---|---|
| `key` | string | Existing F003 key or a new additive F004 key; never renamed (spec FR-002) |
| `frames` | ordered list of sprites, ≥ 1 | Frame 0 is the still frame |
| `framesPerSecond` | int > 0, optional | Required when `frames > 1` |
| `worldSize` | (w, h) world units | Validated against the sprite's pixel size ÷ PPU |
| `pivot` | normalized (x, y) | Footprint bottom-center for machines; per brief otherwise |
| `fallbackKey` | string, optional | The F003 placeholder key used when this entry fails to load (spec FR-024) |

## State-cue set

One row per machine state and slot condition; one active variant (a or b) selected by the Art Bible.

| Field | Type | Rule |
|---|---|---|
| `state` | enum | running, paused, waiting, missingInput, outputFull, inactive; slot: empty, locked, occupied, selected |
| `bodyVariantKey` | key | Machine still or loop key for that state (variant a: dimmed or warning variants) |
| `animated` | bool | True only for running |
| `cueIconKey` | key or none | None for running; distinct shape per problem state |
| `markerKeys` | keys | Boosted, Overdrive; independent of the state cue |
| `labelPolicy` | per-scale rule | Text label hidden below a given scale; always available in the machine panel |
| `precedence` | int | When several states apply, the lowest number wins (F003 precedence carried over) |

## Asset brief (extends the F003 brief format)

`assetId`, `type`, `footprint` (cells), `canvas` (art px), `pivot`, `perspective` (`oblique-3-4` or `isometric-2-1`), `states` with `frames` and `fps` per state, `paletteId` and `paletteVersion`, `lightDirection`, `route` (`blender` | `comfy` | `scripted`) with reason, `importSettings`, `artBibleVersion`, `generator` block (tool and model versions, licenses).

## Asset provenance record

`assetId`, `revision`, `route`, `sources` (paths and hashes), `tools` (name, version, model and LoRA with hash and license), `parameters` (render script commit, camera rig version, prompt, negative prompt, seed, sampler, steps, workflow JSON hash), `touchUp` (scripted step list with script commit), `pixelPass` (script commit, palette version), `outputs` (paths and hashes), `license`, `trainingDataNote`, `producedBy` (role), `reviewedBy` (role, independent), `artBibleVersion`, `briefVersion`. A shipped asset requires a complete record (spec SC-004).

## Display preferences

`version` = 1; `reducedMotion` ∈ {`followSystem`, `on`, `off`}. Effective reduced motion = `on`, or `followSystem` and the OS signal says reduce. Invalid or missing file → `followSystem`. Never part of the save; written atomically.

## Style-spike record

Candidates (projection × route × density × state-cue variant), assets produced, device evidence (phone, tablet, emulator), gate results (grayscale/CVD naming, cue legibility at `n_min`, IoU, palette lock, regenerability, crisp rendering, fit-all rule), effort per asset, Art Director recommendation, Game Designer/PO readability check, **owner decision with date**.

## Performance baseline set

Workloads (W003, W004-V), device and build identifiers, fixture hashes, metrics (research R12), B1/B2/B3 results with same-session controls, frozen budgets, verdicts.

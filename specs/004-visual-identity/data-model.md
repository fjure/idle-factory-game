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
| Display preferences | Display-preferences store (presentation) | Animation clock | `<PersistenceRoots.Resolve(persistentDataPath)>/IdleFactory/Preferences/display-v1.json` | Validated on load |
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

## Presentation catalog entry (extends the merged T110 `PresentationSpriteEntry`)

Merged today: `Key`, frame list (`Sprite` = frame 0, `FrameCount`, `GetFrame`) and optional `FramesPerSecond`. New in T428: the per-key requirement (`worldSize`, `pivot`, frame count) moved from the hard-coded F001 table into a registry. `fallbackFrames` was removed by T476 (#670, owner decision 2026-11-05).

| Field | Type | Rule |
|---|---|---|
| `key` | string | Existing F003 key or a new additive F004 key; never renamed (spec FR-002) |
| `frames` | ordered list of sprites, ≥ 1 | Frame 0 is the still frame |
| `framesPerSecond` | int > 0, optional | Required when `frames > 1` |
| `worldSize` | (w, h) world units | Validated against the sprite's pixel size ÷ PPU |
| `pivot` | normalized (x, y) | Footprint bottom-center for machines; per brief otherwise |

## State-cue set

One row per machine state and slot condition; one active variant (a or b) selected by the Art Bible. StateCueSet is code-authored immutable data (StateCueSet.Default, injectable via Configure); the ScriptableObject wrapper is deferred (T429 review 2026-11-03).

| Field | Type | Rule |
|---|---|---|
| `machineState` | enum | As resolved by the Feature 003 presentation model: running, paused, waiting, missing-input, storage-full, inactive |
| `slotCondition` | enum (separate table) | empty, locked (with level), occupied, selected |
| `bodyVariantKey` | key | Machine still or loop key for that state (variant a: dimmed or warning variants) |
| `animated` | bool | True only for running |
| `cueIconKey` | key or none | None for running; distinct shape per problem state |
| `markerKeys` | keys | Boosted, Overdrive; independent of the state cue |
| `labelPolicy` | per-scale rule | Text label hidden below a given scale; always available in the machine panel |

## Asset brief (extends the F003 brief format)

`assetId`, `type`, `footprint` (cells), `canvas` (art px), `pivot`, `ports`, `format`, `shadow`, `overhang`, `cueAnchor`, `confusionPeers`, `perspective` (`oblique-3-4` or `isometric-2-1`), `states` with `frames` and `fps` per state, `paletteId` and `paletteVersion`, `lightDirection`, `route` (`blender` | `comfy` | `scripted`) with reason, `importSettings`, `artBibleVersion`, `generator` block (tool and model versions, licenses).

## Asset provenance record

`assetId`, `revision`, `route`, `sources` (paths and hashes), `tools` (name, version, model and LoRA with hash and license), `parameters` (render script commit, camera rig version, prompt, negative prompt, seed, sampler, steps, workflow JSON hash), `touchUp` (scripted step list with script commit), `pixelPass` (script commit, palette version), `outputs` (paths and hashes), `license`, `trainingDataNote`, `producedBy` (role), `reviewedBy` (role, session, date; independent), `candidateSelection`, `rigVersion`, `renderEnv` (GPU, driver), `comfyNodeVersions`, `checkReportHash`, `artBibleVersion`, `briefVersion`. A shipped asset requires a complete record (spec SC-004).

## Display preferences

`version` = 1 exactly; `reducedMotion` ∈ {`followSystem`, `on`, `off`}; no other members. Effective reduced motion = `on`, or `followSystem` and the OS signal says reduce. The reader is a strict token walk (no reflection deserialization, IL2CPP-safe), rejects files over 4 KB, unknown members, wrong types and other versions, and falls back to `followSystem`. Never part of the save; written atomically (temp file in the same directory, native replace; a failure leaves the prior file unchanged).

## Style-spike record

Candidates (projection × route × density × state-cue variant), assets produced, device evidence (phone, tablet, emulator), gate results (grayscale/CVD naming, cue legibility at `n_min`, IoU, palette lock, regenerability, crisp rendering, fit-all rule), effort per asset, Art Director recommendation, Game Designer/PO readability check, **owner decision with date**.

## Performance baseline set

Workloads (W003, W004-V), device and build identifiers, fixture hashes, metrics (research R12), B1/B2/B3 results with same-session controls, frozen budgets, verdicts.

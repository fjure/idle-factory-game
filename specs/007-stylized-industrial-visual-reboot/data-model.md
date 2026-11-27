# Data Model: Visual Direction Reboot (Feature 007)

F007 adds **no save, content or simulation state** (FR-001). The entities below fall into two groups:

- **presentation data**: authored Unity assets, read-only at runtime;
- **evidence records**: versioned Markdown or JSON files under `specs/007-stylized-industrial-visual-reboot/reviews/` and `docs/`.

The ownership table follows constitution II.

## Ownership

| Entity | Kind | Owner (writer) | Readers | Persisted? |
|---|---|---|---|---|
| LookProfile | Presentation asset | Technical Artist (authoring); the DevTools selection in development builds | World views, catalog, camera, effect pool | Asset only; the dev selection may sit in the display-preferences file, never in the save |
| MachineArtEntry | Manifest row plus imported sprites or meshes | Art pipeline (generated), reviewed by the Art Director | Machine view, indicator view, effect pool, hit-test checks | Asset only |
| MachineBuildOutStages | Presentation asset | Gameplay Developer, thresholds reviewed by Game Systems | Machine presentation model | Asset only |
| FacilityFinishTiers | Presentation asset | Gameplay Developer, thresholds reviewed by Game Systems | Machine and environment views | Asset only; reads the persisted `PrestigeCount` |
| LayoutPitchSettings (extends `WorldCompositionSettings`) | Presentation asset | Gameplay Developer, values decided at M3 | `LatticeLayoutBuilder`, `FactoryWorldLayout` | Asset only |
| IndicatorGlyphSet | Sprite family | Art pipeline (scripted route) | Indicator view | Asset only |
| DirectionRecord | Evidence | Art Director (brief), parent (status) | Owner, reviewers | `reviews/directions/<A, B or C>.md` |
| ConstraintDisposition | Evidence | Art Director, co-signed by Technical Artist and UI/UX | M3 brief, Art Bible v2 | `reviews/audit-f007.md` |
| ComparisonCapture | Evidence | Capture rig (generated), parent (index) | Owner, rubric reviewers, trial scripts | `reviews/comparison/` (PNG and JSON sidecar) |
| RubricScore | Evidence | Each rubric reviewer, in a separate session | Owner, M3 brief, M6 | `reviews/rubric/*.json` |
| DirectionDecisionRecord | Evidence | Owner (decision), parent (record) | Everything after M3 | `reviews/direction-decision.md` |
| PerformanceBudgetSet | Rules | Performance Engineer (draft), owner (approval) | M5 tasks, QA | Art Bible v2 section |
| DeviceRecord | Evidence | Parent, from owner input | Performance tasks | `reviews/devices-f007.md` |

## LookProfile

Defined in [contracts/look-profile.md](./contracts/look-profile.md).

| Field | Type | Rule |
|---|---|---|
| `id` | string | One of `F006`, `F007-A`, `F007-B`, `F007-C`, `F007`. Unique. |
| `isPrototype` | bool | True for A, B and C. A prototype profile MUST NOT be the shipped default. |
| `spriteCatalog` | catalog reference | Must resolve every required key for this profile (fails closed, as in F004 FR-024). |
| `requirementTable` | per-profile table | Required keys, sizes and pivots for this profile. The existing static registry is the table of the `F006` profile (ARCH-5). |
| `meshSet` | optional prefab set | Only C. Null otherwise. |
| `worldRenderer` | renderer asset reference | `2D` (default) or `Universal` (C1, R06). |
| `pixelsPerUnit` | int | Art px per world unit. 1 world unit is 24 F006 art px, and a location cell is the 70 × 54 F006 lattice (`WorldCompositionSettings.cs:95`; corrected, ARCH-8). D24 → 24, A → 48, B → 128 (hypothesis R05). |
| `filtering` | enum | `Point` (F006, A) or `Bilinear` (B, C). |
| `cameraPolicy` | enum | `IntegerScale` (M2 for all; production A) or `Continuous` (production B or C, decided at M4). |
| `layoutPitch` | LayoutPitchSettings | `F006` lattice or the direction's proposed denser pitch. |
| `environmentFamily` | family key | The world-boundary art (R15). |
| `indicatorSet` | IndicatorGlyphSet | Per R10. |
| `lightingMood` | key | `dusk-v0.3` for every F007 profile (FR-007a). |

**State transitions.**

- Development build: profile selection `F006` ⇄ `F007-x`. It comes from an `ILookProfileSource` that DevTools injects at `BeforeSceneLoad`, and the bootstrap-owned resolver applies it on the next world rebuild (ARCH-6). An unknown persisted id falls back to the shipped default with a log entry (ARCH-11). The world-rebuild path is the one F006 uses for a layout change. The simulation is untouched.
- Release build: a fixed profile.

## MachineArtEntry (manifest row)

| Field | Rule |
|---|---|
| `machineDefinitionId` | Existing content id. No new ids. |
| `look`, `stage` | Profile id; stage 0–2. |
| `canvas`, `pivot` | In art px. The pivot is a whole art px for A. |
| `footprint` | Visual footprint; may exceed the location cell (FR-005, FR-026). |
| `hitShape` | Unchanged per location. It is owned by the layout, not by the art. The check is that the hit shape does not change with the art. |
| `indicatorAnchor` | Inside the body silhouette and inside the location's hit area (R10). |
| `effectAnchors[]` | Exhaust, sparks, glow (FR-021). |
| `frames`, `fps` | Loop frame count and rate on the shared clock. Frame 0 is the still. |
| `passes` | `body`, `shadow`, `emissiveMask` (B/C), optional `normal`. |
| `textureFormat` | Per platform (R13). |
| `provenanceRef` | Path to `provenance.json` (contracts/art-pipeline-v2.md). |

**Validation**:

- the silhouette IoU against the other three types at every stage is no higher than the limit (SC-003);
- the indicator anchor lies inside the body and the hit area;
- the flash check passes;
- the estimated memory per family is within budget.

## MachineBuildOutStages

| Field | Rule |
|---|---|
| `machineDefinitionId` | Existing id. |
| `thresholds[]` | Exactly 2 ascending machine levels (default 10 and 25; the second may move to 50 after Game Systems review), giving stages 0–2 (R11, FR-027a). |

`stage(level) = count(thresholds where level ≥ t)`. The function is pure, total for level ≥ 1, and never persisted.

## FacilityFinishTiers

| Field | Rule |
|---|---|
| `thresholds[]` | 1–3 ascending prestige counts, giving 2–4 tiers including tier 0 (Game Systems review) |
| `overlays` | Per machine type and per site family: material/trim variants or overlay parts per tier |

`tier(prestigeCount) = count(thresholds where prestigeCount ≥ t)`. It reads the persisted `PrestigeCount` only, is never written, and survives resets (FR-027c). Stage × tier combine orthogonally.

## LayoutPitchSettings

| Field | Rule |
|---|---|
| `pitchWorld` | Lattice pitch in **world units**. The F006 value is 70 × 54 art px at D24 = 2.917 × 2.25 world units. It is part of the `LayoutComposition` equality and `IdentitySuffix` (ARCH-4). |
| `groupRule` | Optional arrangement key (for example `bays`), decided at M3. Grouping is by site zone or density, never by production chain (WD-4). |
| `minHitPoints` | At least 44 pt per location at `n_min` or the minimum zoom (F006 FR-005). |

**Invariants**:

- location ids, count and unlock order come from content;
- hit areas never overlap;
- layout-independence tests pass for 6, 12, 20 and L layouts.

## DirectionRecord

`id` (A/B/C) · `brief` (materials, lighting, colour, shape language, references cited for principles only) · `constraintDispositions[]` · `prototypeAssets[]` · `captures[]` · `costs` (each value labelled *measured* or *estimated*) · `rubric[]` · `trials` · `status` ∈ {explored, prototyped, recommended, rejected, approved, revise}.

## ComparisonCapture (JSON sidecar)

`look` · `geometry` ∈ {same, proposed} · `class` ∈ {emuP, emuL, tabL, phoneP-card, phoneL640} · `textScale` · `factoryState` ∈ {partial, dense} · `seed` · `commit` · `buildKind` ∈ {apk-dev, windows-player, editor-playmode} · `device` · `resolution` · `capturedAt` · `deadAreaShare` · `medianMachineAreaPx`.

## DirectionDecisionRecord

`date` · `decider` (owner) · `chosen` · `conditions[]` · `rejected[]` with reasons · `geometry` (chosen pitch and arrangement, FR-005) · `machineAreaFactor` (≥ 1.5, SC-004) · `evidenceLinks[]` · `nextGates` (Art Bible v2, budget set v2, task regeneration).

## RubricScore

`reviewer` (role, session, date) · `look` · `captureSet` · `scores` (10 FR-037 dimensions, 1–5 each, with a one-line justification) · `anchorsVersion`.

A reviewer never scores a look they produced (constitution IX).

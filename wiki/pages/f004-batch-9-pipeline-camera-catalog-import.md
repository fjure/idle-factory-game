---
title: F004 batch 9 pipeline checks, catalog, camera and import
type: component
sources: [S052]
updated: 2026-11-03
---

# F004 batch 9 pipeline checks, catalog, camera and import

Batch 9 merged #616 (T462, PR #620), #541 (T428, PR #621), #539 (T427, PR #622) and #543 (T430, PR #624) up to `cf1d69c`. QA-approved commits: `ee45f36`, `df72571`, `1ecc66c` (after `ddcda74`), `64274fd` (after `b8b01d0`). (S052)

## T462 art-pipeline v1 checks

`tools/art-pipeline/art_pipeline/checks_v1.py` plus `palette-v1.json` implement Art Bible section 9.3: outline-or-rim (R1), overlay backdrop (R2), keep-out, accent subset of body, `intentionallyEmpty`, integral pivot and an id-level palette allowance. The spike Miner reproduces the documented failure (floor_0 56 pairs / 62 %, floor_1 58 / 60 %). The composite checks are standalone: T434-T439 must call them. (S052)

## T428 presentation sprite catalog

`PresentationSpriteCatalog` has a per-key requirement registry and no sprite-name pin. Architect decision: F001 keys are not start-up requirements, F003 keys are validated when present, F004 rows arrive via `Configure(entries, requirements)`. Fallback frames keep their own PPU 64 and are static. Diagnostics `presentation.art.fallback` and `presentation.key.unresolved` are warnings. Frame ranges: plates, resources, items and icons exactly 1; machines, badges, markers and spinner 1..16. The bootstrap scene path cannot apply F004 required rows yet; T429/T456 must choose how. (S052)

## T427 integer camera

`FactoryCameraController` is the sole camera writer and uses integer levels. `RunZoomSettings` is reduced to D 24, readable 24 pt, maxScale 6. The 1 % `n_min` tolerance (owner decision 2026-10-28) also applies to `MarkersVisibleAt` (x0.99). Pan remainder is kept per axis only below one device pixel (Architect A5 fix). The first-run landscape HUD gap (ore-vein overlay plus hint bar) is pinned in `ScreenClassScalePlayTests.OverlayGaps` and tracked as T463 #623 (owner decision 2026-11-03). Open owner decision before T454: the reason for a disabled zoom button is tooltip-only. See [pixel camera scale](./pixel-camera-scale.md). (S052)

## T430 sprite import

Editor-only `F004SpriteImportPostprocessor`, `F004ImportRules` and `F004SpriteAtlasSettings`. The manifest is a dependency and the importer is versioned (v2). The canvas is checked from the PNG IHDR before anything is applied. Rejections are `LogWarning` and leave Unity defaults, so catalog validation or T440 must catch them. Sprite Atlas v2 packer mode (`m_SpritePackerMode` 5) is a recorded settings decision, guarded by a test and not by `check-settings-noise`. No atlas assets exist until T440 calls `CreateFamilyAtlas`. (S052)

Related: [art production pipeline](./art-production-pipeline.md), [Unity presentation boundary](./unity-presentation-boundary.md), [batch 9 pitfalls](./f004-batch-9-workflow-pitfalls.md).

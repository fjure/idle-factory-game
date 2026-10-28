---
title: URP rendering foundation
type: decision
sources: [S038, S039, S041, S042, S043]
updated: 2026-10-28
---

# URP rendering foundation

Feature 004 phase 0 moves the project from the Built-in Render Pipeline (deprecated by Unity from 6.5) to URP **17.6.0**, the version built into Unity 6000.6.0f1, with a 2D Renderer. It runs only after Feature 003's scene, camera and settings Issues merge, because it writes the same single scene and settings files. (S038)

## Parity settings

- Default sprite material Unlit: code-created `SpriteRenderer`s would otherwise get Sprite-Lit and change the look. (S038)
- Color space stays Gamma so palette values stay exact. (S038)
- HDR, post-processing, depth and opaque textures off; no 2D lights in phase 0. (S038)
- The 2D Renderer ignores the `GraphicsSettings` transparency-sort axis; the fixed slot grid uses row-based `sortingOrder` instead. (S038)

## Gates and rollback

Phase 0 is one revertible commit. It must keep the same test pass set, the Windows test player, the Android emulator journey, screenshot parity (≤ 0.1 % differing pixels, ≤ 2/255 per channel) and a B2 performance run in the same session as the Built-in B1 control. `scripts/check-settings-noise.ps1` does not inspect `GraphicsSettings` or `QualitySettings`, so an Edit Mode test asserts the pipeline assignment in every quality level. (S038)

## Pre-switch baselines (T409, 2026-10-27)

Built-in baselines are committed before the switch: 36 PNGs (4 per screen class over 9 classes: 640x480, 360x640, 640x360, 390x844, 844x390, 768x1024, 1024x768, 1280x720, 1920x1080), `RenderParityTests` with 18 tests, and a pinned W004-V fixture hash. The comparator counts any channel delta above 0 as a differing pixel, then requires at most 0.1 % differing and no delta above 2/255. T411 made the comparator R13-literal (owner decision 2026-10-27): only pixels with a delta above 2/255 count, see [F004 URP phase 0 results](./f004-urp-phase0-results.md) (S042). Three runs gave 0 differing pixels, so capture is deterministic. Expected counts after T409: Edit Mode 2323, Play Mode 2248 (2230 + 18); T411 must show the same names plus those 18. (S039)

Metrics are not collectable in the batch editor: `UnityStats` includes Editor rendering and `ProfilerRecorder` counters read 0, so batches, SetPass, shader variants, APK size and player texture memory move to T410. Only 47 sprites and 913,912 B of world-art texture memory (Editor runtime size) are recorded. (S039)

## Update 2026-10-28

The Built-in B1 control exists now: see [F004 B1 Built-in baseline](./f004-b1-builtin-baseline.md) for the noise band and the stand-in metrics. The T411 comparator decision (only deltas above 2/255 count) is in [F004 batch 2 workflow pitfalls](./f004-batch-2-workflow-pitfalls.md). (S041)

## Update 2026-10-28 (batch 3)

Phase 0 merged (T411): parity 18/18, max delta 1/255, 490 stripped variants. The independent review (T413) approved with follow-ups; S4 2D lights need `m_Strip2DUnusedVariants` set to 0. Details: [F004 URP phase 0 results](./f004-urp-phase0-results.md). B2 (T412) is deferred and open. (S042, S043)

Related: [mobile performance targets](./mobile-performance-targets.md), [visual identity and art direction](./visual-identity-art-direction.md).

---
title: F007 directions and measurement plan
type: decision
sources: [S063]
updated: 2026-11-27
---

# F007 directions and measurement plan (planned)

Labels follow the research: measured, derived, hypothesis. Nothing below is a result until its M2 measurement is ingested. (S063)

## The three prototype directions

- **A, Enhanced Pixel Art**: D48 (twice D24), palette up to 96 colours, selective outlines, Light2D L2 envelope (at most 12 point lights). RGBA32 point-filtered pages of 1 MiB hold 9 frames of 160 x 128 per 512 x 512 page (derived). Integer zoom fit is a hypothesis.
- **B, Blender 3D-to-2D**: `rig-v3` (same camera as `rig-v2`), Cycles, Standard view transform, separate soft shadow sprite and emissive mask, 128 art px per cell (hypothesis; 192 px tested on one machine). Frame about 427 x 341 px (derived). Stage-0 memory about 10.4 MB ASTC 6x6 (derived, hypothesis until the build report); about 25-30 MB at M5 with stages. It cannot fit the 8 MB world-art budget, so budget set v2 is required.
- **C, hybrid real-time 3D**: floor in B style, machines as lit meshes. Route C1 is chosen: a dev-only second URP asset with a Universal Renderer (Forward) via `QualitySettings.renderPipeline`; the shipped asset keeps one 2D renderer and 490 variants. Rejected: C2 RenderTexture (fill-rate), C3 2D-lit only (no 3D light). Fact: the 2D Renderer gives meshes no 3D lighting and camera stacks cannot mix renderer types. Spike gate on the Pixel (hypothesis thresholds): p95 at most 16.7 ms, GPU at most 8 ms. Sorting (depth-placed versus transparent-queue meshes) is decided by the spike. (S063)

## ASTC page-cap re-measure

Measured earlier: a 2 MiB RGBA32 page hung Android load, 1 MiB pages load in about 3 s, cause unknown. Hypothesis: the hang follows byte size, not texel count. Plan: byte-matched sweep (ASTC 6x6 at 1448 and 2048 squared) plus a texel-matched control, 3 cold loads each with the maximum reported, on the phone (authoritative) and the emulator, recording whether ASTC is hardware-supported. ASTC 6x6 is 0.444 B/px and 8x8 is 0.25 B/px (corrected from an earlier 2x error). Until it passes, compressed pages stay at 1 MiB and `AtlasPageBudgetTests` stays strict. See [atlas page cap](./f006-android-load-hang-atlas-page-cap.md). (S063)

## Camera, indicators, stages

M2 keeps the integer camera for all looks; M4 may adopt continuous zoom for B/C with explicit re-baselining of `CameraScaleModel` tests. See [pixel camera scale](./pixel-camera-scale.md). The indicator glyph keeps the 24 pt floor at `n_min` on every class (the 60 % size applies only to the lamp housing, hypothesis); the glyph is the normative carrier, with plate contrast at least 3:1. Build-out stage is a pure function of level, finish tier of persisted `PrestigeCount`. The store-full marker is drawn once at the depot. (S063)

## Evidence methods

Capture matrix of 70 cells (5 class variants x 2 factory states x 7 look/geometry columns), sidecar-validated, fixtures pinned. SC-002 rubric uses 8 visual dimensions with anchors written from reference criteria; F006 is scored, not an anchor; blinding is impossible (matched framing, shuffled order). Metric scripts (dead area, body area, IoU) ship with known-answer pytest. The parity test hashes full simulation state per tick under every profile, with a mid-run switch, identical save bytes and a planted negative control. Phone protocol: ABAB, fixed refresh, thermal and charge state recorded, dev-build labels, GPU time and overdraw, 10-minute sustained run. Pipeline v2 `render-hd` route: union-bbox trim, CPU render with pinned seed and no denoiser, prompt lint, no reference screenshots as img2img input. (S063)

See [plan overview](./f007-plan-overview.md), [art production pipeline](./art-production-pipeline.md).

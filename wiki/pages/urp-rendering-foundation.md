---
title: URP rendering foundation
type: decision
sources: [S019]
updated: 2026-10-01
---

# URP rendering foundation

Feature 004 phase 0 moves the project from the Built-in Render Pipeline (deprecated by Unity from 6.5) to URP **17.6.0**, the version built into Unity 6000.6.0f1, with a 2D Renderer. It runs only after Feature 003's scene, camera and settings Issues merge, because it writes the same single scene and settings files. (S019)

## Parity settings

- Default sprite material Unlit: code-created `SpriteRenderer`s would otherwise get Sprite-Lit and change the look. (S019)
- Color space stays Gamma so palette values stay exact. (S019)
- HDR, post-processing, depth and opaque textures off; no 2D lights in phase 0. (S019)
- The 2D Renderer ignores the `GraphicsSettings` transparency-sort axis; the fixed slot grid uses row-based `sortingOrder` instead. (S019)

## Gates and rollback

Phase 0 is one revertible commit. It must keep the same test pass set, the Windows test player, the Android emulator journey, screenshot parity (≤ 0.1 % differing pixels, ≤ 2/255 per channel) and a B2 performance run in the same session as the Built-in B1 control. `scripts/check-settings-noise.ps1` does not inspect `GraphicsSettings` or `QualitySettings`, so an Edit Mode test asserts the pipeline assignment in every quality level. (S019)

Related: [mobile performance targets](./mobile-performance-targets.md), [visual identity and art direction](./visual-identity-art-direction.md).

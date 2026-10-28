---
title: F004 B1 Built-in baseline
type: reference
sources: [S041]
updated: 2026-10-28
---

# F004 B1 Built-in baseline

T410 (#522, PR #576, `c27ced2`) measured the Built-in Render Pipeline control that the URP switch (see [URP rendering foundation](./urp-rendering-foundation.md)) is compared against. Evidence: `specs/004-visual-identity/reviews/baselines.md`, helpers in `scripts/f004-b1/`, APK `artifacts/f004/B1-ac813fd-9c9ed43a.apk` (37,345,615 B). (S041)

## What the numbers allow

- **FPS noise band:** an ABA run spread 58.05 versus 48.18 FPS, so treat about +-5 FPS as noise and run B1 at least 3 times in one cold-booted session. Host load from a game client invalidated earlier sessions (PF-9). See [Android emulator profiling](./android-emulator-profiling.md). (S041)
- **Draw statistics:** the "Batches" and "Draw Calls" counters read 0 in Unity 6000.6 and `UnityStats.batches` is gone. SetPass per render (with minus without the world, 20 renders per frame) stands in for them. (S041)
- **W004-V** is Editor-only, so it yields no emulator FPS. (S041)
- **APK size:** incremental APK builds carry about 14 MB of dead space; compare clean builds or entry sums. (S041)
- **Shader variants:** 693 stripped variants is the do-not-grow figure. The documentation says 51 shaders, the TSV lists 52. (S041)

Related: [Mobile performance targets](./mobile-performance-targets.md).

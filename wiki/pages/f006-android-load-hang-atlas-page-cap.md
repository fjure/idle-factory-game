---
title: F006 Android load hang and the atlas page cap
type: decision
sources: [S060]
updated: 2026-11-13
---

# F006 Android load hang and the atlas page cap (T552)

**Symptom (U-A1, blocker).** The Android GLES3 APK of the batch-A main never left loading on the reference emulator (`SetGameState isLoading: true`, `Loading.AsyncRead` at 100 % CPU, no frame for 8+ minutes) while Windows loaded in about 12 s. (S060)

**Cause.** A 2 MiB atlas page, the F004 `machines` atlas (512 x 1024 RGBA32, since T440), once referenced. Pages of 1 MiB or less load in about 3 s. Why Unity or the emulator stalls is not identified, so the rule is empirical. (S060)

**Fix (#750, PR #754, b3026b1).** Every atlas page is at most 512 x 512 RGBA32 (1 MiB): `F004SpriteAtlasSettings.MaxPageSize` and `MaxPageBytes`, enforced by `AtlasPageBudgetTests`. The `machines` atlas now has two pages (1.0 + 0.25 MiB), total bytes fell. The journey script gained an `isLoading:false` gate. After the fix the shell loaded 4.4 s after the graphics device. (S060)

**Carried.** Budget 8 (SetPass) must be re-measured because body and accent frames may sit on different pages (A-10; T531, T544). `ValidationRejectsAnOversizedPageOverride` asserts constants only. Art Bible 21.9 item 8 records the cap. (S060)

Related: [Android emulator validation](./android-emulator-validation.md), [F004 batch 10 per-page atlas memory](./f004-batch-10-lattice-zoom-ore-vein-art-import.md), [gate and decisions](./f006-p3a-gate-and-owner-decisions.md).

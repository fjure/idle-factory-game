---
title: W003 player timing baseline
type: component
sources: [S018]
updated: 2026-10-08
---

# W003 player timing baseline

T122 (#204) measured the W003 workloads in a Windows Development player with `W003PlayerTimingTests`. Baseline r1 is recorded in `reviews/w003-measurements.md` (Ryzen 7 7800X3D). The Save V2 inputs ship as `Resources` copies with a SHA-256 parity test (`SaveV2PlayerFixtureParityTests`). (S018)

## Results (r1)

| Metric | Result | Target | Verdict |
|---|---|---|---|
| Host frame p95 | 0.02–0.04 ms | ≤ 1 ms | PASS |
| 8 h reconcile, W003-S median | about 320–366 ms | ≤ 500 ms | PASS |
| 8 h reconcile, W003-R median | about 175–216 ms | ≤ 500 ms | PASS |
| 8 h reconcile, W003-B median | about 950–1050 ms | ≤ 500 ms | **MISS** |

W003-B has about 2.76 M completions, 230,401 timestamps and 2 passes per timestamp, about 350 ns per event. The miss goes to T130 (Performance review). SC-008 must not be recorded as passing until then. (S018)

## Allocation (PE-4)

The advance path shows 0 `GC.Alloc` samples. The whole frame, including the HUD read probe, shows 6 samples per 300 frames; the source is the Overdrive status read in `RunShellPresenter.Refresh` (T130). `GC.GetAllocatedBytesForCurrentThread` returns 0 in the player, so count profiler samples instead. (S018)

## Caveats

- The host frame excludes UI Toolkit element writes: the scene-less player cannot build the HUD, because UXML loads through the asset database. It is a lower bound until T124. (S018)
- The W003 catalog is duplicated in the player assembly because the Edit Mode test catalog cannot be referenced. Equivalence rests on exact completion counts and a round trip. (S018)
- A running unrelated Unity editor added about 7 % timing variance; measure with the editor closed. (S018)

Related: [factory run bounds and offline segmentation](./factory-run-bounds-and-offline-segmentation.md), [mobile performance targets](./mobile-performance-targets.md), [Unity player validation](./unity-player-validation.md), [Android emulator validation](./android-emulator-validation.md).

---
title: W003 player timing baseline
type: component
sources: [S018]
updated: 2026-10-10
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

W003-B has about 2.76 M completions, 230,401 timestamps and 2 passes per timestamp, about 350 ns per event. T130 confirmed the miss (see below). (S018)

## Performance review (T130)

Verdict: **SC-008 is not met as written**. W003-B is 951.7 and 1017.6 ms against 500 ms. The whole frame also allocates (PR-2: 6 `GC.Alloc` samples per 300 frames, one Overdrive status object per second from `RunShellPresenter.Refresh`; the advance path is 0; `GC.GetAllocatedBytesForCurrentThread` returns 0 in the player, so count profiler samples). Everything else passes with margin (frame p95 about 4 % of budget, at most 4 passes per timestamp against 13). (S018)

- **Bounds on the verdict**: W003-B is a theoretical bound, not shipped balance (the reference trace credits 6,661 completions for an 8 h absence at minute 30), and it was never run on IL2CPP or the emulator. The emulator 746 ms is the different W003-S set, so that half of the budget is unresolved, not passed. (S018)
- **Suspected cause (inferred, not profiled)**: repeated string-keyed inventory lookups and a duplicate fit calculation in the hot loop, although stage indexes are already resolved. (S018)
- **Options**: 0 measure first (quiet host, IL2CPP, profile); 1 index-based hot loop, bit-identical, est. 1.5x to 2.5x; 2 batch uncontended Miners, at most 1.33x; 3 steady-state jump, over 100x but needs a guard set and differential test; 4 accept and re-baseline. (S018)
- **Architect position (T132)**: Option 1 is acceptable only as an additional index-based `GlobalInventory` API that leaves command paths unchanged; Option 3 needs a written, Architect-reviewed design first; an off-thread reconcile is rejected because `RunSession` has no thread-safety contract. PR-1 is an owner acceptance question, not an architecture defect. (S018)
- **Decided (T133, owner 2026-10-09, D1 and D2)**: option C, accept the W003-B miss as a recorded deviation, with option A (measure first: quiet host, IL2CPP release player, W003-B on the emulator, CPU attribution) as follow-up Issue #353; optimization is decided on that measurement. PR-2 is accepted: the advance path stays allocation-free and event-driven HUD allocations at most once per simulated second are allowed. SC-008 reads "met for the shipped workloads, W003-B miss accepted". (S018, `reviews/final-acceptance.md`; see [F003 final acceptance](./f003-final-acceptance.md))

See [feature review](./f003-feature-review.md).

## Caveats

- The host frame excludes UI Toolkit writes (a scene-less player cannot load UXML), so it is a lower bound until T124. (S018)
- The W003 catalog is duplicated in the player assembly; equivalence rests on exact completion counts and a round trip. (S018)
- An unrelated running Unity editor added about 7 % variance; close it. (S018)
- **Quiet host rule (T133)**: a running Android emulator on the same host inflated the 8 h reconcile by 13 to 18 percent on identical Core code (W003-B 1,154.8 ms with the emulator running against 965.7 ms with it shut down; S 361.5 against 319.7 ms; R 207.3 against 174.6 ms). Shut the emulator and the editor down before timing, and record which was running. (S018, `reviews/final-acceptance.md` section 4)

## Emulator follow-up (T124)

On the Android emulator with the shipped-catalog W003-S set, the 8 h return median is 746 ms (budget 2 s) and producing FPS is 61.1 (budget 60); the T133 rerun on `ac4256c` gave 735 ms and 61.4 fps. These are emulator values only and do not replace the W003-B miss above. See [Android emulator validation](./android-emulator-validation.md). (S018)

Related: [feature review](./f003-feature-review.md), [factory run bounds and offline segmentation](./factory-run-bounds-and-offline-segmentation.md), [mobile performance targets](./mobile-performance-targets.md), [Unity player validation](./unity-player-validation.md), [Android emulator validation](./android-emulator-validation.md).

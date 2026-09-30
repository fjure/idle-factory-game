---
title: Factory run bounds and offline segmentation
type: decision
sources: [S018]
updated: 2026-09-30
---

# Factory run bounds and offline segmentation

## Event budget

The minimum cycle duration is 0.1 seconds, so higher speed increases batch size instead of event count. Content is rejected when an effect is ineffective at the speed bound or a worst-case batch amount exceeds 10^18. A batch larger than the store is valid and completes partially (OD-9). The worst case is 12 machines over 6.4 effective hours, about 2.76 million completions in an eight-hour return. The next event uses a linear scan of at most 12 machines, with no heap and zero allocations when warm. W003 budgets are at most 500 ms on the Windows player and 2 seconds on the emulator; a miss requires Performance review before semantic changes. (S018)

## Workloads (T098)

`W003Workload` has three variants. **W003-B** keeps all 12 machines at the event bound, with auto-sell on Gear only, and reaches the theoretical 2,764,800 completions in 8 h exactly; it carries the event-bound budgets. **W003-S** auto-sells every resource. Whole-stock sale then empties every intermediate at each timestamp, so only the Miners complete (691,200), and S is kept only as an auto-sell sanity check. **W003-R** is the 120-minute reference state. All three show zero `GC.Alloc` samples per warmed `Advance`. The Edit Mode 8-hour timings (W003-B about 0.9 s on Mono) are informational only; the budgets are measured on the Windows player and the emulator in T122/T124. (S018)

## Offline time mapping

Online time is `anchorUtc + activeTicks`. Offline, each real instant `x` maps from saved departure `d` to effective ticks as `E(x) = floor(min(x−d, 8 h) × 4/5)`. Effects and deadlines become events at `E(boundary)`. Because `E` telescopes, segmentation credits the same total as the unsegmented Feature 001 formula. Deadlines are inclusive, so expiry occurs at `deadline + 1 ms`. (S018)

Related: [factory run simulation](./factory-run-simulation.md), [Save V2 and V1 import](./save-v2-and-v1-import.md).

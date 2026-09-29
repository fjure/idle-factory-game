---
title: Factory run bounds and offline segmentation
type: decision
sources: [S018]
updated: 2026-09-29
---

# Factory run bounds and offline segmentation

## Event budget

The minimum cycle duration is 0.1 seconds, so higher speed increases batch size instead of event count. Content is rejected when an effect is ineffective at the speed bound or a worst-case batch cannot fit the store. The worst case is 12 machines over 6.4 effective hours, about 2.76 million completions in an eight-hour return. The next event uses a linear scan of at most 12 machines, with no heap and zero allocations when warm. W003 budgets are at most 500 ms on the Windows player and 2 seconds on the emulator; a miss requires Performance review before semantic changes. (S018)

## Offline time mapping

Online time is `anchorUtc + activeTicks`. Offline, each real instant `x` maps from saved departure `d` to effective ticks as `E(x) = floor(min(x−d, 8 h) × 4/5)`. Effects and deadlines become events at `E(boundary)`. Because `E` telescopes, segmentation credits the same total as the unsegmented Feature 001 formula. Deadlines are inclusive, so expiry occurs at `deadline + 1 ms`. (S018)

Related: [factory run simulation](./factory-run-simulation.md), [Save V2 and V1 import](./save-v2-and-v1-import.md).

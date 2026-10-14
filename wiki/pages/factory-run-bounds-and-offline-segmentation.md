---
title: Factory run bounds and offline segmentation
type: decision
sources: [S018, S022, S027]
updated: 2026-10-14
---

# Factory run bounds and offline segmentation

## Event budget

The minimum cycle duration is 0.1 seconds, so higher speed increases batch size instead of event count. Content is rejected when an effect is ineffective at the speed bound or a worst-case batch amount exceeds 10^18. A batch larger than the store is valid and completes partially (OD-9). The worst case is 12 machines over 6.4 effective hours, about 2.76 million completions in an eight-hour return. The next event uses a linear scan of at most 12 machines, with no heap and zero allocations when warm. W003 budgets are at most 500 ms on the Windows player and 2 seconds on the emulator; a miss requires Performance review before semantic changes. T130 confirmed the W003-B miss; see [W003 player timing baseline](./w003-player-timing-baseline.md). (S018)

## Workloads (T098)

`W003Workload` has three variants. **W003-B** keeps all 12 machines at the event bound, with auto-sell on Gear only, and reaches the theoretical 2,764,800 completions in 8 h exactly; it carries the event-bound budgets. **W003-S** auto-sells every resource. Whole-stock sale then empties every intermediate at each timestamp, so only the Miners complete (691,200), and S is kept only as an auto-sell sanity check. **W003-R** is the 120-minute reference state. All three show zero `GC.Alloc` samples per warmed `Advance`. Budgets are measured on the Windows player (T122) and the emulator (T124); Edit Mode timings are informational. (S018)

## Offline time mapping

Online time is `anchorUtc + activeTicks`. Offline, each real instant `x` maps from saved departure `d` to effective ticks as `E(x) = floor(min(x−d, 8 h) × 4/5)`. Effects and deadlines become events at `E(boundary)`. Because `E` telescopes, segmentation credits the same total as the unsegmented Feature 001 formula. Deadlines are inclusive, so expiry occurs at `deadline + 1 ms`. (S018)

## Offline segmenter (T103)

- `OfflineSegmenter.Reconcile(state, permanent, d, r)` runs the whole absence in one `RunSimulator.Advance` of `E(r)` ticks with `startUtcMs = d`. That is the only start value that keeps Overdrive active at departure correct. Boundaries at or before `d` are emitted at 0, boundaries in `(d, r]` at `E(b)`, and boundaries after the cap at `E(cap)`. A boundary exactly at `r` is processed; one at `r + 1 ms` is not. Exactly 8 h is not capped; 8 h + 1 ms is. (S018)
- A backward, missing or unrepresentable clock processes nothing, not even boundaries that are already due, so the return can be retried. A simulator rejection (`value.limit`) is not retryable (`IsRetryable` is false), T105 forfeits that credit (owner decision 2026-10-01): the state stays as before the absence, the departure advances to `r`, and the summary reports `offline.value-limit`, exactly once. (S018)
- `Reconcile` mutates the state directly and is not idempotent. T105 wires it through `OfflineRunReturnReconciler` in [run persistence](./run-persistence.md): each attempt reconciles a fresh read of the source, `reconciledFromCheckpointId` links it, `d` is the persisted departure, a future departure credits nothing, and the session adopts the return's last completions. (S018)
- `RunOfflineSummary` reports durations, cap and efficiency, cash and stock deltas and flows, effects and Overdrive phases that ended inside `(d, r]`, the contract outcome by before/after comparison, partial batches, blockers at the return instant, and new unlocks. The first version also claimed effects that had ended before the departure; the review corrected that. (S018)

Related: [factory run simulation](./factory-run-simulation.md), [Save V2 and V1 import](./save-v2-and-v1-import.md).

## W003-B measurement and decision (T300, F005)

Windows Mono dev player at a41c53f: median of run medians 962.5 ms (1.92x the 500 ms reference), spread 11.8 % (PF-10 formally not met), CompletionPass 76.6 %, run predicate 14.5 %. Lookups cost at most about 11 %, a calibrated bound. The emulator measured 4.6 s, an upper bound only, because the host was not quiet (League client, TFT). (S027)

Decision: Option 1 (the T360 hot loop) with a required re-measure gate and an escalation path; Option 3 stays out. Per F5-R14 W003-B is "recorded, not gated"; 500 ms and 2 s are the W005-L budgets used as reference. A "quiet host" must name non-Unity background load such as games; quiet-host repeats run before the T360 acceptance measurement. (S027, S022)

## T360 result (2026-10-13)

The Option 1 index-based completion pass left fixture and post-reconcile state digests identical; PF-1 is unchanged (2,764,800 completions, 230,401 timestamps). Quiet-host W003-B median is 199.61 ms on the Windows Mono dev player (base 952.63 ms on the same host), below the 500 ms gate; the emulator measured 431.64 ms against 2 s. IL2CPP release (PF-13, #353) stays open and T326 must measure with and without auto-deliver. See [batch A](./f005-batch-a-economy-and-contracts.md). (S022)

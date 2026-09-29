---
title: Factory run simulation
type: decision
sources: [S018]
updated: 2026-09-29
---

# Factory run simulation

Feature 003 (planned, not yet implemented) replaces the per-machine buffer model of [deterministic production](./deterministic-production.md) for gameplay. Its place is taken by one global inventory and fixed slots that hold one machine each. The Feature 001 closed-form simulator advances each building independently. That is exact only because buildings never interact, so it cannot be reused once they share stock. (S018)

## Event order

`RunSimulator` is a discrete-event loop (S018). Its events are:

- machine completions;
- real-time boundaries (Item effect and Overdrive ends, cooldown end, contract expiry);
- the end of the interval.

It processes every event whose timestamp is at or before the target. At each timestamp it runs these steps (S018):

1. It expires the boundaries that are due.
2. It completes the due machines in the player's priority order. Each completion is an atomic exchange that checks inputs and the post-exchange total against capacity. A completion that does not fit leaves the machine *waiting* at full progress.
3. It auto-sells the whole stock of every enabled resource.
4. Provisionally, it retries waiting completions and sales until nothing changes. This is owner decision OD-2: without the retry, a factory whose only running machines are waiting stalls after a sale frees capacity.
5. It re-evaluates the run predicate. A machine runs exactly when it is active, its complete inputs are present (nothing is reserved) and its net exchange fits. Otherwise it pauses with its progress kept.

Player commands apply after the interval that ends at their timestamp. The session then settles by running steps 3–5. Stock changes only at events, so every time partition yields the same state. (S018)

## Speed and progress

Every multiplier is an integer in parts per million (S018).

- **Speed composition**: level growth and milestones, then Research, Industry, Items and Overdrive. The product is folded with a floored 128-bit `MulDiv` and clamped to a content bound.
- **Cycle length**: `ceil(duration × 10^6 / speedPpm)`.
- **Output effects**: integer factors applied to output amounts only.
- **Progress rescale**: when the cycle length changes, progress becomes `floor(p × D′/D)`. This one formula serves upgrades, effect boundaries, Research, content retuning on load and the V1 import (`floor(old × 2/5)`).

`double` is excluded from all authoritative rules. (S018)

## Cost bound

Content is rejected when any recipe at the maximum combined speed would cycle faster than 0.1 s. The worst case is 12 machines over 6.4 effective hours, about 2.8 M completions for an eight-hour return. The next event is found by a linear scan of at most 12 machines, with no heap and zero allocations when warm. The W003 budgets are ≤ 500 ms on the Windows player and ≤ 2 s on the emulator. A miss goes to a Performance review before any semantic change. (S018)

## Real time

The online run timeline is `anchorUtc + activeTicks`. Offline, the policy maps each real instant `x` to effective ticks through `E(x) = floor(min(x−d, 8 h) × 4/5)`. Effects and deadlines become events at `E(boundary)`. Because `E` telescopes, segmenting at boundaries credits exactly the same total as the unsegmented Feature 001 formula. Deadlines are inclusive, so expiry happens at `deadline + 1 ms`. (S018)

Related: [Save V2 and V1 import](./save-v2-and-v1-import.md), [run economy and prestige](./run-economy-and-prestige.md), [core architecture](./core-architecture.md).

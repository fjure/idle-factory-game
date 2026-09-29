---
title: Factory run simulation
type: decision
sources: [S018]
updated: 2026-09-30
---

# Factory run simulation

Feature 003 (content model and run state implemented in T094/T095; the simulator itself is still pending in T098) replaces the per-machine buffer model of [deterministic production](./deterministic-production.md) for gameplay. Its place is taken by one global inventory and fixed slots that hold one machine each. The Feature 001 closed-form simulator advances each building independently. That is exact only because buildings never interact, so it cannot be reused once they share stock. (S018)

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

- **Speed composition**: level growth and milestones, then Research, Industry, Items and Overdrive. The product is folded with a floored 128-bit `MulDiv`. The content's maximum combined speed is only an overflow bound that valid content never reaches. (S018)
- **Batching** (OD-6): a cycle is never shorter than the content minimum (0.1 s). Speed beyond that bound becomes a per-cycle batch factor `k` on inputs and outputs, so every speed source keeps raising the rate. A batch limited by inputs or store space completes the largest fitting whole number of units and drops the rest; zero units means waiting (OD-9). (S018)
- **Cycle length**: `ceil(duration × 10^6 / speedPpm)`.
- **Output effects**: integer factors applied to output amounts only.
- **Progress rescale**: when the cycle length changes, progress becomes `floor(p × D′/D)`. This one formula serves upgrades, effect boundaries, Research, content retuning on load and the V1 import (`floor(old × 2/5)`).

`double` is excluded from all authoritative rules. (S018)

## Bounds and offline timing

The event workload and offline-time mapping are part of the simulation contract; see [factory run bounds and offline segmentation](./factory-run-bounds-and-offline-segmentation.md). (S018)

The implemented state and its invariants are described in [run state domain](./run-state-domain.md). (S018)

Related: [Save V2 and V1 import](./save-v2-and-v1-import.md), [run economy and prestige](./run-economy-and-prestige.md), [core architecture](./core-architecture.md).

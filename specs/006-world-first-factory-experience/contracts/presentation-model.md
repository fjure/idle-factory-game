# Contract: Presentation Models (F006)

**Revision 2** (plan reviews, 2026-11-11).

| | |
|---|---|
| **Scope** | Completion count; derived machine presentation; attention and shortage; effects; milestones; stages. |
| **Requirements** | FR-001, FR-002, FR-006, FR-025 to FR-031, FR-034 to FR-036. |
| **Research** | R08, R09, R12, R14, R15. |

## 1. Authority and the completion count (FR-001, FR-002)

### Rules

- **What is counted.** `RunSimulator.CompletionPass` records, per advance and per machine, the number of completed cycles. A partial batch counts **+1**, never as the batch factor.
- **When counts become visible.** Counts are exposed only after a successful `Commit()`. A faulted advance exposes 0.
- **Who owns the total.** `RunSession` keeps a `long CompletedCycles` per `InstanceId`:
  - it adds the exposed counts after each successful advance;
  - it prunes the entry when the machine is removed;
  - it resets the entry when the machine is replaced;
  - it starts empty on construction, restore and prestige.
- **How the total is read.** `RefreshViews` passes the value into `MachineStateView` through an added constructor parameter (default 0).
- **Where it must never appear.** The value is never saved and is never read by simulation, economy, offline or prestige code.

### Tests

- `CompletedCyclesLifecycleTests` covers:
  - a faulted advance;
  - multiple completions in one advance;
  - remove and replace;
  - prestige;
  - restore.
- `CompletedCyclesPropertyTests`: the sum of deltas equals the completions in the engine trace (W003 and W005), including batch, offline and restore cases.
- `CompletedCyclesNoObserverEffectTests`: the save SHA and trace SHA are identical with and without a reader.
- Allocation checks: `CopyMachineStates` and `RefreshViews` allocate nothing in steady state.
- The `core-public-api.txt` diff is recorded.

## 2. Machine presentation (FR-025, FR-027, FR-029)

### Rules

- **Visual state.** Computed by `SlotGridModel.StateFor`. There is no second precedence rule.
- **Upgrade marker.**
  - `UpgradeAvailable = NextUpgrade.Available && NextUpgrade.Affordable`.
  - The marker is less salient than any problem cue: it is never red and is smaller than a badge.
  - It is hidden while the machine shows a problem badge.
- **Activity pace.** Derived from the cycle time:

  | Cycle time | Pace |
  |---|---|
  | 4 s or longer | Slow |
  | 1–4 s | Normal |
  | Under 1 s | Fast |

  The pace selects 6, 8 or 10 fps, capped per family by the measured flash rate (the Assembler is capped at 8 fps or lower). A pace change takes effect at the next frame 0.
- **Completion delta.**
  - Reported once per refresh.
  - On a baseline reset (restore, offline return, or a decrease) the delta is 0.
  - Offline deltas never produce effects.
- **Buffers.** Sized from `LocationCount`.

### Tests

- `MachinePresentationModelTests` covers:
  - every visual state;
  - upgrade-marker suppression;
  - the pace buckets and the per-family cap;
  - baseline resets;
  - offline suppression.

## 3. Attention and shortage (FR-026, FR-028, FR-030)

### Rules

- **Shared cause.** Two or more MissingInput machines blocked by the same resource form a shared cause.
- **Shortage.**
  - Candidates are resources that currently block at least one MissingInput machine and have demand (`HasDemand`).
  - Order: coverage ascending, then affected count descending, then content order.
  - No MissingInput machine means no shortage.
  - Copy: "Short on <resource>".
- **Store full.** If any machine is StorageFull, the depot shows the full state.
- **Production attention.** Shared causes, the shortage and store full appear as world markers and one strip indicator.
  - There is one world marker per shortage resource, at most 2 visible, ranked by the shortage order. Each marker sits at the trunk junction of its first affected row, or at the depot for store full, and shows the resource icon.
  - The strip indicator shows the top resource and its affected count, plus "+N" for further resources.
  - Tapping a marker or the strip indicator selects the first affected location.
- **System attention.** Contracts, Research, Prestige and Overdrive drive only the hub dot. Market has no flag.
- **No numbers in the world** (FR-030).
- **When it is computed.** Only on a revision change, into reused buffers.

### Tests

- `AttentionModelTests` covers:
  - grouping;
  - ranking and ties;
  - an auto-sold input with coverage ≥ 1,000,000 and stock 0, which is shown;
  - coverage below 1,000,000 with no blocked machine, which is not shown;
  - no demand;
  - store full;
  - the split between production and system attention.
- `AttentionDisplayTests` (Play Mode).

## 4. Effects and allocations (FR-006, FR-029, FR-034)

### Rules

- **Pool.** 8 renderers: 3 active and 5 draining tails.
  - A cancelled effect drains its remaining frames.
  - A CompletionPop outranks decorative effects.
  - Effects of the same kind at one location coalesce within 250 ms.
  - Timestamps are sized from `LocationCount`.
- **Reduced motion.** No effect spawns. A completion is shown as a static 1 s highlight on the card or the depot.
- **Flash safety.** `check_flash` passes on body + accent + FX composites at every pace.
- **Steady-state allocations are zero** for:
  - the clock tick;
  - the model refresh;
  - effect spawn and coalescing;
  - the card dock;
  - opening and closing the hub.

### Tests

- `EffectPoolTests`.
- `F006AllocationTests`, Play Mode and tagged `StandalonePlayer`.
- The W006-V 600-frame player run.

## 5. Milestones (FR-035)

### Rules

- **Detection.**
  - A factory-level increase raises `FactoryLevelUp` with the newly unlocked locations.
  - A run-id change after prestige raises `PrestigeCompleted`.
- **Banner.**
  - Non-blocking, at most 2.5 s, dismissible.
  - Copy from `reviews/world-copy.md`.
  - Accompanied by the deck reveal and a 2 s foundation highlight.
  - Static under reduced motion.
- **Offline.** Milestones that happened offline appear only in the offline summary.
- **No invented rewards.** The banner never shows a reward the player did not get.

### Tests

- `MilestoneWatcherTests`.
- `MilestoneBannerTests`: input outside the banner is not captured.

## 6. Development stages (FR-036)

See data-model `DevelopmentStageValue` and world-presentation §3.

### Tests

- `DevelopmentStageTests`.
- `StageTriptychDeltaTests`.

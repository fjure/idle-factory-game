# Contract: Presentation Models (F006)

**Scope**: Derived machine presentation, attention, completion feedback, effects, milestones and development stages.
**Requirements**: FR-001, FR-002, FR-006, FR-025 to FR-031, FR-034 to FR-036.
**Research**: R08, R09, R12, R14, R15.

## 1. Authority and the additive read value (FR-001, FR-002)

- **`CompletedCycles` counter.** `MachineStateView` gains `CompletedCycles: long`, monotonic per machine instance within a session.
  - The simulator adds the cycle's batch factor when a cycle completes.
  - The counter is 0 after session construction or restore.
  - It is never saved, never read by any simulation rule, and never used by economy, offline or prestige code.
- **Invariants:**
  - Trace and save fixtures stay byte-identical before and after the change (`CompletedCyclesNeutralityTests`).
  - The counter never decreases within a session (`CompletedCyclesMonotonicTests`).
  - `CopyMachineStates` stays allocation-free (an existing allocation test extended).

## 2. Machine presentation (FR-025, FR-027, FR-029)

- **Visual state.** `VisualState = SlotGridModel.StateFor(isActive, blocker, phase)`. There is no second precedence rule.
- **Upgrade availability.** `UpgradeAvailable = NextUpgrade.Available && NextUpgrade.Affordable`. It shows as a marker with lower salience than every problem cue: never red, smaller than a badge, and hidden while a problem badge is shown on the same machine.
- **Activity pace.** `ActivityPace` comes from `CycleTicks`:
  - Slow: at least 4 s per cycle.
  - Normal: 1–4 s.
  - Fast: under 1 s.

  The loop rate is 6, 8 or 10 fps and is checked against the Art Bible flash limit.
- **Completions.** `CompletionsDelta` is reported once per refresh. When the counter's baseline resets (a restore, or a decrease), the model reports 0.

**Tests**:
- `MachinePresentationModelTests` (Edit Mode): every state, the upgrade-marker suppression, the pace buckets, and the delta across batches, restore and a multi-cycle tick.

## 3. Attention (FR-026, FR-028, FR-030)

**Rules.** These are the rules of data-model `AttentionState`:

- A shared cause needs at least 2 machines.
- The bottleneck is the lowest coverage below 1,000,000 among consumed inputs. Ties go to the higher affected count, then the content order. If no resource is below coverage, there is no bottleneck.

**Display.**
- One world attention marker per shared cause or bottleneck, carrying the resource icon, at the link junction.
- The persistent attention indicator shows the resource icon and the affected count.
- Tapping either one selects the first affected location.
- No numeric rates appear in the world (FR-030).

**Allocation.** Recomputation happens on a revision change only, into reused buffers. An unchanged revision allocates nothing.

**Tests**:
- `AttentionModelTests` (Edit Mode): grouping, ranking, ties, no bottleneck, store full, and the system flags.
- `AttentionDisplayTests` (Play Mode): tapping the marker selects the first affected location.

## 4. Effects and allocations (FR-006, FR-029, FR-034)

**Pool.**
- The pool holds 8 effects, with at most 3 active.
- A new effect cancels the oldest. A CompletionPop outranks a Spark or Steam effect.
- CompletionPop effects coalesce within 250 ms per location.

**Reduced motion.** No effect spawns. Completion shows as a static 1 s output-icon highlight on the card or the depot.

**Steady-state allocations.** These must be 0 for the clock tick, the model refresh, the effect spawn and the card reposition (`F006AllocationTests`, Play Mode, with `GcAllocationCounter`). The 600-frame W006-V run in the Windows player is tagged `[Category("StandalonePlayer")]`.

**Tests**:
- `EffectPoolTests` (Edit Mode): cap, priority, coalescing, and no spawn under reduced motion.

## 5. Milestones (FR-035)

**Detection.** A factory-level increase between two snapshots raises `FactoryLevelUp`, carrying the newly unlocked locations. A run-id change after the prestige promotion raises `PrestigeCompleted`.

**Presentation.**
- The banner is non-blocking, lasts at most 2.5 s and can be dismissed.
- A deck-reveal effect and a 2 s foundation highlight accompany it.
- Under reduced motion, the reveal is static.

**Offline returns.** A milestone that arose during an offline return is listed in the offline summary only, and is not animated.

**Rewards.** The presentation never shows a reward the game did not grant.

**Tests**:
- `MilestoneWatcherTests` (Edit Mode).
- `MilestoneBannerTests` (Play Mode): input outside the banner is not captured.

## 6. Development stages (FR-036)

See data-model `DevelopmentStageValue` and world-presentation §3.

**Tests**: `DevelopmentStageTests`.

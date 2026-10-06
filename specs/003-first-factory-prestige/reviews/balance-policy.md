# Balance policy r1: the reference policy, bands and sensitivity bounds

**Task**: T107 (GitHub Issue #189), Game Systems/Economy with Gameplay Developer. Review lenses: Software Architect, Test/QA.
**Code**: `Assets/IdleFactory/Tests/EditMode/Balance/ReferencePolicy.cs` (the policy), `BalanceTraceRunner.cs` (the runner), `BalanceTraceTests.cs` (the gate).
**Status**: a **target** document. Every band below is a plausibility band for a hypothesis. Nothing here is a claim about current performance until a named human playtest and the owner approval (T127, R13) exist. T125 turns the bands into asserting tests for its proposed revision.

## 1. What the policy is

A deterministic player who looks at the factory at fixed instants, issues the **real application commands** (`RunCommands`) against a manual clock (`RunSession.Advance` and the offline return of `OfflineSegmenter`) and never uses a private shortcut. It is "reasonably active", not optimal. It reads only what the player sees (Cash, stock, machine levels, costs, warnings, unlocked content) plus a small mental model of the factory (section 4).

### Action latency, idle windows and the no-perfect-timing rule (ECO-2)

- **Action latency**: the policy looks every **20 s** of run time (`EpochSeconds`). A purchase that becomes affordable between two looks happens at the next look, on average 10 s late; this action latency is part of every measured time.
- **Idle windows**: the policy is away from the screen for the last 60 s of every 300 s (seconds 240 to 299 of each 300 s, 20 % of the time). It issues no command inside an idle window, including the prestige. Production continues.
- **Action budget**: at most 10 commands per look. Selling a surplus (switching auto-sell on and off again) counts as one action.
- **no-perfect-timing rule**: no command may be issued at an instant that is not a look; no rule may use a value that the player could not read at that look; no rule may wait for an event it cannot see (for example a cooldown that has not ended); timers (Items, Overdrive, contract deadlines) are used only when a look finds them ready. The policy never anticipates a boundary that falls between two looks. A policy change that adds a rule must state which look-time value it reads.

## 2. Rules, in the order of one look

1. **Deliver** the active contract when the stock covers `requiredUnits` and the deadline has not passed.
2. **Abandon** an untimed contract that has been active for 900 s.
3. **Accept** a contract when none is active and the cadence allows (`ContractCooldownSeconds`, 0 in the baseline). Among the unlocked offers whose quantity fits 33 % of the storage capacity and whose resource a built machine can supply in time (model rate; a timed offer must fit its deadline, an untimed one 900 s), take the one with the fewest completions, then the smallest market value, then the ID.
4. **Hoard**: while a contract is active its resource is kept (auto-sell off) and the machines that consume it are switched off until the contract ends.
5. **Auto-sell**: a resource that a built machine consumes is kept (auto-sell off), because a machine only progresses while its inputs are in stock (selling Ore at once starves a Smelter). Everything else is sold automatically. A kept resource whose stock reaches 25 % of the capacity is **sold at once** (auto-sell on, then off).
6. **Items**: activate every owned Item whose target exists (a machine of an allowed type that is active, or the factory) and whose conflict key is free, Tier I first.
7. **Overdrive**: activate it when it is unlocked, off cooldown and at least three machines exist.
8. **Research**: a strict queue, one entry per level (`ReferencePolicy.ResearchOrder`). The policy buys the next entry when its RP and prerequisites allow and waits otherwise.
9. **Investment** (repeat while the budget lasts):
   1. Storage: when the stock fills 60 % of the capacity or a machine shows "storage full", buy the next storage level; save for it when it is affordable within 120 s at the **observed** income.
   2. Build the next machine of the fixed order Miner, Miner, Smelter, Miner, Smelter, Press, Smelter, Assembler, Press, Smelter, Press, Assembler (3 Miners, 4 Smelters, 3 Presses and 2 Assemblers at 12 slots) while a slot is free; a locked type is skipped. Save when the machine is affordable within 90 s.
   3. When the factory is full, buy the next factory level; save for it when it is affordable within 180 s.
   4. Upgrade the machine with the best payback (`cost / income gain` in the model of section 4); save for the best one when it is affordable within 90 s, otherwise buy the best affordable one whose payback is at most twice the best.
10. **Prestige** at the first look outside an idle window at or after eligibility, then spend all IP in the order `industry.calibration`, `industry.seed-capital`, `industry.market-contacts`, `industry.toolmaker`, `industry.warehouse` (the "approved first IP purchase" of the trace; a later owner decision may change the order, not the mechanism).

The **observed income** is the Cash gained per second between two looks, averaged over about four looks. The waiting rules use it, never the model.

## 3. Pass/fail bands (ECO-2)

| Band | Measured as | Pass |
|---|---|---|
| First Gear, run 1 | the first look at which an Assembler has completed a cycle (20 s resolution) | 600 to 900 s |
| Eligibility, run 1 | the first look at which the Factory Value is at least the $10,000,000 threshold, never earlier than the policy could act | 6,600 to 7,800 s |
| Second run, run 2 | the same measure in run 2, after the first IP purchase under the same policy | 4,200 to 4,800 s |
| Runs 3 to 5 (ECO-3) | the same measure, all IP spent after every run | non-increasing from run 1 to run 5, and every run at least 25 % of run 1 |

A run that has not reached the threshold after 21,600 s of play (excluding absences) is reported as "not reached" and fails its band.

The CSV rows at 600, 900, 1,800, 3,600, 5,400 and 7,200 s (10, 15, 30, 60, 90 and 120 minutes) report the state in between. SC-003 and SC-004 name 10 to 15 minutes for the Gear and about 120 minutes for the first prestige as targets; the bands above are the plausibility window of the same targets.

## 4. The mental model (why the policy invests where it does)

Continuous flows at the table speed of every machine (no Items, Research, Industry, Overdrive or storage limit), upstream machines served first, everything that no built machine consumes sold at the base price. It uses `decimal` arithmetic so that every host decides the same. The model only ranks upgrades; every purchase goes through the real command and is paid at its real cost.

## 5. Sensitivity variants and bounds (ECO-4, FR-025)

Each variant runs the same policy once from a fresh run 1 and is compared at the Factory Value milestone of **$250,000** (`SensitivityMilestoneFv`), because the $10,000,000 threshold is not reached in every variant. The play time is the wall time to the milestone minus the absences before it.

| Variant | Change | Bound (play time against the baseline) |
|---|---|---|
| `var.price-80` | every market price x 0.8 (Contract rewards and costs unchanged) | within +/- 250 per mille |
| `var.price-120` | every market price x 1.2 | within +/- 250 per mille |
| `var.contracts-off` | the policy accepts no contract | -50 to +250 per mille |
| `var.contracts-slow` | the policy waits 600 s after a contract ended | -50 to +250 per mille |
| `var.overdrive-off` | the policy never activates Overdrive | 0 to +150 per mille |
| `var.absence-30m` | an eight-hour absence from minute 30 | play time at least 500 per mille (the absence may remove at most half of the baseline play time) |
| `var.absence-60m` | an eight-hour absence from minute 60 | at least 500 per mille |
| `var.returns` | away 10 minutes of every 15, from minute 10 (repeated short returns) | wall time 600 to 1,600 per mille |

`probe.ore-only` is **not** a sensitivity variant and has no bound: it builds Miners only and sells Ore, and it records how fast the content rewards the dominant strategy that `content-record-r1.md` section 2 predicts (ECO-9: a finding, not a policy).

A material offline effect would be a new owner decision, never a silent change to offline policy revision 1.

## 6. The scaled scenario

When the real catalog does not reach the threshold, the second run, the runs 3 to 5 projection, the Item accrual and the SC-001 chain would stay unmeasured. The runner therefore also plays the **same game with the prestige threshold divided by 100** ($100,000, IP divisor 10^5, so that the first award stays 10 IP; `scaled.run1` to `scaled.run5`). It changes nothing else, issues the same commands and proves the mechanics of the chain; it makes **no claim about pacing**. Its run times are reported next to the real ones and are never compared with a band.

## 7. Invariants checked by the runner (SC-001)

Every command that moves Cash, RP, IP, stock or Items is checked against its exact expected change: a build, upgrade or expansion changes Cash by its cost only; a delivery consumes its units, adds its Cash, RP and Item rewards exactly once; a Research purchase moves RP only; an Item activation consumes one Item; an Industry purchase moves IP only; a prestige awards exactly the previewed IP, keeps every Item, and resets exactly the list of `PrestigeTransition.ResetList`. Cash and stock of the delivered resource may grow beyond the command's own change because every command ends with a settle (a machine that waited for room completes); they may never fall short. Any other deviation is a violation and fails the gate.

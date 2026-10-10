# Balance policy r2: the reference policy, strategies and ensemble

**Task**: T301 (GitHub Issue #363, work unit WU02), Game Systems/Economy (author). Review lens: Game Designer/Product Owner.
**Implemented by**: T311 (framework and first strategies) and T341 (policy r2, remaining strategies, ensemble, gates) in `Assets/IdleFactory/Tests/EditMode/Balance/`. This document contains no code.
**Builds on**: `specs/003-first-factory-prestige/reviews/balance-policy.md` (policy r1) and `Assets/IdleFactory/Tests/EditMode/Balance/ReferencePolicy.cs`. Every r1 rule that this document does not change stays as it is.
**Sources**: spec FR-022, FR-023, FR-024, FR-021, SC-002, SC-004, D-C, D-D, D-E, D-J, D-K, D-L, D-O, D-P; research F5-R13, F5-R17 (ECO-9, ECO-12, ECO-13, ECO-14), F5-R19, F5-R20 (MIN-6, MIN-9); plan step 13; plan reviews ECO-13, ECO-14.

## 0. Status and orientation-only statement

**The outputs of policy r2 are orientation, not a promise.** The only asserted gates are the FR-024 list (section 8.2). Minute bands (first Gear, eligibility, second run), the casual ratio, the idle-heavy results, all spreads, the contract share of income, the reward-to-effective-value ratio per delivery, the mining income share, the `mining-only` probe, the cumulative-time metric and every trace warning of section 8.3, unspent IP, and the sensitivity bounds are reported and never fail a test (FR-023, FR-024, FR-025, D-A). A reported number may inform the owner's approval of `contentRevision 3` (FR-027) but is not a claim about how a human plays.

Where a value is listed below, its origin is marked:

- **[catalog]**: read from the loaded `RunCatalog` at run time. The policy never hard-codes it.
- **[pinned]**: a constant of the policy, written once in `ReferencePolicy` and frozen. A change needs a new policy revision (r3) and an explicit decision, not a silent edit.
- **[state]**: read from the session or the application snapshot at the look.

## 1. What the policy is

Policy r2 is the same kind of player as r1 (r1 section 1): a deterministic player who looks at the factory at fixed instants, issues the real application commands against a manual clock, never uses a private shortcut, and reads only what a player sees (Cash, stock, machine levels, prices, blockers, unlocked content, the snapshot). All arithmetic that ranks choices uses `decimal` and ordinal string comparison so that every host decides the same. There is no randomness anywhere. The no-perfect-timing rule of r1 stays: no command at a non-look instant (the one exception is the held mining click of section 6), no use of a value the player could not read at that look, no waiting for an event that cannot be seen, and a rule that is added states which look-time value it reads.

r2 changes five things (FR-022): income as net-worth growth (section 3), storage on `StorageFull` blockers (section 4), IP spent greedily by marginal gain per IP (section 5), a balanced machine mix derived from recipe rates (section 2), and use of `Sell` and auto-deliver (sections 3 and 4). It also adapts to F005 contract features (cooldowns, bonus offers, production objectives) and to mining (D-O, D-P).

## 2. Look cycle, command order and the balanced mix

### 2.1 Timing constants

| Constant | Value | Origin |
|---|---|---|
| `EpochSeconds` (look interval L) | 20 s of run time | [pinned], r1 |
| `IdleCycleSeconds` / `IdleStartSeconds` | 300 / 240: no commands in seconds 240 to 299 of each 300 s of run time | [pinned], r1 |
| `ActionsPerLook` | 10 commands per look. `Mine` and the held click (section 6) do not count. | [pinned], r1 |
| `WaitForMachineSeconds` / `WaitForStorageSeconds` / `WaitForFactorySeconds` | 90 / 120 / 180 | [pinned], r1 |
| `AbandonUntimedAfterSeconds` | 900 | [pinned], r1 |
| `ContractCapacityPercent` | 33 | [pinned], r1 |
| `DumpPercent` / `DumpTargetPercent` | 25 / 10 (new target, section 4.3) | [pinned] |
| `ObservedIncomeLooks` | 4 | [pinned], r1 |
| `RunLimitSeconds` | 21,600 (6 simulated hours) | [pinned], FR-024 |

The look grid, the idle window and the run-time clock restart at second 0 of every run. A look happens at run second `offset + k × L` (k ≥ 0), with the offset of the ensemble member (section 8.1). The offset moves only the look grid, never the idle window (ECO-14). The idle window is a function of the run second (`IsIdle(runSecond)`), not of the look index. At an idle look the policy only observes (it updates the observed-income history and checks eligibility); it issues no command, including prestige and the held click.

### 2.2 Order of one look (non-idle)

Each rule below may stop when the 10-command budget is spent. The order is fixed:

1. **Deliver**: if a delivery contract is active, auto-deliver is off, and stock of its resource ≥ `requiredUnits` [state], issue `DeliverContract`. (With auto-deliver on, the simulator has already delivered, section 4.2; this rule is the fallback only.)
2. **Abandon**: an untimed active contract (no deadline) accepted at least `AbandonUntimedAfterSeconds` ago is abandoned. This applies to delivery and production objectives alike.
3. **Accept** (section 4.1), followed in the same look by `SetAutoDeliver(true)` when the accepted objective is a delivery.
4. **Auto-sell configuration and surplus** (section 4.3).
5. **Items**: unchanged from r1 (activate every owned Item whose target exists and whose conflict key is free, lowest tier first, then ordinal id).
6. **Overdrive**: unchanged from r1 (unlocked, off cooldown, at least 3 machines).
7. **Research**: unchanged from r1 (strict queue `ResearchOrder` [pinned, r1 list]; buy the next entry when RP and prerequisites allow, otherwise wait).
8. **Investment** (section 4.4): storage, the next machine of the balanced mix, factory level, machine upgrades by payback.

The prestige and the Industry purchases are handled between runs (section 5). Prestige happens at the first look that is not idle at or after eligibility, before any other rule of that look. After the prestige and the IP spending, the new run starts at its second 0 and its own look grid.

### 2.3 Balanced machine mix (replaces the fixed `BuildOrder` of r1)

The mix is derived from the recipes of the catalog [catalog]: recipe inputs, outputs, `DurationTicks` and each machine's level-1 speed (`SpeedTable[0]`, ppm). It is recomputed on every build decision for the current `SlotCount` [state], so it follows factory levels without any hard-coded counts. Algorithm (decimal, deterministic):

1. **Terminal**: the machine type whose first output has the highest base price among the types in scope. In scope are the types unlocked now or later in the run's content and, for stop-at-Bar and stop-at-Plate, only the types whose output tier is at or below the stop resource (section 7). Ties go to the lower ordinal machine id.
2. **Rates and machine counts (one unit: executions per second, "exec/s")**: let `e(m)` be the exec/s of one level-1 machine of type m: `e(m) = (SpeedTable[0] / 10^6) / (DurationTicks / 10^7)`. Fix the terminal's required rate at `R(terminal) = 1.0` exec/s. Walk the chain upstream in descending order of output price. For each machine type m (not the terminal) with output resource r, `R(m) = Σ over downstream types d of ( R(d) × amount of r consumed per execution of d ) / (amount of r produced per execution of m)`. Every rate is in exec/s; the division by the amount produced per execution converts "units of r per second" into exec/s of m. The (real) machine count of every type is `count(m) = R(m) / e(m)`. For a recipe with several outputs, the output that feeds the chain is r, and the other outputs are treated as sold surplus.
3. **Counts for S slots**: scale the machine counts of step 2 by one common factor so that they sum to S, then give each type `floor(scaled count)` and distribute the remaining slots by the largest fractional part (ties: the type earlier in the upstream walk, then ordinal id). A type with a scaled count below 1 still receives one slot when S allows (that is, when S ≥ the number of types in scope); otherwise types are dropped from the top of the chain price order (the terminal is dropped last).
4. **Build sequence** (which machine goes into the next free slot, lowest free slot index first): among the in-scope types that are unlocked now and whose owned count is below their target count, pick the type with the largest deficit `target_count × (owned_total + 1) / S − owned_of_type` evaluated in decimal. Ties: the type with the lower output price (upstream first), then ordinal id. If no unlocked type is below its target (a locked type holds the deficit), the policy builds nothing in this branch and continues with the next investment rule; it never builds a surplus type to fill a gap.
5. **Ore start**: while the run has no Miner and Miner is in scope, the next machine is a Miner regardless of the deficit (the first-Miner reachability of D-J/D-O is a content property, FR-016).

The mix is an intended output of the recipes, so a content change in recipes or durations (for example for `chain.dominance`, ECO-12) changes the mix without any change to the policy. Ore-only (section 7) is the scope = {Miner}.

## 3. Income as net-worth growth (FR-022)

- **Net worth** NW = Cash + Factory Value, both taken from the application snapshot at the look [state] (the same quantities as the conservation rule FR-015). The policy does not recompute Factory Value.
- **Observed income** per second at a look = (NW_now − NW_at_look_(k−1)) / L, averaged over the last `ObservedIncomeLooks` looks that exist (fewer at the start of a run), and floored at 1 for use as a divisor. The look grid advances through idle looks too, so an idle look still records an NW sample.
- Why: Cash alone drops with every purchase and rises with every sale, so Cash-per-second misjudges waiting times right after a build. NW is neutral under `Sell` (sale value neutrality, FR-002) and under build and removal within the strict refund rule (FR-015), so a purchase or a sale does not look like income or loss.
- **Where observed income is used**: every "affordable within N seconds" waiting rule of section 4.4 divides the missing Cash by it, exactly as r1 divided by observed Cash income. The fluid model of r1 (section 4 of r1: continuous flows at table speeds, upstream first, leftovers sold at the base price) remains the ranking model for upgrade payback; only the waiting horizon changes to NW growth.
- **Reported, not a rule**: the trace also records NW growth per minute per strategy for the orientation tables.

## 4. Commands the policy issues

### 4.1 Contract acceptance (replaces r1 rule 3)

When no contract is active and the budget allows, consider every offer `o` that the snapshot shows as Available [state]: unlocked, not on cooldown (D-D/F005 cooldowns; the policy reads `readyAt` only as shown, never anticipates it) and, for bonus offers, inside its window. Filter:

- **Capacity (ECO-3)**: `required × 100 ≤ capacity × ContractCapacityPercent` (33 %) and `required ≤ current capacity` (so that `contract.capacity` is never triggered). The cap `quantity.cap` is [catalog].
- **Feasibility**: a built machine produces the resource (fluid-model gross rate > 0). For a delivery objective, the time to cover `required − stock` at that rate must fit the deadline (if timed) or `AbandonUntimedAfterSeconds` (if untimed). For a production objective (`Produced` counted from the accept instant), the time to produce `required` units at the rate must fit the same limits.

Choose among the remaining offers by this strict order:

1. Bonus offers (inside their window) before standing offers, because they are time-limited and carry the reserved higher ratios (D-D). Among several bonus offers: the earliest `WindowEndUtcMs`, then ordinal id.
2. Among standing offers: the fewest completions (the Item ladder rewards early completions of every offer, ECO-11), then the smallest base market value of the required stock (`required × BasePrice` [catalog]), then ordinal id.

Then issue `AcceptContract`. If the accepted objective is a delivery, issue `SetAutoDeliver(true)` in the same look (a second command in the budget). Contracts with `ContractsEnabled = false` (market-only chain, Ore-only, section 7) never accept.

### 4.2 Auto-deliver and the reserve (D-L)

The policy always switches auto-deliver on for delivery contracts, so the delivery happens at the first tick at which `stock ≥ requiredUnits`, not at the next look. This removes the "stock sits idle until the next look" delay of r1 and is the player behaviour the feature intends (Item uptime, deadlines). The simulator reserves the required stock from auto-sell for every active delivery contract, whether or not auto-deliver is on (D-L), so r1's hoard rule (switch auto-sell off for the contract resource) is dropped. r1's second part stays: while a contract is active, every machine whose recipe consumes the contract resource is switched off (`SetActive(false)`) and switched on again when the contract has ended, so that the needed units are not consumed downstream.

### 4.3 Auto-sell and `Sell` (replaces r1 rules 4 and 5)

At each non-idle look, for every unlocked resource in ordinal id order:

- **Kept** if a built machine's recipe consumes it (it must pile up as input); **sold** otherwise. A resource is "sold" when auto-sell is on and "kept" when off. Issue `SetAutoSell` only on a change.
- The contract reserve is handled by the simulator (4.2), so a resource of an active contract is treated like any other resource here.
- **Held-off counter (P6)**: the trace counts, per run, the machine-seconds during which a machine was switched off by the 4.2 rule (the sum over machines of the seconds `SetActive(false)` held by an active contract), and reports it per strategy and run index as orientation. It is a measure of what the contract lever costs the chain.
- **Surplus dump with `Sell`** (replaces the auto-sell on/off trick of r1): for a kept resource whose stock reaches `DumpPercent` of the capacity, issue `Sell(resourceId, quantity)` with `quantity = stock − max(ceil(DumpTargetPercent × capacity / 100), reservedForActiveContract)`. If `quantity < 1` nothing is issued. The quantity is exact, not rounded to the UI's `sell.steps`: the steps are a UI affordance and the command takes any `quantity ≥ 1`. One `Sell` is one command in the budget.
- **Market-only, Ore-only, and the sold terminal**: the end product of the scope (the terminal resource) has auto-sell on, so everything the chain makes is sold at once unless a contract reserve holds it (4.2).
- **`Sell` is never used to liquidate for a purchase decision** and never to make NW look larger: by FR-002 it cannot change Factory Value.

### 4.4 Investment (replaces r1 rule 9)

Repeat while the budget lasts. Observed income below means section 3. Prices are the effective prices after modifiers, from the snapshot or from `TryUpgradeCost` at the look [state/catalog].

1. **Storage on a `StorageFull` blocker**: if the snapshot reports at least one machine blocked with the `StorageFull` reason, or the best otherwise-eligible offer was rejected only for `contract.capacity`, and a next storage level exists, then buy it when Cash ≥ its price. When it is not yet affordable but `(price − Cash) / observed income ≤ WaitForStorageSeconds`, end the investment step for this look (save for it). Otherwise continue with rule 2. The r1 trigger "stock fills 60 % of capacity" is dropped: storage is bought for a blocker, not on a guess.
2. **Next machine of the balanced mix** (section 2.3) while a slot is free: buy when affordable; save when `(price − Cash) / observed income ≤ WaitForMachineSeconds` (end the step); otherwise continue.
3. **Factory level**: when no slot is free and a next factory level exists, buy it when affordable; save when affordable within `WaitForFactorySeconds`; otherwise continue. Buying a level recomputes the mix for the new slot count at the next decision.
4. **Machine upgrades by payback**: candidate = (an owned machine below its maximum level). Payback = `cost / gain`, where gain is the increase of fluid-model income from raising that machine's speed to the next level. Gain ≤ 0 are not candidates. Pick the lowest payback; ties by ordinal instance id of the machine type, then lower slot index. If it is not affordable and `(cost − Cash) / observed income ≤ WaitForMachineSeconds`, save (end the step); otherwise buy the cheapest affordable candidate whose payback is at most twice the best payback (ties: lower payback, then ordinal id). Else end the step.

Removal and rebuild is never used by the policy.

## 5. IP spending: greedy by marginal gain per IP (FR-022, FR-021, D-C)

After each prestige, before the first look of the new run, spend IP in a loop until nothing is bought:

1. For each Industry upgrade `u` [catalog] whose next level `ℓ+1` exists and whose IP cost `ipCost(u, ℓ)` [catalog] ≤ current IP [state], compute `gain(u)` = the reduction of **fluid-model time to the next threshold** (seconds) that buying that level causes (section 5.1).
2. Score = `gain(u) / ipCost(u, ℓ)` (decimal). Candidates with `gain ≤ 0` are **never bought**, in any order or at any later point: a level the model cannot value is not purchased (content guarantees that no purchasable level has zero effect, `speed.level.dead`, so a zero gain only means that the effect is invisible to the model). IP that remains because no candidate has a positive score stays **unspent** for the rest of the run and carries into the next prestige; it is not a failure. The trace reports the **unspent IP per run** (IP held after the spending loop of that run, per strategy and run index) and the list of upgrades whose gain was 0, so that a model blind spot is visible.
3. Buy the best score. Ties: the lower IP cost, then ordinal upgrade id. Repeat with the updated levels and IP.
4. The loop ends when no candidate has a positive score or IP is below every candidate's cost.

There is no fixed order and no hard cap on levels (D-C). The r1 `IndustryOrder` is retired.

### 5.1 The fluid-model time to threshold

`T_fluid(levels)` is the time a closed-form deterministic player needs, in the fluid model of r1 section 4 extended by the Industry modifiers, to go from the run start (starting Cash [catalog], no machines, storage and factory at their start levels) to the run's threshold `T(index)` [catalog, FR-019] under: the balanced mix of section 2.3, builds at the effective prices with the build-price index of the upcoming run, machine upgrades by payback, no Items, no contracts, no Research, no Overdrive, and no storage limit. Industry effects enter through the modifier pipeline of the catalog (speed, sale price, build and upgrade discounts, Seed Capital start Cash, `manualYield`). It is evaluated on the same catalog and the same decimal arithmetic as the rest of the policy.

- **`manualYield` term (FR-043)**: the model adds mined Ore as an income source of `mineOrePerSecond = effectiveYield × capPerSecond / 1000` Ore/s (the effective yield is the milli-yield of D-P after the composed `manualYield` ppm, [catalog], and `capPerSecond` is the catalog click cap), sold at the Ore base price, in the time the strategy mines: the first-Miner window (from run second 0 until the first Miner exists in the model) for every strategy, and additionally over the whole active time (the share `1 − idle share` of each 300 s) for `chain+mining`, at that strategy's pinned fraction. Mining is not modelled when the strategy does not mine. A `manualYield` upgrade therefore has a positive gain exactly when it shortens the first-Miner window or adds mining income.
- **Capacity (warehouse) stays 0, deliberately**: the model has no storage limit, so extra capacity has no effect in it and a capacity-only upgrade has gain 0 and is never bought. This is justified because the reference policy buys storage with Cash on `StorageFull` blockers (4.4) and the Industry capacity effect would only save that Cash, which the model cannot value without a storage limit. The consequence (unspent IP while only capacity candidates remain) is reported through the unspent-IP and zero-gain lists above, not hidden. T345 may replace the model with its pre-solve (plan step 16) under a new policy revision r3.
- **Memoisation and parallelism (P9)**: `T_fluid` is a pure function of (Industry levels, prestige index, catalog hash, strategy mining fraction). Implementations memoise it on that key (a spending loop evaluates the same state for several candidates), and the ensemble histories are independent, so they may run in parallel; results must not depend on the order of completion.

The threshold, the IP awards and the 4.0× practical ceiling are circular (threshold → FV at prestige → IP → spending → the next time to threshold). The policy does not solve the circle: it spends exactly the IP that the trace awarded. The circularity is the topic of the T345 tuning record.

## 6. Mining (D-O, D-P, F5-R19, F5-R20)

Mining is `Mine(activations)`; the simulator caps counted activations per timeline second. The reference player holds the click, which is a continuous activity and therefore the only exception to "commands only at looks":

- At every whole run second `s` that is not idle (`IsIdle(s)` is false) and at or after the first look, the policy issues `Mine(n)` with `n = round_half_up(fraction × capPerSecond)` [capPerSecond: catalog], before the simulator advances to `s + 1`. These held clicks do not count against `ActionsPerLook`. They stop at the look at which the first Miner is built **for strategies with `MiningUntilFirstMiner`**, and continue through the whole active time for chain+mining and mining-only (section 7).
- Every strategy that builds Miners mines at `fraction = 100 %` of the cap from second 0 of each run until its first Miner is built (the start-window assumption of FR-016 and SC-011, with constant starting Cash and no Seed Capital in the check). The mining happens in active seconds only.
- Mined Ore never advances a production objective (MIN-5), and no mining happens offline.
- The pinned fractions are in section 7. The policy never reads the transient remainder or the rate counter.

## 7. Strategies (FR-023)

All strategies run runs 1 to 5 through prestige and IP spending as in section 5 (except where stated), use the same engine, the same catalog and the same look rules, and differ only in the levers below. "Chain" is policy r2 unchanged. A strategy that does not reach a threshold within `RunLimitSeconds` (21,600 s of run time) is reported as "not reached" for that run index and offset.

| Strategy | Id | Decision rules (delta against the chain) | Pinned constants |
|---|---|---|---|
| Chain (reference, "prestige at the threshold") | `chain` | Policy r2 sections 2 to 6. Prestige at the first non-idle look at or after eligibility. Mines until the first Miner (section 6). | none |
| Ore-only | `ore-only` | Scope = {Miner}. Only Miners are built (balanced mix of one type). Upgrades by payback among Miners. Storage on blocker. No contracts. All Ore sold by auto-sell. Prestige at threshold. Research, Overdrive and Items keep the r1 behaviour restricted to in-scope machines (a purchase or activation whose target is not a Miner or the factory is skipped); the mix is the only lever. | none |
| Contracts-only | `contracts-only` | Chain machine mix. Auto-sell is off for every resource; `Sell` is never issued; the only Cash from the factory is contract rewards. Contracts are accepted continuously (4.1) with auto-deliver on. Surplus stays in stock; when the store is full the production blocks (the policy still buys storage on blockers). Prestige at threshold. | none |
| Idle-heavy | `idle-heavy` | Chain, but the player is away most of the time: idle window is seconds 120 to 299 of each 300 s (60 % idle). Everything else unchanged. Held mining only in active seconds. | `IdleStartSeconds = 120` |
| Casual pace | `casual` | Chain, but the look interval is `L_casual` instead of 20 s, and the held click mines at `CasualMiningFraction` of the cap. The idle window is the reference one. The five offsets scale proportionally to the casual grid: `0, L_casual/5, 2·L_casual/5, 3·L_casual/5, 4·L_casual/5` (integer division, in seconds), so that the five phases cover the same fractions of a look interval as 0, 4, 8, 12 and 16 s cover of 20 s. Reported without pass/fail (orientation, FR-023). | `L_casual` and `CasualMiningFraction = 25 %` |
| Prestige later | `prestige-later` | Chain, but the prestige is delayed: prestige at the first non-idle look at which Factory Value ≥ `LaterFactor × threshold` (or at the `RunLimitSeconds` limit, whichever comes first). IP spent as in section 5. It runs twice, once per factor (ids `prestige-later-1.5` and `prestige-later-3`). Both are reported; the 4.0× rule takes the maximum IP over both. The cumulative-time metric and warning of 8.3 compare it with the chain. | `LaterFactorPpm = 1,500,000` (1.5×) and `3,000,000` (3×) |
| Stop at Bar | `stop-at-bar` | Scope = machine types whose output tier is at most Iron Bar (Miner, Smelter). The Bar is the terminal and is sold. Contracts: only the offers that pass the feasibility filter (4.1), which for this scope excludes Plate and higher. Prestige at threshold. | none |
| Stop at Plate | `stop-at-plate` | Scope = Miner, Smelter, Press; the Plate is the terminal and is sold. Same contract filter. Prestige at threshold. | none |
| Market-only chain | `market-only-chain` | Chain with `ContractsEnabled = false`: no `AcceptContract`, no `SetAutoDeliver`; Items from the ladder are not earned. Prestige at threshold. | none |
| Chain + mining (F5-R20) | `chain+mining` | Chain, but the held click mines at `ChainMiningFraction` of the cap in every active second of the run, runs 1 to 5. Gate: not faster than the chain by more than `miningAdvantageMaxPpm` (catalog) in time to each threshold (MIN-9). | `ChainMiningFraction = 100 %` |
| Mining-only (F5-R20, orientation) | `mining-only` | Builds nothing. Mines at 100 % of the cap in every active second, sells by auto-sell, buys Industry as in section 5. Expected "not reached" or far below the chain; reported. | none |

**Single-lever set** (ECO-13): `ore-only`, `contracts-only`, `stop-at-bar`, `stop-at-plate`. This is the set of FR-024's gate "no single-lever strategy faster than the chain" and of SC-002. `market-only-chain` and `idle-heavy` are not part of the set. The nine FR-023 strategies are chain, ore-only, contracts-only, idle-heavy, casual, prestige-later (two factors), stop-at-bar, stop-at-plate and market-only chain; chain+mining and mining-only are the two additional F5-R20 strategies.

### 7.1 The casual calibration (pinned constant)

`L_casual` is the one fixed integer number of seconds such that, for **policy r2** (including the 25 % held mining click of the casual strategy) on **content revision 2 values** (the content of the T311 report), the median over the five offsets of the Factory Value at run second 900 (minute 15) of run 1 is **at most 1/2.5 (0.4)** of the median Factory Value of the `chain` strategy at the same instant, with the smallest such integer chosen from a search upward starting at 40 s in steps of 1 s. T311 performs this search once, writes the resulting integer into `ReferencePolicy` as a [pinned] constant with its derivation in a comment, and it is **never recalibrated** by a later content revision, a policy change, or the T345 tuning (so the casual ratio drifts and is reported as orientation, which is what FR-023 asks for). The constant is not derived in this document because the design cannot compute Factory Value without running the engine; the procedure and the pin rule are the decision (Open question Q1). The reported number is the observed ratio `FV15_casual / FV15_chain` with its spread.

## 8. Ensemble and asserted gates (FR-023, FR-024, ECO-14)

### 8.1 Ensemble

- **Offsets**: five look-grid offsets in seconds, **0, 4, 8, 12, 16**. An offset `o` shifts the first look of every run to run second `o`; the following looks are `o + k × L`. The idle window and the run clock are not shifted.
- **Histories**: one independent history per (strategy, offset): runs 1 to 5 played in sequence, each starting from the previous prestige and its IP spending. Histories do not share state.
- **Median per run index**: for run index `n` (1 to 5), the value is the median of the five offsets' times to threshold at run `n`. Times are compared as numbers with "not reached" ordered above every number. The median of five values is the third smallest. If three or more offsets are "not reached", the median is "not reached". The spread reported is (min, max) of the five.
- **Time to threshold** = the run second of the first grid tick (including idle ticks) at which Factory Value ≥ the run's threshold [state], never earlier than the policy could act. The prestige itself happens at the first non-idle look at or after that tick.
- **Reported per (strategy, run index)**: median, min, max, Factory Value at run seconds 600, 900, 1,800, 3,600, 5,400, 7,200 (median), IP awarded, number of contract deliveries, contract share of income (Cash from deliveries over Cash from deliveries plus sales), the reward-to-effective-value ratio per delivery, mining Ore share of total Ore, the observed NW growth per minute, the machine-seconds held off by contracts (4.3), unspent IP per run (5), and the cumulative elapsed time of 8.3.
- **Runtime**: before the matrix size is committed, T341 estimates the full matrix from the measured r2 run time (PF-15). The matrix is 12 strategy definitions (the 11 rows of section 7, with prestige-later counted at its two factors) × 5 offsets × 5 runs = 300 run-plays. Order of measures if the projected runtime exceeds the 20-minute `BalanceFull` budget: (1) memoise `T_fluid` (5.1) and run the independent histories in parallel; (2) if still over, move only the F5-R20 `mining-only` strategy to an opt-in category; (3) if still over, escalate to the owner. An FR-023-named strategy is never moved, and the offset count is never reduced below five, in this document. Opt-in strategies must be run, with the result recorded, before the FR-027 approval of `contentRevision 3`.
- **Fast gate** (default Edit Mode run): chain and Ore-only, one offset (0), runs 1 and 2.

### 8.2 Gates asserted by the trace (FR-024; everything else is orientation)

| Gate | Definition in terms of the ensemble |
|---|---|
| Conservation and exact per-command deltas | Every command is checked as in r1 section 7; any violation fails. |
| Sale value neutrality (FR-002) | Every `Sell` the policy issues changes Factory Value by 0. |
| Auto-deliver partition invariance | Replaying a trace with the advances partitioned differently gives the same state (SC-003). |
| Restated ECO-3 (FR-021) | Under `chain`: `T_chain(n+1) ≤ T_chain(n) + L` for n = 1…4 and `T_chain(n) ≥ 0.25 × T_chain(1) − L` for n = 2…5, on the medians, with the tolerance of **one look interval L (20 s)**. |
| Contract ratio bounds (FR-024, SC-001, D-D) | On base **and** effective prices, for every delivery in the trace. **Standing offers**: reward / base value of the delivered stock ≤ 1.5, and reward / effective value (after the sale-price modifiers at the completion instant) is in [1.0, 1.5]. **Bonus offers**: reward / base value is > 1.5 and ≤ 2.0 (the range D-D reserves for them). If the content (`maxRewardRatioPpm` and the offer ratios, [catalog]) defines the bounds differently, the bounds of the spec/D-D text win and the catalog values are checked against them. |
| Item uptime (ECO-6) | As defined in the plan for the ladder (`ladder.uptime`). |
| Remove/rebuild (ECO-7) | Removing and rebuilding never raises Cash plus Factory Value (FR-015). |
| Start affordability | First Miner reachable at each prestige index by mining (FR-016, SC-011). |
| Chain dominance per slot | `chain.dominance` (ECO-12). |
| No single-lever faster than the chain | For each strategy in {ore-only, contracts-only, stop-at-bar, stop-at-plate} and each run `n` = 1…5: `median(strategy, n) ≥ median(chain, n)` (a tie counts as not faster). Contracts-only must be strictly longer (SC-002). The comparison has no tolerance. |
| Chain reaches every threshold within 6 h | For every offset and every run 1 to 5: the time to threshold ≤ `RunLimitSeconds`. |
| 4.0× rule (FR-021, ECO-9) | The practical Industry ceiling ≥ 4.0 × the **highest** IP award over runs 1 to 4, taken as the maximum over the five offsets and over the strategies `chain`, `prestige-later-1.5` and `prestige-later-3` (the worst case, not the median). |
| Chain+mining (F5-R20, MIN-9) | `median(chain, n) − median(chain+mining, n) ≤ miningAdvantageMaxPpm × median(chain, n) / 10^6` for n = 1…5 (`miningAdvantageMaxPpm` [catalog]). |

The gate list is the FR-024 list plus the F5-R20 chain+mining gate and the FR-021 4.0× rule, which F5-R13/F5-R17 place in the trace. Minute bands, the casual ratio, idle-heavy, prestige-later results other than the 4.0× input, market-only chain, mining-only, all shares and every warning of 8.3 are **not** gates.

### 8.3 Orientation metrics and trace warnings (never gates)

- **Cumulative time (P2)**: for each strategy and offset, `C(n)` = the elapsed play time from the start of run 1 to the prestige that completes run n, including the wait after eligibility until the prestige and every earlier run (so for prestige-later the delay is counted). Reported as the median over offsets with its spread, for `chain`, `prestige-later-1.5` and `prestige-later-3`, n = 1…5.
- **Warning W-later**: printed when, for some n, `median C(n)` of a prestige-later factor is smaller than that of `chain`. It means that waiting beats prestiging at the threshold, which weakens the meaning of the threshold; the owner reads it at the FR-027 approval.
- **Warning W-market (P4a)**: printed when `market-only-chain` has a median time to threshold below `chain` at some run index (contracts then cost time).
- **Warning W-mining (P4b), loud**: printed prominently when `mining-only` reaches a threshold at all, or beats `chain` at any run index. This is the D-P dominance concern. It stays orientation unless the spec makes it a gate.
- **Report (P4c)**: for `contracts-only`, the share of its income that comes from contracts (always 100 % by construction, reported as the Cash per minute) and the run index and run second at which it stalls (production blocked on a full store with no deliverable contract for a whole look interval, or "not reached"); and, for every strategy, the contract share of income of 8.1.
- **Counters**: the machine-seconds held off by contracts (4.3) and the unspent IP (5).

## 9. Interaction with the content revision gating (ECO-4)

Until `contentRevision 3` is tuned (T345), the policy runs on the transitional v3 content with revision 2 values (T311, T357); gates that depend on revision ≥ 3 semantic rules are reported there, not asserted. The policy text is revision-independent: no value is read from the content other than as marked [catalog].

## 10. Traceability

| Section | Requirement / decision |
|---|---|
| 0, 8.2 | FR-023 (orientation), FR-024, FR-025, D-A; F5-R13 |
| 2.3 | FR-022 (balanced mix), D-E, ECO-12 |
| 3 | FR-022 (net-worth growth), FR-002, FR-015 |
| 4.1 | FR-022, D-D, ECO-3 (`contract.capacity`), ECO-11, F5-R17 |
| 4.2 | FR-022 (auto-deliver), D-L, ECO-12 (reserve), F5-R17 |
| 4.3 | FR-022 (manual selling), FR-002, F5-R05 |
| 4.4 | FR-022 (storage on `StorageFull`), D-J |
| 5 | FR-022 (parameterised IP), FR-021, D-C, ECO-9, F5-R17 policy r2 Industry |
| 6 | D-O, D-P, F5-R19, F5-R20 (MIN-5, MIN-6, MIN-9), FR-016, SC-011 |
| 7 | FR-023, ECO-13, SC-002 |
| 7.1 | FR-023 (casual), ECO-14 |
| 8.1 | FR-023, ECO-14, F5-R13, F5-R17, PF-15 |
| 8.2 | FR-024, FR-021, SC-001, SC-002, SC-004, D-D |
| 8.3 | FR-023 (orientation), FR-024, D-P, review findings P2, P4, P6 |

## 11. Owner questions and review status

Defaults below let T311 and T341 proceed. Items marked "accepted by Product/Game Systems review" are settled for r2 and are listed for the record.

- **Q1 (casual constant)**: accepted by Product/Game Systems review. T311 finds the smallest integer `L_casual` (policy r2, 25 % casual mining, content revision 2 values) and pins it; no recalibration (section 7.1).
- **Q2 (offsets vs the 20-minute budget)**: resolved by parent decision. Keep five or more offsets; memoise and parallelise first; then move only `mining-only` to an opt-in category; then escalate to the owner. Opt-in strategies run before the FR-027 approval (section 8.1). Not decided in this document beyond that order.
- **Q3 (contracts-only definition)**: accepted by Product/Game Systems review. Chain mix, auto-sell off everywhere, no `Sell`.
- **Q4 (idle-heavy and prestige-later values)**: values accepted (`IdleStartSeconds = 120`; prestige-later factors 1.5× and, added by P2, 3×). The measure changed per P2: the cumulative time of 8.3 is the reported comparison, not the single-run time.
- **Q5 (stop-at-Bar and stop-at-Plate keep feasible contracts)**: accepted by Product/Game Systems review.
- **Q6 (ECO-3 floor tolerance applies to the 25 % floor as well)**: accepted by Product/Game Systems review.
- **Q7 (4.0× rule uses the maximum over offsets, runs and both prestige-later factors)**: accepted by Product/Game Systems review.
- **Q8 (Industry model gain)**: resolved in r2. `manualYield` has an explicit fluid term; capacity stays 0 by stated design; unspent IP and zero-gain upgrades are reported (sections 5 and 5.1).
- **Q9 (held mining click) - owner question, open**: confirm (a) that holding the click at 100 % of the cap for the whole run (chain+mining, mining-only) is the intended worst case for the MIN-9 gate and the dominance warning, and (b) that the casual 25 % mining fraction is a pinned policy constant of this document and not a number from the spec. Also confirm that the held click, which is outside the 20-second look grid, is the intended reading of "continuous mining in active windows" (F5-R20).

## 12. Review-finding resolution (QA fix and Product/Game Systems review)

| Finding | Where resolved |
|---|---|
| Q-F1 units in the balanced mix | 2.3 steps 2 and 3 (exec/s only) |
| Q-F2 contract ratio bounds | 8.2 |
| Nit, "section 8" | section 0 |
| P1 Industry gain, manualYield, capacity, unspent IP | 5, 5.1, 11 (Q8) |
| P2 prestige-later cumulative time, warning, 3x factor | 7, 8.3 |
| P4 warnings and reports | 8.3 |
| P6 held-off counter | 4.3 |
| P7 Ore-only Research/Overdrive | 7 |
| P8 casual wording and offsets | 7, 7.1 |
| P9 memoisation, parallel histories | 5.1 |

# Feature Specification: Economy and Contracts

**Feature Branch**: feat/005-economy-contracts  
**Created**: 2026-10-10  
**Status**: Draft — owner decisions D-A…D-I recorded 2026-10-10  
**Input**: Owner playtest 2026-10-09 notes 4–16, `docs/product/ideas.md` (Feature 005), Feature 003 follow-ups #353–#358. This spec carries the telemetry and playtest list of #358.

## Context and authority

Feature 003 shipped the first Iron loop: Miner → Smelter → Press → Assembler, contracts, Items, Research, prestige and Industry. Content revision 2 is approved only as a fit to the scripted reference policy r1. The owner playtest (stopped at about $40,000 Factory Value, first prestige never human-played) and the F003 reviews found three structural weaknesses:

- The player has no way to earn money by hand, so the loop collapses into handing in contracts.
- Pacing rewards an Ore-only plan, build prices are flat, and later runs are flat.
- The numbers rest on a policy with known artifacts and on almost no player data.

Feature 005 fixes the economy and contract loop. It also delivers the measurement tools that the mandatory Play-Tester role and the next balance revision need. Feature 004, which follows, restyles every control this feature adds. Wiki recovery, the discovery waves and every owner decision are in [reviews/discovery.md](reviews/discovery.md).

## Clarifications

### Session 2026-10-10

Owner decisions after two discovery waves:

- D-A: Scope and offline value. One feature. The redesign of offline value (efficiency and the 8-hour cap) is out until the session log provides real return data. The 80 % offline effectiveness and the 8-hour cap stay unchanged.
- D-B: Order of work. The internal refactor (#356) comes first and must not change behaviour. Development-only time acceleration and the strategy policies come next, so that the mandatory Play-Tester can play from the first gameplay batch. Gameplay changes follow.
- D-C: Prestige threshold. The Factory Sale threshold rises with each prestige. ECO-3 is restated; its canonical form is in FR-021. Industry upgrades lose their hard caps and get diminishing returns instead. The practical Industry ceiling is at least 4.0× the IP awarded over runs 1–4 (FR-021).
- D-D: Contract rewards. A contract's Cash reward is a ratio to the base market value of the delivered stock. The same sale-price modifiers apply as for selling. Standing offers have a ratio of at most 1.5. The range from 1.5 to 2.0 is reserved for bonus offers.
- D-E: Chain value per slot. The production chain must earn more per occupied slot over time than Ore-only. This is reached within the existing whole-number content format (recipe quantities, durations, prices) and is guarded by a static check. There is no change to the price units and no price migration.
- D-F: Scope boundaries.
  - Telemetry is a local session log, never sent over a network.
  - Smart auto-sell is out and stays an idea.
  - Follow-ups #353, #354, #355, #356 and #357 are in scope.
  - Feature 004 owns styling, navigation, the Stats tab, the Research/Industry tree split, the offline popup rework, touch targets F-9 and N-5.
- D-G: Save version. New persisted state uses save version 3. A version 2 save loads with defined defaults. Unknown or malformed data still fails closed. Downgrade is not supported.
- D-H: Offline performance gate. The theoretical W003-B stress workload stays a documented bound. The gate for this feature is a realistic late-game workload held to the existing return budgets. W003-B is measured on the pre-F005 base first.
- D-I: Accelerated sessions are marked only in the session log. Their saves get no special handling.
- D-J (revised 2026-10-10, replaces the plan-review version): Starting Cash stays constant and low in every run. It does not grow with prestige. The first Miner of a run is reached through manual mining (D-O), not through a cash scaling.
- D-O (2026-10-10): Manual mining. An ore vein in the Factory view lets the player produce Iron Ore by hand, without building a machine, in every run from the start.
  - Each counted click or tap adds a content-defined yield of Ore to stock. Research and Industry effects can raise the yield.
  - Counted clicks are capped at a content-defined rate per second (initially 10), so an auto-clicker gains nothing over a fast human.
  - Pressing and holding mines at the capped rate, with mouse, touch or keyboard.
  - F004 designs the visuals.
- D-P (2026-10-10, mining review): A counted activation yields a fraction of an Ore. The initial yield is 0.1 Ore, stored as milli-Ore. A visible progress indicator fills, and whole Ore enter stock as the fractions add up. Continuous mining is therefore about 1 Ore per second, an active boost that does not displace machines.
- D-K (plan review): The prestige threshold stops rising at the content-defined maximum index. Prestige stays possible beyond it, so there is no hard end.
- D-L (plan review): Automatic sales reserve the required stock of any active delivery contract, whether or not auto-deliver is on. This resolves the F003 auto-sell conflict.
- D-M (plan review): The session log is exported by path on every platform; on Android the file is retrieved with `adb pull`. A system share action is deferred to Feature 004. The Android backup exclusion stays mandatory.
- D-N (plan review): Development builds with time acceleration write saves and logs to a separate save root, so accelerated progress never reaches a regular save. D-I otherwise stands.

Defaults adopted from the discovery reviews (the owner may still override them in clarify):

- Manual selling uses the same price as automatic selling.
- The sell steps are 1, 10, 100 and All.
- Auto-deliver applies to a single active contract and is off by default.
- Build prices scale with the owned count of a type and with the number of completed prestiges.
- Intermediate goals are display-only.
- A wall-clock gap above a threshold is treated as an offline return.

### Session 2026-10-10 (clarify)

- Q: When does a bonus offer (reward ratio 1.5–2.0) appear? → A: Deterministically, once per run, triggered by reaching a content-defined milestone or unlock, and available for a content-defined limited time.
- Q: What measures "prestige progress" in the build price curve? → A: The number of completed prestiges (already persisted); no new saved state.
- Q: Which contract endings start an offer's cooldown? → A: Every ending: delivery, abandonment and expiry. Abandonment stays without reward or penalty (OD-4) apart from the cooldown.
- Q: What counts as production for a production-objective contract? → A: Every completed output unit of the target resource anywhere in the factory between acceptance and the deadline. The units go to stock as usual and stay freely usable (including selling); reaching the quantity grants the reward without removing stock.
- Q: Can the player turn the session log off? → A: Yes. It is on by default and can be switched off by a per-device setting that is not part of the save; switching it off stops recording, and existing data stays until cleared.

## User Scenarios & Testing

### User Story 1 — Sell stock by hand (Priority: P1)

The player sells a chosen amount of any unlocked resource on demand, in addition to automatic selling, and sees exactly what was sold and earned.

**Why this priority**: This is the missing core verb (note 4). Without it, contracts are the only active income.

**Independent Test**: With auto-sell off, accumulate stock, sell 1, 10, 100 and All, and verify that the exact Cash and stock changes and the rejections leave no partial change.

**Acceptance Scenarios**:

1. Given 250 Iron Ore and a sale price p, when the player sells 100, then stock is 150 and Cash rises by exactly 100 × p. The confirmation names the units and Cash received.
2. Given 7 units and the step 10, when the player sells, then nothing changes and the reason is shown next to the control.
3. Given the whole stock of a resource, when the player sells All, then Factory Value is unchanged. A partial sell never raises Factory Value.
4. Given an active contract that needs 40 Plates and 50 in stock, when the player chooses All for Plates, then a confirmation states that the contract will fall short by 40 before anything is sold.

---

### User Story 2 — Contracts are a choice, not a substitute (Priority: P1)

Contracts pay a bounded premium over selling, come with varied goals and cooldowns, and grant Items on a predictable ladder. Delivering a contract and selling the same stock are both reasonable choices.

**Why this priority**: The owner could deliver contracts forever and never sell (notes 6 and 7). The F003 ratios (1.67–1.75 against a cap of 2.0) leave no headroom.

**Independent Test**: For each offer, compare the reward with the market value of the required stock under every sale-price modifier. Then run a contracts-only and a mixed strategy and compare their times to the threshold.

**Acceptance Scenarios**:

1. Given any standing delivery offer and any combination of sale-price modifiers, when its reward is computed, then the reward lies between 1.0× and 1.5× the effective market value of the required stock.
2. Given an offer just delivered, abandoned or expired, when its cooldown has not passed, then it cannot be accepted, and the remaining time is shown.
3. Given an offer whose goal is production of a quantity, when the player only buys and sells, then the goal does not progress; when the factory completes the target quantity of that resource before the deadline, then the reward is granted once and no stock is removed.
4. Given the content's Item ladder, when an offer is completed repeatedly in one run, then Items are granted at the same completion indices for every offer kind, up to a per-run cap.
5. Given a delivery offer whose required quantity exceeds the current storage capacity, when the player tries to accept it, then it is rejected with a reason and nothing changes; content never defines a quantity above the top storage capacity.

---

### User Story 3 — Auto-deliver a contract (Priority: P2)

The player marks the active contract for automatic delivery. It is then delivered as soon as the stock is there, before automatic selling can consume that stock.

**Why this priority**: Deadlines are missed while the goods sit in stock (note 5), and auto-sell eats contract stock (a known F003 conflict, resolved for every active delivery contract by D-L).

**Independent Test**: With auto-sell on for the contract's resource, enable auto-deliver and let production reach the requirement, live and offline. Verify the delivery instant, the single reward and that auto-sell took only the remainder.

**Acceptance Scenarios**:

1. Given auto-deliver on and the required stock reached at instant t ≤ deadline, then the contract is delivered at t, the reward is granted once, and only the remainder is auto-sold.
2. Given auto-deliver off, then behaviour equals Feature 003.
3. Given the same state and elapsed time, when it is advanced in one step or in any partition (live or offline), then delivery instant and resulting state are identical.
4. Given a contract that is delivered, abandoned or expired, then its auto-deliver setting is cleared and does not carry over to a re-accepted offer.
5. Given a save and load with auto-deliver on, then the setting is preserved.

---

### User Story 4 — Prices grow with the factory (Priority: P1)

Building another machine of a type costs more than the previous one, and costs grow with the number of completed prestiges. A factory of 12 machines is a real investment, not small change.

**Why this priority**: "$69 for an Assembler is practically free" (note 8). Flat prices remove build decisions.

**Independent Test**: Build successive machines of one type, remove and rebuild, and prestige. Compare every shown price with the price charged and with the content formula.

**Acceptance Scenarios**:

1. Given n owned machines of a type, when one more is built, then its price is the content price for n + 1. This is at least the price for n and at most the content cap.
2. Given a shown build price, when the player builds, then exactly that price is charged. If the state changed in between, the build is rejected with a reason.
3. Given a machine is removed and rebuilt, then the player never ends with more Cash or Factory Value than before the removal.
4. Given any prestige index and no discounts, then the first Miner of a run can be bought from starting Cash plus the sale of Ore mined by hand at the capped rate within the content-defined start window (initially 60 s).

---

### User Story 5 — See the next goal (Priority: P2)

The player always sees the next Factory Value milestone and how far away it is, so progress toward the prestige threshold feels reachable.

**Why this priority**: After 15 minutes, $1M felt very far away (note 9).

**Independent Test**: Start a fresh run and verify that a next goal with its remaining amount is visible from the start. Pass two milestones and check the final state.

**Acceptance Scenarios**:

1. Given any Factory Value below the threshold, then the next milestone, the amount reached and the percentage are visible.
2. Given a milestone is reached, then a one-time notice appears and the next milestone replaces it.
3. Given the threshold is reached, then the goal line shows that Factory Sale is available.

---

### User Story 6 — Every run feels stronger (Priority: P2)

Later runs stay meaningful. The prestige threshold rises, IP per run grows with it, and Industry upgrades keep giving smaller but real gains instead of capping out after the first prestige.

**Why this priority**: In F003, runs 3–5 are flat and the second prestige feels front-loaded (content approval item 3).

**Independent Test**: Run the strategy trace for runs 1–5. Check the restated ECO-3, the IP awards and the share of the Industry ceiling bought after each prestige.

**Acceptance Scenarios**:

1. Given prestige n, then the threshold for run n + 1 is the content-defined value for n + 1, and the preview shows it.
2. Given the chain strategy, then the time to each run's threshold does not increase from run to run and is at least 25 % of run 1.
3. Given the IP awarded over runs 1–4, then it is at most 25 % of the practical Industry ceiling.
4. Given a retried or interrupted prestige, then the threshold index and the IP award advance exactly once.

---

### User Story 7 — Strategy trace and time acceleration for balancing (Priority: P2)

The owner and the Play-Tester role compare named strategies over runs 1–5 in a reproducible trace, and play long sessions in development builds at an accelerated, clearly marked speed.

**Why this priority**: Balance revision 3 and the mandatory Play-Tester role depend on it. F003's numbers are a fit to a single flawed policy.

**Independent Test**: Run the trace with each strategy and confirm that the results repeat. In a development build, set acceleration, play, and confirm the marker and the log flag. Confirm that a release build contains no acceleration.

**Acceptance Scenarios**:

1. Given the trace, then it reports Ore-only, contracts-only, chain, idle-heavy and casual-pace strategies for runs 1–5, each as the median of several phase offsets with its spread.
2. Given a development build with acceleration N ≠ 1, then an "accelerated ×N" marker is always visible and the session log records it.
3. Given a release build, then no acceleration control or code is present.
4. Given acceleration, then simulated work per frame never exceeds the declared maximum.

---

### User Story 8 — Local session log (Priority: P3)

Play data is recorded locally while the player plays and can be exported as a file, so human playtests answer the open balance questions of #358.

**Why this priority**: It has no direct player value. It enables tuning with real data instead of invented certainty.

**Independent Test**: Play a session that includes a sale, a contract, a prestige and an offline return. Export the log and verify the recorded fields, that the log stays within its size limit, and that it contains no personal data.

**Acceptance Scenarios**:

1. Given play, then the log records the defined events and samples and nothing else.
2. Given a write failure such as a full disk, then play and saving continue unaffected and a dropped-event count increases.
3. Given the player exports, then the file is written to a location owned by the game, its path or share action is shown, and no data leaves the device on its own.
4. Given the player clears the log, then it is empty and recording continues.
5. Given the player switches the log off, then nothing more is recorded until it is switched on again; existing data stays exportable until cleared, and the setting survives a restart and a prestige.

---

### User Story 10 — Mine ore by hand (Priority: P1)

The player clicks, taps or holds an ore vein in the Factory view to produce Iron Ore without any machine. This bootstraps every run and stays useful as a small, upgradable income.

**Why this priority**: With constant starting Cash and prestige-scaled build prices, manual mining is how the first Miner of a later run becomes affordable (D-J, D-O). It is also the active verb that clicker players expect.

**Independent Test**: In a fresh run and at a high prestige index, mine by clicks and by holding. Check the exact Ore per counted click, the rate cap, the full-store behaviour, and that the first Miner becomes affordable within the start window.

**Acceptance Scenarios**:

1. Given an ore vein and a yield of 0.1 Ore, when the player clicks ten times within the cap, then the progress indicator fills in tenths and stock rises by exactly 1 Ore. A "+0.1" feedback appears per counted click.
2. Given a cap of 10 counted clicks per second, when an auto-clicker sends 50 clicks within one second, then exactly 10 are counted.
3. Given the player holds the vein (pointer, touch or keyboard), then Ore is mined at the capped rate for as long as the hold lasts. Releasing stops it, and rotation or focus loss ends the hold without extra Ore.
4. Given a full store, when the player mines, then nothing is added and the reason is shown next to the vein.
5. Given a Research or Industry upgrade of the mining yield, when bought, then every later counted click yields the upgraded amount.

---

### User Story 9 — Waking from sleep is an offline return (Priority: P2)

On a desktop that sleeps while the game runs unfocused, the elapsed time is credited like any other offline return instead of being simulated live at full value.

**Why this priority**: This closes the T250 follow-up, where an OS sleep while unfocused could be simulated live without the offline rules.

**Independent Test**: Simulate a wall-clock gap above the threshold during a live session and compare the result with an equivalent regular quit and restart.

**Acceptance Scenarios**:

1. Given a gap above the threshold, then the result equals a departure at the last frame followed by a standard offline return, with 80 % effectiveness, the 8-hour cap and the return summary.
2. Given a gap below the threshold, then it is simulated live as before.
3. Given the wall clock moves backwards, then no time is credited.
4. Given time acceleration, then only real wall time counts toward the threshold.

### Edge Cases

- **Manual mining**: clicks above the cap, a click and a hold at the same time, a hold across rotation or focus loss, mining at a full store, Ore overflow at 10^18, mining while the session is lifecycle-blocked, mining during time acceleration.

- **Selling**: selling more than the stock, a stale sell quantity, All at zero stock, selling the active contract's resource, selling during a full-store block, overflow of Cash at 10^18.
- **Contracts**:
  - delivery at the exact deadline tick, and at the expiry boundary tick (deadline + 1 tick);
  - auto-deliver offline across the deadline;
  - auto-deliver while auto-sell is on, and auto-deliver with too little stock at the deadline;
  - cooldown across an offline return;
  - prestige with a cooldown running or auto-deliver on.
- **Build prices**: a price change between display and tap, remove and rebuild, a price above the content cap or above 10^18, scaling combined with Toolmaker and Research discounts.
- **Goals and threshold**: no goals defined in content, the threshold index at its cap (the threshold stays, prestige continues), a retried prestige.
- **Session log**: full disk, a torn last line, the size limit reached, export failure, a cleared log during play.
- **Time and clocks**: a negative clock delta, a gap exactly at the threshold, a focus-loss checkpoint during a gap, acceleration combined with a gap.
- **Save compatibility**: a version 2 save with an active contract, a version 3 save with unknown members, an out-of-range new field.

## Requirements

### Functional Requirements

#### Selling

- **FR-001**: The player can sell a chosen quantity of an unlocked resource from stock. Quantity steps are content data, initially 1, 10, 100 and All. Each sale moves whole units atomically and is rejected without change if the quantity exceeds stock, the resource is locked, or Cash would exceed the numeric bound. A quantity is evaluated against the stock at the instant of the command, never a displayed value; there is no silent reduction to the available stock.
- **FR-002**: Manual and automatic sales use one price: units × base price × effective sale-price modifiers, floored once per sale. A manual sale is one command. An automatic sale is one sale per resource per simulation instant. Both use the modifiers in effect at that instant, after the instant's expiries. Factory Value keeps the Feature 003 basis: it values stock at the same effective sale price. Selling the whole stock of a resource therefore leaves Factory Value unchanged, and the single floor means no sale raises it.
- **FR-003**: "All" sells the stock at the moment of the command. If that would leave an active delivery contract short, the player confirms first, and the confirmation names the shortfall. Production objectives never trigger this confirmation.

#### Contracts

- **FR-004**: Each offer's Cash reward is a content-defined ratio R to the base market value of its required stock, with the same sale-price modifiers applied as for selling. Content validation enforces 1.0 ≤ R ≤ 1.5 for standing offers and R ≤ 2.0 for bonus offers. A bonus offer becomes available deterministically, once per run, when a content-defined milestone or unlock is reached, and stays available for a content-defined limited time measured on the run timeline, including offline time; when the window ends unaccepted, the bonus offer is gone for the run. It is not repeatable within the run.
- **FR-005**: Offers have content-defined cooldowns measured on the run timeline, including offline time. The cooldown starts when the contract ends in any way: delivery, abandonment or expiry. An offer in cooldown cannot be accepted and shows its remaining time.
- **FR-006**: Content can define offers whose objective is production of a quantity during the contract, in addition to delivery from stock. Buying, selling and manual mining never advance a production objective. Progress counts every completed output unit of the target resource anywhere in the factory from acceptance to the inclusive deadline; those units enter stock as usual and remain freely usable, and completion grants the reward without removing stock. Delivery of a production objective happens automatically when the quantity is reached, independent of the auto-deliver setting; the auto-deliver toggle is shown only for delivery objectives. Because the produced units keep their market value and can later count toward a delivery contract, content validation requires 0 < R_prod and R_prod + the highest standing delivery R for the same resource ≤ 2.0 (the reward cap). The stacked per-unit payout of producing and later delivering the same units therefore never exceeds the cap.
- **FR-007**: A delivery offer's quantity curve has a content cap no larger than the top storage capacity, and content validation proves this. At accept time, a required quantity above the current storage capacity is rejected with a reason, and the offer shows that reason as soft-disabled.
- **FR-007a**: Prestige resets every run-scoped contract state: active contract, auto-deliver setting, production progress, offer cooldowns and bonus-offer availability. Milestone notices start over with the next run's threshold.
- **FR-008**: Item rewards follow one content-defined ladder of completion indices for every offer kind, with a per-run cap. The cap is a content value. Content validation checks that the cap, combined with Item durations, keeps the Item uptime invariant ECO-6.
- **FR-009**: Reward arithmetic is identical for manual and automatic delivery and for production objectives. The sale-price modifiers are read at the completion instant, not at acceptance.

#### Auto-deliver

- **FR-010**: The player can turn auto-deliver on or off for the active contract. It is off by default, is cleared when the contract ends in any way, and is persisted with the run.
- **FR-011**: With auto-deliver on, the contract is delivered at the first simulation instant, at or before the inclusive deadline, at which the required stock is present.
  - At each instant the order is: completions, then auto-delivery, then automatic sales.
  - While any delivery contract is active, automatic sales of its resource leave the required quantity in stock (D-L), with or without auto-deliver.
  - Expiry is the boundary at deadline + 1 tick and is evaluated before that tick's completions, as in Feature 003.
  - If a numeric bound blocks an auto-delivery, nothing changes, the blocked state is shown, and delivery is retried at later instants.
- **FR-012**: Auto-delivery gives identical results for any partition of the same elapsed time, live or offline. There is no automatic acceptance of offers.

#### Build prices

- **FR-013**: A machine's build price follows a content-defined curve over the owned count of its type and the number of completed prestiges. The curve is computed with whole-number checked arithmetic and rounded once, upward, before discounts. Discounts keep the existing rule that a positive price never becomes zero. A price above the content cap is capped; a price that would exceed the numeric bound rejects the build with a reason.
- **FR-014**: The price shown is the price charged. A build whose price changed after display is rejected with a reason, and nothing is charged.
- **FR-015**: Removing and rebuilding can never increase Cash plus Factory Value. The removal refund keeps the Feature 003 rule (strictly less than paid).
- **FR-016**: Starting Cash stays constant across runs (D-J). Content validation proves that the first Miner of every run is reachable from starting Cash plus the sale of Ore mined by hand at the capped rate within the content-defined start window. The check uses no discounts and no Seed Capital, at every prestige index up to the threshold's maximum index.
- **FR-017**: The chain earns more per occupied slot-second than Ore-only by a content-defined margin m > 0, proven by a static check on content values. A margin of 0 or less fails validation.

#### Goals and progression

- **FR-018**: The game shows the next content-defined Factory Value milestone, progress toward it in amount and percentage, a one-time notice when it is reached, and a "Factory Sale available" state at the threshold. Milestones grant nothing. Content validation requires milestones that are strictly ascending, positive and below the run's threshold. If content defines none, the goal line shows the threshold itself as the only goal.
- **FR-019**: The Factory Sale threshold for run n is a content-defined, rising function of the number of completed prestiges, bounded so it stays within the numeric range. Beyond the content-defined maximum index the threshold stays constant, and prestige remains possible (D-K).
- **FR-020**: The IP award keeps the Feature 003 formula unchanged, floor(10 × sqrt(Factory Value / $10,000,000)), with the fixed $10M reference. Because Factory Value at prestige rises with each run's threshold, IP per run grows with it. A retried or interrupted prestige advances the index and awards IP exactly once.
- **FR-021**: Industry upgrades have diminishing per-level returns instead of hard caps. Each upgrade declares in content a practical-ceiling level, the level beyond which one more level adds less than a content-defined minimum gain. The practical Industry ceiling is the cumulative IP to bring every upgrade to that level. The balance trace (not static content validation) asserts that this ceiling is at least 4.0× the highest IP awarded over runs 1–4 across the at-threshold and prestige-later strategies. Structural safeguards stay in content validation: a combined build and upgrade cost floor, and no purchasable level with zero effect. ECO-3 (canonical form): under the chain strategy, the time to each run's threshold does not increase from run to run, and is at least 25 % of run 1.

#### Balance and tooling

- **FR-022**: A revised reference policy (r2):
  - measures income as net-worth growth;
  - buys storage when stores are blocked full;
  - spends IP in a parameterised order;
  - builds a balanced machine mix;
  - uses manual selling and auto-deliver.
- **FR-023**: The deterministic trace runs these strategies for runs 1–5, each as the median over at least five look-phase offsets with its spread reported:
  - Ore-only;
  - contracts-only;
  - chain;
  - idle-heavy;
  - casual pace (a fixed calibration constant scaling the look interval so that Factory Value at minute 15 is 1/2.5 of the reference; orientation only, reported without pass/fail);
  - prestige at the threshold versus later;
  - stop-at-Bar and stop-at-Plate;
  - market-only chain (contracts off).

  The single-lever set is Ore-only, contracts-only, stop-at-Bar and stop-at-Plate. The contract share of income and the reward-to-effective-value ratio per delivery are reported as orientation.

  This closes #357.
- **FR-024**: Asserted trace gates:
  - conservation and exact per-command deltas;
  - sale value neutrality (FR-002);
  - auto-deliver partition invariance;
  - restated ECO-3, with a tolerance of one look interval on the medians;
  - the contract ratio bounds on base and effective prices;
  - Item uptime (ECO-6);
  - remove/rebuild (ECO-7);
  - start affordability;
  - chain dominance per slot;
  - no single-lever strategy faster than the chain in runs 1–5 (median per run index);
  - the chain reaches every threshold within 6 simulated hours.

  Minute bands and the casual ratio are reported as orientation only.
- **FR-025**: A non-rounding price modifier replaces the rounding `var.price-80` sensitivity variant. Sensitivity bounds are re-derived under policy r2 with a tolerance at least as wide as the measured spread. Re-derived bounds, including the `returns` bound, are measurement tolerances only. They never change the 80 % offline effectiveness or the 8-hour cap (D-A).
- **FR-026**: Development builds offer a time acceleration factor with a maximum of 10 simulated seconds per frame (S_max). The plan's performance section may lower S_max, but never raise it without a spec change. Acceleration scales simulated time, never Cash. An "accelerated ×N" marker is permanently visible while N ≠ 1, and the session log records it. Release builds contain none of this, proven by inspecting release builds for Windows and Android. Development builds with acceleration use a separate save root (D-N).
- **FR-027**: A new contentRevision 3 is approved by the owner only after the trace above and a named human playtest that reaches the first prestige and a second run (#358).

#### Session log

- **FR-028**: The game keeps a local session log separate from the save. It is size-bounded (about 256 KB, oldest data dropped first), has a schema version, and is written to storage only at lifecycle points and periodic samples (at most every 30 s of wall time), never during simulation steps; events in between are buffered in memory. It is never read back into game state.
- **FR-029**: The log contains only defined, typed fields. No free text, exception messages, file paths, user or device identifiers, or save contents.
  - Header: schema, contentRevision, build kind, platform class, acceleration, a random session id that is new for every app start and never derived from the device.
  - Timeline samples at 10, 15, 30, 60, 90 and 120 minutes, and milestone times: first Gear, contract, Item, prestige, run-2 eligibility.
  - Manual versus automatic sales per resource.
  - Manual mining per minute: counted activations, ignored activations above the cap, Ore mined.
  - Contract events, with ratio and auto flag.
  - Prestige: Factory Value against the threshold, wait since eligible, Industry purchases.
  - Offline returns: absence, credited time, cap hit, production, store-full stalls, sales, Cash.
  - Bottleneck time per resource, meaning machine-seconds in which a machine waits on missing input or on a full store. Also purchases and clock events.
  - Lifecycle events.
- **FR-030**: Log failures never interrupt play, a save or a lifecycle callback. Dropped events are counted.
- **FR-031**: The player can export the log to a location owned by the game, with its path shown (D-M), and can clear it after a confirmation. Recording is on by default and the player can switch it off and on with a per-device setting that is not part of the save. No data is transmitted automatically.
  - Exporting an empty log writes nothing and says so.
  - With recording off, existing data remains exportable.
  - A failed export shows its reason, changes nothing and can be retried.
  - On Android the log is excluded from automatic device backup. This is a release gate before the first Android build distributed outside the development workstation.

#### Time, saves and engineering follow-ups

- **FR-032**: In a live session, a wall-clock gap strictly above a content-defined threshold (initially 60 s, at most 8 hours; a gap equal to the threshold is simulated live) is converted into a departure at the previous frame followed by the standard offline return. A negative gap credits nothing. Acceleration does not count toward the threshold.
- **FR-033**: Desktop focus-loss safety checkpoints keep a minimum interval of at least the FR-032 threshold, read from the same content value. A failing checkpoint write leaves the run live and unpublished, and the next save succeeds. Quit followed by focus loss behaves correctly (#354).
- **FR-034**: Save version 3 persists:
  - the auto-deliver setting;
  - offer cooldowns;
  - production-objective progress;
  - bonus-offer state (triggered, window end, consumed);
  - the run-wide contract completion total, kept when content drift drops per-offer counts.

  This list is closed; any further persisted value needs a spec change.

  A version 2 save loads with defined defaults: auto-deliver off, no cooldowns. Unknown members, out-of-range values and malformed data fail closed. A failed read never leaves a partially upgraded save; a failed version 3 write keeps the previous generation. An active contract or cooldown whose offer no longer exists in the loaded content is dropped without reward, and the load reports it; nothing else fails because of it.
- **FR-035**: Every reason code that the run application returns is listed in its contract and has player copy. A test fails if a code is added without both. This closes #355, and its three missing codes are added.
- **FR-036**: Before any gameplay change, the run persistence and snapshot code is reorganised by responsibility. Command-only state changes are no longer callable from outside the core. The gates for this step are an unchanged public behaviour, unchanged test suites and a public-interface comparison (#356).
- **FR-037**: The W003-B offline workload is measured on the pre-F005 base. The hot-path decision is recorded before the selling and auto-sell changes land (#353).

#### Manual mining (D-O)

- **FR-040**: The Factory view has an ore vein that is available from the start of every run. Each counted click, tap or keyboard activation adds the effective mining yield in milli-Ore to a mining remainder. Every full 1,000 milli-Ore move one whole Ore of the content-defined resource (Iron Ore) to stock as one atomic action. The remainder is transient run state and is not saved, so a load loses less than 1 Ore. A multi-activation request counts `min(requested, cap remaining, activations whose whole Ore fit the store)`. Activations rejected for a full store or the numeric bound do not use the cap. Nothing happens while the session is lifecycle-blocked.
- **FR-041**: Counted activations are capped at a content-defined maximum per second of run timeline (initially 10). Activations above the cap are ignored without error. The cap applies to clicks and holds together.
- **FR-042**: Pressing and holding the vein, with pointer, touch or a held keyboard key, mines at the capped rate until release. Release, rotation, focus loss or a dialog ends the hold and grants no time beyond the hold.
- **FR-043**: The base yield is content data in milli-Ore. A new effect kind for the mining yield can be granted by Research and Industry. It composes with the existing effect rules, and every purchasable level must change the effective milli-yield. Manual mining produces nothing offline. Mined Ore never counts toward a production objective (FR-006).
- **FR-044**: Content validation bounds manual mining. The maximum mining output per second (yield × cap, with the maximum Research and Industry yield) must stay at or below a content-defined share (at most 3.0×) of the output of one Miner at a content-defined reference level, computed with the maximum Industry speed. The trace asserts that a chain strategy with continuous mining in its active windows reaches each threshold no more than a content-defined margin faster than the chain without mining, for runs 1–5.

#### Player interface (functional; Feature 004 restyles)

- **FR-038**: Every new control has defined states: normal, soft-disabled with a visible reason, empty, rejection copy next to the control, confirmation where specified, and recovery. Controls are touch, mouse and keyboard operable with visible focus in reading order within their panel. Every control has an accessible name that includes its state, for example "Auto-deliver, on" or "Sell 10 Iron Plate, unavailable: not enough stock". This includes the development-only acceleration control. Controls are at least 44×44 UI points, readable at 200 % text without clipping, and understandable without colour alone. Compact numbers floor and never overstate, and exact values can be inspected. Affordability compares exact values.
- **FR-039**: The controls this feature adds are:
  - the ore vein in the Factory view (click, tap, hold, keyboard);
  - sell steps per inventory row in Market;
  - the auto-deliver toggle on the active contract;
  - a text badge with an accessible name on the Contracts tab when a contract is deliverable;
  - the next price and the owned count on build controls;
  - the goal line at the top of Market;
  - session log export, clear and on/off;
  - development-only acceleration with its marker.

  A hand-off list to Feature 004 names each control's stable identifier, the action it calls and the reasons it can show.

### Key Entities

- **Sale**: resource, whole quantity, unit price at the instant of sale, Cash received, manual or automatic.
- **Contract Offer**: objective (deliver from stock or produce), required quantity by completion index, reward ratio, standing or bonus (bonus: trigger milestone or unlock, availability window, once per run), cooldown, Item ladder.
- **Active Contract**: offer, accepted instant, deadline, progress, auto-deliver setting.
- **Build Price Curve**: base, growth by owned count, growth by number of completed prestiges, cap.
- **Milestone**: Factory Value target relative to the current threshold.
- **Prestige Threshold**: rising function of completed prestiges, with an index cap.
- **Strategy Policy**: named, deterministic play rules used by the trace and the Play-Tester.
- **Session Log**: versioned, bounded, local record of typed play events and samples.
- **Manual Mining**: resource, base yield, yield effects, maximum counted activations per second, start window for the first-Miner check.
- **Checkpoint V3**: Checkpoint V2 plus auto-deliver setting, offer cooldowns, production-objective progress and bonus-offer state.

## Success Criteria

### Measurable Outcomes

- **SC-001**: For every standing delivery offer and every reachable modifier combination, the contract reward is between 1.0× and 1.5× the effective market value of its stock.
- **SC-002**: Comparing medians over the phase offsets for each of runs 1–5:
  - contracts-only takes strictly longer than the chain to reach the threshold;
  - Ore-only takes at least as long as the chain (a tie counts as not faster).

  The spreads are reported alongside.
- **SC-003**: Identical state and ordered actions give identical state for one large advance and for every tested partition, including selling, auto-delivery, cooldowns, gaps above the threshold and offline returns.
- **SC-004**: ECO-3 in its canonical form (FR-021) holds for runs 1–5. The IP awarded over runs 1–4 is at most 25 % of the practical Industry ceiling (the inverse of FR-021's 4.0×).
- **SC-005**: A named human playtest reaches the first prestige and a second run on the shipped build. Its session log and notes record the human-to-trace time ratio at the timeline samples. The playtest record answers "Did both selling and contracts matter to your income?" on a 1–5 scale, with a free note. A score of 4 or higher is the target. The owner keeps the veto.
- **SC-006**: Every version 2 fixture loads into an equivalent version 3 state with defined defaults. Every malformed or out-of-range version 3 fixture is rejected without change.
- **SC-007**: The plan names a realistic late-game workload: a versioned save fixture derived from the policy-r2 trace near the run-3 threshold, with 12 slots and auto-deliver active.
  - On that workload, an 8-hour return completes within 500 ms on the Windows player and within 2 s on the Android emulator.
  - During play on that workload, the Windows player and the Android emulator keep the Feature 003 60 fps development target. At maximum acceleration, simulation per frame stays within 1 ms at the 95th percentile on Windows. The session log causes no allocations during simulation steps.
- **SC-008**: The session log never exceeds its size bound and contains no field outside the defined schema. An export succeeds on Windows and on the Android emulator. A forced write failure leaves play and saving unaffected.
- **SC-009**: Every new control is reachable, unclipped and operable on the supported phone, tablet and desktop screen classes carried forward from Feature 003 (FR-033 matrix) at 100 % and 200 % text, with visible focus and non-colour state.
- **SC-010**: A Play-Tester report exists for every implementation batch that changes the economy, contracts, progression or the core loop, and the owner has reviewed it.
- **SC-011**: At every prestige index up to the maximum, the first Miner is affordable within 60 seconds of manual mining at the capped rate from constant starting Cash with no discounts. In the trace, manual mining never exceeds the content-defined share of one Miner's output.

## Assumptions

- The game stays local, offline-capable and single-player. There is no online service, analytics upload, monetization or new resource family.
- Offline effectiveness (80 %) and the 8-hour cap are unchanged (D-A). Their redesign waits for session-log data.
- All numbers are tuning content in the existing whole-number format and are owner-approved through contentRevision 3. Minute bands are orientation; the owner's feel decides (F003 Session 2026-10-09).
- Clock manipulation stays an accepted local risk (F003 E-5/S1-6). FR-032 narrows it for live gaps. Because cooldowns and bonus windows use UTC stamps, forward clock jumps also shorten them, which widens the accepted risk slightly. The session log records clock events.
- The Android target is checked on the emulator only. iOS stays deferred (#39).
- Smart auto-sell, automatic contract acceptance, contract queues and the offline-value redesign are excluded. So are all Feature 004 items: navigation, the Stats tab, the tree split, the offline popup, the Priorities relocation, touch targets F-9, N-5, art and the render pipeline.

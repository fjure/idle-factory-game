# Feature Specification: First Iron Factory and Prestige Loop

**Feature Branch**: feat/003-first-factory-prestige  
**Created**: 2026-09-29  
**Status**: Clarified — owner decisions recorded 2026-09-29  
**Input**: [Feature 003 draft](../../Spec-003-Draft.md), after completed Features 001 and 002.

## Context and authority

Feature 001 supplies the deterministic foundation, local Save V1 and an 80% offline policy with an eight-hour credited cap. Feature 002 supplies one packaged, strictly validated JSON content authority. Feature 003 adds the first complete Iron progression loop. Its global inventory and visible slots deliberately replace development machine buffers and free placement. The old development IDs need an explicit V1 import. Wiki recovery and its coverage gaps are in [reviews/wiki-recovery.md](reviews/wiki-recovery.md); independent discovery is in [reviews/discovery.md](reviews/discovery.md).

## Clarifications

### Session 2026-09-29

- Global inventory: one shared store; no machine-local gameplay inventories.
- V1: import valid development checkpoints through an explicit mapping; reject incompatibilities without altering V1 files.
- Offline: timed contracts, Items and Overdrive advance in real time; apply effects only during their active portion of offline production.
- Prestige: eligibility at 10 million Factory Value from Cash, inventory market value and invested expansion costs; the square-root IP curve is the initial candidate.
- Placement: the grid presents fixed visible slots, exactly one machine per slot.
- Items: Tiers I, II and III all need playable content and acquisition.
- V1 mapping: dev.input → Iron Ore, dev.output → Iron Bar, dev.machine.basic → Smelter; five-second progress scales proportionally to two seconds. Reject stock or slot overflow safely.
- Contention: the player sets machine priorities.
- Visuals: a versioned, coherent provisional Art Bible and new development assets.
- Removal: confirmed removal refunds a content-defined fraction (initial tuning 50%) of the Cash actually paid for that machine and its upgrades, drops its cycle progress and frees its slot. The machine's full invested cost leaves Factory Value, so a build/remove cycle always lowers Factory Value. Replacement is remove then build, each atomic.
- Mid-cycle change: a level or speed change preserves the completed fraction of the current cycle and recalculates the remaining time under the new duration at the action/effect boundary. No input or output moves before atomic completion.
- Event order at each simulation timestamp: expire real-time effects and deadlines; resolve due completions in player priority order with atomic net-capacity checks; perform enabled auto-sales of the resulting stock; start or progress subsequent cycles. Player commands apply at their timestamp after the preceding simulation interval.
- Contracts: three deterministic contract types (Plate, Gear, timed Bar) once unlocked, at most one active. The player accepts and explicitly delivers from available stock. The deadline is inclusive. Expiry grants no reward and no penalty. Completed or expired offers can be accepted again with content-defined scaling; there is no random generation. Each reward is one atomic grant.
- Items: acquired through deterministic contract reward/milestone definitions; all three tiers are obtainable during the first run. At most one active instance of an identical effect per target; a conflicting activation is rejected without consumption. Different effect categories and Overdrive compose multiplicatively within content-defined bounds. Durations use real UTC.
- Overdrive: ×2 speed for 30 s, then a content-defined five-minute real-time cooldown; no reactivation while active. Effect and cooldown continue offline and reset on prestige.
- Factory Value: current Cash + all stored stock at current market value + Cash actually paid for currently owned machines and their upgrades + current factory/storage expansion upgrades. Removed machines contribute nothing. Research, contract rewards and spent Items have no separate value. Whole-Cash arithmetic; floor only in the final IP calculation.
- Numeric scope: resource units, Cash, RP and IP are exact nonnegative integers with a documented checked 64-bit bound; overflow is rejected atomically. Display shows a short suffix/scientific form and the exact value on inspection. Values up to 1e150 require a later explicit numeric/save/content migration.
- Balance: a versioned initial content sheet (start Cash, costs, unlocks, contract cadence/rewards, Item grants, Research graph, Industry costs/caps) is a hypothesis. A deterministic scripted active policy logs 10/15/30/60/90/120-minute states and an independent human playtest validates the feel; content is revised and owner-approved before the 2-hour and second-run targets are claimed.
- V1 import procedure: validate V1 with its original development definitions and reconcile its pending offline interval under Feature 001 rules first; sum buffers into global Ore/Bar; map each dev.machine.basic to a Smelter in a stable fixed slot keeping active state and identity; progress floor(oldProgressTicks × 2 / 5). Write, validate and promote the complete V2 candidate once, keep the V1 generation recoverable, and detect an already-promoted migration on retry without a second offline credit.
- Item targets (OD-1): Items target a machine type or the whole factory, never a single machine instance.
- Same-timestamp retry (OD-2): after auto-sales, waiting machines get a bounded retry pass in priority order (each machine completes at most once per timestamp), so a sale can unblock a full store in the same timestamp.
- Failed V1 import (OD-3): the failed rule is shown with Retry and a confirmed Start new run; V1 files stay unchanged on disk.
- Contract abandonment (OD-4): an active untimed contract can be abandoned without reward or penalty, like an expiry.
- Imported run defaults (OD-5): fields absent in V1 take fresh-run content defaults; Ore, Bar and Smelter are marked unlocked.
- Speed beyond the event bound (OD-6, plan review 2026-09-29): a cycle is never shorter than the content-defined minimum (0.1 s). The part of the combined speed multiplier above that bound multiplies the inputs and outputs of each cycle (a batch) instead, so every speed source keeps raising the effective rate. Goods arrive in larger packets at the bound.
- Partial batches (OD-9, follow-up to OD-6): a due batch completes the largest whole number of recipe units (1 to the full batch) whose inputs are in stock and whose output fits the store; if not even one unit fits, the machine waits as before. The completion is atomic and deterministic, and the unprocessed remainder is not carried over: the next cycle starts fresh.
- Save downgrade (OD-7): progress written to `v1/` by a Feature 002 build after a V2 import is ignored by Feature 003, and a Feature 002 build ignores `v2/`. Both directions are documented in the release note and the security/operations checklist (development saves only); there is no detection code.
- Research presentation (OD-8): Research is shown as a graph with a specified keyboard traversal order, pan or scroll at phone width, 44 pt nodes and a layout test at every screen class.

### Session 2026-10-09

Owner decisions after the T126 playtest (`reviews/playtest-flo-20261009.md`) and the T127 approval (`reviews/content-approval.md`):

- Pacing targets (SC-003, SC-004): the minute bands are orientation, not acceptance thresholds. Whether the first prestige takes one, two or three hours is not decisive; the owner's playtest judgment of the feel decides. The trace keeps reporting the bands, and the content gate keeps pinning the approved revision.
- Desktop focus (T250): on desktop players, losing window focus (Alt-Tab, another window, minimize) is not a departure; the run keeps simulating, and offline credit applies only after a regular shutdown or an unclean exit and the next start. Phones and tablets keep the Feature 001 FR-037 behaviour, where focus loss and pause record a departure and the return reconciles.
- Research scrolling (T248): OD-8's pan or scroll also applies to pointer devices at every screen class, not only at phone width.

## User Scenarios & Testing

### User Story 1 — Build and diagnose an Iron factory (Priority: P1)

The player builds and improves a Miner, Smelter, Press and Assembler in finite slots. They inspect rates, shortages and storage blockers, choose duplicate machines, and set priorities for competing machines.

**Why this priority**: This is the recurring factory decision loop.

**Independent Test**: Build all four types, create a shortage and a full-store block, then resolve each through an explicit choice while verifying exact resource accounting and feedback.

**Acceptance Scenarios**:

1. Given an empty slot and sufficient Cash, when the player builds, then one machine appears and its cost is charged once. A duplicate type remains possible.
2. Given no slot, a locked type or insufficient Cash, when building is attempted, then nothing changes and the blocker is explained.
3. Given two machines needing scarce input at the same instant, when they complete, then the saved player priority determines allocation regardless of screen position or frame rate.
4. Given a full store, when 2 Ore become 1 Bar, then the net-fitting conversion may finish; an input-free Miner remains blocked.
5. Given an owned machine at a milestone threshold, when upgraded, then its rate and milestone change once and its next cost/result are visible.

### User Story 2 — Sell strategically and complete contracts (Priority: P1)

The player chooses which Iron products to keep and which to sell automatically. Contracts create visible delivery goals and grant Cash, Research Points or Items.

**Independent Test**: Enable Bar selling to cause a downstream shortage, disable it, accumulate Bars and explicitly deliver a contract with a one-time reward.

**Acceptance Scenarios**:

1. Given unlocked resources, when one auto-sell setting changes, then later production follows it without changing other settings.
2. Given enough stock for an accepted contract, when the player delivers, then exactly the required stock is consumed and the defined reward is granted once.
3. Given a timed contract expiring offline, when the player returns, then late delivery is unavailable, its outcome is visible and no duplicate reward appears.

### User Story 3 — Use Items, Overdrive and immediate Research (Priority: P1)

The player earns playable Items of all three tiers, activates temporary effects tactically, uses Factory Overdrive and spends earned RP on Research that helps only the current run.

**Independent Test**: Obtain one item per tier, reject an invalid activation without spending it, apply and expire an effect, purchase Research immediately and verify prestige reset/preservation.

**Acceptance Scenarios**:

1. Given an owned Item and valid target, when activated, then exactly one is consumed and its effect/countdown is visible until real-time expiry.
2. Given an invalid target or incompatible effect, when activation is attempted, then the Item remains and the reason is shown.
3. Given enough RP, when Research is purchased, then the run benefit starts immediately without a timer; a rejected purchase changes nothing.
4. Given unused Items and active boosts, when prestige completes, then unused Items survive and active boosts do not.

### User Story 4 — Sell and rebuild a stronger factory (Priority: P1)

At eligibility, the player previews Factory Value, Industry Points and the exact reset/preserve list, confirms the Factory Sale, buys a permanent Industry upgrade and starts again stronger.

**Independent Test**: Reach the threshold in a scripted run, confirm once, inspect all state and the durable save, then compare the next run under the same policy.

**Acceptance Scenarios**:

1. Given Factory Value below 10 million, when the player opens Prestige, then sale is disabled with the missing value shown.
2. Given eligibility, cancel changes nothing; confirmation publishes one coherent reset and IP award only after durable save.
3. Given interrupted save/promotion, retry or restart yields either the old run with no award or the new run with exactly one award.
4. Given a purchased Industry upgrade, the new run shows its defined benefit and reaches the equivalent milestone faster under the reference policy.

### User Story 5 — Return safely and import V1 saves (Priority: P1)

On return, the player sees capped reduced production, actual resource changes, expired effects and contract outcomes. Existing valid development saves import once through the stated mapping; invalid ones remain recoverable.

**Independent Test**: Reconcile one-hour, eight-hour and longer absences with timer boundaries. Import every accepted V1 fixture plus over-capacity, over-slot and interrupted cases.

**Acceptance Scenarios**:

1. Given valid absence, at most eight hours are credited and at most 6.4 hours of productive time applied; real-time deadlines still pass at their actual instants.
2. Given a fitting valid V1 checkpoint, import maps IDs, quantities, machines and proportional progress into a valid V2 checkpoint and publishes it exactly once.
3. Given invalid or unrepresentable import, V1 bytes stay identical, no partial world is published and the error identifies the failed rule.
4. Rotation without actual departure creates no offline summary or second reward.

### User Story 6 — Author and validate expanded content (Priority: P1)

A designer can tune Iron recipes, prices, costs, contracts, Items, Research and prestige in the packaged content document. Validation prevents broken content from reaching gameplay or checkpoints.

**Independent Test**: Validate canonical Feature 003 content and broken copies with bad IDs, references, effects, dependencies and curves without a scene.

**Acceptance Scenarios**:

1. Canonical content resolves completely and drives runtime and balance traces.
2. Invalid content reports the entry, field and violated rule; no partial catalog, factory or checkpoint action occurs.

### Edge Cases

- Insufficient inputs, full store with net-fitting conversion, simultaneous completions, priority changes, selling during downstream demand, no-input Miner, numeric overflow.
- Duplicate building, no slot, removed/replaced machine mid-cycle, upgrade mid-cycle, milestone crossing and invalid action without partial spend.
- Contract delivery at its exact deadline, expiry offline, auto-sell conflict, repeated reward, active contract at prestige.
- Item target incompatibility, multiple effects, exact expiry, online/offline boundary, Overdrive during prestige, unused-item preservation.
- Research prerequisites/max levels, invalid content, missing presentation asset, empty/disabled/error/recovery UI.
- V1 unsupported ID, summed stock above capacity, too many machines, corrupt V2, backward/missing clock and interrupted promotion.

## Requirements

### Functional Requirements

#### Iron production and factory

- **FR-001**: Only the Iron Ore → Iron Bar → Iron Plate → Gear family and Miner/Smelter/Press/Assembler are playable. No other raw resource family or logistics simulation is introduced.
- **FR-002**: Versioned initial targets are Miner 2 Ore/2 s with no input, Smelter 2 Ore → 1 Bar/2 s, Press 2 Bars → 1 Plate/4 s, Assembler 2 Plates → 1 Gear/5 s. These are tuning hypotheses. At these values, one Press supplies only 62.5% of one Assembler's demand; the first bottleneck is visible.
- **FR-003**: A run owns exactly one global inventory of nonnegative whole resource units. Machines own no resource buffers. Every completed cycle consumes all inputs and adds all outputs atomically.
- **FR-004**: Inactive, missing-input or storage-blocked machines pause without backlog. A cycle may complete at full storage when its post-exchange total fits. No action may create negative stock, exceed capacity, duplicate or silently destroy units.
- **FR-005**: Advancement is deterministic for the same state and ordered actions across frame rates, JSON order and equivalent time partitions. Competing simultaneous cycles follow the player's saved priority order, never visual position. Per timestamp: effect/deadline expiry, completions in priority order, auto-sales, then subsequent cycles.
- **FR-006**: The player can view and reorder unique machine priorities. A new machine joins last; removal closes its position. Priorities reset on prestige.
- **FR-007**: Global capacity starts at 1,000; initial upgrade targets for levels 2–5 are 2,000/4,000/8,000/16,000 at $2,500/$10,000/$40,000/$160,000. Approved values live in versioned content and reset on prestige.
- **FR-008**: Fixed visible slots start at four and target six/eight/ten/twelve at factory levels 2–5. One machine occupies each slot; duplicate types are allowed. Slot position has no economic or production effect.
- **FR-009**: Build, remove, replace and per-instance upgrade are atomic actions with shown cost, result and rejection reason. Removal requires confirmation and refunds the content-defined fraction (initial 50%) of Cash paid for the machine and its upgrades; replacement is remove then build. A level change mid-cycle preserves the completed fraction. Machine level and milestones reset on prestige.
- **FR-010**: Initial tuning evaluates cost near Base Cost × 1.15^Level, normal growth near 1.08^(Level−1), and milestone multipliers ×2/×2/×3/×5 at levels 10/25/50/100. Approved content defines rounding, composition and caps.
- **FR-011**: Inspection shows effective production, consumption and net rate per resource, all current blockers, store usage and possible corrective actions without choosing the player's optimal action.

#### Economy, contracts and research

- **FR-012**: Initial market targets are $1 Ore, $3 Bar, $8 Plate and $24 Gear. The player controls automatic selling per unlocked resource; Cash and settings reset on prestige.
- **FR-013**: Products and types unlock progressively; Gear becomes relevant near minutes 10–15 in the defined first-run reference policy.
- **FR-014**: Contracts have defined offers, objective, required stock, reward and state transitions. Timed contracts show real-time deadlines. Explicit delivery removes stock and grants Cash, RP or Items exactly once; contract progression resets on prestige.
- **FR-015**: A contract's total reward generally exceeds market sale of the same stock. Initial content includes Plate, Gear and timed Bar contracts with explicit units and rounding. At most one contract is active; deadlines are inclusive; offers are deterministic and re-acceptable with content-defined scaling.
- **FR-016**: RP primarily comes from contracts. Research purchases are immediate, run-specific and have explicit prerequisites/effects; RP and purchases reset on prestige.

#### Items, Overdrive and prestige

- **FR-017**: Item Inventory holds unused Tier I, II and III Items; each tier has at least one obtainable and usable definition. Only a successful activation consumes one item. Unused items survive prestige.
- **FR-018**: Early temporary effects generally last 1–2 minutes. Tier-I Diamond Drill Head targets ×2 Miner output for 60 s. Effect composition, precision, incompatibility and expiration are explicit.
- **FR-019**: Factory Overdrive is an active ×2 speed effect for an initial 30 s followed by an initial five-minute real-time cooldown; it composes multiplicatively with other effects within content bounds and both states are visible.
- **FR-020**: Factory Sale begins at 10 million Factory Value. Value includes Cash, globally stored stock at market prices, Cash paid for currently owned machines and their upgrades, and factory/storage expansion costs, with no hidden contract/Research value. The preview shows valuation and rounding before confirmation.
- **FR-021**: Initial IP reward is floor(10 × sqrt(FactoryValue / 10,000,000)): 10 at $10M, 20 at $40M, 30 at $90M and 100 at $1B. The approved content curve is versioned and supported by balance evidence.
- **FR-022**: Prestige resets Cash, stock, machines/levels/priorities, factory/slots/storage upgrades, unlocks, contracts, RP/Research and active effects. It preserves IP, Industry purchases and unused Items. Achievements/lifetime statistics are outside this feature.
- **FR-023**: At least one permanent Industry upgrade is affordable with the first IP award and measurably accelerates the next run. Initial tuning families are production, starting Cash, upgrade costs, starting storage and speed.
- **FR-024**: Prestige preview, confirmation, award, reset and save form one coherent retry-safe transition. Cancellation or failure retains the prior run. No retry can duplicate IP or Items.

#### Offline, saves, content and UI

- **FR-025**: Offline return retains local UTC, eight-hour credited cap, 4/5 production effectiveness, nondecreasing departure, exactly-once reconciliation and durable-before-publication. Invalid time grants no production and explains recovery.
- **FR-026**: Timed contracts and active Item/Overdrive effects advance in real time offline. Production uses effective credited time, segmented at real-time expiration/deadline boundaries so effects apply only while active.
- **FR-027**: Save V2 persists every authoritative run, temporary and permanent value required for equivalent continuation. Unknown or corrupt data fails closed. Content and save schema versions remain separate.
- **FR-028**: V1 import maps dev.input → Ore, dev.output → Bar and dev.machine.basic → Smelter; five-second progress maps proportionally to two seconds. Legacy buffer quantities sum into the global inventory and machines occupy one slot each. Any unsupported ID, numeric, capacity or slot conflict fails without alteration or partial publication. Successful V2 promotion precedes publication and a recoverable V1 generation remains.
- **FR-029**: One packaged JSON authority with an approved new content schema validates Iron resources/recipes/machines, costs, unlocks, slots, storage, markets, contracts, Items, Research, Overdrive, prestige and Industry upgrades before checkpoint I/O.
- **FR-030**: Stable canonical IDs remain separate from player-facing text/presentation references. Ordinary tuning is content-only; new mechanics require code. Unknown/duplicate members, references, unsupported effects, impossible dependencies and malformed curves reject the full content with actionable diagnostics.
- **FR-031**: The game stays local and offline-capable. Belts/routing, adjacency, permanent Equipment, other worlds and endgame are excluded. Decorative transports never alter quantities.
- **FR-032**: Every new journey covers normal, loading, empty, disabled, error and recovery states. Actionable controls are touch/mouse usable, desktop keyboard activatable with visible focus, at least 44×44 UI points, scalable and understandable without color alone.
- **FR-033**: The existing phone/tablet/desktop matrix, safe areas, Android rotation and UI pointer ownership carry forward. Rotation cancels transient actions without spending, placing or prestiging and creates no false offline summary.
- **FR-034**: A versioned provisional Art Bible and structured briefs govern all new development assets; independent Art Direction approval precedes Technical Art integration and source provenance/import evidence is recorded.
- **FR-035**: Feature 003 defines a supported exact numeric range, rounding, overflow behavior, display notation and future evolution policy. It does not claim 1e150 support without implementation and tests.

### Key Entities

- **Run**: Cash, slots, global inventory, machines/priorities, unlocks, auto-sell, contracts, RP/Research and active timed effects.
- **Machine Instance**: Stable ID, type, fixed slot, level, active state, progress and priority, without resource buffers.
- **Contract**: Offer/active/completed/expired state, objective, deadline where applicable, explicit delivery and one-time reward identity.
- **Item Stack/Effect**: Tier, definition, quantity and real-time effect interval/target; unused stacks persist, active effects reset.
- **Industry Progression**: Persistent IP and bought upgrades, separate from run-specific RP/Research.
- **Checkpoint V2**: Versioned coherent run and permanent state with promotion identity and time boundary.

## Success Criteria

### Measurable Outcomes

- **SC-001**: A canonical fresh-run trace demonstrates Ore → Bar → Plate → Gear → contract → Item → Research → prestige → Industry upgrade → faster next run with zero unintended resource/currency/Item changes.
- **SC-002**: Identical state and ordered actions yield identical complete state for one large time advance and equivalent partitions across shared contention, full storage, selling, effects and offline boundaries.
- **SC-003**: A documented reasonably active reference policy targets Gear at 10–15 minutes and first prestige near 120 minutes. A deterministic trace reports 10/15/30/60/90/120-minute state and a named human playtest records actual time/deviation. These are targets, not current performance claims.
- **SC-004**: With the same policy and approved first IP purchase, equivalent second-run progression targets 70–80 minutes. The draft's later 45–55 and 30–40 minute bands are directional later-progression targets.
- **SC-005**: Approved valid V1 fixtures import into equivalent mapped V2 state exactly once; unsupported, over-capacity/slot and interrupted cases preserve V1 bytes and publish no partial state.
- **SC-006**: Deterministic Edit Mode, Unity Play Mode and tagged Windows Player journeys pass with no relevant errors or unexplained skips. Android is labeled emulator-only; physical Android and iOS are not claimed.
- **SC-007**: All new actions/states are reachable without clipped or obscured controls on supported phones, tablets and desktops with visible focus, non-color feedback and 44×44-point touch targets.
- **SC-008**: A producing twelve-slot factory and capped offline return meet declared measured budgets. The 60 FPS Android emulator development target is remeasured on an actively producing workload, not inferred from idle D001 evidence.

## Assumptions

All operational gameplay policy is approved in Clarifications (options analysed in [reviews/remaining-decisions.md](reviews/remaining-decisions.md)). Balance values labeled targets or initial tuning stay provisional until a versioned content revision, deterministic trace and owner review agree. The removal refund fraction is tuning content. No online service, monetization, analytics, soundtrack, music composition or 3D workflow is introduced.

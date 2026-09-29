# Feature 003 discovery review

**Date:** 2026-09-29  
**Input:** [renumbered draft](../../../Spec-003-Draft.md)  
**State:** Initial product choices recorded in spec.md; operational rules and balance approval remain open.

## Product and economy (independent Game Designer/Product Owner + Game Systems lens)

- The full Iron → contract → item → research → prestige loop is the intended Feature 003 scope. All three item tiers and the 2-hour first-prestige target are explicit draft requirements; they must not be reduced silently.
- Feature 001 uses machine-local buffers, while the draft requires one shared inventory. This changes observable resource flow, state ownership and save meaning. The owner has been asked to choose the inventory model and legacy-save policy.
- One base Miner, Smelter and Press balance at 1 ore/s → 0.5 bar/s → 0.25 plate/s. A 5-second Assembler consumes 0.4 plate/s and so the one-each chain sustains only 0.125 gear/s. This may be a deliberate bottleneck, but the draft's claim that the initial chain is approximately synchronized needs qualification or a changed target.
- At 0.125 gear/s and $24/gear, two hours of selling yields only $21,600 gross. Even a fully supplied base Assembler yields $34,560. The example $10M threshold requires a validated upgrade/contract/meta path, not an assertion. Record a deterministic first-run action policy and a time/value table at 10, 15, 30, 60, 90 and 120 minutes, plus sensitivity cases.
- Feature 001's offline rule is 4/5 efficiency with an 8-hour credited cap (6.4 effective hours maximum). The draft names 80% but does not state the cap. The owner has been asked how new timers behave offline.
- The draft lists achievements/lifetime statistics among prestige-persistent state without defining them. They are not automatically in scope.

## Player interaction (independent UI/UX lens)

- The specification must cover normal, loading, disabled, empty, error and recovery states for build/replace, upgrade, sell, contracts, items, research, offline return, prestige and meta purchases. Rejected operations must leave resources and items untouched and show the reason.
- Carry forward touch/mouse pointer ownership, visible keyboard focus, non-color state cues, 44×44 UI-point actionable controls, safe areas and the existing phone/tablet/desktop screen matrix. Android rotation cancels transient gestures/confirmations and does not itself show an offline return summary.
- Feature 001 graphics are development placeholders, not an approved final Art Bible. Any new visual family needs a versioned brief, independent Art Direction approval and Technical Art integration review. Final versus explicitly provisional visual quality remains a product decision.

## Technical feasibility (Software Architect early finding)

- Existing simulation advances machines independently through an interval. Shared inventory requires a deterministic global cycle event order and partition-equivalence tests; redirecting buffer references would be incorrect.
- Current recipe definitions require at least one input. The input-free Miner needs an explicit domain/content evolution.
- New costs, contracts, items, research and prestige require a new content schema version under Feature 002's contract.
- Save V1 has only development IDs and machine buffers. There is no principled automatic mapping from those IDs into Iron content; a compatibility decision is required.

## Role disposition for design

Mandatory: Product Owner, Software Architect, Test/QA, Game Systems/Economy, UI/UX, Art Direction and Technical Art (new visuals), Performance (shared simulation hot path), Security/Privacy (new persisted state). DevOps/Operations is required if build, automation or migration changes are selected. Online Services is omitted: no remote boundary is proposed. Sound Designer, Composer and 3D/Render Artist are omitted: this draft requests no audio, music or 3D pipeline. Asset production waits for an approved brief and Art Bible.

## Owner decisions recorded

1. Import compatible Save V1 development checkpoints with explicit ID/progress mapping and safe refusal when stock or slots do not fit.
2. Timed contracts, Items and Overdrive use real elapsed time offline; the effect applies until expiry.
3. Prestige threshold is $10M Factory Value; Cash, stock at market value and invested expansion costs contribute; the candidate square-root IP formula starts tuning.
4. The grid shows fixed slots; only those slots accept a machine.
5. Gameplay has global inventory only.
6. All three Item tiers ship with concrete content and acquisition.
7. dev.input → Ore, dev.output → Bar and dev.machine.basic → Smelter; progress maps 5s to 2s proportionally.
8. The player sets the priority of competing machines.
9. Feature 003 uses a versioned provisional Art Bible and new development assets.

## Independent Test/QA lens

The highest risks are resource integrity under contention and storage limits; complete-run save/prestige atomicity; V1→V2 interruption/recovery; real-time expiration versus reduced offline production; and an end-to-end real-component player journey. Use Edit Mode for deterministic rules and content validation, Play Mode for UI/bootstrap/lifecycle, tagged Windows Player tests for the full journey, and qualified Android emulator evidence for touch, rotation and actively producing performance. Keep existing Feature 001/002 regression fixtures and checks green. The final QA matrix must cite named tests and artifacts, not an assembled PASS label.

Do not clear the specification or planning gate until remaining operational rules in [remaining-decisions.md](remaining-decisions.md) are approved and the 2-hour target has an explicit validation plan.

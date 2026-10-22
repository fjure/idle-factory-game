# What I Learned: Economy and Contracts (F005)

**Feature**: Economy and contracts redesign, built on a behaviour-preserving refactor first
**Generated**: 2026-10-22 (batch 0 on 2026-10-10, batch 1 on 2026-10-13, batch A on 2026-10-14, batch B on 2026-10-21, batch C on 2026-10-22)
**Scope**: Full feature, MAQA batches 0, 1 and A (T300-T304, planning change #426, T305-T315 and T357, then T360, T316-T324, T331, T342, T358, T359)
**Implementation status**: batch C merged on origin/main 082d6bd (Issues #406, #408, #409, #410, #411, #489, #490; PRs #488, #491-#497 incl. docs PR #491), after batch B. Batch B merged on main 85b43d1 (Issues #381, #390-#405, #464-#466, #483; PRs #461-#486 incl. docs PR #482). Batch A merged on main 77e04b0 (Issues #429, #383, #379, #396, #380, #384, #385, #382, #387, #386, #388, #389, #407). Batch 1 merged too (Issues #367-#378, parent gate T315 on main 53ceb54: Edit Mode 1662, Play Mode 1553, player 24/24). Batch 0 merged (5 implementation tasks, Issues #362-#366, plus planning change #426). `tasks.md` checkboxes are mirrored from closed Issues; GitHub Issues are the status authority.

---

## Key Decisions

### 1. Record W003-B as "recorded, not gated", then fix it with a dedicated task (Option 1)

**What we did**: T300 measured the worst-case offline return on the Windows Mono dev player: median 962.5 ms, 1.92x the 500 ms reference. We chose Option 1 (a new task T360, "Option 1 hot loop", Issue #429) with a re-measure gate and an escalation path.

**Why**: Per F5-R14 the 500 ms and 2 s figures are the W005-L budgets, so W003-B is context, not a gate. Lookups cost at most about 11 % (a calibrated bound), so the cost is in the loop itself. Fixing it in one named task keeps performance changes out of other tasks' diffs.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Option 3 (relax the budget or change semantics) | Moves the goalpost before any optimization was tried. |
| Ignore it, since it is "not gated" | The late-game workload would inherit a known 2x miss. |

**When you'd choose differently**: If the quiet-host re-measure lands inside the budget, T360 shrinks to a regression guard. If the optimized loop still misses, escalate to the owner for a budget decision instead of guessing again.

---

### 2. Make command-path mutators internal instead of trusting callers

**What we did**: T304 made 36 command-path mutators internal and added `InternalsVisibleTo("IdleFactory.PlayModeTests")`. Restore paths (`TrySetActive`, `TryReorderPriority`, `OverdriveState.TryRestore`) stay public.

**Why**: The compiler then enforces "gameplay changes go through commands". Review still found two bypasses, `RunState.Effects` (an `ActiveEffectSet`) and `RunState.Priority` (`PriorityOrder.TryReorder`): exposing a mutable object through a getter exposes its mutators too.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Convention and code review only | Does not stop a future caller; the bypasses were found by a reviewer, not a compiler. |
| Split into separate read-only interfaces | Larger than a behaviour-preserving refactor allows; revisit in T334 (#399) with one restore surface. |

**When you'd choose differently**: When many assemblies need access, `InternalsVisibleTo` grows unwieldy and a facade or interface split is cleaner.

---

### 3. Behaviour-neutral partial-class splits, guarded by a public API snapshot

**What we did**: T302 added `PublicApiSnapshotTests` with the committed baseline `core-public-api.txt`; `RunPersistence` was then split by line range into partials. T303 did the same for `RunSnapshots.cs`.

**Why**: A line-range slice cannot change behaviour, and the snapshot proves the public surface did not move. Regeneration needs `IDLEFACTORY_REGENERATE_PUBLIC_API=1` and ends Inconclusive, so nobody refreshes the baseline by accident.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Redesign while splitting | Mixes refactor and behaviour change; reviews cannot tell them apart. |
| Rely on existing tests | They do not notice an accidentally added or removed public member. |

**When you'd choose differently**: If generic constraints and attributes must be pinned (the snapshot does not cover them), extend the format first. Rates types stayed in the main file because F5-R01 names no Rates partial; a `RunSnapshots.Goals.cs` seam is needed at plan step 9.

---

### 4. Insert a task by renumbering the ledger

**What we did**: T360 went into the work-unit ledger as WU18 after T315 and T300; later units moved up by one and the issue bodies were refreshed. T300 now gates T316, T325 and T326.

**Why**: Sequential units whose prerequisites only point backwards keep the ledger checkable by script.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Append T360 as the last unit | Prerequisites would point forward and break the ordering rule. |
| Use a suffix like WU17b | Defeats mechanical validation and sequencing. |

**When you'd choose differently**: With external references to unit numbers (dashboards, docs), renumbering is costly and stable IDs plus an ordering field fit better.

---

## Concepts to Know

### Measurement hygiene ("quiet host")

**What it is**: A timing result is only as good as the conditions it names. The emulator figure (4.6 s) is an upper bound only, because a League client and TFT were running.

**Where we used it**: `reviews/w003b-measurement.md`; quiet-host repeats are scheduled before the T360 acceptance measurement.

**Why it matters**: A "quiet host" must name non-Unity load such as games, or a noisy number gets treated as truth. PF-10 (10 % spread) was formally not met at 11.8 %.

### Doc-only tasks still need independent review

**What it is**: T301 (policy r2) produced only a document, yet QA failed it once (mix units, missing FR-024 base-price bound) and Product/Game Systems raised P1-P9.

**Where we used it**: `reviews/balance-policy-r2.md`.

**Why it matters**: Combining QA and Product findings into one fix round saved a cycle; review separately, fix together.

### Deterministic gates before model judgment

**What it is**: Compiler, snapshot and script checks give the same answer every time. After QA PASS the parent merges via `scripts/maqa-merge.ps1 -Issue <n>`, and `resource-lease.ps1` treats leases of closed `issue-<n>` as stale (#431).

**Where we used it**: #431, T302.

**Why it matters**: Rules a script enforces do not depend on every agent remembering them.

### PowerShell 5.1 traps

**What it is**: `.Count` on a single PSCustomObject returned from a function is unreliable, so wrap it in `@()`. `gh ... --json` output must be joined with `-join "\n"` before `ConvertFrom-Json`. `R` is a built-in alias (Invoke-History), so never name a helper `R`.

**Where we used it**: ledger and issue refresh scripts.

**Why it matters**: These fail silently or confusingly in automation.

---

## Architecture Overview

Core stays the independently testable domain assembly. Persistence is now several small files around an internal `SafeRunStore`; snapshots are split by concern (Factory, Economy, `RunDerivation.cs`). The public surface is pinned by a snapshot; mutation is limited to the command path by `internal`. Follow-ups for a single restore surface, dual-use setters and timer validation gaps are owned by T334 (#399).

---

## Glossary

| Term | Meaning |
|------|---------|
| W003-B | Worst-case offline return workload (12 machines at the event bound, 8 h). |
| W005-L | Realistic late-game workload that carries the F005 budgets (500 ms Windows, 2 s emulator). |
| Policy r2 | Reference balance policy (net-worth income, greedy Industry by marginal gain per IP) in `reviews/balance-policy-r2.md`. |
| Option 1 | The decision to optimize the hot loop (T360) and re-measure, rather than relax the budget. |
| Public API snapshot | Committed `core-public-api.txt` compared by `PublicApiSnapshotTests`. |

---

## Batch 1 additions

### 5. Ship content schema v3 with a labelled transitional adapter and golden

**What we did**: T313 reads `contentSchemaVersion 3` strictly and computed contract Cash through the transitional `RunRewardAdapter` (ceil(quantity x base price x ratioPpm / 10^6), checked long math). The golden `trace-r2.csv` was regenerated once as `trace-r2-transitional.md`; F003 pacing bands are reported, not asserted. T320 removed the adapter on 2026-10-14 (batch A); T341 still restores the bands.

**Why**: The reader, validator and tests could land before the final reward formula (T320) if the bridge is explicit, small and scheduled for deletion. Conservation and SC-001 stay asserted, so the loosening is limited to the bands the formula change is known to move (chain run 1 eligibility is now 6,548 s, 52 s under ECO-2).

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Build the final formula in the same task | One huge diff mixing a reader, a formula and a re-baseline; reviews could not separate them. |
| Keep the old golden and make the bands fail | Red default suite for a change that is expected and owned by a later task. |

**When you'd choose differently**: If no later task is named to delete the bridge, do not add it. A transitional shim needs an owner and a removal task, or it becomes permanent.

---

### 6. Validate content in layers, and keep constants visibly provisional

**What we did**: T314 added the structural rules (`contract.ratio.*` gated on revision >= 3, `cost.floor`, `contract.produce.min`, ...). The v2 `contract.reward.ratio` still runs, and the gate prevents double reports. Constants such as `ProduceMinQuantity` 10 and the 0.5x cost floor sit in the validator and are documented as pending an owner decision.

**Why**: Shipped revision-2 content must keep validating while stricter rules arrive. Documenting a constant as provisional stops a number chosen for a validator from becoming a design decision by accident.

**When you'd choose differently**: Once the owner decides, move such constants into content, so tuning does not need a code change.

---

### 7. Gate heavy tests by environment variable, never `[Explicit]`

**What we did**: T310 added `test-unity.ps1 -Category` (requires `-Suite` or `-TestFile`). The strategy baseline report is written only with `IDLEFACTORY_WRITE_STRATEGY_BASELINE=1`; its assertions run always.

**Why**: The runner counts every non-`Passed` result, including `Skipped`, as a failure. An `[Explicit]` test would make a green suite red or invisible.

**When you'd choose differently**: With a runner that reports skips separately, `[Explicit]` is the idiomatic way.

---

### 8. Make observers unable to hurt the run

**What we did**: T307's `IRunEventSink` is guarded: sink exceptions are swallowed and counted in `DroppedEvents`, but OutOfMemoryException propagates. A reflection test enforces the event allowlist, and a run is identical with no sink, a no-op sink, a recording sink and a throwing sink. T306 similarly lets a failing safety-checkpoint write leave the run live.

**Why**: A diagnostic must never change or crash the thing it observes. Letting OOM through avoids hiding a process that is already unsafe. Enforcing the allowlist by reflection protects privacy (SEC-1) mechanically.

**When you'd choose differently**: A sink that is part of correctness (an audit log) must fail loudly instead.

---

### 9. Throttle on the injected clock, with explicit bypasses

**What we did**: The autosave interval equals `liveGapThresholdMs` (fallback 60,000 ms) on `IUtcClock`. Quit, `SaveNow` and prestige bypass it; a failed write does not restart it; a backwards clock never throttles.

**Why**: Safety checkpoints must not hammer storage, yet data-losing moments must always save. Never throttling on an unreadable clock favours saving.

**When you'd choose differently**: If writes were expensive on the target device, a longer interval or write coalescing would be worth the extra staleness.

---

### 10. Dev tooling must be unreachable from release by construction

**What we did**: `IDLEFACTORY_DEV` is only set through `extraScriptingDefines` (never ProjectSettings), the assembly uses `defineConstraints`, `PersistenceRoots` gives a separate `IdleFactory-Dev/` save root, the S_max clamp applies only at scale > 1, and `scripts/check-release-build.ps1` scans Windows and Android builds. DevTools tests need a temporary define to compile.

**Why**: A define in ProjectSettings leaks into every build and creates settings noise. The scan turns "we believe it is excluded" into evidence.

**When you'd choose differently**: Large dev feature sets would justify a separate dev scene or build profile.

---

## Batch 1 concepts

### Orientation reports versus gates
The strategy framework (T311) takes medians over 5 look offsets and reports chain, ore-only, contracts-only, idle-heavy and casual without asserted bands. The casual pin L = 73 s is provisional (T341 owns it). The Play-Tester (T357) used it to surface findings: contracts-only stalls by construction, and ore-only beats the chain in every run index (F2). Reporting before asserting lets the owner decide whether a result is a defect or acceptable.

### Contract tables with negative tests
T305's `docs/contracts/run-reason-codes.md` is pinned to the code constants by `ReasonCodeContractTests`, which includes four negative tests proving the test can fail. A pin test that was never seen failing proves little.

### Workflow pitfalls
Rate limits interrupted workers: resume the same agent and renew its leases. "Filename too long" breaks `git worktree remove` for Unity worktrees (use the `\\?\` recipe in `docs/agents/workstation-pitfalls.md`). The command guard refuses a whole chain if one `Remove-Item` is blocked, so run deletes alone.

### Batch 1 glossary
| Term | Meaning |
|------|---------|
| Transitional adapter | `RunRewardAdapter` (history): the temporary Cash formula bridge of batch 1; T320 removed it in batch A. |
| S_max | Per-frame simulated-time clamp (10 s) used under DevTools time scaling. |
| DroppedEvents | Counter of sink exceptions swallowed by the guarded event sink. |
| `-Category` | `test-unity.ps1` option mapping to Unity `-testCategory`; needs `-Suite`. |

---

## Batch A additions

### 11. One pricing function for every way money moves

**What we did**: `SalePricing.Value` (T316) is the only sale arithmetic. Auto-sell, Factory Value, the `Sell`/`SellAll` commands (T317) and the contract reward (T320) all call it; T320 then applies a single ratio floor.

**Why**: When a sale and the valuation of the same stock use one function and one floor, selling a full stock is neutral for Factory Value by construction, and nobody can find a rounding path that earns a free unit. A mutation (`income + 1`) was caught by the tests only after QA found that an FV test had too much slack.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Inline arithmetic at each call site | Rounding drifts apart over time and nothing tests the seam. |
| Ceil in one place for "player-friendly" rounding | It broke index-0 parity in the transitional adapter and had to be replaced. |

**When you'd choose differently**: If a sale genuinely needs a different rule (a bulk bonus), model it as a separate priced event, not a second formula for the same event.

---

### 12. Expose a read-only snapshot instead of re-deriving a rule in the view

**What we did**: T359 first folded Research/Industry yield inside the UI. Review forced a narrow public `RunSnapshots.Mining`; T324 added `OwnedOfType`, `PriceCapped` and `RebuildPrice` to `MachineTypeSnapshot`. `HasRoom` later moved from the view into the snapshot.

**Why**: A rule copied into presentation drifts silently. A snapshot filled from existing internal functions adds no arithmetic and keeps the role rule "views consume application contracts".

**When you'd choose differently**: Snapshots allocate per call; fine on a click or a structure change, wrong for per-frame reads, which need cached values.

---

### 13. Let hard bounds in content replace clever speed code

**What we did**: For the prestige threshold (T331) the owner rejected 64-bit fast paths and instead bounded `prestige.threshold.maxIndex` to 1..10,000 in the validator, keeping an early overflow stop and an oracle test. Likewise `decayPpm` 0 is rejected from revision 3, with 1,000,000 as the explicit "no decay".

**Why**: A bound that content must satisfy is cheaper to audit than two arithmetic paths, and an explicit value beats a magic zero.

**When you'd choose differently**: If designers needed unbounded growth indices, optimize the audited path instead.

---

### 14. Transitional code with an owner is deleted on schedule

T320 deleted `RunRewardAdapter`, `ContractRewardDef.FromTable` and their API line, and the goldens changed only in `trace-r2.csv`. Stale comments naming the adapter outlived it and needed a cleanup pass, so grep for a removed name's comments in the same change.

---

## Batch A concepts

### Interim decisions with a visible owner
When the owner was away, the parent recorded interim choices (for example `perRunCap` as total entries per run) in Issue comments marked "open owner decision" so each can be overruled with one function or rule change. Make an interim choice cheap to reverse and name who decides.

### Cooldowns evaluated lazily
Cooldowns are UTC stamps (`readyAt = end + CooldownMs`) computed on demand, never scheduled. Live and offline paths therefore agree, which QA proved with an identity test.

### QA tests the tests
Three QA failures in batch A (#380, #385, #396) were test strength only: a mutation survived. Ask what mutation each new assertion would catch.

### Play-Tester without a human
`playtest-agent-batch-a.md` has a balance-runner channel and a scripted player session, labels feel statements as inference, and decides nothing. It found that the runner exercises none of the batch-A verbs, that revision-2 content hides cooldowns, bonuses and objectives, and defects in dev tooling (overlay without a theme, a focus exception).

### Batch A glossary
| Term | Meaning |
|------|---------|
| `SalePricing.Value` | The single floored sale price used by sales, valuation and rewards. |
| `Produced` | Board-owned count toward a production objective; restore path deferred to T334 (#399). |
| `runCompletionTotal` | Run-wide completion count that indexes the Item ladder. |
| `MiningSnapshot` | Read-only mining state (yield, remainder, cap, `HasRoom`) consumed by the ore vein view. |

---

## Batch B additions

### 15. A pre-check must be at least as pessimistic as the mutation it guards

**What we did**: Both auto-deliver (#391) and production completion (#392) failed QA on the same shape: `SettleFits` assumed the best case. It added the reward from the current Cash instead of the post-command Cash, counted a reserve for a contract already expired, and ran before `Active` was set on accept.

**Why**: A command must not mutate and then fail (`EndCommand` forbids it). Near the 10^18 cap an optimistic pre-check lets AutoSell overflow after the settle already changed state. The fix takes the reward from the same planner the simulator uses, with zero balances.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Roll back after overflow | Needs a second commit path and breaks the validate-then-commit rule. |
| Ignore the 10^18 edge | The bound is a game rule; QA found a concrete path. |

**When you'd choose differently**: If the check were costly on the hot path, cache the plan once per settle; do not loosen it.

---

### 16. Deviate from a formula when the formula is a no-op, and write down why

**What we did**: The live-gap plan said departure = f(atUtcMs, ...), which made `atUtcMs` irrelevant. T337 used `max(max(timelineEnd, activeDeparture), min(atUtcMs, clock))` and documented it (#402).

**Why**: The review proved the manipulated-clock case: the `min` with the real clock means a player gains nothing beyond quit-and-restart. A literal implementation would have passed the reviewed text and still ignored the input.

**When you'd choose differently**: If the plan were still negotiable, fix the plan text first; here it went into the docs PR afterwards.

---

### 17. Drift handling is fail-closed and never grants

**What we did**: Save V3 (T334/T335) drops content that left the catalog with `contract.dropped`, persists `runCompletionTotal` and restores it as max(saved, sum), and bounds future instants by departure + cooldown or window + 7 d.

**Why**: Every drop path that lowers a counter can re-grant a reward (a duplicate Item). The interpretations (unlock IDs dropped only when a contract-scoped list names them; objective change only with V3 evidence plus a differing `contentRevision`) are deliberately narrow: ambiguity keeps the player's contract.

**When you'd choose differently**: If saves were server-authoritative, drift would be a migration, not a client rule.

---

### 18. Derive log metrics from the read model, and make the observer unable to change the run

**What we did**: T364 computes Bottleneck per advance step from the read model (blocked machines in machine-seconds) instead of adding simulator counters, gates it on an optional `IRunLogRecordingState`, and proves byte-identical saves with logging on, off and failing.

**Why**: Simulator counters would add work to the hot loop and risk determinism; a derived metric is a lower bound, which is enough for SC-005. The gate means a disabled log costs nothing.

**When you'd choose differently**: If exact attribution mattered, a counter in the simulator is the only exact source; pay for it with a benchmark.

---

## Batch B concepts

### A capability interface is silently lost by decorators
`IRunLogRecordingState` is optional. A sink that wraps another must forward it, or accrual stops with no error. Test decorators explicitly.

### Lower-bound semantics
`secondsSinceAffordable` and Bottleneck seconds are lower bounds across returns. State that in the contract so analysts do not read them as exact.

### Real-input gates
Batch-mode UI Toolkit drops device pointer events, so F6 (Mine hold) was tested through panel events and closed only by a manual 8 s hold in the Windows DevTools build (#408). That same gate found the overlay covering Mine in 16:9. A green automated suite does not cover the device-to-UI leg.

### Measure, then re-baseline openly
The 10 % performance guard was exceeded (+10.5 %) from intended simulator work, not from auto-deliver (0.2-0.6 %). The owner re-baselined at 222.28 ms with the loss of the fixed anchor stated. A guard that moves must say what it stopped protecting.

### Platform limits show up late
Unity 6 rejects `Assets/Plugins/Android/res`, so the backup rules became an `.androidlib` (T339). The Android test player uses Vulkan and crashes in the emulator, so PF-7 Android calibration waits for T351. Verify the build path on the target early.

### Workflow
Usage limits stopped workers twice; resume the same agent after renewing leases. QA failed five Issues once each and every fix was verified by a fresh QA on the new tip.

### Batch B glossary
| Term | Meaning |
|------|---------|
| Live gap | A long real-time stall inside a running app, credited like an offline return (T337). |
| `contract.dropped` | Offline-summary code for a saved contract removed from content (T335). |
| `AutoDeliverWasOn` | Flag on the expired-contract snapshot so the short-stock line survives restart (T340). |
| `IRunLogRecordingState` | Optional sink interface gating Bottleneck accrual on recording (T364). |
| PF-10 | W003-B guard, now re-baselined at 222.28 ms. |
| W005-L | Largest Save V3 return fixture, 233.8 / 274.1 ms against a 500 ms budget. |

---

## Batch C additions

### 19. Gate the content-dependent checks, assert the engine ones, and give the transition an end date

**What we did**: T341 split the FR-024 gates into always-asserted engine rules (conservation, partition invariance, the engine half of the ratio bounds) and `ContentTuned` gates that report their measured value while `contentRevision` is below 3 and assert from 3. A test pins that exactly four ids may be transitional. T345 then tuned content until all four asserted, and the ratio bound was split into `ratio-bounds` (engine) and `ratio-content`.

**Why**: Revision 2 content genuinely failed four rules. A red gate would have blocked all other balance work; a deleted gate would have lost the target. The transitional state kept the measurement visible and the end condition machine-checked.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Weaken the thresholds until revision 2 passes | Hides the defect T345 had to fix. |
| Mark the tests `[Explicit]` | The runner counts skipped tests as failures. |

**When you'd choose differently**: If the content were never going to change, assert the gate and fix the content now.

---

### 20. Tune in the order of the dependency, with the lever that moves the fewest player-visible numbers

**What we did**: Revision 3 fixed chain dominance first, by multiplying the three downstream cycle times by 0.4 instead of raising prices or slowing the Miner, then re-tuned pace, ratios, Industry, threshold and bands, re-measuring the whole matrix after each step.

**Why**: Prices feed contracts, market and Factory Value of stock; the cycle times feed only the output per slot-second that the rule measures. A closed-form pre-solve ranked the options, but the trace decided, because the pre-solve cannot see storage, Items, Research or the look grid (`T_fluid` overstated revision 3 by 76 %).

**When you'd choose differently**: If a design goal were a visible price change, the price lever would be the honest one; here the rule was about throughput.

---

### 21. Resolve a literal-text defect with a recorded owner reading, not a silent code deviation

**What we did**: T341 hit four places where the policy text defeated itself (storage blocker read after the sale, a factory level blocked by a locked machine, hold-off applied to production objectives, a click fraction before the first Miner). It listed each as an interpretation; the owner confirmed them and the document was amended (Q10).

**Why**: The same defect had produced policy r1's slow casual run 2. A worker that "fixes" the text silently makes the next review unable to tell intent from drift.

**When you'd choose differently**: For a typo-level fix with no behavioural effect, correct the text and note it in the commit.

---

## Batch C concepts

### A valid value must never double as the "unset" marker
The machine panel's change-detection signature used the empty string for "no blockers", which equalled the cache's initial empty value, so a running machine kept the previous slot's guidance (T365, `MachinePanelGuidanceTests`). Use a distinct sentinel for "never rendered".

### A hit test only sees its own panel
Separate UIDocuments are different panels, so a single-panel hit test misses them. `RuntimePanelHitTest` checks all active screen UIDocuments, and the DevTools overlay consumes pointer input (T366). The batch-mode suite could not show this; a real-input session did.

### Wall-clock budgets need a quiet host
The regenerated W005-L fixture measured 548.5 ms on a loaded host against a 500 ms budget (449.4 ms for the algorithm; revision 2 was 233.8 / 274.1 ms on a quiet host). Record the host state beside the number and let T349 decide.

### The runner cannot see what the policy hides
Policy r2 dumps surplus by `Sell`, so it never meets the store-full stall the player does (C-F2). The owner chose a reserve-mode auto-sell, which the spec lists as excluded "smart auto-sell", so it needs a scope change and a Game Systems review.

### Workflow
Usage limits stopped workers again; resuming the same agent worked. `BalanceFull` self-gates by `-testCategory`, `-testFilter` or an env var because a skipped test fails the runner.

### Batch C glossary
| Term | Meaning |
|------|---------|
| `ContentTuned` gate | A balance gate that reports until content revision 3 and asserts after (T341). |
| `ratio-content` | Content half of the ratio bounds: standing at most 1.5, bonus in (1.5, 2.0]. |
| `RuntimePanelHitTest` | Hit test over all active screen UIDocuments (T366). |
| Reserve mode | Decided auto-sell that sells only the surplus above machine and active-contract needs (after T352). |
| `T_fluid` | Policy r2 fluid model of time to the next threshold; ranks Industry purchases only. |

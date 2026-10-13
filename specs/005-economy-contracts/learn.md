# What I Learned: Economy and Contracts (F005)

**Feature**: Economy and contracts redesign, built on a behaviour-preserving refactor first
**Generated**: 2026-10-13 (batch 0 on 2026-10-10)
**Scope**: Full feature, MAQA batches 0 and 1 (T300-T304, planning change #426, T305-T315 and T357)
**Implementation status**: batch 1 merged too (Issues #367-#378, parent gate T315 on main 53ceb54: Edit Mode 1662, Play Mode 1553, player 24/24). Batch 0 merged (5 implementation tasks, Issues #362-#366, plus planning change #426). `tasks.md` checkboxes are mirrored from closed Issues; GitHub Issues are the status authority.

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

**What we did**: T313 reads `contentSchemaVersion 3` strictly and computes contract Cash through `RunRewardAdapter` (ceil(quantity x base price x ratioPpm / 10^6), checked long math). The golden `trace-r2.csv` was regenerated once as `trace-r2-transitional.md`; F003 pacing bands are reported, not asserted. T320 removes the adapter and T341 restores the bands.

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
| Transitional adapter | `RunRewardAdapter`; temporary Cash formula bridge removed by T320. |
| S_max | Per-frame simulated-time clamp (10 s) used under DevTools time scaling. |
| DroppedEvents | Counter of sink exceptions swallowed by the guarded event sink. |
| `-Category` | `test-unity.ps1` option mapping to Unity `-testCategory`; needs `-Suite`. |

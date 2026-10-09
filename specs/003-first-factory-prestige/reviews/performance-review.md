# Performance review (T130)

**Task:** T130 (Issue #210, WU66), SC-008. **Role:** Performance Engineer, read-only review. **Date:** 2026-10-09. **Base:** `872984a`.
**Method:** document and code review of the recorded measurements. No Unity, player, build or emulator run was made for this review (the Unity and emulator leases were held elsewhere). Where a figure below is not a recorded measurement it is labelled estimate, and the measurement that would settle it is given. No code, content, setting or other artifact was changed.

**Sources:** `plan.md` > Performance (W003, budgets 1 to 5), `research.md` R04, `reviews/w003-measurements.md` (T122, baseline r1, Windows player), `reviews/android-emulator-evidence.md` (T124 rerun on the T245 sets, emulator-only), `reviews/full-suite-evidence.md` (T122 section), `reviews/balance-trace-r2.md` section 9, wiki `w003-player-timing-baseline`, `factory-run-bounds-and-offline-segmentation`, `mobile-performance-targets`; code `Assets/IdleFactory/Core/Run/Simulation/RunSimulator.cs`, `Assets/IdleFactory/Core/Run/Domain/GlobalInventory.cs`, `Assets/IdleFactory/Core/Run/Domain/SpeedComposer.cs`, `Assets/IdleFactory/Tests/EditMode/Fixtures/Run/W003Workload.cs`.

## 1. Verdict

**SC-008 is not met as written.** One declared budget is missed, and one sub-item is not zero:

1. **Budget 3 (8 h offline reconcile, Windows, median of 5, at most 500 ms) on W003-B: MISS**, 951.7 ms and 1017.6 ms in two runs, 1.9 to 2.0 times the limit. W003-B is the plan's designated SC-008 event-bound record.
2. **Budget 1, second part (no steady-state `GC.Alloc` on the player host frame path): not zero** on the whole frame including the HUD read path (6 samples in 300 frames, about one per simulated second while Overdrive is active). The advance path itself is zero.
3. **Open, not a measurement:** the emulator budget (at most 2 s) has only been measured on a different workload (W003-S shipped set, 746 ms). The W003-B event bound has never run on the emulator, so the emulator half of budget 3 is **unresolved for the bound**, not passed.

Everything else passes with large margin. The decision the owner must make is in section 7. No optimization is assigned by this review.

## 2. Budget table (measured against target)

| # | Budget | Workload | Measured | Target | Verdict |
|---|---|---|---|---|---|
| 1a | `GC.Alloc` samples per warmed `Advance` (Edit Mode, informational host) | B, S, R | 0 samples (T098 `W003PerformanceTests.WarmedAdvance_HasZeroGcAllocSamples`, passing in the T122 run) | 0 | PASS |
| 1b | Player host frame path, advance path (gate, `Advance`, hot copies) | B, S, R | 0 samples in 300 frames | 0 | PASS |
| 1c | Player host frame path, whole frame with HUD refresh read path | B, S, R | 6 samples in 300 frames (1 per simulated second) | 0 steady state | **MISS (minor), see 4.2** |
| 2a | Full host frame p95, 600 frames | B | 0.041 / 0.040 ms | at most 1 ms | PASS (about 4 percent of budget) |
| | | S, R | 0.017 / 0.018 ms | at most 1 ms | PASS |
| 2b | Snapshot rebuilds by `StructureRevision` | B, S, R | 4 rebuilds for 4 structural changes | at most structural changes | PASS |
| 3a | 8 h reconcile, Windows player, median of 5 | **B (event bound, SC-008 record)** | **951.7 / 1017.6 ms** (full cold start 988.1 / 1047.2) | at most 500 ms | **MISS** |
| | | S | 319.4 / 331.7 ms | at most 500 ms | PASS |
| | | R (context) | 175.5 / 181.8 ms | at most 500 ms | PASS |
| 3b | 8 h reconcile, emulator, median of 3 | W003-S shipped set (not B) | 746 ms [761, 745, 746] | at most 2 s | PASS for that set; **B on emulator: not measured** |
| 4 | Producing-factory FPS, emulator | 12-machine shipped set | 61.145 average (same-session control 61.5) | at least 60 | PASS |
| 5 | Passes per timestamp (PE-7) | B / S / R | max 2 / 2 / 4 | at most N + 1 = 13 | PASS |

Run 1 and run 2 are two Windows player runs on the same tree (T122). Budget 3a is repeatable: the five samples inside each run spread by less than 3 percent, and the two runs differ by 6.9 percent.

## 3. Measurement caveats

- **Host:** one machine, AMD Ryzen 7 7800X3D, 31.8 GB, Windows 11. A fast desktop CPU: it is the favourable end of the Windows range and says nothing about a typical player PC or phone.
- **Build type:** Windows Standalone **development** player, Mono scripting, `Debug.isDebugBuild` true (needed by the allocation recorder). Not IL2CPP, not a release build. The offline reconcile is pure integer code with checked arithmetic, so IL2CPP and release settings can change it noticeably in either direction. This has not been measured and is the largest unknown on the budget 3a verdict.
- **Interference:** another Unity editor was running during both runs (about 7 percent run-to-run noise per T122). The runs were not made on a quiet machine.
- **Sample counts:** 5 reconciles per variant per run, 2 runs (10 samples for B); 600 frames for the host frame; 300 frames for the allocation check. Adequate for a 2x miss; not adequate for deciding a margin of a few percent after an optimization.
- **Workload:** W003-B, S and R use a test-local catalog rebuilt in the player (`W003PlayerFixtures`); equivalence is shown by exact completion counts (2,764,800 / 691,200) and fixture hashes, not by sharing code. The shipped catalog is a different workload. W003-B is a **theoretical bound** (every machine completing every 0.1 s for 6.4 effective hours), not a state the shipped balance reaches early: the reference policy trace credits 6,661 completions for an 8 h absence at minute 30 (`balance-trace-r2.md`, offline paragraph).
- **Host frame figure** excludes UI Toolkit element writes and the slot grid render (a scene-less player cannot load the UXML). It is the host plus application read path, a lower bound. The device FPS gate (budget 4) is what covers the real HUD.
- **Emulator figures** are emulator-only (API 36 x86_64 host-GPU AVD, IL2CPP, development build) and are no statement about physical Android or iOS devices (iOS deferred, Issue #39).
- **Edit Mode timings** (editor Mono: B 1089.9 ms, S 357.1 ms, R 197.1 ms) are informational and not compared with a budget.

## 4. Misses and findings

### 4.1 Finding PR-1: W003-B 8 h reconcile, 0.95 to 1.02 s against 500 ms

**Measured problem.** `OfflineSegmenter.Reconcile` over the capped 8 h (E = 2.304e11 ticks) on W003-B processes 2,764,800 completions in 230,401 timestamps with 460,802 passes (2 passes per timestamp), at 344 to 368 ns per completion, 4.1 to 4.4 microseconds per timestamp. The full cold start adds 30 to 36 ms. The reconcile is a single synchronous call, so on the Windows player a return after a long absence blocks the main thread for about one second in the worst case.

**Evidence.** `w003-measurements.md`, sections "Budget verdicts" and "8 hour offline reconcile"; `full-suite-evidence.md`, section "W003 player timings (T122)"; wiki `w003-player-timing-baseline`. The miss is pure event count times a per-event cost: the work is linear in completions (R04 bounds it), and no pathological pass count is involved (budget 5 passes).

**Likely cause (code paths).**

- Event count is at its design maximum. `RunSimulator.Advance` (`Assets/IdleFactory/Core/Run/Simulation/RunSimulator.cs`, the `while (now < ticks)` loop) steps to the next completion and calls `ProcessTimestamp` once per distinct completion time. On W003-B all 12 machines complete at every 0.1 s (`minCycleTicks` = 1,000,000), so there are 230,401 timestamps each with 12 completions.
- Per-timestamp cost is dominated by repeated, string-keyed work, in code that already resolves indexes once per `Stage` (`_inputIndex`, `_outputIndex`, `_sellIndex`) but does not use them in the hot loop:
  - `CompletionPass` calls `UnitsThatFit` then `SpeedComposer.TryCompletedUnits` (`Core/Run/Domain/SpeedComposer.cs`), which calls `GlobalInventory.Units(string)` per input, a binary search with `string.CompareOrdinal` (`GlobalInventory.IndexOf`), and reads `recipe.Inputs` and `Outputs` through `IReadOnlyList<ResourceAmount>` interface calls.
  - `GlobalInventory.TryExchange` calls `Apply` (`Core/Run/Domain/GlobalInventory.cs`), which runs `HasDuplicate` on both lists (string `Equals` loops), then `IndexOf` several times per resource across validate and commit, with an inner nested string comparison per output.
  - The same fit calculation runs again in the run predicate (step 5 of `ProcessTimestamp`, `UnitsThatFit` for every machine), and the second pass re-scans every machine and calls `AutoSell` (`GlobalInventory.Units(string)` per auto-sell resource) although nothing completes.
  - So each completion pays about three string-keyed fit evaluations plus one exchange, each with several ordinal binary searches. That is the plausible reason the cost per event is a few hundred nanoseconds on Mono. This is an inference from reading the code. It has not been profiled; the profile that would confirm it is listed under Option 0.
- Batch size (k) does not matter for cost: arithmetic is O(1) per completion, which is why R04's bound holds. Only the number of events matters.

**Impact assessment.** On W003-B the miss is a factor of about 2 on a favourable CPU in a Mono development build. It is a synthetic worst case; the realistic shipped-balance absence is orders of magnitude smaller (6,661 completions in the trace; the W003-S shipped set on the emulator takes 746 ms). The risk is concentrated in late-game states with many fast machines and in the unmeasured emulator and IL2CPP numbers for B. I estimate, without having measured it, that B on the emulator would be at or above the 2 s limit: the only emulator comparison available (746 ms for the shipped set, against 320 to 366 ms for W003-S on Windows) is a different workload and cannot be scaled reliably, but it offers no headroom for a 2.76 M event workload that already takes 1 s on a 7800X3D. Treat this as a risk to measure, not as a result.

### 4.2 Finding PR-2: once-per-second allocation on the HUD read path (budget 1)

**Measured.** Whole host frame including `HudRefreshProbe`: 6 `GC.Alloc` samples in 300 frames on all three variants; the advance path alone: 0. The six frames are the second boundaries, matching the once-per-second `RunSnapshots.Overdrive` status read that `RunShellPresenter.Refresh` performs while Overdrive is active (the status object is allocated). Evidence: `w003-measurements.md`, "Player allocation check (PE-4)".

**Assessment.** It is event-driven (one small allocation per second, not per frame, and not proportional to machine count), so GC pressure is negligible compared with a per-frame allocation. It does not satisfy the literal wording "no steady-state `GC.Alloc` on the whole host frame path", and the real UI Toolkit element writes are not covered by this figure. Options: (a) accept and amend the wording to "advance path and hot read-model copies allocate nothing; the HUD path allocates at most one small object per second"; (b) cache the Overdrive status and rebuild it only when its inputs change (the remaining whole seconds change once per second, so the cache must hold a mutable or pooled value, or the presentation must read primitives instead of the snapshot object). Gain: removes about 1 allocation per second. Complexity: low for (a), low to medium for (b) (touches the `RunSnapshots` read-model contract and `RunShellPresenter`). Recommendation: **accept (a)** unless the device GC profile shows a visible hitch; the T124 FPS result (61.145 average, 6 dropped frames in 60 s, equal to the control within noise) does not indicate one. Measure the real UI path on the device before spending effort on (b).

## 5. Options for the W003-B miss (PR-1)

All options must keep results bit-identical to the per-event simulator (same state, completions, flows and summary); R04 already requires the Performance Engineer to review exact accelerations before any semantic change. None is selected here.

**Option 0: measure first (no code change).** Re-measure W003-B on a quiet Windows host with the editor closed, in an IL2CPP release player if available (a build setting change that DevOps and the Architect must approve), and on the emulator with a W003-B workload. Add a CPU profile (sampling, or Stopwatch accumulators by phase: step scan, `CompletionPass`, run predicate, `AutoSell`, string lookups) to attribute the 4.1 microseconds per timestamp. Expected gain: none directly; it replaces two inferences (per-event cost split, emulator and IL2CPP numbers) with data and may change the verdict. Complexity: low; needs the unity and emulator leases. Workload and commands:
  - Windows: W003-B via `Assets/IdleFactory/Tests/PlayMode/W003PlayerTimingTests.cs` (fixture `05-w003-b-bound.json`, SHA-256 `7e278091c3b6fd3a9047bf90555fc2749663e406ac97218a26f611a35e6e8be0`): `scripts/test-unity.ps1 -StandaloneWindowsPlayer -TestFile Assets/IdleFactory/Tests/PlayMode/W003PlayerTimingTests.cs`, median of 5, at least 3 runs, editor closed, leases `unity,emulator`.
  - Emulator: no committed device set carries the W003-B bound against the shipped catalog (T245 records what the shipped content allows), so this needs a new owner-approved decision on what to push: a state with 12 machines at the engine event bound within the shipped catalog if the content can reach it, or the test-local catalog in a development player. Record as a new task if wanted.

**Option 1: constant-factor optimization of the hot path.** Make the hot loop index-based: use the per-machine `_inputIndex` and `_outputIndex` already computed in `Stage`, an index-based fit and exchange on `GlobalInventory` that skips `HasDuplicate` (validated once in `Stage`), compute the fit once per machine per timestamp and reuse it for the run predicate, and skip the second `CompletionPass` when no machine can complete. Expected gain (estimate, to be confirmed by the Option 0 profile): 1.5x to 2.5x. On B that is 0.4 to 0.7 s, so it may reach 500 ms on this host but not by a comfortable margin, and gives less on a slower device. Complexity: low to medium; contained in `RunSimulator`, `SpeedComposer` and `GlobalInventory`; verified by the existing `RunSimulatorTests`, `RunSimulationEquivalenceTests`, `OfflineSegmentationTests`, the W003 completion counts and a golden equality of final state. Risk: low (no behavior change), but `GlobalInventory` is shared with commands, so those paths must stay as they are.

**Option 2: batch uncontended machines in closed form (named in R04, candidate a).** Machines whose inputs no other machine consumes, and which cannot be blocked by shared capacity, can be advanced by a closed-form count of completions over a span. On W003-B only the 3 Miners qualify: they have no inputs, while the 4 Smelters share Iron Ore, the 3 Presses share Iron Bar and the 2 Assemblers share Iron Plate, so each family is contended inside itself and also coupled through the chain and the shared store capacity (`Free`, `k_cap`). The 3 Miners are 691,200 of 2,764,800 completions (25 percent, the same figure as W003-S). Expected gain: at most 1.33x on B, i.e. about 0.7 to 0.77 s: **not sufficient on its own**. Complexity: high, because priority order within a timestamp, store capacity, partial batches (OD-9), `ApplyBoundary` events and the last-completion reporting must stay exact. Not recommended as a stand-alone measure.

**Option 3: steady-state detection (named in R04, candidate b).** When two consecutive timestamps leave every machine's progress and cycle unchanged and the per-timestamp stock, cash, produced, consumed and sold deltas identical, the simulator jumps n whole periods in one step, bounded so that nothing in the per-event model can change: the next boundary event, the remaining ticks, store capacity and non-negativity of every stock over the whole jump (linear, checked in closed form), the exactness bound 10^18 (checked at the end values, monotone), every `k' = min(k, k_in, k_cap)` staying on the same branch, no unlock candidate (`UnlockPass`) flipping, and auto-sell staying linear. Any guard failure falls back to per-event. W003-B is exactly periodic (period 0.1 s, 12 completions, same deltas), so the 230,401 timestamps collapse to a few and the reconcile would drop to the millisecond range. Expected gain: more than 100x on B and on any balanced running factory, none on a chaotic or blocking state (those are cheap anyway: R takes 175 ms). Complexity: high but well contained in `RunSimulator` (one new phase before the step; the counters `LastCompletions`, `LastTimestamps` and `LastPasses` must stay defined because the T122 tests assert them). Risk: highest of the options, because exactness rests on proving a guard set. It needs a differential test (jump against per-event) over randomized catalogs and states, plus a deliberate guard-failure set (capacity edge, stock edge, event inside the jump, unlock inside the jump, overflow edge). It must be an Architect-approved design because it changes how R01's "one ordered procedure" is realized, even though results must be identical.

**Option 4: do not optimize; reframe the budget (owner decision).** Keep the exact simulator and record that 500 ms is met on S and R and on realistic states, with W003-B documented as a theoretical bound measured at about 1 s on a fast Windows host in a Mono development player; re-baseline the SC-008 target on B (for example at most 1.5 s on Windows, with the emulator unresolved until measured) or explicitly accept the miss. Gain: none; cost: none; risk: the unmeasured emulator and IL2CPP numbers, and a long late-game absence may stall the main thread for one second or more on the return path. A variant that changes no simulation semantics is to run the reconcile behind a visible "restoring" state or off the main thread; it touches the Application and Persistence boundary (`RunPersistence` cold start, staged commit) and is an Architect topic, not assessed further here.

## 6. Recommendation

1. **Do Option 0 first.** It is cheap, has no semantic risk, and the verdict may change: the IL2CPP and release numbers and the quiet-host numbers for W003-B are unknown, and B on the emulator was never run. Without it the miss is characterized only for a Mono development player on one host.
2. **If B still misses after Option 0, take Option 1, then Option 3 if needed.** Option 1 is low risk and likely closes part or all of a 2x gap on desktop, but is not enough for a slow device. Option 3 is the only option that removes the dependency on device speed for balanced workloads; use it if the owner wants SC-008 to hold at the event bound on the emulator.
3. **Do not pursue Option 2 alone**: at most 1.33x on W003-B.
4. **PR-2: accept (a)** (amend the wording of the allocation budget) unless the device shows a hitch.
5. **No optimization is assigned by this review.** Any optimization requires owner approval and the agreement of the Software Architect (on the design of the accelerated path and its equivalence with the per-event model), followed by Test/QA verification on the committed result and a re-measurement against the same fixtures and hashes (baseline r1) so that the change is comparable. A semantic change, as opposed to a bit-identical speed-up, requires a separate owner decision (R04).

## 7. Decision the owner must make

Choose one for the W003-B 8 h reconcile miss (budget 3a / SC-008):

- **A. Measure first** (Option 0, recommended): approve a quiet-host and IL2CPP/release re-measurement of W003-B plus a profile, and decide whether an emulator W003-B measurement is wanted (needs a device state or a development player carrying the test catalog); then decide on B.
- **B. Fund Option 1** (and optionally Option 3) as new Issue(s), with Architect agreement and Test/QA gates, to bring W003-B under 500 ms on Windows.
- **C. Accept and re-baseline** (Option 4): record the miss as accepted risk and change the SC-008 target for the bound, with the emulator result left as an open risk until measured.

And separately: accept the once-per-second HUD allocation (PR-2, option a) or request the cached-status change (option b).

SC-008 should not be reported as passing in `final-acceptance.md` (T133) until this decision is recorded.

## 8. Open risks

- W003-B on IL2CPP/release Windows and on the emulator is unmeasured; the emulator 2 s budget has no evidence at the event bound.
- Both Windows runs were made with another Unity editor running; one host, one CPU class, no slow-PC or physical-device data.
- The host frame figure excludes the UI Toolkit writes; the allocation check excludes them too.
- A blocking one-second main-thread reconcile at return is a player-visible stall in the worst case, which this review does not size for physical devices.
- The W003 catalog is a player-side copy; equality with the Edit Mode catalog is proven by completion counts and fixture round trips only.

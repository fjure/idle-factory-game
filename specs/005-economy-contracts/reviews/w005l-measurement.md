# W005-L early measurement (T336, Issue #401)

**Task:** T336, plan step 10, research F5-R14 (gate), plan-reviews QA-2, PF-9, PF-10, PF-11, PF-16. First W005-L timing right after Save V3 (T334, T335) as an early warning, long before the final evidence of T349 and T351. Measure only: no production, content or semantic change.

**Status:** phase 1 (fixture, tests, method) is done. The player timing of phase 2 is **not taken yet**: the Windows rows below are `TBD`. Nothing in this file is a gate verdict until they are filled.

## Result in one paragraph

`TBD (phase 2).` Fill in: the median of the run medians of the 8 h return on the Windows Mono development player, the spread, the factor against the 500 ms budget (F5-R14), and whether the budget holds. A miss opens a Performance review task with options (QA-2); there is no semantic change without an owner decision.

## Workload (W005-L)

| Item | Value |
|---|---|
| Fixture | `Assets/IdleFactory/Tests/EditMode/Fixtures/SaveV3/01-w005-l.json` (Save V3, 4,748 bytes) |
| Player copy | `Assets/IdleFactory/Tests/PlayMode/Fixtures/SaveV3/Resources/W005PlayerFixtures/01-w005-l.json`, proved byte-identical by `SaveV3PlayerFixtureParityTests` |
| SHA-256 | `ae620458ed18286df02805b21871cb54acc2aee862f74a1d7924035d8842bf3e` (checked in the player test, `W005L-FIXTURE` line) |
| Regeneration | from code: `W005LWorkload.Build` against the shipped catalog; `W005LFixtureTests.CommittedFixture_IsByteIdenticalToItsInMemoryRegeneration` fails on drift and never skips. `IDLEFACTORY_REGENERATE_SAVEV3_FIXTURES=1` rewrites the original, the player copy and `Fixtures/SaveV3/README.md` |
| State | run 3 (prestige count 2) near the Sale threshold, from the policy-r2 trace row `run3,75.33E`: factory level 5 (12 slots), storage level 5 (capacity 16,000), 3 Miners level 66, 4 Smelters level 63, 3 Presses level 60, 2 Assemblers level 48, the Research levels of the row, Industry upgrades of two earlier prestiges, completion counts of the trace (run-wide total 140), auto-sell on Gear only, the Gear contract active (201 units) with auto-deliver on. Stock: Bar 45, Plate 15,919 (the store is nearly full, as in the trace row), Ore 0, Gear 0 |
| Catalog | the shipped catalog (`Assets/IdleFactory/Content/Iron/iron-factory-content.json`, content revision 2, the player builds it from the packaged copy) |
| Timed operation | one `OfflineSegmenter.Reconcile(run, permanent, departure, departure + 9 h)` call, the 8 h credited cap applies (E(8 h) = 2.304e11 ticks). A fresh decode and a fresh `OfflineSegmenter` precede every call and are not timed (the "algorithm" path of W003). The full path (`RunPersistence.ColdStart`: read, decode, reconcile, promote, install) is timed as well |
| Per launch | 1 unscored cold reconcile (JIT), then 5 timed reconciles. The run median is the median of those 5 (PF-10) |

The machine cycles are 1,001,245, 1,006,157, 1,018,308 and 1,022,981 ticks (Miner, Smelter, Press, Assembler), a hair above `minCycleTicks` 1,000,000. The cycles sit at the simulator's event bound, yet W005-L completes 1,470,584 times in 8 h (53 % of the W003-B bound of 2,764,800), because machines wait on a nearly full store and on their inputs. Phase 2 does not need to explain this; it records it.

## Deterministic counters (valid on any host and runtime)

The counters do not depend on timing. They come from `OfflineSegmenter.Simulator` and were computed in phase 1 with a plain .NET harness over the same Core sources (the Unity run of phase 2 must reproduce them; a difference is a finding):

| Counter | Value |
|---|---|
| Completions in the 8 h return | 1,470,584 (event bound 2,764,800) |
| Processed timestamps | 538,775 |
| Passes | 1,386,680 |
| Passes per timestamp | mean 2.574, maximum 4 (PF-1 bound: N + 2 = 14) |
| Effective ticks | 230,400,000,000 (8 h) |
| Contract | the Gear contract (201 units) auto-delivered during the return; no active contract after it |
| Save V3 size | 4,748 bytes; the file reads and writes back to identical bytes |

Phase 2 fills in the player-side `W005L-RECONCILE-ALGORITHM` line (`completions`, `processedTimestamps`, `passes`, `maxPassesPerTimestamp`, `partialBatches`, `nsPerEvent`, `nsPerTimestamp`).

## Indicative figures from the phase 1 harness (not evidence)

A plain .NET 10 console harness (not Unity, not Mono, not a player) ran the same reconcile on a busy workstation. It is listed only so that phase 2 has an expectation, and it must not be quoted as a measurement: Release build, run medians 105.7 to 119.3 ms (7 runs after a 251.7 ms cold call); Debug build 560 to 602 ms. Save V3 write by the mapper alone: 0.16 to 0.25 ms. The Windows Mono development player is slower than a .NET Release build. For orientation, W003-B (2,764,800 completions) takes 199.6 ms on the player after T316, about 72 ns per completion; at that rate W005-L would take about 106 ms, and the order of 100 to 300 ms is the expectation. Phase 2 decides.

## Method (phase 2, to run on a quiet host with the `issue-401` lease)

1. **Leases:** hold `unity` and `emulator` (`scripts/resource-lease.ps1 -Acquire -Resource unity,emulator -Owner issue-401 ...`). No emulator and no Unity editor process may run during a timed window (PF-9). Record the process list and the window (start and end UTC) of every launch, as `w003b-measurement.md` does.
2. **Correctness first:** `scripts/test-unity.ps1 -TestFile Assets/IdleFactory/Tests/EditMode/W005LFixtureTests.cs` and `SaveV3PlayerFixtureParityTests.cs` (Edit Mode), then `scripts/test-unity.ps1 -StandaloneWindowsPlayer -TestFile Assets/IdleFactory/Tests/PlayMode/W005PlayerTimingTests.cs`. The generator test must pass without regeneration; if it differs from the committed bytes under Unity, regenerate once and record both hashes.
3. **Timing:** the Windows Mono development player, **10 launches**, each with 1 cold and 5 timed reconciles of the algorithm path, 1 cold and 5 timed `ColdStart` runs of the full path, and 5 timed Save V3 writes (mapper, and the store's durable `Promote`). The decision value is the **median of the run medians**, with the spread of the run medians recorded (limit 10 %, PF-10). If the spread exceeds 10 %, take 10 more launches before concluding.
4. **Quiet host (PF-9):** the batch editor that builds the player exits before the timed launches. `scripts/test-unity.ps1 -StandaloneWindowsPlayer` keeps the batch editor alive next to the player (about 4 % slower in W003-B), so that run is the correctness run and the "editor alive" comparison set. The timed set is launched from the built player with no `Unity.exe` running (an uncommitted batch build script as in T300, if the test runner's player cannot be started stand-alone).
5. **Record per launch:** the `W005L-HOST` line (it includes the editor, emulator and adb process counts), `W005L-FIXTURE`, `W005L-RECONCILE-*` and `W005L-WRITE` lines.
6. **Emulator:** the emulator return (budget 2 s, median of 3) belongs to T351 and is not part of this early record.

## Windows results (T336 phase 2)

| Launch | Algorithm run median ms | Raw timed runs ms | Full return run median ms | Window start UTC | Window end UTC | Processes of note |
|---|---|---|---|---|---|---|
| 1 to 10 | TBD | TBD | TBD | TBD | TBD | TBD |

| Statistic | Algorithm ms | Full return ms |
|---|---|---|
| Median of run medians | TBD | TBD |
| Spread of run medians (max - min) / median | TBD | TBD |
| Budget (F5-R14) | 500 | 500 |
| Verdict | TBD | TBD |

## Save V3 write (PF-11)

| Measure | Value |
|---|---|
| Size | 4,748 bytes |
| Mapper (`SaveV2Mapper.Serialize`), median of 5 | TBD ms |
| Durable `JsonRunCheckpointStore.Promote` into an empty root, median of 5 | TBD ms |

## Host and build (fill in)

| Item | Value |
|---|---|
| Windows | TBD |
| CPU, RAM | TBD |
| Unity | `6000.6.0f1` |
| Build type | Windows x64 Mono development player (`Debug.isDebugBuild` true), batchmode, no graphics |
| Commit | TBD (the exact commit of the measured player) |
| Quiet-host deviations (PF-9) | TBD |

## Regeneration rule (PF-16)

After the step 16 retune (T345, content revision 3) the fixture is regenerated (`IDLEFACTORY_REGENERATE_SAVEV3_FIXTURES=1`), the player copy follows, and both hashes and both timings are recorded here. The hash pinned in `SaveV3PlayerFixtureParityTests.W005LSha256` and `W005PlayerTimingTests` changes with it.

| Revision | Fixture SHA-256 | Algorithm median ms | Full return median ms |
|---|---|---|---|
| Content revision 2 (T336, this record) | `ae620458ed18286df02805b21871cb54acc2aee862f74a1d7924035d8842bf3e` | TBD | TBD |
| Content revision 3 (T345) | (regenerated by T345) | | |

## Notes

- The offline reconciler builds its candidate checkpoint with the source's old `contentRevision` (`OfflineReturn.cs`, the `RunCheckpoint` it creates after a reconcile), so the checkpoint written by a return carries the old revision until the next autosave writes the live one. It is harmless, and the fixture's revision equals the shipped one, so the full-return path of this record does not hit content drift.
- W003-B (re-measured by T318 and T329) stays the bound; W005-L is the gate (F5-R14, D-H).

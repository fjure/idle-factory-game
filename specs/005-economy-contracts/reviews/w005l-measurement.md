# W005-L early measurement (T336, Issue #401)

**Task:** T336, plan step 10, research F5-R14 (gate), plan-reviews QA-2, PF-9, PF-10, PF-11, PF-16. First W005-L timing right after Save V3 (T334, T335) as an early warning, long before the final evidence of T349 and T351. Measure only: no production, content or semantic change.

**Status:** done. The quiet-host Windows timing was taken on 2026-10-21 (09:23 to 09:25 UTC) under the `issue-401` leases. The Windows rows are filled in below. This is an early warning, not the final gate: T349 repeats it on the final content (T345).

## Result in one paragraph

The 8 h return of W005-L takes **233.8 ms** (median of 10 run medians, each the median of 5 timed reconciles) on the Windows Mono development player with no Unity editor, no test player, no emulator and no adb running. That is **2.1 times below** the 500 ms budget (F5-R14, PF-3). The full return through `RunPersistence.ColdStart` (read, decode, reconcile, promote, install) takes **274.1 ms** (also within 500 ms). The spread of the run medians is 5.4 % (algorithm) and 6.7 % (full return), both below the 10 % limit of PF-10, so no extra launches were needed. The fastest algorithm run median is 231.3 ms and the slowest 243.9 ms. A Save V3 write takes 0.353 ms in the mapper and 17.6 ms through the store's durable `Promote` (4,748 bytes). **Verdict: the early gate is met (QA-2, PF-11); no Performance review is triggered.** The headroom is 266 ms for the algorithm and 226 ms for the full return, which is what T338/T339 and the F005 per-event work (auto-deliver, log, cooldowns) may consume before T349.

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

The machine cycles are 1,001,245, 1,006,157, 1,018,308 and 1,022,981 ticks (Miner, Smelter, Press, Assembler), a hair above `minCycleTicks` 1,000,000. The cycles sit at the simulator's event bound, yet W005-L completes 1,470,584 times in 8 h (53 % of the W003-B bound of 2,764,800), because machines wait on a nearly full store and on their inputs.

## Deterministic counters (valid on any host and runtime)

The counters do not depend on timing. They come from `OfflineSegmenter.Simulator`. Phase 1 computed them with a plain .NET harness over the same Core sources; the Unity Edit Mode run, the Unity test player and the quiet-host probe all reproduced them exactly.

| Counter | Value |
|---|---|
| Completions in the 8 h return | 1,470,584 (event bound 2,764,800) |
| Partial batches | 1,020,133 |
| Processed timestamps | 538,775 |
| Passes | 1,386,680 |
| Passes per timestamp | mean 2.574, maximum 4 (PF-1 bound: N + 2 = 14) |
| Effective ticks | 230,400,000,000 (8 h) |
| Contract | the Gear contract (201 units) auto-delivered during the return; no active contract after it |
| Save V3 size | 4,748 bytes; the file reads and writes back to identical bytes |
| Cost | about 159 ns per completion and 434 ns per timestamp at the median of 233.8 ms |

## Indicative figures from the phase 1 harness (not evidence)

A plain .NET 10 console harness (not Unity, not Mono, not a player) ran the same reconcile on a busy workstation: Release build 105.7 to 119.3 ms, Debug build 560 to 602 ms. It was an expectation only. The Mono development player is about twice as slow as that Release figure.

## Method

1. **Leases:** `unity` and `emulator` held as `issue-401` for the whole run. No emulator and no Unity editor process during any timed window (PF-9); the runner refuses to start otherwise and records the forbidden-process check.
2. **Correctness first (under the leases, editor alive, not evidence):** `W005LFixtureTests` 6 of 6, `SaveV3PlayerFixtureParityTests` 4 of 4, the full Edit Mode suite 2,167 of 2,167, and the test-runner player run `W005PlayerTimingTests` 5 of 5 (algorithm median 230.9 ms and full return 271.8 ms with the batch editor alive, 0.7 % and 2.9 % spread). The Unity-generated fixture bytes equal the phase 1 harness bytes, so the hash did not change.
3. **Timing:** a probe player with the same logic as `W005PlayerTimingTests` but without NUnit (the test runner's player cannot start stand-alone, and `scripts/test-unity.ps1 -StandaloneWindowsPlayer` keeps the batch editor alive next to it). The probe is untracked tooling: `.t336/run-w005l-quiet.ps1` builds a Windows x64 Mono development player with the batch editor (`-executeMethod T336Probe.Editor.T336ProbeBuild.Build`), waits until the editor has exited, takes a process snapshot, and launches `W005Probe.exe -batchmode -nographics -t336delay 3 -t336quit -t336out <file> -logFile <log>` **10 times**. Each launch does 1 cold and 5 timed algorithm reconciles, 1 cold and 5 timed `ColdStart` runs, and 5 timed Save V3 writes (mapper and durable `Promote`). A fresh decode and a fresh `OfflineSegmenter` precede every timed call and are not timed. The decision value is the median of the run medians, with the spread of the run medians (PF-10).
4. **Emulator:** the emulator return (budget 2 s, median of 3) belongs to T351 and is not part of this early record.

## Windows results (T336 phase 2b, quiet host, 2026-10-21 09:23 to 09:25 UTC)

| Launch | Algorithm run median ms | Raw timed runs ms | Full return run median ms | Window start UTC | Window end UTC | Busiest other processes (avg cores) |
|---|---|---|---|---|---|---|
| 1 | 231.3 | 231.0, 231.2, 231.3, 233.7, 233.9 | 272.8 | 09:23:19.7 | 09:23:30.9 | claude 0.06, steamwebhelper 0.04, steamwebhelper 0.03, audiodg 0.03 |
| 2 | 235.6 | 230.8, 231.2, 235.6, 241.7, 251.0 | 272.9 | 09:23:31.1 | 09:23:38.2 | steamwebhelper 0.04, chrome 0.04 |
| 3 | 238.1 | 232.7, 237.6, 238.1, 246.5, 250.3 | 272.2 | 09:23:38.3 | 09:23:45.4 | steamwebhelper 0.04, Battle.net 0.03 |
| 4 | 233.6 | 232.6, 232.7, 233.6, 233.9, 234.1 | 288.9 | 09:23:45.5 | 09:23:52.6 | steamwebhelper 0.04, Docker Desktop 0.03 |
| 5 | 232.9 | 231.1, 231.6, 232.9, 234.7, 237.4 | 270.5 | 09:23:52.6 | 09:23:59.7 | none above 0.03 |
| 6 | 232.5 | 231.3, 231.7, 232.5, 233.9, 236.7 | 274.6 | 09:23:59.8 | 09:24:06.9 | steamwebhelper 0.04, steamwebhelper 0.04 |
| 7 | 243.9 | 242.8, 242.9, 243.9, 244.2, 256.6 | 284.1 | 09:24:07.0 | 09:24:15.1 | steamwebhelper 0.03, steamwebhelper 0.03, Battle.net 0.03 |
| 8 | 233.9 | 232.1, 233.1, 233.9, 240.6, 244.2 | 273.5 | 09:24:15.2 | 09:24:22.3 | RiotClientServices 0.05, steamwebhelper 0.04, audiodg 0.04, Battle.net 0.03 |
| 9 | 235.4 | 233.7, 235.1, 235.4, 235.8, 239.9 | 275.1 | 09:24:22.4 | 09:24:29.5 | steamwebhelper 0.04, Docker Desktop 0.04, steamwebhelper 0.04 |
| 10 | 233.5 | 230.9, 232.7, 233.5, 233.9, 234.8 | 275.7 | 09:24:29.5 | 09:24:36.6 | steamwebhelper 0.04, steamwebhelper 0.03, chrome 0.03, Battle.net 0.03 |

| Statistic | Algorithm ms | Full return ms |
|---|---|---|
| Median of run medians | 233.8 | 274.1 |
| Spread of run medians (max - min) / median | 5.4 % | 6.7 % |
| Budget (F5-R14) | 500 | 500 |
| Verdict | met | met |

The cold (first, JIT-included) call of a launch took about 241 to 244 ms for the algorithm and about 280 ms for the full return; it is not part of the median.

## Save V3 write (PF-11)

| Measure | Value |
|---|---|
| Size | 4,748 bytes |
| Mapper (`SaveV2Mapper.Serialize`), median of the run medians | 0.353 ms |
| Durable `JsonRunCheckpointStore.Promote` into an empty root, median of the run medians | 17.6 ms |

The editor-alive test run measured 0.155 ms and 17.7 ms; the mapper figure differs by sub-millisecond noise, the durable write is I/O bound.

## Host and build

| Item | Value |
|---|---|
| Windows | Windows 11 Pro 10.0.26200 |
| CPU, RAM | AMD Ryzen 7 7800X3D (8 cores, 16 threads), 31.8 GB RAM |
| Unity | `6000.6.0f1` |
| Build type | Windows x64 Mono development player (`Debug.isDebugBuild` true), batchmode, no graphics, empty scene |
| Measured commit | branch head `949e4c0` on base `a80242b` (T337 merged). Main has since gained T338 (session log, `126134e`); the W005 reconcile path does not touch it |
| Fixture SHA-256 | `ae620458ed18286df02805b21871cb54acc2aee862f74a1d7924035d8842bf3e`, checked in every launch (`hashOk=True`) |
| Quiet-host check (PF-9) | before the first launch: no `Unity`, `UnityCrashHandler64`, `PlayerWithTests`, emulator, adb or probe process; the build editor had exited; the host line of every launch records `editorProcesses=0 emulatorProcesses=0 adbProcesses=0`. Process snapshots (before and after) and the per-launch busiest processes are kept in the untracked `.t336/results/20261021T092252Z` |
| Deviations from a fully idle host | the owner's other applications were open but near idle, each at or below 0.06 average cores per window (table above): Riot Client and RiotClientServices, Steam (`steamwebhelper`), Battle.net, Docker Desktop, Chrome and the Claude desktop process. The MCP python servers were idle. Together they use well under 0.3 cores of 16 threads, so the effect is within the 5 to 7 % spread |

## Regeneration rule (PF-16)

After the step 16 retune (T345, content revision 3) the fixture is regenerated (`IDLEFACTORY_REGENERATE_SAVEV3_FIXTURES=1`), the player copy follows, and both hashes and both timings are recorded here. The hash pinned in `SaveV3PlayerFixtureParityTests.W005LSha256` and `W005PlayerTimingTests` changes with it.

| Revision | Fixture SHA-256 | Algorithm median ms | Full return median ms |
|---|---|---|---|
| Content revision 2 (T336, this record) | `ae620458ed18286df02805b21871cb54acc2aee862f74a1d7924035d8842bf3e` | 233.8 | 274.1 |
| Content revision 3 (T345) | recorded by T345 when it regenerates the fixture | | |

## Notes

- The offline reconciler builds its candidate checkpoint with the source's old `contentRevision` (`OfflineReturn.cs`, the `RunCheckpoint` it creates after a reconcile), so the checkpoint written by a return carries the old revision until the next autosave writes the live one. It is harmless, and the fixture's revision equals the shipped one, so the full-return path of this record does not hit content drift.
- W003-B (re-measured by T318 and T329) stays the bound; W005-L is the gate (F5-R14, D-H).

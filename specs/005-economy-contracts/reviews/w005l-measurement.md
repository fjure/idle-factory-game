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
| Content revision 3 (T345 regenerated; T349 measured, quiet-host protocol) | `7066eed15513d2bdeb2c349bfa98c2a3fdc500d6b179b997ed8fe92b954a70fd` | 273.5 | 312.1 |

## Notes

- The offline reconciler builds its candidate checkpoint with the source's old `contentRevision` (`OfflineReturn.cs`, the `RunCheckpoint` it creates after a reconcile), so the checkpoint written by a return carries the old revision until the next autosave writes the live one. It is harmless, and the fixture's revision equals the shipped one, so the full-return path of this record does not hit content drift.
- W003-B (re-measured by T318 and T329) stays the bound; W005-L is the gate (F5-R14, D-H).

## Addendum (T345, issue #410): W005-L regenerated on content revision 3

The shipped content became `contentRevision` 3, so the fixture bytes changed (PF-16): `Fixtures/SaveV3/01-w005-l.json` and its StandalonePlayer copy are 4,785 bytes (was 4,748), SHA-256 `7066eed15513d2bdeb2c349bfa98c2a3fdc500d6b179b997ed8fe92b954a70fd` for **both** files (was `ae620458ed18286df02805b21871cb54acc2aee862f74a1d7924035d8842bf3e`). The shape is the run-3 eligibility row of the revision 3 trace: 6 Miners at level 59, 2 Smelters at 54, 2 Presses and 2 Assemblers at 52, run-wide completion total 101 (`balance-trace-r3.md` section 12).

Timings from `scripts/test-unity.ps1 -StandaloneWindowsPlayer` on 2026-10-22 (Windows Mono development test player, a batch editor alive next to it, a loaded host; NOT the quiet-host protocol of this document, so indicative only): algorithm median 449.4 ms (runs 420.8, 434.1, 449.4, 487.5, 506.8; spread 19.1 %; cold first call 455.6 ms), full `ColdStart` return median 548.5 ms (425.1 to 682.0; spread 46.8 %; cold 564.9 ms), 1,721,253 completions, Save V3 write 0.39 ms (mapper) and 21.2 ms (durable store). Against the 500 ms budget the algorithm is inside and the full return above it in this run; revision 2 measured 233.8 / 274.1 ms on a quiet host. The final gate is T349 on a quiet host, the emulator figure T351. Risk and cause: `balance-trace-r3.md` section 13, risk 5.


# T349 (Issue #414): final Performance evidence on content revision 3

**Task:** T349, plan step 17c, research F5-R14, F5-R18 (PF-3, PF-4, PF-5, PF-9, PF-10, PF-14). Measure only: no production, content or semantic change. Measured on 2026-10-27 between 12:00 and 12:15 UTC, branch `maqa/issue-414-t349-performance` on base `3a44af5` (main after T347 and T350), under the parent's `unity` and `emulator` leases as `issue-414`. No Unity editor, no emulator and no adb ran during any timed window (checked by the runner before every launch, which refuses to start otherwise). The batch editor used for the builds had exited before the first launch. W005-L is revision 3 (SHA-256 `7066eed1...70fd`, 4,785 bytes, `hashOk=True` in every launch).

## Result in one paragraph

**Performance verdict: meets, with one recorded open item (IL2CPP).** The 8 h return of W005-L takes **273.5 ms** for the algorithm and **312.1 ms** for the full `ColdStart` return (median of 10 run medians; spread of the run medians 4.4 % and 3.6 %, both below the 10 % limit, so no extra launches were needed), against the 500 ms budget: 1.83 and 1.60 times below. The 548.5 ms of T345 and the 376.8 ms of the batch-C player run were host load (editor and test player alive; see their records). W003-B with auto-deliver (the `pending` variant) takes **219.24 ms** (spread 1.4 %), which is **-1.4 %** against the re-baselined reference 222.28 ms (owner decision 2026-10-21): PF-10 is met (limit +10 %, i.e. 244.51 ms). Frame time with the real UI at W005-L is pinned to the 60 fps cap: average **16.68 ms**, p95 **16.87 to 16.89 ms** over 600 frames (three launches), no frame above 33.4 ms. In the separate development-player run the acceleration step (the host's frame simulation) on W003-B with auto-deliver has a p95 of **0.17 to 0.20 ms at x600** and **0.20 to 0.21 ms at Max** (budget 1 ms, three launches), with the effective multiplier at about 600 (10 simulated seconds per frame, the S_max clamp) and an unchanged frame time of 16.68 ms average and 16.8 to 16.9 ms p95. With the real UI at W005-L the whole script Update path (host step plus UI refresh) stays at a p95 of 0.54 ms at x600 and 0.68 to 0.71 ms at Max. Nothing missed, so no Performance review task is opened.

## Host and builds

| Item | Value |
|---|---|
| Windows | Windows 11 Pro 10.0.26200 (the player reports `Windows 11 (10.0.26200)`) |
| CPU, RAM, GPU | AMD Ryzen 7 7800X3D (8 cores, 16 threads; Unity reports `cores=8`), 31.8 GB RAM, NVIDIA GeForce RTX 5070 Ti (D3D12) |
| Unity | `6000.6.0f1`, Windows x64 **Mono** players (PF-13), built in batch mode from this worktree with `-buildTarget win64` |
| Code under test | `3a44af5` (content revision 3). `IdleFactory.Core.dll` of the timing player: SHA-256 `E83FC4B2FBED2ADA8C009FBBDE4DB6FCEE7B83E5F221C3B97CD0F249AECD238A` |
| Player A (W005-L and W003-B timing) | empty scene, development player, no DevTools, `T349PROBE` define, `-batchmode -nographics` (the T336 and T329 method) |
| Player B (frame time with UI) | the game scene, **non-development** player, define `IDLEFACTORY_DEV` only to select the development save root (the release root would be the owner's real save), so the DevTools overlay row is part of the UI. Windowed 1280x720, vsync 0 and a 60 fps target (set by `CoreGameFoundationBootstrap`) |
| Player C (acceleration with UI, overlay checks) | the game scene, development player with `IDLEFACTORY_DEV` (DevTools). Windowed 1280x720 and 390x844 |
| Player D (acceleration step on W003-B) | empty scene, development player with `IDLEFACTORY_DEV`; the real `FactoryRuntimeHost` over a `RunSession` of the W003-B fixture with the auto-deliver contract, vsync 0 and 60 fps as the game applies them |
| Probe | untracked tooling in `.t349/` and a throw-away `Assets/T349Probe` (never committed); sources kept as text under `w005l-measurement/probe/` (SHA-256 in "Reproduction"). It starts itself by `RuntimeInitializeOnLoadMethod` when `-t349mode` is passed |

## PF-9: host load

The owner closed TFT on 2026-10-27; Steam, Riot Client, Docker Desktop and Chrome stayed open, as agreed, and were **not closed**. They were measured per window as the difference of `Process.TotalProcessorTime` before and after each launch (runner script `probe/run.ps1.txt`).

| Process (all in the background in every window) | Average cores in the timed windows |
|---|---|
| `LeagueClient` (the Riot **client**, not a game) | 0.59 to 0.68 |
| `LeagueClientUx` | 0.40 to 0.47 |
| `LeagueClientUxRender` (two processes) | 0.18 to 0.48 and 0.02 to 0.12 |
| Docker Desktop, `com.docker.backend` | 0.02 to 0.05 |
| `Battle.net`, `CurseForge`, `Discord`, `EOSOverlayRenderer`, `audiodg` (while audio ran) | 0.02 to 0.07 each |
| Claude desktop processes (`claude`; other agent sessions had shells only, no Unity, no test run) | 0.03 to 0.07 |
| Chrome (about 50 processes), Steam (`steamwebhelper`), Riot Client services | at or below 0.02 each, never among the five busiest in a window |
| **Total of all other processes** | **1.46 to 2.05 cores of 16 threads (mean 1.69 in the W005-L windows, 1.64 in the W003-B windows)** |

This is a PF-9 deviation from a fully idle host, and a larger one than in T336 (about 0.3 cores): the League of Legends client was running (the game and TFT were not). It is the same load as in the clean windows of T300, T360, T318 and T329 (about 1.4 cores, same client). The load was constant over all launches (1.46 to 2.05 cores in total), the processes use few threads, and the CPU has 16 threads, so the effect shows at most in the 3 to 4 % spread; the W003-B figure reproduces T329 within -1.4 %. The MCP servers and shells of the worker sessions were idle. A leftover check at the end found no process of this worktree.

## W005-L return (Windows Mono development player, 10 launches)

Per launch: 1 unscored cold reconcile (JIT), then 5 timed `OfflineSegmenter.Reconcile` calls (fresh decode and fresh segmenter before every call, not timed), then 1 unscored cold and 5 timed `RunPersistence.ColdStart` runs (read, decode, reconcile, promote, install) over an isolated root. Run median = median of the 5 (the raw runs are listed sorted). Decision value = median of the 10 run medians (PF-10).

| Launch | Algorithm run median ms | Raw timed runs ms | Full return run median ms | Raw full-return runs ms | Window start UTC | Window end UTC | Total other cores | Busiest other processes (avg cores) |
|---|---|---|---|---|---|---|---|---|
| 1 | 274.1 | 268.2, 273.6, 274.1, 274.9, 277.7 | 312.9 | 305.1, 309.1, 312.9, 334.6, 335.8 | 12:00:23.7 | 12:00:31.1 | 1.70 | LeagueClient 0.63, LeagueClientUx 0.43, LeagueClientUxRender 0.27, LeagueClientUxRender 0.06 |
| 2 | 272.9 | 269.6, 271.2, 272.9, 277.7, 279.8 | 313.2 | 308.4, 308.6, 313.2, 319.7, 321.9 | 12:00:31.3 | 12:00:38.5 | 1.50 | LeagueClient 0.60, LeagueClientUx 0.41, LeagueClientUxRender 0.19, claude 0.06 |
| 3 | 271.0 | 267.1, 267.2, 271.0, 272.8, 273.5 | 311.3 | 309.6, 310.5, 311.3, 317.5, 321.4 | 12:00:38.7 | 12:00:46.0 | 1.58 | LeagueClient 0.63, LeagueClientUx 0.42, LeagueClientUxRender 0.20, audiodg 0.05 |
| 4 | 268.2 | 264.3, 267.2, 268.2, 268.7, 270.5 | 308.3 | 306.6, 307.8, 308.3, 310.1, 311.6 | 12:00:46.1 | 12:00:53.4 | 1.61 | LeagueClient 0.59, LeagueClientUx 0.45, LeagueClientUxRender 0.19, LeagueClientUxRender 0.05 |
| 5 | 268.4 | 266.4, 267.6, 268.4, 275.0, 289.8 | 312.4 | 306.3, 311.1, 312.4, 314.8, 332.2 | 12:00:53.5 | 12:01:00.8 | 2.05 | LeagueClient 0.60, LeagueClientUxRender 0.48, LeagueClientUx 0.44, Riot Client 0.09 |
| 6 | 271.8 | 268.4, 268.7, 271.8, 281.4, 282.3 | 317.6 | 308.1, 311.4, 317.6, 325.7, 339.2 | 12:01:01.0 | 12:01:08.5 | 1.69 | LeagueClient 0.65, LeagueClientUx 0.42, LeagueClientUxRender 0.27, LeagueClientUxRender 0.04 |
| 7 | 275.5 | 269.1, 274.9, 275.5, 285.5, 294.9 | 313.5 | 309.6, 311.6, 313.5, 322.8, 324.6 | 12:01:08.6 | 12:01:16.1 | 1.87 | LeagueClient 0.61, LeagueClientUx 0.46, LeagueClientUxRender 0.27, LeagueClientUxRender 0.08 |
| 8 | 278.3 | 268.0, 272.2, 278.3, 280.0, 282.1 | 306.5 | 305.7, 306.1, 306.5, 307.3, 316.3 | 12:01:16.2 | 12:01:23.6 | 1.63 | LeagueClient 0.63, LeagueClientUx 0.46, LeagueClientUxRender 0.25, audiodg 0.06 |
| 9 | 279.3 | 269.3, 269.5, 279.3, 280.8, 291.3 | 311.7 | 308.1, 311.2, 311.7, 313.8, 329.1 | 12:01:23.7 | 12:01:31.0 | 1.70 | LeagueClient 0.62, LeagueClientUx 0.43, LeagueClientUxRender 0.23, LeagueClientUxRender 0.06 |
| 10 | 280.2 | 268.6, 276.6, 280.2, 282.2, 286.2 | 311.2 | 306.8, 309.3, 311.2, 313.2, 314.4 | 12:01:31.1 | 12:01:38.4 | 1.61 | LeagueClient 0.64, LeagueClientUx 0.46, LeagueClientUxRender 0.27, CurseForge 0.03 |

| Statistic | Algorithm ms | Full return ms |
|---|---|---|
| Median of run medians | **273.5** | **312.1** |
| Spread of run medians (max - min) / median | 4.4 % (268.2 to 280.2) | 3.6 % (306.5 to 317.6) |
| Budget (F5-R14, PF-3) | 500 | 500 |
| Headroom | 1.83 times | 1.60 times |
| Verdict | met | met |

Deterministic counters (valid on any host; identical in all launches and equal to the T345 values): 1,721,253 completions, 683,440 processed timestamps, 1,733,911 passes, at most 3 passes per timestamp (PF-1 bound 14). About 159 ns per completion and 400 ns per timestamp at the median. Cold first calls: about 283 ms (algorithm) and 315 ms (full return). The ratio to revision 2 (233.8 / 274.1 ms, T336) is 1.17 and 1.14; the completions grew by 1.17 times (1,721,253 against 1,470,584), so the cost per completion is unchanged.

History of the revision 3 figures (same fixture hash): T345 548.5 ms full return on a loaded host (editor and test player alive, spread 47 %); batch C 376.8 ms (two editor processes alive); T348 QA run algorithm median 290.6 ms; **T349 quiet-host protocol 273.5 / 312.1 ms**.

## PF-10: W003-B with auto-deliver against 222.28 ms

Per the owner decision of 2026-10-21 (#414, `maqa:decision:#414:owner-2026-10-21-pf10`) the reference is the T327 value **222.28 ms** (`6fc283d`, variant `pending`), not 199.61 ms or 201.12 ms. Workload: the W003-B fixture `05-w003-b-bound` (SHA-256 `7e278091...8be0`, `hashOk=True`), the T329 `pending` variant (active delivery contract `contract.probe-plate`, 15,000 Iron Plate, auto-deliver on through the internal `ContractBoard.TrySetAutoDeliver`, never satisfied, so the per-pass check runs at every pass), one `OfflineSegmenter.Reconcile` over 9 h away, 2 unscored warm-ups and 5 timed reconciles per launch, 10 launches, Windows Mono development player, `-batchmode -nographics`.

| Launch | Run median ms | Raw timed runs ms | Window start UTC | Window end UTC | Total other cores |
|---|---|---|---|---|---|
| 1 | 219.13 | 220.1, 221.3, 217.8, 217.7, 219.1 | 12:01:43.1 | 12:01:48.2 | 1.76 |
| 2 | 219.99 | 221.0, 222.0, 219.3, 218.2, 220.0 | 12:01:48.4 | 12:01:53.4 | 1.52 |
| 3 | 219.72 | 218.9, 219.7, 219.9, 219.7, 225.2 | 12:01:53.6 | 12:01:58.8 | 1.86 |
| 4 | 218.15 | 218.2, 217.1, 220.3, 220.5, 217.3 | 12:01:58.9 | 12:02:04.0 | 1.70 |
| 5 | 218.08 | 216.9, 216.6, 218.1, 229.2, 225.5 | 12:02:04.1 | 12:02:09.1 | 1.46 |
| 6 | 219.34 | 218.3, 219.3, 216.5, 227.9, 219.8 | 12:02:09.3 | 12:02:14.3 | 1.52 |
| 7 | 219.40 | 218.9, 224.5, 222.4, 217.1, 219.4 | 12:02:14.4 | 12:02:19.5 | 1.50 |
| 8 | 217.90 | 216.5, 217.9, 217.5, 219.7, 219.6 | 12:02:19.6 | 12:02:24.7 | 1.77 |
| 9 | 220.51 | 223.0, 218.7, 221.9, 220.5, 219.8 | 12:02:24.8 | 12:02:29.9 | 1.62 |
| 10 | 217.48 | 216.7, 219.0, 217.2, 217.5, 219.1 | 12:02:30.0 | 12:02:35.0 | 1.68 |

| Statistic | Value |
|---|---|
| Median of run medians | **219.24 ms** (217.48 to 220.51) |
| Spread of run medians | 1.4 % (limit 10 %: met) |
| Reference (T327, `6fc283d`, owner decision) | 222.28 ms |
| Change against the reference | **-1.4 %** (guard: at most +10 %, i.e. 244.51 ms) |
| PF-10 verdict | **met** |
| Absolute against the 500 ms gate (PF-3) | 2.28 times below |
| Counters | 2,764,800 completions, 230,401 timestamps, 460,802 passes, at most 2 passes per timestamp, contract still active after the return (`activeAfter=True`), about 79.3 ns per completion (T329: 80.4 ns) |
| Post-reconcile state SHA-256 | `4ec35a56d1f9a3d7423bcdc9073700ea3358bad68260e9941d17732ebe001447` in every launch |

The probe is a new reconstruction of the T329 probe (the T329 file was never committed), so the figures are compared with the recorded 222.28 ms, not with a same-session baseline. The result is 1.4 % below that recorded value and 9.8 % above the T360 reference of 199.61 ms, the order of the known T326/T327 growth of the common path.

## Frame time with UI at W005-L (PF-14)

Player B (non-development, real game scene and UI, windowed 1280x720, vsync 0, 60 fps target by the game). The probe seeds the W005-L Save V3 into the development save root before the scene loads, so the shell cold-starts the revision 3 W005-L factory (12 machines in 12 slots, Cash 289M after the 8 h return, the real HUD, the machine grid, the tab bar and the DevTools row). After 240 settle frames it records 600 frame intervals twice: with the offline summary dialog open, and after the dialog was dismissed (the live factory). Three launches:

| Launch | Phase | Average ms | p50 ms | p95 ms | p99 ms | Max ms | Frames above 20 ms | Above 33.4 ms |
|---|---|---|---|---|---|---|---|---|
| 1 | live | 16.689 | 16.668 | 16.887 | 17.607 | 20.359 | 1 | 0 |
| 2 | live | 16.679 | 16.669 | 16.872 | 17.268 | 19.207 | 0 | 0 |
| 3 | live | 16.683 | 16.667 | 16.891 | 17.292 | 18.902 | 0 | 0 |
| 1 to 3 | dialog open | 16.677 to 16.686 | 16.667 to 16.668 | 16.869 to 16.877 | 17.16 to 17.42 | 17.95 to 19.99 | 0 | 0 |

The frame interval sits at the 60 fps pacing (16.67 ms) because the game caps the rate and the machine has headroom. The figure therefore proves that no frame misses the cap (p95 below 17 ms, maximum 20.7 ms in 1,800 live frames), not the raw CPU cost per frame. The measured script Update path (all `Update` calls of one frame) is 0.11 ms on average and 0.22 to 0.25 ms at p95. The 60 fps goal (F5-R14) is met on Windows. The emulator frame time belongs to T351.

## Acceleration (development players, OPS-1)

Budget (F5-R14): host frame simulation p95 of at most 1 ms at S_max = 10 s per frame on Windows. Method: the real `FactoryRuntimeHost` with the real `DevTimeScale` (`DefaultTimeScaleSource`, set by `IdleFactory.DevTools`), five steps one after the other (x1, x10, x60, x600, Max), each with 60 unscored warm-up frames and 600 scored frames. The host step is timed as the span from an `Update` of execution order -30000 to one of +30000. In player D that span contains the host's `Update` (accounting, scale and clamp, `RunSession.Advance`), the DevTools overlay's `Update` and nothing else.

**Player D: W003-B with auto-deliver** (the `pending` contract is never satisfied, so the per-pass auto-delivery check runs at every pass; the contract stays active). Three launches:

| Step | Effective multiplier (reported) | Simulated s per frame (avg, max) | Host step p95 ms, launch 1 / 2 / 3 | Host step avg ms (launch 1) | Frame avg ms | Frame p95 ms |
|---|---|---|---|---|---|---|
| x1 | 1.0 | 0.02, 0.02 | 0.038 / 0.045 / 0.042 | 0.026 | 16.68 | 16.77 to 16.79 |
| x10 | 10.0 | 0.17, 0.19 | 0.040 / 0.044 / 0.045 | 0.029 | 16.68 to 16.69 | 16.75 to 16.82 |
| x60 | 60.0 | 1.00, 1.2 | 0.055 / 0.056 / 0.055 | 0.040 | 16.68 to 16.69 | 16.76 to 16.79 |
| **x600** | 600.0 | 9.99, **10.00** | **0.173 / 0.172 / 0.200** | 0.141 | 16.68 | 16.81 to 16.84 |
| **Max** | 599.0, 600.6, 599.5 (about 600) | 10.00, **10.00** | **0.207 / 0.205 / 0.197** | 0.157 | 16.68 | 16.80 to 16.89 |

x600 and Max both reach the S_max clamp (10 simulated seconds per 16.67 ms frame at the 60 fps cap), so the two steps are the same load; Max reports an effective multiplier of about 600 because the frame interval hovers around 16.67 ms. Worst single host step in 600 frames: 1.41 ms (one frame at x600, launch 1; the p99 is 0.28 to 0.38 ms). **Acceleration p95: x600 0.17 to 0.20 ms, Max 0.20 to 0.21 ms against 1 ms: met (about 5 times below).** Frame time during acceleration is unchanged: 16.68 ms average, p95 below 16.9 ms, at most one frame above 20 ms per 600.

**Player C: the real UI at W005-L during acceleration** (development player with DevTools, 1280x720 and 390x844; the whole script `Update` path, host step plus UI refresh, so an upper bound of the host step):

| Step | Script Update p95 ms (1280x720 / 390x844) | Frame avg ms | Frame p95 ms | Frames above 20 ms (1280x720 / 390x844) |
|---|---|---|---|---|
| x1 | 0.232 / 0.249 | 16.68 | 16.91 | 0 / 0 |
| x10 | 0.366 / 0.325 | 16.67 to 16.68 | 16.85 to 17.00 | 1 / 0 |
| x60 | 0.365 / 0.403 | 16.67 to 16.68 | 16.84 to 16.87 | 0 / 1 |
| x600 | 0.536 / 0.540 | 16.68 | 16.93 | 0 / 1 |
| Max | 0.714 / 0.682 | 16.67 to 16.69 | 16.99 to 17.02 | 0 / 1 |

Even with the whole UI refresh included the path stays below 1 ms at p95 (worst 0.71 ms); the frame rate holds at 60 fps during acceleration. Emulator acceleration timing is evidence only and was not run.

## #370 follow-up: DevTools overlay, marker and safe area (checked, not fixed)

The T308 review finding 7 said the overlay row was anchored at `bottom: 0` without a safe-area inset. That placement was replaced by T340/T344 (the row sits at the top left under the HUD strip and the detail, notice and world-hint bars, inside the safe area). The check in player C converts element bounds to screen pixels (`worldBound` times `scaledPixelsPerPoint`):

| Check | Result |
|---|---|
| Overlay row and "ACCELERATED xN" marker against the HUD (top strip, tab bar, detail line, notice bar, world hint), x1 and x600, 100 % text scale, at 1280x720 and 390x844 | no overlap in any of the four combinations (`overlapStrip`, `overlapTabBar`, `overlapDetail`, `overlapNotice`, `overlapWorldHint` all false). Example 390x844 at x600: strip [0,0 390x35], marker [5,40 204x23], row [5,68 284x53], tab bar [0,802 390x42] |
| Same at 200 % text scale with x1 (marker hidden) | no overlap: at 390x844 the strip is 69 px high and the row starts at y = 79; at 1280x720 the strip is 51 px and the row starts at y = 69 |
| 200 % text scale with the marker showing | held only in the declared-safe-area check below. A text-scale change made together with the marker appearing was reset to 100 % by the shell in the same layout pass (`TextScalePercent` read 100 after re-applying). On Windows the shell's system factor is 100 %, and 200 % exists only through the probe's `ApplyTextScale` |
| Safe area | `Screen.safeArea` on Windows is the full screen, so a real inset cannot be produced. The probe drove `RefreshPlacement` with a declared safe area (40 px sides, 60 px bottom, 90 px top inset) at 200 % text and x600: the union of row and marker lay inside the declared area at 1280x720 (overlay [49,99 567x159] in [40,90 1200x570]) and at 390x844 (overlay [45,95 284x81] in [40,90 310x694]), without overlapping the strip or the tab bar. A real device cutout is covered only by the emulator evidence of T351 |
| Transient | `PlacementSeconds` is 0.5 s: after a text-scale change the overlay row keeps its old position until the next placement refresh, so for up to 0.5 s it can sit over the enlarged top strip at 390x844 (a capture in the frame of the change showed this). It is a development-only overlay and a run-time text-scale change does not occur on a device (the system factor is read at start-up), so it is recorded, not an acceptance finding |
| Digit shortcuts 1 to 5 | the probe queues real Input System key states for `Digit1` to `Digit5` in the player: the selected step follows (x1, x10, x60, x600, Max) in 5 of 5 cases at both window sizes |
| Tab order | all five buttons are focusable with `tabIndex` 0 in document order x1, x10, x60, x600, Max. A synthetic `KeyDownEvent` for Tab did not move focus (probe limitation). A synthetic `NavigationMoveEvent` (Next), the event the Tab key produces, moved focus x1 > x10 > x60 > x600 > Max and wrapped to x1. A real OS Tab key press was not injected, and Left, Right, Home and End were not checked here |

Recommendation for a later UI/UX pass (not a defect found by this run): add Play Mode coverage for the digit shortcuts and the Tab order of the overlay, since the overlay tests do not cover them yet; the checks above are one-off evidence.

## IL2CPP (open item, PF-13)

The Windows gate is the Mono development player (PF-13), as in T300, T336, T318 and T329. A Windows IL2CPP player was not built or measured. The Android ARM64 player is IL2CPP: its W005-L return (budget 2 s on the emulator), its 60 fps and its acceleration timing are evidence only and belong to T351, not to this record. The risk that IL2CPP changes the ratio stays open and recorded; at 273.5 ms on Windows Mono the algorithm has 1.83 times headroom on the 500 ms budget.

## Notes and deviations

- **Probe provenance.** The T336 probe (`.t336/`) did not survive; the W005-L probe is a new implementation of the same protocol (same fixture, same timed calls, 1 cold and 5 timed per path, 10 launches). Its algorithm figure follows the T336 method but is not byte-identical tooling, so the comparison with T336 (233.8 ms on revision 2) is by the ratio of completions only. Player A was built with the first version of the probe; the later edits touched only modes B, C and D (frame pacing in the empty scene, the dialog dismissal, the overlay and Tab checks) and none of the `w005l` and `w003b` code.
- **`runInBackground`.** The players report `Application.runInBackground = True`; the project setting was not changed and no `ProjectSettings` change is committed. Every windowed launch logged `focused=True` at each step, so focus did not matter in these runs.
- **Player B is not a pure release build.** It is a non-development player (no `Development` flag, no profiler), but it carries the `IDLEFACTORY_DEV` define so that it uses the development save root and cannot touch the owner's real save, and it carries the probe. Its frame time therefore includes the DevTools overlay (a small row of buttons; its `Update` is part of the measured 0.11 ms). A strictly release player would be slightly cheaper.
- **Dialog.** The W005-L cold start shows the offline summary dialog; both phases (open and dismissed) are recorded.
- **Development save root.** Players B and C seeded W005-L into `IdleFactory-Dev` under the player's persistent data path. The original contents were copied first and restored afterwards (6 files, hashes identical) and the leftover directories of this run were removed. The release root `IdleFactory` was never read or written.
- **Screenshots.** `frame-live-1280x720.png` shows the live W005-L factory with the DevTools row. `overlay-390x844-declared-safe.png` was captured by the screenshot call at the end of the frame in which the probe had already applied its next step, so it shows a transient state; the numeric bounds above are the evidence and the image is illustrative.

## Reproduction (T349)

1. This worktree at `3a44af5` with the probe files copied to `Assets/T349Probe/` (`T349Probe.cs`, `IdleFactory.T349Probe.asmdef`, define constraint `T349PROBE`) and `Assets/T349Editor/Editor/T349Build.cs`, never committed (the text copies are `w005l-measurement/probe/*.txt`).
2. `Unity.exe -batchmode -nographics -projectPath <worktree> -buildTarget win64 -executeMethod T349Build.BuildAll -t349root <dir> -t349which a,b,c,d` builds A (empty scene, development, `T349PROBE`), B (game scene, non-development, `T349PROBE;IDLEFACTORY_DEV`), C (game scene, development, both defines) and D (empty scene, development, both defines).
3. `run.ps1 -Exe <A> -Mode w005l|w003b -Launches 10 -Headless`; `-Mode frame` on B and `-Mode uiaccel` on C with `-SeedFresh -ExtraArgs "-screen-width 1280 -screen-height 720 -screen-fullscreen 0"`; `-Mode accel` on D. Every launch writes `T349 ...` lines to `-t349out` and quits (`-t349quit`).
4. Probe file SHA-256: `T349Probe.cs.txt` `27d762c89904bd2829e74d199b5a0addf4b44cd05108e1be4ed0fb8a74a77ae5`, `T349Build.cs.txt` `09fb6767000ddcb6f70504384411052c9e91999ebf65325124499ff9ce15b4de`, `IdleFactory.T349Probe.asmdef.txt` `049e39a048267a19607792504a1687925c00ec9c079a9353f7baf2fd1d20ed0d`, `run.ps1.txt` `92616ff064abf882565e9aacaeb38a04890d5edfd711e6d3f6ec146bd91fd9d6`. Raw result lines of every launch: `w005l-measurement/raw/`.

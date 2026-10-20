# W003-B Option 0 measurement (T300, Issue #362, #353)

**Task:** T300, plan step 0, research F5-R02 and F5-R18 (PF-3, PF-10), #353 Option 0. Measure only: no production, content or semantic change. This record gates T316, T325 and T326 (F5-R02, F5-R18 PF-3).

**Base:** commit `a41c53fbe991a7972377ea567ed42f886a4a8413`, measured in a separate detached worktree. All instrumentation and harness code lived there as uncommitted local changes and is reproduced in the appendix. Nothing of it is committed.

## Result in one paragraph

W003-B takes **962.5 ms** (median of 10 run medians, each the median of 5 timed reconciles) on the Windows Mono development player, with the Unity editor and the emulator closed. That is **1.92 times** the 500 ms Windows budget. All ten launches miss: the fastest run median is 934.9 ms (1.87 times), the slowest 1048.2 ms (2.10 times). The spread of the run medians is **11.8 percent over all ten launches, so the PF-10 criterion (at most 10 percent) is formally not met**. This is immaterial for the decision: the criterion protects a comparison against a budget or a baseline, and here every single launch is at least 1.87 times the budget, so no spread of this size can move the median to 500 ms. (The first five launches alone give 10.1 percent; the last five give 6.4 percent, which is a post-hoc supplement, not a pass.) The Stopwatch attribution puts **77 percent** of the time into `CompletionPass` and **14.5 percent** into the run predicate. Both are inventory-access code (`UnitsThatFit`, `TryExchange`, string-keyed lookups). The step scan is about 1 percent. The host deviated from the quiet-host rule (see "Host deviations from the quiet-host rule (PF-9)"). On the emulator W003-B takes **4.6 s** (median of 3 run medians) against the 2 s budget, measured on a loaded host. **Decision: Option 1 (index-based hot loop as an additional API) with a required re-measure gate and an escalation path.** Option 1 is necessary; this record does not show that it is sufficient. It is owned by task T360 and must precede T316 and T326. Details and conditions are in "Decision".

## Workload (W003-B)

| Item | Value |
|---|---|
| Fixture | `Assets/IdleFactory/Tests/PlayMode/Fixtures/SaveV2/Resources/W003PlayerFixtures/05-w003-b-bound.json` (Save V2, 3745 bytes) |
| SHA-256 | `7e278091c3b6fd3a9047bf90555fc2749663e406ac97218a26f611a35e6e8be0` (checked in every launch, `hashOk=True`) |
| State | 12 slots (3 Miners, 4 Smelters, 3 Presses, 2 Assemblers), one Item effect, Overdrive active, auto-sell on Gear only |
| Catalog | the test-local W003 catalog, rebuilt exactly as `W003PlayerFixtures.BuildCatalog` does for the bound variant (`level2SpeedPpm` 40/30/40/37.5 M, `maxLevel` 2, `storageCapacity` 16,000, `maxCombinedSpeedPpm` 10^9) |
| Timed operation | one `OfflineSegmenter.Reconcile(run, permanent, departure, departure + 9 h)` call, the 8 h credited cap applies (E(8 h) = 2.304e11 ticks). A fresh decode and a fresh `OfflineSegmenter` precede every call and are not timed. This is the "algorithm" path of `reviews/w003-measurements.md` in Feature 003 |
| Work done per call (deterministic) | 2,764,800 completions (12 x 230,400, the event bound), 230,401 processed timestamps, 460,802 passes, maximum 2 passes per timestamp |
| Per launch | 2 unscored warm-up reconciles (JIT), then 5 timed reconciles. The run median is the median of those 5 |

## Host and build

| Item | Value |
|---|---|
| Windows | Windows 11 Pro 10.0.26200, AMD Ryzen 7 7800X3D (8 cores, 16 threads), 31.8 GB RAM, board MS-7E56 |
| Unity | `6000.6.0f1` |
| Build type | Windows x64 **Mono** (`ScriptingImplementation.Mono2x`) **development** player (`BuildOptions.Development`, `Debug.isDebugBuild` true), batchmode, no graphics, empty scene |
| Harness | a standalone launch of the player (`-batchmode -nographics`), so **no Unity editor process exists during the timed window**. Details in the appendix. This differs from `scripts/test-unity.ps1 -StandaloneWindowsPlayer`, where the batch editor stays alive next to the player. A comparison set from that runner is given below |
| Emulator | AVD `Medium_Phone_API_36.0`, cold boot with host GPU, `-no-snapshot-load`. Not running during any Windows window |
| Android build | `IdleFactory.Editor.PlatformBuildCommands.BuildAndroidArm64DevelopmentGles3` (IL2CPP, ARM64, OpenGL ES 3 only, development, `-noProfilerAutoconnect`), APK SHA-256 `2dc59576d5b1f9b9bb3d1d3f27259ae5cd05a79a0b7737eeb8254ed44d4d1ef7` |

### Host deviations from the quiet-host rule (PF-9)

Two deviations from the quiet-host rule apply to this record:

1. **A League of Legends client (three processes, about 1.4 cores together) ran in all ten clean Windows windows.** It was not closed (not mine to close) and its load was the same in every window.
2. **A game (`TFTClient-Win64-Shipping`, 2.0 to 2.8 cores) overlapped the emulator windows and all instrumented Windows windows (phase attribution).** It was not running in the ten clean Windows windows.

Quiet-host repeats of the Windows phase and the emulator phase are **required before the Option 1 acceptance measurement**, which uses the same probe harness (appendix). The emulator figure is an upper bound and evidence only (PF-13).

The workstation also runs the owner's other applications: the League of Legends client (three processes, about 1.4 cores together in every window), Taskmgr, Claude desktop processes, Steam, iCUE, Battle.net and Chrome. Their load was the same in all ten clean windows (table below). What the rule requires was otherwise true: no Unity editor, no `PlayerWithTests`, no emulator and no foreign `adb` process during the ten clean Windows windows. The Unity Hub processes and `unity.exe serve` (Hub helper) were present and idle (0.00 s CPU per sample).

## Windows results (clean player, no instrumentation)

Ten launches of the same player. Each row is one launch: the run median (of 5), the 5 raw timed values in execution order, and the window.

| Launch | Run median ms | Raw timed runs ms | Window start UTC | Window end UTC | Samples in window | Processes of note (avg cores) |
|---|---|---|---|---|---|---|
| 1 | 955.07 | 944.02, 952.01, 955.07, 990.17, 973.75 | 17:55:04.6 | 17:55:11.3 | 3 | LeagueClient 0.63, UxRender 0.41, Ux 0.41 |
| 2 | 963.64 | 957.35, 963.64, 971.39, 1022.13, 952.90 | 17:55:21.8 | 17:55:28.6 | 3 | LeagueClient 0.64, Ux 0.39, UxRender 0.37 |
| 3 | 1022.66 | 1021.06, 1030.10, 1022.66, 1024.13, 1018.70 | 17:55:39.0 | 17:55:46.2 | 3 | LeagueClient 0.62, UxRender 0.44, Ux 0.38 |
| 4 | 1048.16 | 1048.16, 1079.74, 1037.61, 1043.83, 1060.67 | 17:55:56.2 | 17:56:03.5 | 4 | LeagueClient 0.62, UxRender 0.48, Ux 0.37 |
| 5 | 950.65 | 957.14, 948.22, 949.36, 973.44, 950.65 | 17:56:13.4 | 17:56:20.1 | 3 | LeagueClient 0.66, UxRender 0.40, Ux 0.40 |
| 6 | 977.09 | 977.09, 975.66, 976.18, 992.96, 985.85 | 17:56:36.5 | 17:56:43.4 | 3 | LeagueClient 0.63, UxRender 0.43, Ux 0.38 |
| 7 | 996.24 | 982.81, 985.90, 1001.10, 1053.57, 996.24 | 17:56:53.7 | 17:57:00.8 | 3 | LeagueClient 0.64, UxRender 0.45, Ux 0.39 |
| 8 | 934.85 | 933.05, 939.60, 934.85, 929.37, 957.38 | 17:57:10.9 | 17:57:17.4 | 3 | LeagueClient 0.62, Ux 0.36, UxRender 0.36 |
| 9 | 949.59 | 955.82, 962.77, 949.59, 947.07, 943.85 | 17:57:27.0 | 17:57:33.7 | 3 | LeagueClient 0.63, UxRender 0.42, Ux 0.37 |
| 10 | 961.28 | 972.91, 961.28, 961.44, 957.90, 955.37 | 17:57:43.2 | 17:57:49.9 | 3 | LeagueClient 0.64, UxRender 0.46, Ux 0.38 |

All windows are on 2026-10-10. Key processes present in every sample: `Unity Hub` (5 idle processes) and `unity.exe serve` only. The sampler (`Get-Process` with CPU deltas every 2 s, top 10 by delta plus a fixed list of Unity, player, adb and emulator names) wrote one snapshot per 2 s, so a 7 s window holds 3 to 4 snapshots. The sampler's own CPU use is below 0.1 core.

| Statistic (PF-10) | Value |
|---|---|
| **Median of the 10 run medians** | **962.46 ms** |
| Range of the run medians | 934.85 to 1048.16 ms |
| Spread (max - min) / median, all 10 | **11.8 %** (limit 10 %: formally not met) |
| Spread, launches 1 to 5 | 10.1 % (median of medians 963.64 ms) |
| Spread, launches 6 to 10 (post-hoc supplement, not a pass) | 6.4 % (median of medians 961.28 ms) |
| Median of all 50 timed runs | 972.15 ms (range 929.37 to 1079.74) |
| Per completion | 348 ns (962.46 ms / 2,764,800) |
| Per processed timestamp | 4.18 microseconds |

**Spread:** the 10 percent limit is exceeded by a small margin on the full set, and launches 3 and 4 are about 8 percent slower than the rest. The process lists of all ten windows show the same background load, so the cause is not visible (host boost clock, other applications' bursts below the 2 s sample resolution). The decision does not depend on it: even the best launch is 1.87 times the budget. Launches 6 to 10 give 6.4 percent and the same median (961.28 ms); this is a post-hoc supplement (a subset chosen after seeing the data), not a pass of PF-10.

**Comparison set with the editor alive (`scripts/test-unity.ps1 -StandaloneWindowsPlayer -TestFile ...`, same logic in a test):** five launches, run medians 1018.79, 958.37, 1014.15, 1003.31 and 973.83 ms, median of medians 1003.31 ms, spread 6.0 percent. The batch editor next to the player (it used 2.2 to 2.6 cores in the sampler) adds about 4 percent. The first launch of this set had the coarse sampler only. The F003 value for W003-B on `ac4256c` was 951.7 and 1017.6 ms (T122) and 965.7 ms with the emulator closed (T133), so the baseline did not move.

## Phase attribution (Stopwatch accumulators)

Instrumentation: `Stopwatch.GetTimestamp` pairs around the phases of `RunSimulator.Advance`/`ProcessTimestamp` (appendix). Phases: step scan (the two loops that find the next event and advance progress), boundary/refresh (`ApplyBoundary`, `RefreshDerived`), `CompletionPass`, `AutoSell`, unlock pass, run predicate. Build **light** (timers only, 5 launches x 5 timed runs = 25 runs) is the attribution basis.

| Phase | Mean ms per reconcile (instrumented) | Share of the instrumented total (1415.7 ms) |
|---|---|---|
| `CompletionPass` (`UnitsThatFit` and `TryExchange` per completion) | 1084.6 | **76.6 %** |
| Run predicate (loop over the 12 machines, `UnitsThatFit` per eligible machine) | 204.7 | **14.5 %** |
| `AutoSell` | 34.7 | 2.5 % |
| Step scan | 19.0 | 1.3 % |
| Boundary expiry and `RefreshDerived` | 8.3 | 0.6 % |
| Unlock pass | 8.5 | 0.6 % |
| Sum of the timed intervals | 1359.8 | 96.0 % |
| `ProcessTimestamp` total (all phases above except the step scan, plus untimed glue) | 1388.7 | 98.1 % |

Split inside `CompletionPass` and `AutoSell` (build **lightfine**, 3 launches x 5 timed runs = 15 runs, mean total 2043 ms, so about twice the clean time). **This fine split carries about 2 times overhead and is valid for ordering only, not for sizing:** `UnitsThatFit` (`SpeedComposer.TryCompletedUnits`) 544.3 ms (26.6 percent of that total), `GlobalInventory.TryExchange` 890.5 ms (43.6 percent), together 96 percent of the instrumented `CompletionPass` (1488.4 ms). In `AutoSell`, `GlobalInventory.Units` 19.3 ms and `TrySell` 14.0 ms of 80.4 ms. The run predicate also spends its time in `UnitsThatFit`.

**Lookups (string-keyed `GlobalInventory.IndexOf`).** A counting build (build **coarse** and **fine**, counters on) gives, per 8 h reconcile: 460,802 `CompletionPass` calls, 460,802 `AutoSell` calls, 5,529,612 `UnitsThatFit` calls (2 per completion) and **14,745,663 `IndexOf` calls** (64 per timestamp, 5.33 per completion). An isolated calibration in the same player (`inventory.Units("iron.gear")`, 5,000,000 calls, 4 resource ids, binary search with `string.CompareOrdinal`) costs **7.3 ns per call including the loop**. That bounds the lookups at 14.7 M x 7.3 ns = **about 107 ms, about 11 percent of the 962 ms clean time**. This is a calibrated bound, not a measurement, because a timer inside `IndexOf` would cost more than `IndexOf`. The rest of the inventory-access cost (interface iteration over `IReadOnlyList<ResourceAmount>`, per-call validation and `HasDuplicate` checks in `TryExchange`, checked arithmetic) is not lookup cost.

**Instrumentation overhead (caveat).** Clean total 962.5 ms. Timer-only build: median of 5 run medians 1427.2 ms (1308.8 to 1469.7), +48 percent. Timers plus counters: 1432.95 ms. Timers plus the fine timers: 2102.3 ms (+118 percent). One timer read costs 34 to 37 ns in isolation (calibration lines in the result files), 15 reads per timestamp would explain about 130 ms, but the observed overhead is about 465 ms, so a read costs about 135 ns in situ (it probably limits Mono's inlining and register use in the hot methods). The absolute millisecond figures of the phases are therefore inflated and **only the shares are used**, with these biases: phases with many reads per unit of work (`AutoSell`, the run predicate, the fine split) are inflated more than `CompletionPass`, so the true share of `CompletionPass` is, if anything, larger than 77 percent. Also, the instrumented sets ran while the owner's game client `TFTClient-Win64-Shipping` was running (2.1 to 2.5 cores in the light windows), which the ten clean launches did not have. The shares were stable across the five light launches (`CompletionPass` 75 to 78 percent), which argues that this does not change the shares.

## Emulator (development player, test catalog)

The Android player cannot run the test assembly, so the same logic ran as a runtime probe (appendix) inside the development APK: it loads the packaged `05-w003-b-bound.json` through `Resources` (the fixtures ship in every build), rebuilds the identical catalog, waits 20 s after scene load, and then runs 2 unscored plus 5 timed reconciles on the main thread with the scene idle. Results come from `adb logcat -s Unity`.

| Item | Value |
|---|---|
| Device | AVD `Medium_Phone_API_36.0` (`sdk_gphone64_x86_64`, Android 16 / API 36), `emulator-5554`, 2 GB RAM, 4 vCPUs (`nproc`), `SystemInfo.processorCount` 2, host GPU, cold boot (`-gpu host -no-snapshot-load`) |
| Player | IL2CPP ARM64 GLES3 development APK of `a41c53f` plus the probe, the game scene loaded (`com.idlefactory.player.IdleFactoryGameActivity`) |
| Hash and work | `hashOk=True`; 2,764,800 completions, 230,401 timestamps, 460,802 passes in every run, the same as Windows |
| Procedure | install, one unscored launch (4854.37 ms median, discarded), then three scored cold launches (`am start -W` after `am force-stop`) |

| Launch | Run median ms | Raw timed runs ms | Window start UTC | Window end UTC | Samples in window | Processes of note (avg cores) |
|---|---|---|---|---|---|---|
| 1 | 4611.89 | 4790.92, 4754.94, 4547.24, 4559.35, 4611.89 | 18:46:31.9 | 18:47:05.2 | 16 | TFTClient 2.00, qemu 0.91, LeagueClientUxRender 0.78 |
| 2 | 5383.12 | 5161.13, 5383.12, 6013.41, 5409.11, 5228.20 | 18:47:42.2 | 18:48:20.4 | 18 | TFTClient 2.77, qemu 0.90, LeagueClientUxRender 0.88 |
| 3 | 4565.42 | 4576.54, 4733.27, 4565.42, 4534.22, 4487.53 | 18:48:55.5 | 18:49:28.2 | 16 | TFTClient 2.06, qemu 1.03, LeagueClientUxRender 0.81 |

**Median of the 3 run medians: 4611.89 ms.** Spread (5383.12 - 4565.42) / 4611.89 = **17.7 %**. Against the 2 s emulator budget this is a miss by a factor of **2.3**; the whole range is 2.3 to 2.7 times. The emulator is 4.8 times slower than the clean Windows player on this workload.

**Caveats, read before using these numbers:**
- **The host was loaded during the emulator windows.** The owner's game client `TFTClient-Win64-Shipping` used 2.0 to 2.8 cores, next to the League client (about 1.5 cores) and the emulator (`qemu-system-x86_64`, about 0.9 to 1.0 core, so the emulated CPU is effectively one host thread). The quiet-host rule could not be met for this phase (I do not close the owner's applications). The values are probably an upper bound on the quiet-host time; I cannot quantify by how much (the emulated CPU is one host thread on a 16-thread host, so contention is partial, not total). Treat 4.6 s as "misses by at least 2 times" only if a quiet-host rerun confirms it.
- The Android player is IL2CPP over ARM64 translation, the Windows player is Mono. They are not comparable code paths. T124 measured W003-S on this emulator at 746 ms against 320 ms on Windows (2.3 times); here W003-B is 4.8 times. The difference is not explained by this measurement and is a reason to repeat the emulator phase on a quiet host.
- The foreign-process check before each launch found no `adb` or Unity process other than the idle `adb` server and my own transient `adb` client calls (the one extra count before launch 3 was such a client). No `monkey` process existed. The Unity batch builds had finished before the emulator booted. The Android batch build killed the ADB server as documented; `adb wait-for-device` preceded the install.
- The probe is not the shipped `ResumeAndReconcile` path (no file I/O, no checkpoint promotion). It matches the Windows probe and the F003 "algorithm" figure.

## Process lists and windows

The task rule asked for `Get-Process | Sort-Object CPU -Descending | Select-Object -First 25` before each window. Cumulative CPU time does not show what ran during a 7 s window, so a sampler wrote, every 2 s from 4 s before the launch until 2 s after it, the 10 processes with the largest CPU delta plus the Unity, player, adb and emulator processes. The per-window digest is in the tables above. One verbatim in-window snapshot of each kind follows; the other 100+ snapshots are in the scratch files of the run and are not committed.

Clean Windows launch 1, snapshot inside the window (taken at the first sample at or after 17:55:06 UTC):

```
=== 2026-10-10T17:55:07.7823431Z procs=455
  top  W003B                      pid=36548  dCpu=  2.02s cpu=       6.4s ws=145MB
  top  LeagueClient               pid=27716  dCpu=  1.38s cpu= 164.642.8s ws=310MB
  top  LeagueClientUx             pid=31168  dCpu=  0.91s cpu=  93.766.4s ws=94MB
  top  LeagueClientUxRender       pid=28500  dCpu=  0.38s cpu=  23.992.2s ws=137MB
  top  LeagueClientUxRender       pid=12424  dCpu=  0.38s cpu=  65.322.4s ws=1235MB
  top  Taskmgr                    pid=13768  dCpu=  0.23s cpu=  13.024.7s ws=271MB
  top  steamwebhelper             pid=16516  dCpu=  0.22s cpu=   5.366.5s ws=163MB
  top  claude                     pid=30420  dCpu=  0.17s cpu=   2.934.4s ws=135MB
  top  steamwebhelper             pid=18612  dCpu=  0.09s cpu=   3.530.8s ws=348MB
  top  claude                     pid=27632  dCpu=  0.06s cpu=   1.988.9s ws=652MB
  key  unity                      pid=28276  dCpu=  0.00s cpu=       0.1s ws=16MB
  key  Unity Hub                  pid=17040  dCpu=  0.00s cpu=     767.4s ws=174MB
  key  Unity Hub                  pid=19952  dCpu=  0.02s cpu=      93.3s ws=119MB
  key  Unity Hub                  pid=27784  dCpu=  0.00s cpu=       0.2s ws=41MB
  key  Unity Hub                  pid=28156  dCpu=  0.00s cpu=       9.7s ws=61MB
  key  Unity Hub                  pid=28640  dCpu=  0.00s cpu=      30.0s ws=140MB
```

Emulator launch 1, snapshot inside the window (first sample at or after 18:46:40 UTC):

```
=== 2026-10-10T18:46:40.8724189Z procs=443
  top  TFTClient-Win64-Shipping   pid=19220  dCpu=  4.00s cpu=     796.8s ws=3239MB
  top  qemu-system-x86_64         pid=31472  dCpu=  1.53s cpu=     643.0s ws=3100MB
  top  LeagueClientUxRender       pid=37576  dCpu=  1.41s cpu=   1.683.5s ws=859MB
  top  LeagueClient               pid=24232  dCpu=  1.00s cpu=     910.2s ws=777MB
  top  LeagueClientUx             pid=17828  dCpu=  0.53s cpu=     546.1s ws=84MB
  top  claude                     pid=30420  dCpu=  0.23s cpu=   3.086.3s ws=130MB
  top  chrome                     pid=23648  dCpu=  0.16s cpu=       0.2s ws=36MB
  top  Taskmgr                    pid=13768  dCpu=  0.16s cpu=  13.323.5s ws=245MB
  top  iCUE                       pid=11548  dCpu=  0.14s cpu=   5.803.3s ws=130MB
  top  chrome                     pid=30936  dCpu=  0.13s cpu=       0.1s ws=61MB
  key  adb                        pid=18260  dCpu=  0.00s cpu=       0.4s ws=14MB
  key  crashpad_handler           pid=6564   dCpu=  0.00s cpu=       0.0s ws=9MB
  key  crashpad_handler           pid=31280  dCpu=  0.00s cpu=       0.1s ws=9MB
  key  crashpad_handler           pid=31596  dCpu=  0.00s cpu=       0.0s ws=9MB
  key  emulator                   pid=21188  dCpu=  0.00s cpu=       0.0s ws=13MB
  key  qemu-system-x86_64         pid=31472  dCpu=  1.53s cpu=     643.0s ws=3100MB
  key  unity                      pid=28276  dCpu=  0.00s cpu=       0.1s ws=16MB
  key  Unity Hub                  pid=17040  dCpu=  0.00s cpu=     780.7s ws=168MB
  key  Unity Hub                  pid=19952  dCpu=  0.00s cpu=      94.9s ws=115MB
  key  Unity Hub                  pid=27784  dCpu=  0.00s cpu=       0.2s ws=41MB
  key  Unity Hub                  pid=28156  dCpu=  0.00s cpu=       9.7s ws=60MB
  key  Unity Hub                  pid=28640  dCpu=  0.00s cpu=      30.3s ws=136MB
```

## Decision

**Option 1 with a required re-measure gate and an escalation path.** Option 1 is an index-based hot loop as an additional API in `RunSimulator`, `SpeedComposer` and `GlobalInventory`, command-path semantics unchanged, applied before the sale-pricing and auto-delivery work units. **Option 1 is necessary; it is not shown to be sufficient.** Option 3 (steady-state jump) is out of F005 (F5-R02).

### Trigger

F5-R02 and F5-R18 define no numeric trigger. The trigger used here (the clean Windows median of run medians exceeds the 500 ms budget, and more than half of the time is in the inventory-access paths Option 1 replaces) is **adopted by the Architect review of 2026-10-10**. Both conditions hold. The trigger attributes where time is spent; it does not predict that Option 1 reaches the budget.

### Evidence

1. **The miss is large and stable.** 962.5 ms against 500 ms (1.92 times); no launch is below 934.9 ms (1.87 times). The 11.8 percent spread cannot move the median to the budget. The emulator, on a loaded host, misses its 2 s budget by 2.3 times or more (upper bound, evidence only).
2. **The cost sits in the code F005 changes.** 91 percent of the time (`CompletionPass` 77 percent, run predicate 14.5 percent) is `UnitsThatFit` and `TryExchange`, which read and write the inventory by string key through `IReadOnlyList` recipes. F005 adds `SalePricing` into `AutoSell` (T316), per-pass auto-delivery with a reserve (T326) and the Timeline mapping (T325) on top of that path. The step scan, which the F003 wiki names as the structural cost, is 1.3 percent: the loop itself is not the problem, the per-completion inventory work is.
3. **Waiting would mix causes.** F5-R02: the settle path changes in F005, so a later measurement could not separate the F003 cost from F005's. This record is the baseline for PF-3.
4. **What "no change" would require.** At 348 ns per completion the W005-L gate of 500 ms (F5-R14) **holds only up to about 1.4 M completions** in the 8 h return, before F005 adds per-event work. The W005-L fixture does not exist yet (step 10), so its event count is unknown, which is a reason not to bet on it.

### Scope of Option 1

- **In scope:** specialising the `UnitsThatFit` / `TryExchange` path: precomputed input and output index and amount arrays, no interface enumeration, a fused exchange with the fit computation.
- **Out of scope (where Option 3 begins):** any steady-state or segment jump. No skipped timestamps, no closed-form batching of completions.
- **Unchanged:** the passes per timestamp, their ordering, and the PF-1 pass numbers. Results stay **bit-identical** against the committed W003 fixtures and hashes (F5-R02). The command path keeps its semantics.

### Re-measure gate and escalation

- **Owner of the work:** task **T360 "Option 1 hot loop"** (added to tasks.md by the parent). T360 depends on T300 and T315 and is a predecessor of T316 and T326. Its acceptance: bit-identical W003 fixtures and hashes, unchanged pass and PF-1 numbers, and the PF-3 re-measure with this record's protocol (median of 5 per launch, at least 3 launches, same fixture hash, same probe harness) **on a quiet host**.
- **Escalation:** if the re-measure after T360 is still above 500 ms, the parent records an **owner decision before T326**. The decision covers a budget or fixture decision, a follow-up issue, or a further specialisation. Option 3 stays out of F005.
- **Not claimed:** that Option 1 reaches the budget. Mono-only numbers cannot show it; the IL2CPP release player is an open PF-13 item.

### Limits of the evidence, which T360 must treat as open

- **String lookups alone are about 11 percent** (about 107 ms, a calibrated bound, not a measurement). Replacing `IndexOf` by precomputed indexes cannot reach 500 ms. A reduction of at least 48 percent needs the whole exchange and unit-fit path to be specialised. Whether that is achievable was **not measured**: no prototype was built, because that is the work unit's job.
- The fine split of `CompletionPass` (`UnitsThatFit` 544 ms, `TryExchange` 890 ms) has about 2 times overhead and orders the two costs only; it does not size them.
- The 10 percent regression guard (PF-10) applies to the post-#356 baseline, not to this one.
### What this gates

**This record gates T316, T325 and T326 (F5-R02, F5-R18 PF-3).** T360 (Option 1 hot loop, depends on T300 and T315) is a predecessor of T316 and T326.

- **T316 (plan step 5, sale pricing, `Sell`, auto-sell):** applies Option 1 first, as its own text says ("applies Option 1 first if T300 chose it"). T300 chose it; the implementation is T360.
- **T325 and T326 (plan steps 7a-1 and 7a-2):** both depend on this record. F5-R18 PF-3 gates 7a-2 (T326, scratch state, auto-delivery) on the step-0 decision; T325 (the Timeline mapping) lists T300 as a dependency in tasks.md. T360 must be done before T326 adds auto-delivery to the settle loop. T325's own acceptance (bit-identical W003 fixtures for the mapping) is unrelated to the bit-identity of Option 1, which belongs to T360.
- **PF-3 baseline: 962.5 ms, spread 11.8 %** (clean player, this harness; to compare later measurements with): 962.46 ms median of 10 run medians; 348 ns per completion; 2,764,800 completions, 230,401 timestamps, 460,802 passes, maximum 2 passes per timestamp; fixture hash `7e278091...8be0`. After steps 1, 5, 7a-1, 7a-2 and 7b the same player type, harness and fixture must be used, with and without auto-delivery.

### Not done, still open

- **IL2CPP release player** (PF-13, #353): not measured; recorded as an open item.
- **Quiet-host repeats (Windows and emulator):** required before the T360 acceptance measurement. The 4.6 s emulator figure was taken with the owner's game client running and is an upper bound and evidence only (PF-13).
- **PR-2** (once-per-second HUD allocation, #353): out of scope here.
- The leases were held by the parent during the measurement; this worker did not acquire, renew or release them.

## Appendix: reproduction and instrumentation

All files are in the measurement worktree `.maqa-worktrees/base-a41c53f` (detached at `a41c53f`) and removed afterwards. `git status` there showed, besides the files below, a `ProjectSettings/ProjectSettings.asset` re-serialisation from the Unity builds, which was never committed.

SHA-256 of the uncommitted files:

| File | SHA-256 |
|---|---|
| tracked diff of the instrumentation (below) | `6f96bf50af035d81dbb201c235925e58b2d8d2c9f554a74241d47ea19cb03a03` |
| new `Assets/IdleFactory/Core/Run/Simulation/T300Phases.cs` | `6496981c9260889edbd9c3ae58bc438f04cdc6c219a048bfeac2e902f21ecd6c` |
| new `Assets/IdleFactory/Unity/Bootstrap/W003BDevicePlayer.cs` (probe) | `8f5f2fb7d4e329d44c9ecda23601d2feb801b97b92390d6df76cfcdbdb913325` |
| new `Assets/IdleFactory/Editor/T300Build.cs` (build entry point) | `dd2db1134e35ba7d77349a891fe0904aab4610808fbb092b885fcf0f0977d3f6` |
| new `Assets/IdleFactory/Tests/PlayMode/W003BOption0MeasurementTests.cs` (comparison set via the test runner) | `d5bd06d21af556bc59d29a6fff0091fb7af6175a20f447eea37e424b2880c30a` |

The clean player was built from the unmodified Core sources of `a41c53f` plus the probe, the build entry point and an empty scene. The instrumented players added `T300Phases.cs` and the tracked diff below, with these scripting defines passed through `BuildPlayerOptions.extraScriptingDefines`: `light` = `T300_PHASES`, `lightfine` = `T300_PHASES;T300_PHASES_FINE`. The earlier `coarse` and `fine` builds were the same without the `T300_COUNTS` guards (counters always on). `T300_COUNTS` was not defined in any scored build; the counts quoted above come from `coarse`/`fine` (deterministic, identical in every run). The Android APK was built from the clean sources plus the probe.

Probe (`W003BDevicePlayer`), in short: a `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` component waits `-t300delay` seconds (3 on Windows, 20 on Android), loads the packaged fixture, checks the SHA-256, runs 7 reconciles (2 unscored, 5 timed) with `Stopwatch` around `OfflineSegmenter.Reconcile` only, writes `T300-DEVICE` lines to the log, to `-t300out` (Windows) or `persistentDataPath/t300-result.txt` (Android), and quits on `-t300quit`. Under `T300_PHASES` it also logs the phase accumulators per run and the two calibration figures. Windows command per launch: `W003B.exe -batchmode -nographics -t300delay 3 -t300quit -t300out <file> -logFile <log>`. Build command: `Unity.exe -batchmode -nographics -projectPath <worktree> -buildTarget win64 -executeMethod IdleFactory.Editor.T300Build.BuildWindowsMono -t300exe <exe> [-t300defines <defines>]`. Android: `-buildTarget Android -executeMethod IdleFactory.Editor.PlatformBuildCommands.BuildAndroidArm64DevelopmentGles3 -noProfilerAutoconnect -buildOutput <apk>`; launch `adb shell am start -W -n com.DefaultCompany.idlefactorygame/com.idlefactory.player.IdleFactoryGameActivity`.

Calibration lines (build light): `stopwatchGetTimestampNs=37.46`, `inventoryUnitsLookupNs(4 ids, loop incl.)=7.28`, `stopwatchFrequency=10000000` (build coarse: 34.15 and 7.23).

### `T300Phases.cs`

```csharp
using System.Diagnostics;
using System.Globalization;

namespace IdleFactory.Run.Simulation
{
    /// <summary>T300 (#362) Stopwatch phase accumulators. Uncommitted measurement instrumentation, base worktree a41c53f only.</summary>
    public static class T300Phases
    {
        public static long Step, Boundary, Completion, AutoSell, Unlock, Predicate, ProcessTs, Total;
        public static long CompUnitsFit, CompExchange, AutoSellUnits, AutoSellTrySell, PredUnitsFit;
        public static long CompletionCalls, AutoSellCalls, UnitsFitCalls, IndexOfCalls, Timestamps;

        public static long Now() => Stopwatch.GetTimestamp();

        public static void Reset()
        {
            Step = Boundary = Completion = AutoSell = Unlock = Predicate = ProcessTs = Total = 0;
            CompUnitsFit = CompExchange = AutoSellUnits = AutoSellTrySell = PredUnitsFit = 0;
            CompletionCalls = AutoSellCalls = UnitsFitCalls = IndexOfCalls = Timestamps = 0;
        }

        private static string Ms(long ticks) => (ticks * 1000.0 / Stopwatch.Frequency).ToString("F1", CultureInfo.InvariantCulture);

        public static string Dump()
        {
            return "stepScanMs=" + Ms(Step) + " boundaryRefreshMs=" + Ms(Boundary) + " completionPassMs=" + Ms(Completion) +
                " autoSellMs=" + Ms(AutoSell) + " unlockPassMs=" + Ms(Unlock) + " runPredicateMs=" + Ms(Predicate) +
                " processTimestampTotalMs=" + Ms(ProcessTs) +
                " | fine: compUnitsFitMs=" + Ms(CompUnitsFit) + " compExchangeMs=" + Ms(CompExchange) + " autoSellUnitsMs=" + Ms(AutoSellUnits) +
                " autoSellTrySellMs=" + Ms(AutoSellTrySell) + " predUnitsFitMs=" + Ms(PredUnitsFit) +
                " | counts: completionPassCalls=" + CompletionCalls + " autoSellCalls=" + AutoSellCalls + " unitsFitCalls=" + UnitsFitCalls +
                " indexOfCalls=" + IndexOfCalls + " timestamps=" + Timestamps;
        }
    }
}
```

### Tracked diff (Core, `a41c53f`)

```diff
diff --git a/Assets/IdleFactory/Core/Run/Domain/GlobalInventory.cs b/Assets/IdleFactory/Core/Run/Domain/GlobalInventory.cs
index 9397105..80956d2 100644
--- a/Assets/IdleFactory/Core/Run/Domain/GlobalInventory.cs
+++ b/Assets/IdleFactory/Core/Run/Domain/GlobalInventory.cs
@@ -214,6 +214,9 @@ namespace IdleFactory.Run.Domain
 
         private int IndexOf(string resourceId)
         {
+#if T300_COUNTS
+            IdleFactory.Run.Simulation.T300Phases.IndexOfCalls++;
+#endif
             if (resourceId == null) return -1;
             int lo = 0, hi = _ids.Length - 1;
             while (lo <= hi)
diff --git a/Assets/IdleFactory/Core/Run/Simulation/RunSimulator.cs b/Assets/IdleFactory/Core/Run/Simulation/RunSimulator.cs
index bdc219c..ded4e3c 100644
--- a/Assets/IdleFactory/Core/Run/Simulation/RunSimulator.cs
+++ b/Assets/IdleFactory/Core/Run/Simulation/RunSimulator.cs
@@ -268,6 +268,7 @@ namespace IdleFactory.Run.Simulation
             if (!ProcessTimestamp(0, true)) return Finish(_fault);
             while (now < ticks)
             {
+                long t3a = T300Phases.Now();
                 long step = ticks - now;
                 if (_eventIndex < _events.Count)
                 {
@@ -283,7 +284,10 @@ namespace IdleFactory.Run.Simulation
                 for (int i = 0; i < _machines; i++)
                     if (_running[i]) _progress[i] += step;
                 now += step;
+                long t3b = T300Phases.Now();
+                T300Phases.Step += t3b - t3a;
                 if (!ProcessTimestamp(now, false)) return Finish(_fault);
+                T300Phases.ProcessTs += T300Phases.Now() - t3b;
             }
 
             Commit();
@@ -580,6 +584,10 @@ namespace IdleFactory.Run.Simulation
         private bool ProcessTimestamp(long now, bool settle)
         {
             LastTimestamps++;
+#if T300_COUNTS
+            T300Phases.Timestamps++;
+#endif
+            long q0 = T300Phases.Now();
 
             // 1. Expire every effect, Overdrive state and contract deadline whose boundary is due; then re-derive
             //    the effective cycle of every machine and rescale its progress once (R03).
@@ -593,6 +601,7 @@ namespace IdleFactory.Run.Simulation
                 if (!RefreshDerived()) return false;
                 _configDirty = false;
             }
+            T300Phases.Boundary += T300Phases.Now() - q0;
 
             // Settle (first timestamp of an advance only): a state handed over by a command may hold stock of an
             // auto-sell resource (auto-sell just enabled, a delivery, an import). The command rule of R01 settles
@@ -614,18 +623,31 @@ namespace IdleFactory.Run.Simulation
                 while (true)
                 {
                     passes++;
+                    long c0 = T300Phases.Now();
                     int completed = CompletionPass();
+                    long c1 = T300Phases.Now();
+                    T300Phases.Completion += c1 - c0;
+#if T300_COUNTS
+                    T300Phases.CompletionCalls++;
+#endif
                     if (completed < 0) return false;
                     if (!AutoSell()) return false;
+                    T300Phases.AutoSell += T300Phases.Now() - c1;
+#if T300_COUNTS
+                    T300Phases.AutoSellCalls++;
+#endif
                     if (completed == 0) break;
                 }
+                long u0 = T300Phases.Now();
                 bool unlocked = UnlockPass(evaluateAll);
+                T300Phases.Unlock += T300Phases.Now() - u0;
                 evaluateAll = false;
                 if (!unlocked) break;
             }
             LastPasses += passes;
             if (passes > LastMaxPassesPerTimestamp) LastMaxPassesPerTimestamp = passes;
 
+            long r0 = T300Phases.Now();
             // 5. Run predicate: a machine with progress below its cycle runs until the next event exactly when it
             //    is active and at least one recipe unit's inputs are in stock and its net exchange fits.
             for (int i = 0; i < _machines; i++)
@@ -638,6 +660,7 @@ namespace IdleFactory.Run.Simulation
                 }
                 _running[i] = run;
             }
+            T300Phases.Predicate += T300Phases.Now() - r0;
             return true;
         }
 
@@ -711,7 +734,17 @@ namespace IdleFactory.Run.Simulation
         {
             units = 0;
             if (!_allowed[i]) return true;
-            if (!SpeedComposer.TryCompletedUnits(_batch[i], _recipe[i], _outputFactor[i], _stock, out units))
+#if T300_COUNTS
+            T300Phases.UnitsFitCalls++;
+#endif
+#if T300_PHASES_FINE
+            long f0 = T300Phases.Now();
+#endif
+            bool fitOk = SpeedComposer.TryCompletedUnits(_batch[i], _recipe[i], _outputFactor[i], _stock, out units);
+#if T300_PHASES_FINE
+            T300Phases.CompUnitsFit += T300Phases.Now() - f0;
+#endif
+            if (!fitOk)
                 return Fail(RunAdvanceResult.ArithmeticOverflow);
             return true;
         }
@@ -732,7 +765,13 @@ namespace IdleFactory.Run.Simulation
                 if (units < 1) continue;
                 MachineRecipe recipe = _recipe[i];
                 int limit = units < _batch[i] ? LimitOf(i, units) : LimitNone;
+#if T300_PHASES_FINE
+                long x0 = T300Phases.Now();
+#endif
                 RunStateResult exchange = _stock.TryExchange(recipe.Inputs, recipe.Outputs, units, _outputFactor[i]);
+#if T300_PHASES_FINE
+                T300Phases.CompExchange += T300Phases.Now() - x0;
+#endif
                 if (exchange != RunStateResult.Ok)
                 {
                     Fail(exchange == RunStateResult.ValueLimit ? RunAdvanceResult.ArithmeticOverflow : RunAdvanceResult.InvalidArgument);
@@ -779,13 +818,26 @@ namespace IdleFactory.Run.Simulation
         {
             for (int s = 0; s < _sellCount; s++)
             {
+#if T300_PHASES_FINE
+                long y0 = T300Phases.Now();
+#endif
                 long units = _stock.Units(_sellId[s]);
+#if T300_PHASES_FINE
+                T300Phases.AutoSellUnits += T300Phases.Now() - y0;
+#endif
                 if (units < 1) continue;
                 if (!CheckedMath.TryMultiply(units, _sellBase[s], out long gross) ||
                     !MulDiv.MulDivFloor(gross, _sellPpm[s], SpeedComposer.Ppm, out long credit) ||
                     !CheckedMath.TryAdd(_cash, credit, out long cash))
                     return Fail(RunAdvanceResult.ArithmeticOverflow);
-                if (_stock.TrySell(_sellId[s], units) != RunStateResult.Ok) return Fail(RunAdvanceResult.InvalidArgument);
+#if T300_PHASES_FINE
+                long y1 = T300Phases.Now();
+#endif
+                var sold = _stock.TrySell(_sellId[s], units);
+#if T300_PHASES_FINE
+                T300Phases.AutoSellTrySell += T300Phases.Now() - y1;
+#endif
+                if (sold != RunStateResult.Ok) return Fail(RunAdvanceResult.InvalidArgument);
                 _cash = cash;
                 _sold[_sellIndex[s]] += units;
             }
```


---

# T360 (#429) Option 1 hot loop: PF-3 re-measure

**Task:** T360, plan step 5 (Option 1 part), research F5-R02, F5-R18 (PF-3). **Code under test:** commit `b8773e0` (branch `maqa/issue-429-t360-option1-hot-loop`, based on `9d60a1f`): `RecipeIndex`, `GlobalInventory.TryExchangeIndexed` and the index-based completion pass in `RunSimulator`. **Protocol:** the T300 protocol of this record (W003-B fixture `7e278091...8be0`, one `OfflineSegmenter.Reconcile` over 9 h away, 2 unscored warm-ups plus 5 timed reconciles per launch, run median per launch, median of run medians), same fixture and workload, Windows Mono development player, `-batchmode -nographics`, no editor process of this worker running during the timed windows.

## Probe harness: reconstructed, not the T300 original

The T300 probe (`W003BDevicePlayer.cs`) and build entry point (`T300Build.cs`) were uncommitted and no longer exist in any ref or worktree. Both were **reconstructed** for T360 from the T300 appendix description (listings below). **Their SHA-256 values therefore differ from the T300 table.** They reproduce the T300 protocol as closely as the description allows. Two additions beyond the T300 description: the probe also prints the post-state SHA-256 of the reconciled state (`SaveV2Mapper.Serialize` of the final run state, a bit-identity check) and the work counters. To make the comparison independent of the reconstruction, the **same reconstructed probe was also built and run against the pre-change base `9d60a1f`** ("base" below) under the same conditions. Neither the probe nor any instrumentation was committed to Core or to Assets; both were copied into throw-away detached worktrees (`probe-new` at `b8773e0`, `probe-base` at `9d60a1f`).

| File | SHA-256 |
|---|---|
| `W003BDevicePlayer.cs` (reconstructed, listing below) | `fa56b9292506d1faff8f970459e3a5b93f7702fb2b76eee24c2363a452df01da` |
| `T300Build.cs` (reconstructed, listing below) | `afd02e51433d3eb9fe8882536c7247ced70d70200bcb1d0321f69f5a6e45119e` |
| `IdleFactory.Core.dll` of the new player | `725ba6d4b84239c143d6b2d63a0d43a71939be1258b9401ea714aaa4c4df8a51` |
| `IdleFactory.Core.dll` of the base player | `77bf224f50ae2141a29bc6c5b47101c774b73b18093d8d3b5507265331ef7b5e` |
| Android dev APK (new, GLES3, IL2CPP ARM64, `-noProfilerAutoconnect`) | `9909d8cf5ddcedea54133d88bdefdf05a6b22c5b01bb199aa127424fe65e7ce2` |

The Windows player build command was `Unity.exe -batchmode -nographics -projectPath <wt> -buildTarget win64 -executeMethod IdleFactory.Editor.T300Build.BuildWindowsMono -t300exe <exe>` (Mono, development, one empty scene). The APK was built with `IdleFactory.Editor.PlatformBuildCommands.BuildAndroidArm64DevelopmentGles3 -noProfilerAutoconnect`, package `com.DefaultCompany.idlefactorygame`. The `W003B.exe` launcher stub is byte-identical for both players; the code differs in `IdleFactory.Core.dll`.

## Bit-identity evidence

| Check | Base `9d60a1f` | New `b8773e0` |
|---|---|---|
| Fixture SHA-256 in every launch (`hashOk`) | `7e278091...8be0`, True (10 of 10) | same, True (10 of 10 on Windows, 5 of 5 on the emulator) |
| Completions / timestamps / passes / max passes per timestamp | 2,764,800 / 230,401 / 460,802 / 2 | 2,764,800 / 230,401 / 460,802 / 2 (PF-1 pass numbers unchanged, also on the emulator) |
| Cash delta | 33,177,600 | 33,177,600 |
| SHA-256 of the serialized post-reconcile state | `1bba8949f6e394f99ca3d8456c0942cf27d0d3773fb8ac8e20b07e2f056d98d0` (10 of 10) | `1bba8949f6e394f99ca3d8456c0942cf27d0d3773fb8ac8e20b07e2f056d98d0` (10 of 10 on Windows, 5 of 5 on the emulator) |

The post-state digest is identical for base and new, on Windows (Mono) and on the emulator (IL2CPP ARM64). The committed W003 fixtures and hashes are untouched. Unity suites at `b8773e0`: Edit Mode 1666 of 1666, Play Mode 1553 of 1553, Windows player suite (`-StandaloneWindowsPlayer`) 24 of 24, targeted `RecipeIndexEquivalenceTests` 4 of 4, none skipped.

## Windows results (clean player, 10 launches each)

Each value is the run median of 5 timed reconciles. The summary is the median of the 10 run medians.

| Player | Run medians ms (launch 1 to 10) | Median of run medians | Range | Spread | ns per completion |
|---|---|---|---|---|---|
| Base `9d60a1f` (this host, same probe) | 938.15, 946.77, 942.94, 963.91, 962.32, 938.74, 958.49, 944.67, 968.33, 974.73 | **952.63 ms** | 938.15 to 974.73 | 3.9 % | 344.6 |
| New `b8773e0` | 199.33, 198.78, 200.54, 199.97, 197.60, 198.05, 198.32, 199.88, 200.93, 201.29 | **199.61 ms** | 197.60 to 201.29 | 1.8 % | 72.2 |

Base reproduces the T300 baseline (962.5 ms, spread 11.8 %, 348 ns per completion) within 1.0 %, which supports the reconstructed probe. **The new median of 199.6 ms is 4.8 times faster than the base on the same host (a 79 % reduction) and 2.5 times below the 500 ms budget (F5-R14 W005-L gate, PF-3).** Raw timed values of each launch were written by the probe; for example launch 1 of the new player: 200.06, 199.27, 198.57, 199.33, 200.82.

The speedup is larger than the T300 attribution suggested (string lookups alone about 11 %): the whole exchange and unit-fit path mattered (interface enumeration through `IReadOnlyList`, per-call sums, duplicate and overlap scans), as the T300 limits of evidence allowed. The same-condition base-versus-new comparison above, not the cross-session T300 number, is the evidence for this change.

## Emulator results (5 launches)

AVD `Medium_Phone_API_36.0`, cold boot with host GPU (`-gpu host -no-snapshot-load -no-snapshot-save`), Android 16 / API 36, x86-64 guest, 2 cores, 1971 MB, development build, IL2CPP; 20 s delay; the probe wrote `t300-result.txt` into the app's external files directory. The game scene runs in the same APK (as in T300), so the figure includes the game's own startup load.

| Launch | Run median ms | Raw timed runs ms |
|---|---|---|
| 1 | 428.33 | 430.59, 428.33, 429.53, 424.56, 426.53 |
| 2 | 431.64 | 431.44, 434.02, 432.15, 426.23, 431.64 |
| 3 | 444.79 | 456.94, 441.81, 441.98, 447.75, 444.79 |
| 4 | 436.89 | 436.89, 435.50, 440.08, 435.73, 436.91 |
| 5 | 429.25 | 429.25, 429.18, 430.79, 428.23, 430.59 |

**Median of run medians: 431.64 ms** (range 428.33 to 444.79, spread 3.8 %), 154 to 161 ns per completion, against the 2 s emulator budget: 4.6 times below it. T300 recorded 4.6 s (an upper bound with the game client running); this run was on a quiet host. No pre-change emulator baseline was built (the base comparison exists for Windows only). The emulator is evidence; the release-device IL2CPP measurement stays PF-13 (#353).

## Gate

The clean Windows median is **199.6 ms, below the 500 ms gate**; the T300 escalation condition is not met and no owner decision is needed before T326 on account of PF-3. Option 3 stays out of F005. PF-3 reference for later steps (T316, T325, T326, with and without auto-delivery): **199.61 ms median, spread 1.8 %, 72.2 ns per completion, same player type, harness and fixture (`7e278091...8be0`)**.

## Host and PF-9 deviations

| Item | Value |
|---|---|
| Host | the T300 host (Windows 11 Pro 10.0.26200, Ryzen 7 7800X3D, 31.8 GB), Unity `6000.6.0f1` |
| Windows windows | 2026-10-13 13:23:17 to 13:27:25 UTC (new 13:23:17 to 13:24:50, base 13:24:58 to 13:27:25) |
| Emulator windows | 2026-10-13 13:46:22 to 13:49:09 UTC |
| Owner-confirmed state | the owner closed Steam, the Epic launcher and the foreign Unity editor before the run |

PF-9 deviations: the host was not strictly idle. These ran in the background in all windows, each near 0 average cores: the Unity Hub processes (PIDs 6884, 11356, 21368, 23288, 34044), `unity.exe serve` (PID 13332), `mcp-for-unity` (PID 23080), the Riot Client services (idle launcher, not the in-game client), Claude desktop, ChatGPT desktop, iCUE, Battle.net, CurseForge, Discord, Docker Desktop and Chrome. Average cores of the busiest processes per window (process CPU time sampled before and after each window): Windows windows: `claude` (PID 31032) 0.10 to 0.16, `ChatGPT` (PID 35448) 0.04 to 0.12, `claude` (PID 13340) 0.03 to 0.08, `iCUE` 0.02 to 0.04, `Battle.net` 0.02 to 0.04, everything else at or below 0.04. Together about 0.3 to 0.4 cores of 16 threads. Emulator windows: `qemu-system-x86_64` 1.17 to 1.38 cores (the guest), `ChatGPT` 0.04 to 0.06, `iCUE` 0.02 to 0.03, `Battle.net` 0.02 to 0.03. No Unity editor or CLI process of this worker, no `PlayerWithTests.exe` and no emulator ran during a Windows window; the Android build and the Unity test runs had finished before the first timed launch. The emulator was shut down with `adb emu kill` after the last launch. The League of Legends game and TFT of T300 did not run.

## Reproduction (T360)

1. Detached worktrees at the commit under test and at the base, with the two probe files copied to `Assets/IdleFactory/Unity/Bootstrap/W003BDevicePlayer.cs` and `Assets/IdleFactory/Editor/T300Build.cs` (never committed there).
2. Build the player with the `T300Build.BuildWindowsMono` command above. Per launch: `W003B.exe -batchmode -nographics -t300delay 3 -t300quit -t300out <file> -logFile <log>`.
3. Android: build the APK with `BuildAndroidArm64DevelopmentGles3` (above), cold-boot the emulator, `adb install -r -t`, `adb shell am start -W -n com.DefaultCompany.idlefactorygame/com.idlefactory.player.IdleFactoryGameActivity`, and read `/sdcard/Android/data/com.DefaultCompany.idlefactorygame/files/t300-result.txt` once it contains `T300-DEVICE result`.

### Reconstructed `W003BDevicePlayer.cs`

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Simulation;
using IdleFactory.Unity.Persistence;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace IdleFactory.Unity.Bootstrap
{
    /// <summary>
    /// T300/T360 probe (W003-B offline reconcile timing in a player). RECONSTRUCTED for T360 from the description in
    /// reviews/w003b-measurement.md; the T300 original was never committed. Temporary measurement code: it is copied
    /// into a throw-away worktree and never lives in Core or in a committed Assets folder.
    /// A RuntimeInitializeOnLoadMethod(AfterSceneLoad) component waits -t300delay seconds (3 on Windows, 20 on Android),
    /// loads the packaged W003-B fixture (05-w003-b-bound, SHA-256 checked), runs 7 reconciles (2 unscored warm-ups, 5
    /// timed) with a Stopwatch around OfflineSegmenter.Reconcile only (a fresh decode and a fresh OfflineSegmenter
    /// precede every call), logs T300-DEVICE lines, writes them to -t300out (Windows) or
    /// persistentDataPath/t300-result.txt (Android) and quits on -t300quit.
    /// </summary>
    public sealed class W003BDevicePlayer : MonoBehaviour
    {
        private const string FixtureResource = "W003PlayerFixtures/05-w003-b-bound";
        private const string FixtureSha256 = "7e278091c3b6fd3a9047bf90555fc2749663e406ac97218a26f611a35e6e8be0";
        private const long TenHoursMs = 10L * 3600 * 1000;
        private const int Warmups = 2;
        private const int Timed = 5;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var host = new GameObject("W003BDevicePlayer");
            DontDestroyOnLoad(host);
            host.AddComponent<W003BDevicePlayer>();
        }

        private static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }

        private static bool Flag(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

        private IEnumerator Start()
        {
            bool android = Application.platform == RuntimePlatform.Android;
            float delay = android ? 20f : 3f;
            string delayArg = Arg("-t300delay");
            if (delayArg != null) delay = float.Parse(delayArg, CultureInfo.InvariantCulture);
            yield return new WaitForSecondsRealtime(delay);

            var lines = new List<string>();
            try
            {
                Run(lines);
            }
            catch (Exception exception)
            {
                lines.Add("T300-DEVICE error=" + exception);
            }

            foreach (string line in lines) Debug.Log(line);
            string output = android ? Path.Combine(Application.persistentDataPath, "t300-result.txt") : Arg("-t300out");
            if (!string.IsNullOrEmpty(output)) File.WriteAllText(output, string.Join("\n", lines) + "\n");
            if (Flag("-t300quit")) Application.Quit();
        }

        private static string Sha256Hex(byte[] bytes)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string Num(double value) => value.ToString("F2", CultureInfo.InvariantCulture);

        private static void Run(List<string> lines)
        {
            lines.Add("T300-DEVICE host unity=" + Application.unityVersion + " platform=" + Application.platform + " os=" + SystemInfo.operatingSystem +
                " cpu=" + SystemInfo.processorType + " cores=" + SystemInfo.processorCount + " ramMB=" + SystemInfo.systemMemorySize +
                " developmentBuild=" + Debug.isDebugBuild + " stopwatchHighRes=" + Stopwatch.IsHighResolution + " frequency=" + Stopwatch.Frequency);
            var asset = Resources.Load<TextAsset>(FixtureResource);
            if (asset == null) throw new InvalidOperationException("fixture missing: " + FixtureResource);
            byte[] bytes = asset.bytes;
            string hash = Sha256Hex(bytes);
            bool hashOk = hash == FixtureSha256;
            lines.Add("T300-DEVICE fixture bytes=" + bytes.Length + " sha256=" + hash + " hashOk=" + hashOk);

            var samples = new List<double>();
            long completions = 0, timestamps = 0, passes = 0, maxPasses = 0;
            string digest = "";
            long cashDelta = 0;
            for (int run = 0; run < Warmups + Timed; run++)
            {
                RunCatalog catalog = BuildCatalog();
                var read = SaveV2Mapper.Deserialize(bytes, catalog);
                if (!read.IsSuccess) throw new InvalidOperationException("decode failed: " + read.Code);
                SaveV2Document document = read.Document;
                var segmenter = new OfflineSegmenter(catalog);
                long departure = document.Meta.DepartureUtcMs;
                long returnUtcMs = departure + 9L * 3600 * 1000; // nine hours away: the eight hour credited cap applies

                long t0 = Stopwatch.GetTimestamp();
                RunOfflineSummary summary = segmenter.Reconcile(document.Run, document.Permanent, departure, returnUtcMs);
                double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;

                if (summary.Outcome != RunOfflineOutcome.Applied) throw new InvalidOperationException("outcome " + summary.Outcome);
                completions = segmenter.Simulator.LastCompletions;
                timestamps = segmenter.Simulator.LastTimestamps;
                passes = segmenter.Simulator.LastPasses;
                maxPasses = segmenter.Simulator.LastMaxPassesPerTimestamp;
                cashDelta = summary.CashDelta;
                var written = SaveV2Mapper.Serialize(document.Meta, document.Run, document.Permanent);
                digest = written.IsSuccess ? Sha256Hex(written.Bytes) : "serialize-failed:" + written.Code;
                bool scored = run >= Warmups;
                if (scored) samples.Add(ms);
                lines.Add("T300-DEVICE run=" + run + " scored=" + scored + " ms=" + Num(ms));
            }

            var sorted = new List<double>(samples);
            sorted.Sort();
            double median = sorted[sorted.Count / 2];
            lines.Add("T300-DEVICE work completions=" + completions + " timestamps=" + timestamps + " passes=" + passes +
                " maxPassesPerTimestamp=" + maxPasses + " cashDelta=" + cashDelta + " postStateSha256=" + digest);
            lines.Add("T300-DEVICE result runMedianMs=" + Num(median) + " nsPerCompletion=" + Num(median * 1_000_000.0 / Math.Max(1, completions)) +
                " raw=" + string.Join(",", samples.ConvertAll(v => Num(v))) + " hashOk=" + hashOk);
        }

        // The W003-B catalog exactly as W003PlayerFixtures.BuildCatalog builds it for the bound variant.
        private static RunCatalog BuildCatalog()
        {
            long[] level2SpeedPpm = { 40_000_000, 30_000_000, 40_000_000, 37_500_000 };
            var always = new UnlockCondition("always");
            var resources = new[]
            {
                new ResourceDef("iron.ore", "Iron Ore", "F003-RES-IRON-ORE", 1, always),
                new ResourceDef("iron.bar", "Iron Bar", "F003-RES-IRON-BAR", 3, always),
                new ResourceDef("iron.plate", "Iron Plate", "F003-RES-IRON-PLATE", 8, always),
                new ResourceDef("iron.gear", "Gear", "F003-RES-IRON-GEAR", 24, always),
            };
            var recipes = new[]
            {
                new MachineRecipe("recipe.mine-iron-ore", new ResourceAmount[0], new[] { new ResourceAmount("iron.ore", 2) }, 20_000_000),
                new MachineRecipe("recipe.smelt-iron-bar", new[] { new ResourceAmount("iron.ore", 2) }, new[] { new ResourceAmount("iron.bar", 1) }, 20_000_000),
                new MachineRecipe("recipe.press-iron-plate", new[] { new ResourceAmount("iron.bar", 2) }, new[] { new ResourceAmount("iron.plate", 1) }, 40_000_000),
                new MachineRecipe("recipe.assemble-iron-gear", new[] { new ResourceAmount("iron.plate", 2) }, new[] { new ResourceAmount("iron.gear", 1) }, 50_000_000),
            };
            var cost = new Curve(10, 1_150_000, "ceil");
            string[] ids = { "machine.miner", "machine.smelter", "machine.press", "machine.assembler" };
            string[] labels = { "Miner", "Smelter", "Press", "Assembler" };
            var machines = new MachineDef[4];
            for (int i = 0; i < 4; i++)
                machines[i] = new MachineDef(ids[i], labels[i], "F003-MACHINE-" + labels[i].ToUpperInvariant(), recipes[i].Id,
                    2, cost, level2SpeedPpm[i], new SpeedMilestone[0], always);
            var items = new[]
            {
                new ItemDef("item.boost", 1, "Boost", "F003-ITEM-BOOST", TenHoursMs, "factory", new string[0],
                    new EffectDef("machineSpeed", "allMachines", null, 2_000_000)),
            };
            return new RunCatalog(1, resources, recipes, machines,
                new[] { new StorageLevelDef(1, 1000, 0), new StorageLevelDef(2, 16_000, 2500) },
                new[] { new FactoryLevelDef(1, 12, 0) },
                new ContractOfferDef[0], items, new ResearchDef[0], new IndustryUpgradeDef[0],
                new OverdriveDef(TenHoursMs, 300_000, 2_000_000, always),
                new PrestigeDef(10_000_000, "sqrt", 10, 10_000_000),
                new RunStartDef(25, new[] { "iron.ore", "machine.miner" }, 500_000, 1_000_000,
                    1_000_000_000L, 4, 2_000_000),
                new RunOfflinePolicy(1, 4, 5, 288_000_000_000L));
        }
    }
}
```

### Reconstructed `T300Build.cs`

```csharp
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IdleFactory.Editor
{
    /// <summary>
    /// T300/T360 probe build entry point. RECONSTRUCTED for T360 (the T300 original was never committed). Temporary: it lives only
    /// in a throw-away measurement worktree. Builds a Windows x64 Mono development player containing one empty scene (the probe
    /// component starts itself through RuntimeInitializeOnLoadMethod). Usage: Unity.exe -batchmode -nographics -projectPath
    /// &lt;worktree&gt; -buildTarget win64 -executeMethod IdleFactory.Editor.T300Build.BuildWindowsMono -t300exe &lt;exe&gt; [-t300defines A;B]
    /// </summary>
    public static class T300Build
    {
        private const string ScenePath = "Assets/T300Empty.unity";

        private static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }

        public static void BuildWindowsMono()
        {
            bool ok = false;
            try
            {
                string exe = Arg("-t300exe");
                if (string.IsNullOrEmpty(exe)) throw new ArgumentException("Missing -t300exe <path>");
                string defines = Arg("-t300defines");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(exe)));
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = exe,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development,
                    extraScriptingDefines = string.IsNullOrEmpty(defines) ? null : defines.Split(';'),
                });
                Debug.Log("[T300Build] result " + report.summary.result + ", " + report.summary.totalErrors + " errors, output " + report.summary.outputPath);
                ok = report.summary.result == BuildResult.Succeeded;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
```

---

# T318 (#381) W003-B re-measure after T317 (sell commands)

**Task:** T318, plan step 5 (sale pricing, T317 = Issue #380), research F5-R02, F5-R18 (PF-3, PF-10). **Code under test:** `78f2a25` (merge of PR #450, T317). Auto-deliver does not exist at this commit, so there is one variant. **Shared session:** by owner decision (2026-10-15, #381 and #394) T318 and T329 share one quiet-host session; the host, the probe, the protocol and the baseline below are the same for both records. Each task records its own result (T329 follows this section). Measured 2026-10-20, 23:24 to 23:36 UTC. Windows Mono development player only; see "Platform scope".

## Result in one paragraph

W003-B at `78f2a25` takes **204.50 ms** (median of 10 run medians; spread 5.9 %; 74.0 ns per completion) against **201.12 ms** for the post-Option-1 baseline `a96b0df` measured in the same session with the same probe: **+1.7 %**. A second, independent series gave 203.52 ms against 200.12 ms (**+1.7 %**). Against the T360 recorded reference (199.61 ms) the figure is +2.5 %. The **10 % regression guard (PF-10) is met** for T317, and the 500 ms Windows gate (PF-3) is met by a factor of 2.4. Work counters and the post-reconcile state digest are bit-identical to the baseline (T317 changes no W003-B behavior). The spread of series A is 5.9 % (limit 10 %, met).

## Baseline applied for the 10 % guard, and why

PF-10 (research.md F5-R18) asks for "a relative regression guard of 10 % on W003-B with auto-deliver against the post-#356 baseline". The record offers two readings:

1. **T300 baseline `a41c53f` (962.5 ms), measured after #356 and before the Option 1 hot loop.** Every later commit is far below it (about 4.7 times faster), so a 10 % guard against it cannot detect a regression of the kind PF-10 is meant to catch.
2. **The post-Option-1 reference of T360 (#429).** Its Gate section states the PF-3 reference for later steps (T316, T325, T326, with and without auto-delivery): 199.61 ms median, spread 1.8 %, 72.2 ns per completion, same player type, harness and fixture. This is the first measurement after the baseline's hot loop was replaced and the only one against which a 10 % guard has meaning.

I apply **reading 2**, and because host conditions drift between sessions I **re-measured its code in this session**: `a96b0df` (merge of PR #446, T360). Its `IdleFactory.Core` source is identical to `b8773e0`, the T360 code under test (`git diff --stat b8773e0 a96b0df -- Assets/IdleFactory/Core` is empty). The guard compares same-host, same-session figures. The recorded 199.61 ms is shown for reference: the session baseline reproduces it within 0.8 % (201.12 ms; series B 200.12 ms = +0.3 %), which supports the reconstructed probe and an unchanged host condition. Reading 1 is also reported where it matters (T329).

## Probe harness (extended reconstruction)

The probe is the T360 reconstruction (listing in "Reconstructed `W003BDevicePlayer.cs`" above) with these additions, none of which changes the timed operation (a `Stopwatch` around `OfflineSegmenter.Reconcile` only):

- `-t300variant none|pending|early` selects a variant; `none` is exactly the T360 workload (T360 catalog, no contract). The catalog adds two delivery offers only for the other variants (used in T329).
- After the reconcile (outside the timer) it reads `LastPassBudget`, `LastPassBudgetExceeded`, `LastAutoDelivered` and `LastAutoDeliverBlocked` by reflection (`n/a` where the member does not exist at that commit) and prints the contract state.

| File | SHA-256 |
|---|---|
| `W003BDevicePlayer.cs` (T318/T329 extension of the T360 listing) | `8a66712c3d5934b18a714ffcd12199d2443f2793c63a8a7468c616f8b7ee23c1` |
| `T300Build.cs` (unchanged T360 listing) | `afd02e51433d3eb9fe8882536c7247ced70d70200bcb1d0321f69f5a6e45119e` |
| Fixture `05-w003-b-bound.json` | `7e278091c3b6fd3a9047bf90555fc2749663e406ac97218a26f611a35e6e8be0` (`hashOk=True` in all 130 series launches) |

The `IdleFactory.Core.dll` hashes of the five players are `2914e54ad9f84a6b3a6f1bd647c3b37601acc48e34acf9a0b2fed78fa6d856a1` (`a96b0df`), `a34db8cffa8e5dab7c0691d80e302e28935e235741fb3c5fdcce8095c554d6a0` (`78f2a25`), `b4cfd12a7c21d5d33e4593c35dc99c65ebef9871e7a75a0b8207754440e54a1b` (`355104f`), `4a2e011014210346537456563e2f25df06150b0b843a45d94691ba010533ad37` (`0ba64a7`) and `2a81f849ae91318df42a4d144f4e32d669b2aba0eaf1a380f6a140a6514ba831` (`6fc283d`). The probe extension source is the T360 listing with the `Variant`, `Diag` and `ApplyContract` helpers and the `BuildCatalog(variant)` offers described in the T329 section. It was copied into one detached throw-away worktree per commit (`.maqa-worktrees/w003b-probe-<sha>`) and never committed.

## Windows results (T318)

Players built with `Unity.exe -batchmode -nographics -quit -projectPath <wt> -buildTarget win64 -executeMethod IdleFactory.Editor.T300Build.BuildWindowsMono -t300exe <exe>` (Mono, development, one empty scene), one per commit; all builds had finished before the first timed launch. Per launch: `W003B.exe -batchmode -nographics -t300delay 3 -t300quit -t300variant none -t300out <file> -logFile <log>`; 2 unscored warm-ups plus 5 timed reconciles; run median of 5. Launches were **interleaved round-robin** across the configurations of the session (one launch of each configuration per round, 10 rounds, series A), so slow host drift affects all configurations alike. A second series (B) of 10 rounds repeated the key configurations in a different order.

| Series | Player | Run medians ms (launch 1 to 10) | Median of run medians | Range | Spread | ns per completion |
|---|---|---|---|---|---|---|
| A | Baseline `a96b0df` | 202.1, 202.9, 201.3, 202.5, 198.5, 200.3, 200.2, 203.3, 199.2, 200.9 | **201.12 ms** | 198.5 to 203.3 | 2.4 % | 72.7 |
| A | **T317 `78f2a25`** | 205.3, 206.2, 205.1, 203.2, 201.7, 213.8, 203.9, 202.9, 202.0, 209.2 | **204.50 ms** | 201.7 to 213.8 | 5.9 % | 74.0 |
| B | Baseline `a96b0df` | 198.8, 199.8, 201.8, 199.4, 200.5, 198.7, 203.6, 200.9, 198.9, 206.9 | 200.12 ms | 198.7 to 206.9 | 4.1 % | 72.4 |
| B | **T317 `78f2a25`** | 204.6, 201.5, 203.8, 203.2, 210.4, 204.1, 208.8, 202.8, 203.0, 202.9 | 203.52 ms | 201.5 to 210.4 | 4.4 % | 73.6 |

| Check | Baseline `a96b0df` | T317 `78f2a25` |
|---|---|---|
| Completions / timestamps / passes / max passes per timestamp | 2,764,800 / 230,401 / 460,802 / 2 | 2,764,800 / 230,401 / 460,802 / 2 (20 of 20 launches) |
| Cash delta | 33,177,600 | 33,177,600 |
| SHA-256 of the serialized post-reconcile state | `1bba8949f6e394f99ca3d8456c0942cf27d0d3773fb8ac8e20b07e2f056d98d0` | same (20 of 20 launches; equals the T360 digest) |
| PF-1 diagnostics (`LastPassBudget`, `LastPassBudgetExceeded`) | n/a (members do not exist at this commit) | n/a |

| Guard (PF-10) | Series A | Series B | Verdict |
|---|---|---|---|
| T317 against session baseline `a96b0df` | 204.50 / 201.12 = **+1.7 %** | 203.52 / 200.12 = **+1.7 %** | within 10 %: **met** |
| T317 against the T360 recorded reference 199.61 ms | +2.5 % | +2.0 % | within 10 %: met |
| Absolute gate (PF-3, F5-R14 W005-L) | 204.50 ms against 500 ms | | met |
| Spread limit (PF-10, 10 %) | 5.9 % | 4.4 % | met |

**T318 verdict: the 10 % regression guard is met (+1.7 % against the same-session baseline, reproduced in a second series).** Against the T300 figure of reading 1 (962.5 ms) the result is 78.7 % lower.

## Platform scope

Windows Mono development player only. PF-13 makes the Windows Mono development player the gate and the emulator (IL2CPP) evidence only; the T318 and T329 task texts, plan step 5 and F5-R18 PF-3 require no emulator figure for these re-measures (the T360 record already holds the emulator reference, 431.64 ms). The emulator was not started and `adb` was not used.

## Host and PF-9 evidence

| Item | Value |
|---|---|
| Host | the T300/T360 host: Windows 11 Pro 10.0.26200, Ryzen 7 7800X3D (16 threads), 31.8 GB, Unity `6000.6.0f1` |
| Timed windows | series A 23:24:26 to 23:32:20 UTC (473.8 s, 90 launches); series B 23:32:49 to 23:36:11 UTC (202.6 s, 40 launches). Per-launch start and end timestamps were logged |
| Editor / emulator | no `Unity.exe`, `UnityCrashHandler`, `PlayerWithTests`, `adb`, `qemu` or foreign `W003B` process existed at either series start (full process snapshot, 445 processes, taken immediately before each series) or during it; the five builds had exited before series A. Unity Hub processes were not running |
| Probe lifetime | each launch quits itself (`-t300quit`); no `W003B.exe` remained after the series |
| Background load | CPU time of every process sampled before and after each series. Average cores over the series (excluding the probe): LeagueClient 0.62, LeagueClientUx 0.46, LeagueClientUxRender 0.33 (second renderer 0.06), Chrome 0.17 and 0.06, Battle.net, Docker Desktop, audiodg and CurseForge 0.02 each; **about 1.9 cores in total of 16 threads**, identical in both series |

PF-9 deviation: the owner stated that the host was idle, but the **League of Legends client** (client, Ux, UxRender; about 1.4 cores together, the same load as in the ten clean windows of T300) and the usual background applications (Steam, Chrome, Battle.net, iCUE, Docker Desktop, the Claude and Codex desktop processes) were running. They are not mine to close. The load was constant across both series and all configurations (interleaving), and the session baseline reproduces the T360 reference within 0.8 %, so the comparison is same-host and same-session. The deviation is the one T300 recorded; it does not change the verdict because the figures lie 2.4 times below the budget and the guard verdict is a within-session ratio.

## Reproduction (T318 and T329)

1. For each of `a96b0df`, `78f2a25`, `355104f`, `0ba64a7` and `6fc283d`: `git worktree add --detach <abs path> <sha>`; copy the two probe files to `Assets/IdleFactory/Unity/Bootstrap/W003BDevicePlayer.cs` and `Assets/IdleFactory/Editor/T300Build.cs` (never committed); copy a `Library/` from the first built probe worktree to the others to shorten the import; build with the command above.
2. Launch the series round-robin as in "Windows results"; no editor or other build runs while a series runs.
3. Remove the probe worktrees afterwards.

## Deviations from the T360 protocol

- The probe has the extensions listed above, so its SHA-256 differs from the T360 value; the `none` variant is the T360 workload and reproduces the T360 numbers (digest, counters, 201 ms against 199.61 ms).
- Launches were interleaved across configurations instead of ten consecutive launches per player. This guards against drift and is not a relaxation. Ten launches per configuration (series A), plus ten more for the key configurations (series B).
- The probe worktrees shared a copied `Library/`; the player build is a clean `BuildPipeline.BuildPlayer`, so the copied cache affects import time only.
- The baseline is the merge commit `a96b0df` (Core identical to `b8773e0`), measured in this session.

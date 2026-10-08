# W003 measurements (baseline r1)

**Task:** T122 (Issue #204), SC-008, plan.md Performance (PE-1 to PE-7, QA-7). Measure-only: no production, content or semantic change. A miss goes to the Performance review in T130 with the options of R04; nothing is optimized here.

**Baseline:** this is baseline **r1** (PE-3). There is no comparable earlier measurement: Feature 001 T063 to T065 measured the closed-form buffer model, and the W001 records are legacy regression references only. They are not used as a budget or a comparison here.

**Host:** Windows 11 Pro 10.0.26200, AMD Ryzen 7 7800X3D (8 cores), 31.8 GB RAM. Unity `6000.6.0f1`, Windows Standalone **development** test player (`scripts/test-unity.ps1 -StandaloneWindowsPlayer`, Mono scripting, `Debug.isDebugBuild` true, which the allocation recorder needs). Another Unity editor process of this workstation was running during the runs; the two runs below differ by up to 7 percent, which is the run-to-run noise of this host.

**Test:** `Assets/IdleFactory/Tests/PlayMode/W003PlayerTimingTests.cs` (`[Category("StandalonePlayer")]`), with its helpers in `RunPlayerTestSupport.cs`. The tests assert robust facts only (fixture hash, round trip, workload ran, event-bound completions, counters sane) and log every figure as a `W003-*` line, which `scripts/test-unity.ps1` keeps in `PlayerPlayMode-results.xml`. They never fail on a wall-clock budget; the verdicts below are recorded by hand.

## Workload and fixture hashes (QA-7)

The committed Save V2 fixtures of T104 are loaded in the player through Play Mode `TextAsset` copies under `Assets/IdleFactory/Tests/PlayMode/Fixtures/SaveV2/Resources/W003PlayerFixtures/`. `SaveV2PlayerFixtureParityTests` (Edit Mode) proves every copy hash-equal to its original and to the hash the fixture README lists, and fails on a mismatch (negative case included). The player test checks the same hashes on the packaged bytes and that each fixture re-serializes to identical bytes.

| Variant | File | Bytes | SHA-256 |
|---|---|---|---|
| W003-B (bound, carries budgets 2 and 3) | `05-w003-b-bound.json` | 3745 | `7e278091c3b6fd3a9047bf90555fc2749663e406ac97218a26f611a35e6e8be0` |
| W003-S (auto-sell sanity, as assigned) | `06-w003-s-stress.json` | 3668 | `0467d293d69ed358ae1ec74d5b94192aaaf5f983f75cfad23bca6dd27a95ab73` |
| W003-R (reference, as assigned) | `07-w003-r-reference.json` | 3790 | `7cb3d7a7461ac31a8d8273fff19ad4d30e9547414a7332542c9345dd02e9d22d` |

All three are 12-slot factories (3 Miners, 4 Smelters, 3 Presses, 2 Assemblers), one Item effect and Overdrive active. The catalog they load against is the test-local one of `W003Workload` (Edit Mode), which a player assembly cannot reference; `W003PlayerFixtures` rebuilds it with identical numbers. The equivalence is checked by the fixture round trip and by the results: W003-B reaches exactly 12 x 230,400 = 2,764,800 completions and W003-S exactly 691,200, the figures of T098.

W003-B is measured in addition to the assigned S and R because plan.md, Performance names it as the carrier of budgets (2) and (3) and of the SC-008 event bound, and states that W003-S must not be used as that record.

## Budget verdicts

| Budget | Variant | Result (run 1 / run 2) | Verdict |
|---|---|---|---|
| (2) Host frame p95 over 600 frames, at most 1 ms | W003-B | 0.041 / 0.040 ms | PASS |
| | W003-S | 0.017 / 0.018 ms | PASS |
| | W003-R | 0.017 / 0.018 ms | PASS |
| (2) Snapshot rebuilds by `StructureRevision` at most the structural changes in the window (PE-5) | all three | 4 rebuilds for 4 structural changes (revision delta 4) | PASS |
| (3) 8 h offline reconcile, median of 5, at most 500 ms (Windows) | W003-B (SC-008 event bound) | **951.7 / 1017.6 ms** (full cold start 988.1 / 1047.2 ms) | **MISS, route to T130** |
| | W003-S | 319.4 / 331.7 ms (full 352.1 / 366.4 ms) | PASS |
| | W003-R (context) | 175.5 / 181.8 ms (full 208.1 / 216.2 ms) | PASS |
| (1) 0 `GC.Alloc` samples per warmed `Advance` (PE-4) | advance path (gate, `Advance`, hot copies) | 0 samples in 300 frames, all three | PASS |
| | whole frame with the HUD refresh read path | 6 samples in 300 frames, all three | **Not zero, route to T130** (see below) |
| (5) Passes per timestamp at most N + 1 = 13 (PE-7) | all three | maximum 2 (B, S), 4 (R) | PASS |

Run 1 is the focused player run of the test file; run 2 is the full `-StandaloneWindowsPlayer` suite on the same tree. Budget (4), emulator FPS, belongs to T124.

## Host frame (PE-5)

Per frame: `FactoryRuntimeHost.RunNeedsAdvance` and `RunSession.Advance` (the body of `AdvanceToCumulativeTicks`, 166,667 ticks, one 60 FPS frame), then the read path of `RunShellPresenter.Refresh` in `HudRefreshProbe`: `ReadCash`, `ReadRp`, the inventory totals, the `StructureRevision` gate with its snapshot rebuilds (`RunSnapshots.Items`, `Research`), the machine-state copy and change detection, the once-per-second `RunSnapshots.Overdrive` status and, on a change, `RunSnapshots.Factory` plus `SlotGridModel.DescribeAll`. 120 warm-up frames, then 600 measured frames; four structural commands (`SetActive` off then on for slot 0 and slot 3, at frames 100, 200, 300 and 400) run before the host frame as the input event of that frame, so the rebuild they cause is inside the measured frame.

**Limit:** a player test has no scene, and `RunShellDocument` loads its UXML and USS through the asset database, which a player cannot do. The UI Toolkit element writes (`shell.SetCash` and the rest) and the slot grid render are therefore not in the figure. The figure is the host plus the application read path; the real HUD is covered by the emulator present-interval gate (T124).

| Variant | p50 | p95 | p99 | max | mean | Rebuilds by revision | Structural changes | Factory snapshot rebuilds (total) | Overdrive status reads |
|---|---|---|---|---|---|---|---|---|---|
| W003-B run 1 | 0.015 | 0.041 | 0.059 | 0.469 | 0.019 | 4 | 4 | 43 | 14 |
| W003-B run 2 | 0.015 | 0.040 | 0.043 | 0.724 | 0.019 | 4 | 4 | 43 | 14 |
| W003-S run 1 | 0.015 | 0.017 | 0.035 | 0.050 | 0.016 | 4 | 4 | 4 | 14 |
| W003-S run 2 | 0.016 | 0.018 | 0.024 | 0.044 | 0.016 | 4 | 4 | 4 | 14 |
| W003-R run 1 | 0.015 | 0.017 | 0.032 | 0.079 | 0.016 | 4 | 4 | 4 | 14 |
| W003-R run 2 | 0.016 | 0.018 | 0.023 | 0.053 | 0.016 | 4 | 4 | 4 | 14 |

All times in ms. The other 39 factory snapshot rebuilds of W003-B are machine-state changes (every machine completes every 0.1 s, so the phase changes), not `StructureRevision` changes; they are the gate `MachineStatesChanged`, which the budget does not cap.

## 8 hour offline reconcile (PE-1, PE-7)

Return instant departure + 9 h, so the 8 h credited cap applies: E(8 h) = 2.304e11 ticks at 4/5 efficiency, one `RunSimulator.Advance` call. The first call of each variant (JIT included) is logged separately and excluded; the median is over 5 further runs of a fresh decode.

Two paths are timed. **Algorithm:** `OfflineSegmenter.Reconcile` on the decoded state, with the simulator's test-only counters (`LastCompletions`, `LastTimestamps`, `LastPasses`, `LastMaxPassesPerTimestamp`). **Full return:** `RunPersistence.ColdStart` over a store seeded with the fixture (read, decode, reconcile, promote the result generation to disk, install); this is the path the `ResumeAndReconcile` timing log of the emulator measures.

| Variant | Run | Median algorithm ms | Runs (ms) | Median full return ms | Completions | Processed timestamps | Passes | Max / mean passes per timestamp | ns per event | Extrapolated to 2,764,800 completions |
|---|---|---|---|---|---|---|---|---|---|---|
| W003-B | 1 | 951.7 | 943.9, 946.8, 951.7, 954.0, 975.3 | 988.1 | 2,764,800 | 230,401 | 460,802 | 2 / 2.000 | 344 | 952 ms (measured directly) |
| W003-B | 2 | 1017.6 | 1016.6, 1016.9, 1017.6, 1017.8, 1018.0 | 1047.2 | 2,764,800 | 230,401 | 460,802 | 2 / 2.000 | 368 | 1018 ms (measured directly) |
| W003-S | 1 | 319.4 | 315.6, 317.0, 319.4, 319.6, 322.3 | 352.1 | 691,200 | 230,401 | 460,802 | 2 / 2.000 | 462 | 1278 ms |
| W003-S | 2 | 331.7 | 330.7, 331.7, 331.7, 331.9, 331.9 | 366.4 | 691,200 | 230,401 | 460,802 | 2 / 2.000 | 480 | 1327 ms |
| W003-R | 1 | 175.5 | 171.5, 173.5, 175.5, 176.0, 177.0 | 208.1 | 322,499 | 102,326 | 248,358 | 4 / 2.427 | 544 | 1505 ms |
| W003-R | 2 | 181.8 | 179.0, 181.0, 181.8, 183.8, 184.4 | 216.2 | 322,499 | 102,326 | 248,358 | 4 / 2.427 | 564 | 1559 ms |

Reading:

- **The SC-008 record is W003-B**, which processes the whole event bound of 2,764,800 completions directly and takes about 0.95 to 1.02 s (about 344 to 368 ns per event, 4.1 to 4.4 microseconds per processed timestamp, two passes per timestamp). This **misses the 500 ms Windows budget by a factor of about 2** in both runs. The full cold start adds 30 to 36 ms on top.
- W003-S passes, but only because it completes 691,200 events (the auto-sell empties the intermediate stocks, owner decision 2026-09-30); its extrapolation to 2.76 M completions (about 1.3 s) is not a record and is shown only because the task asks for it. Its ns per event is higher than B's because every timestamp also runs the auto-sell path.
- W003-R passes with a large margin; its extrapolation is meaningless because R cannot reach the bound (322,499 completions) and is given for completeness only.
- Passes per timestamp are at most 2 on the event-bound variants and at most 4 on R, far below N + 1 = 13 (OD-2).
- Edit Mode timings are informational and are not compared with a budget: editor Mono, 1089.9 ms (B), 357.1 ms (S) and 197.1 ms (R) in the same full run (`W003PerformanceTests.EightHourWorstCase_TimingRecord_IsInformational`, T098). The player is 7 to 13 percent faster than the editor on B.
- **Miss routing:** budget (3) on W003-B is missed at about 2 times the limit. Per plan.md it goes to the Performance review in T130 with the options of R04. No optimization and no semantic change was made here.

## Player allocation check (PE-4)

Method: after 120 warm-up frames, `GcAllocationCounter` (Unity's `GC.Alloc` profiler recorder, which counts samples, not bytes) around every frame of 300 frames. A positive control (`new byte[1024]`) is detected (1 sample), so the zeros below mean something; `GC.GetAllocatedBytesForCurrentThread` returns 0 even for the control in this player, so bytes are not available and are not used.

| Path | W003-B | W003-S | W003-R |
|---|---|---|---|
| Advance path: gate, `Advance`, `ReadCash`, `ReadRp`, `CopyInventory`, `CopyMachineStates` | 0 samples | 0 samples | 0 samples |
| Whole frame including the HUD refresh read path | 6 samples in 6 frames | 6 samples in 6 frames | 6 samples in 6 frames |

- The **advance path and the hot read-model copies allocate nothing** in the steady state, the same as the T098 Edit Mode result (`W003PerformanceTests.WarmedAdvance_HasZeroGcAllocSamples`, 0 samples per warmed `Advance` on W003-B, S and R; passing in this run, Edit Mode 1502 of 1502). Edit Mode timings and the Edit Mode allocation count are informational.
- The **whole frame is not allocation-free**: 6 of 300 frames allocate, one per simulated second, matching the once-per-second `RunSnapshots.Overdrive` status read that `RunShellPresenter.Refresh` performs while Overdrive is active (5 simulated seconds in 300 frames, plus the second boundary of the warm-up). The allocation is the status object. This is event-driven (once per second, plus snapshot rebuilds on structural or machine-state changes), not per frame, but it is not "no steady-state `GC.Alloc`" on the whole host frame path. It is recorded as a finding for T130, which decides between accepting it and caching the status. No code was changed.
- Because the figure comes from the probe and not the UI Toolkit shell, allocations of the element writes are not covered either; T124 on the device covers the real HUD.

## Commands

```powershell
scripts/test-unity.ps1 -StandaloneWindowsPlayer -TestFile Assets/IdleFactory/Tests/PlayMode/W003PlayerTimingTests.cs   # run 1
scripts/test-unity.ps1                                                                                              # Edit Mode and Play Mode
scripts/test-unity.ps1 -StandaloneWindowsPlayer                                                                    # run 2, tagged player suite
```

Raw lines: `test-results/unity/20261008T173040093Z/PlayerPlayMode-results.xml` (run 1) and `test-results/unity/20261008T174029551Z/PlayerPlayMode-results.xml` (run 2), searched for `W003-`.

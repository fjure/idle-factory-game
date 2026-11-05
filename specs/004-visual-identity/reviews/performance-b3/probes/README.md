# T444 measurement probes (throwaway sources, kept as text)

These files are the measurement sources behind `../../performance-b3.md`. They are stored as `.txt` so that they do not compile and do not change the test manifest. **Nothing here lives under `Assets/`.** To repeat a measurement, copy the `.cs.txt` file to `Assets/IdleFactory/Tests/PlayMode/<name>.cs` for one run and delete it (and its `.meta`) afterwards. Both derive from `scripts/f004-b1/B1W004VProbeTests.cs` and `Assets/IdleFactory/Tests/PlayMode/PresentationAllocationTests.cs` of commit `1838f22` and use `internal` test-assembly types (`W004VFixture`, `LegacyCameraFraming`, `GcAllocationCounter`, `ShellRig`, `RunSceneRun`).

| File | SHA-256 | Role |
|---|---|---|
| `B3SetPassProbeTests.cs.txt` | `70e7eb030651a2c1b1da3d56810f59c45e491370086a26f1a3429c5d020b6a22` | Graphical-Editor Play Mode probe, real F004 art: `MeasureW004V` (SetPass per world render, floor on/off, legacy and production camera model, texture sizes), `BareEdge_RealArt` (bare edge, frozen-floor control), `WindowSetPass` (phone, tablet and tile-cap windows through `FactoryCameraController` at `n_min` and fit). Editor-only (`#if UNITY_EDITOR`). |
| `F004B3ProbeTests.cs.txt` | `60a8a5a2d9154d72fb738b05d318d7e683d43d5cab1437f7c024fdcec5dbf2bf` | Windows-player probe, `[Category("StandalonePlayer")]`, synthetic sprite catalog of `PresentationAllocationTests`: `Cost_FloorOnVersusOff_AtIntegerScales`, `Cost_LiveLoopFrameTime_AtLowestZoom`, `UpdateFloor_Timing`, `Pinch_GcAllocation`, `W004V_600Frames_Allocation`, `Cap_FloorCost_AtTheTileCap`, `BareEdge_FastPanAndPinch` (the last one is invalid: the synthetic floor sprite is mostly transparent, so its magenta counts are meaningless; the bare-edge evidence comes from the Editor probe). |
| `aba4.sh.txt` | `a9032962eae717099ffddd0e1a8235047660e386777b63df17ceea3733aad8d7` | The emulator ABA session driver (cold boot, sampler, six journey runs, hash checks). Adapted from the B2 `aba2.sh`. |
| `zoomrun.sh.txt` | `30762c43827bcbac5a978cada9dbfef795ab6520a707b9ca267c0a2e428f1c54` | The fit-all / `n_min` capture driver (T472 point 3). |
| `b3-zoom.ps1.txt` | `e6ae69244741dcbd87976e792ffc5256fc436c6e2b69d6f7edcb27893f9b1dc0` | One producing capture at a chosen zoom state, Welcome-back dismissed by tap. |

The SHA-256 values are those of the final versions. The Editor probe ran in its earlier states for the `MeasureW004V` and `BareEdge_RealArt` results (the later edits only added `WindowSetPass` and changed its screen list, not the earlier tests), and the player probe ran in its earlier states for the cost, pinch and 600-frame results (the later edits only added `UpdateFloor_Timing` and `Cap_FloorCost_AtTheTileCap`).

## Exact command lines (worktree `.maqa-worktrees/issue-562-t444-performance-b3`, Unity 6000.6.0f1, leases `unity,emulator,gpu` held by `issue-562`)

```powershell
# Android builds (B3, then the 30 FPS control with -ControlFrameRate30)
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/f004-b1/run-build.ps1 -Output <apk> -OutDir <dir> [-ControlFrameRate30]

# Active build target to Windows before the graphical Editor probes
Unity.exe -batchmode -quit -projectPath <worktree> -buildTarget StandaloneWindows64 -logFile <log>

# Graphical (not batch) Editor probes: copy the .cs.txt in as B3SetPassProbeTests.cs, then
Unity.exe -projectPath <worktree> -runTests -testPlatform PlayMode -testFilter B3SetPassProbeTests -testResults <xml> -logFile <log>   # MeasureW004V (4 tests)
Unity.exe -projectPath <worktree> -runTests -testPlatform PlayMode -testFilter BareEdge_RealArt   -testResults <xml> -logFile <log>   # 6 cases, env B3_OUT = PNG folder
Unity.exe -projectPath <worktree> -runTests -testPlatform PlayMode -testFilter WindowSetPass     -testResults <xml> -logFile <log>   # phone, tablet, cap windows

# Windows player: copy F004B3ProbeTests.cs.txt in as F004B3ProbeTests.cs, then
powershell -NoProfile -File scripts/test-unity.ps1 -StandaloneWindowsPlayer -TimeoutSeconds 3000   # env B3_OUT = PNG folder; the probe lines are in the result XML (B3-COST, B3-LIVE, B3-UPDATEFLOOR, B3-PINCH-GC, B3-W004V-600, B3-CAP-*)

# Emulator sessions
bash aba4.sh      # six runs: B1, B3, B1, 30 FPS control, B3, B1
bash zoomrun.sh   # fit-all / n_min captures, same boot

# OPS-4 (worktree t412-b2-build-b9221ca, detached at b9221ca, then at ac813fd)
powershell -NoProfile -File scripts/test-unity.ps1 -Suite PlayMode -TimeoutSeconds 3000

# OPS-7 (same worktree)
git switch -c scratch/t444-ops7-rollback b9221ca ; git revert -m 1 --no-commit b9221ca
powershell -NoProfile -File scripts/test-unity.ps1 -Suite PlayMode -TestFile Assets/IdleFactory/Tests/PlayMode/RenderParityTests.cs -TimeoutSeconds 3000
# cleanup: git reset --hard ; git switch --detach b9221ca ; git update-ref -d refs/heads/scratch/t444-ops7-rollback
```

# B1 baselines: archived APK and measurements (T410)

Issue #522, pre-switch commit `ac813fd` (`ac813fdf9c73ba17dbf5e59b060c71aa78f89571`, main after T409: Built-in pipeline, Gamma, Feature 003 and 005 merged). Evidence for FR-006, FR-025, FR-027; research R12. Companion of [baselines-b1-manifest.md](./baselines-b1-manifest.md) (T409: test manifest, screenshot baselines, W004-V fixture). Everything here is emulator or Editor development evidence on one workstation; no physical-device, battery or thermal claim (FR-027). No budget is frozen here: T412 freezes `performance-budgets.md` after B2 (constitution VI, provisional budgets).

## 1. Archived B1 APK

| Field | Value |
|---|---|
| File | `<git-common-dir>/../artifacts/f004/B1-ac813fd-9c9ed43a.apk` (gitignored, outside every worktree) |
| SHA-256 | `9c9ed43aaf1443b4de43200c65c7d4be00338fde0ad9b0ec3d3a1b4fa3527318` (re-hashed after the copy) |
| Size | **37,345,615 bytes** (35.6 MiB); 416 entries, 111,244,877 bytes uncompressed |
| Source commit | `ac813fd`, clean tree; only the Editor-only measurement probe `scripts/f004-b1/B1BuildProbe.cs` was present during the build (it adds nothing to the player) |
| Unity | 6000.6.0f1 |
| Target | Android arm64-v8a, IL2CPP, Development build, **OpenGL ES 3 only** (the GLES3 emulator build, `PlatformBuildCommands.BuildAndroidArm64DevelopmentGles3`; project Graphics APIs stay automatic before and after, checked by the build command) |
| Profiler | profiler-free: `-noProfilerAutoconnect`, log line `profiler autoconnect False`; 0 "Autoconnect Profiler" lines in every FPS capture |
| Pipeline | Built-in, Gamma color space |
| Build | `scripts/f004-b1/run-build.ps1`; 5 min 39 s from an empty `Library/` (0 errors, 3 warnings) |

Rebuild fallback: check out `ac813fd` and run `scripts/f004-b1/run-build.ps1 -Output <apk> -OutDir <dir>`.

**APK size comparisons need a clean build.** The 30 FPS control APK (same commit, same entries, 111,243,708 bytes uncompressed, identical compressed entry sizes) is 51,721,798 bytes because it was built incrementally into a warm `Library/`: the file carries about 14 MB of dead space. T412's "APK growth <= +10 MB against the paired B1" must compare clean builds (empty `Library/`) or the sum of entry sizes (`unzip -v`), not raw file sizes of incremental builds.

Control APK (same commit, `CoreGameFoundationBootstrap.ApplyFrameRateTarget` frame-rate line commented out for this build only, restored with `git checkout`, so Android keeps its implicit 30 FPS cap as in T065): SHA-256 `53e2873541d3e36cbd2dd3e6b87cb392498c9d4b3bc1b7b7fb1bf6e02a6154b2`, not archived (rebuildable with `run-build.ps1 -ControlFrameRate30`).

## 2. Build-report metrics (the archived build; the control build is identical in every row)

Source: `scripts/f004-b1/B1BuildProbe.cs` (`BuildReport.GetLatestReport()` dump and `IPreprocessShaders` at `callbackOrder` max). Raw dumps: `artifacts/f004/B1-evidence/build-b1/`.

| Metric | B1 value |
|---|---|
| APK size | 37,345,615 B (section 1) |
| Texture2D in the build (`packedAssets`) | 101 entries, **4,556,816 B**; of these Unity's built-in splash logo 2,796,376 B and the project's 100 textures 1,760,440 B (largest 65,676 B). Compressed player formats. |
| Atlases | none: 0 `SpriteAtlas` assets; 100 `Sprite` objects (45,900 B). Atlas-page sum is therefore 0 at B1. |
| Other texture-like data | 1 Cubemap, 524,492 B |
| Shader assets in the player data | 5 entries, 28,816 B, plus the 469,304 B BuiltInShaders file |
| **Shader variants handed to the compiler after Unity's stripping** (GLES3, per snippet vertex-stage callback) | **693 variants in 104 snippets of 51 shaders**. Game-relevant: `Sprites/Default` 12 (1 snippet); UI Toolkit shaders `Hidden/Internal-UIRDefault` 120, `UI/Default` 24, `Hidden/UIR/GaussianBlur` 12, `Hidden/UIR/DropShadowComposite` 6, `Hidden/UIR/ColorEffect` 6. Largest others are built-in resources (`Hidden/Internal-DeferredShading` 81, `Legacy Shaders/Diffuse` 51). Full list: `shader-variants.tsv`. |
| Files by role | libil2cpp.so + libunity.so + libc++ about 83.9 MB uncompressed (3 `so`), `global-metadata.dat` 8.2 MB, `classes.dex` 7.5 MB, `unity default resources` 3.8 MB |

The variant count is the figure that must not grow at T411/T412 without an explanation (URP adds its own shaders). It is a count of what Unity passes to the compiler, not Unity's pre-stripping count (not exposed without a custom stripper).

## 3. Render counts on W003-S and W004-V (device-independent, Editor, labelled as such)

Method (`scripts/f004-b1/B1W004VProbeTests.cs`, run by `scripts/f004-b1/run-w004v.ps1`): one graphical (non-batch) Unity 6000.6.0f1 editor on the RTX 5070 Ti (Direct3D 12), a throwaway Play Mode test, not committed (a committed test would change the test manifest T411 compares). It copies the T409 capture code (production `RunSession`, `SlotGridView`, `RunCameraFraming`) and renders the world camera into an exact-size RenderTexture. The Editor's own rendering dominates a single frame, so each "with" frame calls `camera.Render()` 20 times, each "without" frame none; phases alternate (without, with, without, with, 30 frames each); value per render = (with - without) / 20 on the `ProfilerRecorder` counter "SetPass Calls Count". The first Frame Debugger attempt (reflection on `FrameDebuggerUtility`) reported 0 events without an open Frame Debugger window and was dropped.

| Frame | 640x480 | 390x844 | 1280x720 | Sprite renderers |
|---|---|---|---|---|
| W003-S, fit-all | **7.86** SetPass | 7.88 | 7.91 | 48 |
| W004-V, `n_min` | **13.90** | 13.91 | 13.85 | 47 |
| W004-V, fit-all | **13.90** | 13.93 | 13.96 | 47 |

SetPass per world-camera render is stable to 0.1 across screen classes. **Not available:** the "Batches Count" and "Draw Calls Count" counters read 0 in this Editor/backend, and the Triangles/Vertices counters were not stable (the Editor's other views vary), so batches, draw calls and triangles are not recorded. `UnityStats` has no `batches` member in 6000.6. T412/T411 must use the same method so the number is comparable (URP's SRP Batcher changes what SetPass means; compare draw-state changes, not names). Raw log: `artifacts/f004/B1-evidence/w004v/w004v-editor-run3.log`.

Texture memory in the Editor for the textures behind the 47 drawn W004-V sprites stays as recorded in the manifest (913,912 B, 14 textures, Editor runtime size, T409); the build-report sum above is the compressed player figure.

## 4. Emulator frame pacing (B1 on W003-S, producing)

Environment: AVD `Medium_Phone_API_36.0`, API 36, `emulator -gpu host -no-snapshot-load` (cold boot), host GL renderer NVIDIA GeForce RTX 5070 Ti, 1080x2400. Workload: committed `w003-s-shipped` device set (12 machines, loaded in 308-438 ms in the accepted starts, SHA-256 checked on the device), journey `-Scenario F003 -Steps Preflight,Install,Launch,W003Push,ProducingFps,F003LogScan`. The Launch step is the unscored launch after each install. Each window: cold launch, 30 s warm-up, SurfaceFlinger TimeStats cleared, 60 s capture; percentiles from the present-to-present histogram are bucket lower bounds (nearest rank, `summarize-fps.py`). "fresh" rows are the journey's own empty-run controls (`pm clear`) before and after the producing window. Guest CPU is the `/proc/<pid>/stat` utime+stime difference over the window in percent of one core, sampled every 5 s by `sample-device.ps1`.

W004-V is an Editor fixture and cannot run in a player without a production-code hook, so the emulator FPS of B1 is W003-S only; W004-V is covered by section 3. T412 should decide whether B3 gets a development-only hook.

### Session 3 (the valid session: cold boot, leases `unity,emulator,gpu`, no game process during any run)

| Run (APK) | Window | Avg FPS | Frames / dropped | p50 / p95 / p99 ms | Intervals >= 50 ms | Guest CPU % of a core |
|---|---|---|---|---|---|---|
| 1 B1 | fresh 1 | 57.61 | 3366 / 56 | 16 / 28 / 40 | 18 | 46.8 |
| 1 B1 | **W003-S producing** | **58.05** | 2935 / 40 | 16 / 28 / 40 | 11 | 50.7 |
| 1 B1 | fresh 2 | 58.53 | 3416 / 47 | 16 / 28 / 36 | 11 | 44.2 |
| 2 control (30 FPS) | fresh 1 | 29.82 | 1772 / 0 | 33 / 46 / 54 | 69 | 28.1 |
| 2 control (30 FPS) | W003-S producing | 30.03 | 1782 / 3 | 33 / 38 / 54 | 34 | 30.7 |
| 2 control (30 FPS) | fresh 2 | 29.88 | 1773 / 0 | 33 / 44 / 54 | 62 | 28.2 |
| 3 B1 (again) | fresh 1 | 58.70 | 3423 / 54 | 16 / 27 / 36 | 3 | 45.8 |
| 3 B1 (again) | **W003-S producing** | **48.18** | 2832 / 311 | 17 / 36 / 58 | 44 | 55.8 |
| 3 B1 (again) | fresh 2 | 53.21 | 2724 / 61 | 16 / 34 / 58 | 35 | 53.8 |

Order B1 -> 30 FPS control -> B1 (ABA). Memory of the same workload (outside any timed window, `pss-probe.ps1`, one `dumpsys meminfo` sample 45 s after the reconcile, same process): **total PSS 493,587 KiB**, Native Heap 38,200, Code 103,664, Private Other 299,548, "Unknown" 153,837 (where the emulator accounts GL memory); "Graphics" reports 0 here, so GPU texture memory on the device is not separately measurable on this emulator.

### Validity reading (R12 rule) and noise band

- **Control: valid.** The 30 FPS control reproduces 29.8-30.0 FPS in all three windows (T065: 30.31-30.36).
- **B1 agreement: not met for the producing window.** The two B1 producing windows are 58.05 and 48.18 FPS (spread 9.9 FPS, 17 % of the mean 53.1); the fresh-run controls of the same APK range 53.21-58.70. The host was not idle (background load in the table below), and frame pacing varied inside the second B1 run (311 dropped frames). By the R12 rule the ABA session is therefore **not strictly valid as a regression instrument**; the recorded noise band for B1 on this host is about +/- 5 FPS around 53 and about 6 FPS lower than the best clean windows. T412 should run B1 at least three times (not twice), report B2 against the paired B1 only, and treat a regression smaller than this band as undetected.
- The B1 average is below the provisional 60 FPS budget in session 3 (58.05) and above it in session 2 (61.26); the budget decision belongs to T412, which freezes it after B2.

### Session 2 (supplementary: cold boot 21:36, first run only; the owner started a game at 21:49:21)

Run 1 B1 finished at 21:44, before the game started; host busy 3.2-3.4 threads. Fresh 1 **60.98** FPS (3556 frames, 6 dropped, p50/p95/p99 16 / 19 / 24 ms, 2 intervals >= 50 ms, guest CPU 39.5 %); **W003-S producing 61.26** FPS (3570 / 8 dropped, 16 / 19 / 24 ms, 0 intervals >= 50 ms, CPU 42.4 %); fresh 2 56.13 FPS (3279 / 235 dropped, 16 / 33 / 36 ms, 0 >= 50 ms, CPU 34.2 %). The 30 FPS control in that session: fresh 1 30.15 FPS (33 / 38 / 50 ms, 22 intervals >= 50 ms), clean; its later windows (22.7-23.3 FPS) and runs 3-4 overlapped the game and are discarded. T124 measured 61.145 FPS on the same workload, so session 2's first run reproduces the earlier figure.

### Discarded sessions (kept as raw evidence, not used)

- First session, 20:59: the owner was playing a game client (host CPU 100 %); B1 36.9 FPS, controls 38.5/26.3.
- Second pass in that cold boot (21:18-21:35, sampler also sampled `dumpsys meminfo` every 30 s, which forces a GC inside the windows): B1 59.3 / 31.1 / 40.4, control 21.3 / 20.3 / 23.5; invalidated by the game (and the meminfo sampling), control not 30.
- Session 2 runs 2-4 (game running from 21:49:21).

Raw material (SurfaceFlinger dumps, logcats, journey reports, CPU samples with host load): `artifacts/f004/B1-evidence/session/` (`s3-attempt1/` is session 3; `run1-b1a/` is session 2 run 1).

## 5. Preflight checklist (session 3 and the others)

| Item | Evidence |
|---|---|
| Leases `unity,emulator,gpu` held, owner `issue-522` | `resource-lease.ps1 -Status` before and during (renewed by the parent after the rate-limit interruption) |
| No Unity editor | no `Unity.exe` under `...\Editor\`; the `unity.exe` left is Unity Hub's `serve` process; my own editor runs (build, W004-V probe) had exited before the emulator was booted |
| No ComfyUI / Blender | ComfyUI Desktop and its python (13 processes) were closed by me with the owner's permission (2026-10-27); no `blender.exe` (only `blender-mcp`/`comfy-mcp` idle MCP servers) |
| Cold-booted emulator | `emulator -avd Medium_Phone_API_36.0 -gpu host -no-snapshot-load`, started and adb started from the main checkout path; one `emulator.exe`/`qemu` pair; uptime about 3 min at run 1 plus a 90 s settle |
| `adb devices` healthy, no foreign adb/monkey | `emulator-5554 device`; only the single `adb -L tcp:5037 fork-server`; no `monkey` process; the adb server restart by Unity runs happened before the boot |
| APK verified | the archived file hashed after copy; the journey installs it and the unscored Launch follows |
| Settings noise | `ProjectSettings/ProjectSettings.asset` re-serialized by every Unity run (iOS icon slots) was restored with `git checkout` each time; nothing under `ProjectSettings/` is committed |

**PF-9 deviation (host load).** The host was not idle: Riot/League client and Nolvus dashboard (together about 1-2 cores), Docker Desktop, iCUE, Battle.net, Steam, Discord, about 70 Chrome processes, OneDrive. Host busy threads (all-process CPU seconds per second, 8 logical CPUs) during session 3 windows: 2.8-3.4 on average including the emulator's own 1.3-1.5 threads, maximum 4.7. A game client (TFT) ran at about 4 cores during the discarded sessions; `check-clean.py` refuses a run when a game process exists or the mean host load is above 4.5 threads, and session 3 passed it. Because the load remains, the +/- 5 FPS band in section 4 is an upper estimate of this host's noise in a pessimistic condition; a quiet host (T124) gave 61.1-61.6.

## 6. Tools added for this measurement

All under `scripts/f004-b1/` (no production code, no scene, prefab or setting, no change to an existing test or its expectation): `run-build.ps1` + `B1BuildProbe.cs` (build with report and shader-variant dump, optional 30 FPS control), `run-w004v.ps1` + `B1W004VProbeTests.cs` (graphical Editor run of the throwaway counter test; copied into the Tests folder for the run and removed), `summarize-fps.py`, `sample-device.ps1`, `summarize-device.py`, `check-clean.py`, `pss-probe.ps1`. They exist so that T411/T412 can repeat the method. The test manifest of [baselines-b1-manifest.md](./baselines-b1-manifest.md) is unchanged by T410.

## 7. Open items for T411/T412

- B2 must be measured with the same build recipe from an empty `Library/`, the same journey steps and `check-clean.py`, in one cold-booted session with at least three B1 runs.
- Batches and draw calls are not recorded (section 3); T412 may add a development hook or a Windows player run if it wants them.
- GC allocations per frame and the 600-frame Windows-player W004-V run belong to B3 (R12).
- Windows host frame p95 for W003 remains the T122 figure (0.02-0.04 ms); it was not re-measured here.

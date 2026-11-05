# B3 performance review and T412 carry-overs (T444)

Issue #562, Performance Engineer (Medium tier, high reasoning). Measured commit: **`1838f22`** (origin/main, merge of #666; the code is that of `701e8c8` plus the T442 evidence documents), worktree `.maqa-worktrees/issue-562-t444-performance-b3`. Unity 6000.6.0f1, Windows 11 host (AMD Ryzen 7 7800X3D, 8 logical CPUs, RTX 5070 Ti), emulator AVD `Medium_Phone_API_36.0` (API 36, 1080x2400, host GPU, GLES 3.1). Evidence for FR-025, FR-026, FR-027 and SC-007; research R12, constitution VI. Budgets are the frozen ones of [performance-budgets.md](./performance-budgets.md) section 4, baselines are [baselines.md](./baselines.md) (B1, `ac813fd`) and section 2 and 3 of the budgets file (B2, `b9221ca`).

**Every emulator and Editor number here is exploratory evidence from one workstation (FR-027).** There is no physical-device, battery or thermal measurement. The physical-device gap stays open (section 9) and is not closed by any row below. Nothing in this record changes product code, tests, scenes, settings or scripts; the measurement probes were throwaway files, copied into the project for a run and deleted afterwards (section 10). Raw material is outside git in `artifacts/f004/B3-evidence/` of the worktree (not committed).

## 0. Summary and honest verdict

| Area | State |
|---|---|
| Frozen budgets met with evidence | 4 (W003-S), 6, 7, 10, 11; 5 for the advance and presentation paths; 8 on every measured phone frame and on tablet portrait (single pass, at most 173 renderers) |
| Frozen budgets **not met** | **8 above about 175 renderers** (2 SetPass per render on the tablet-landscape `n_min` frame, 2 to 3 on desktop n=1 windows with 266 and 588 floor tiles, 7 at the 2048-tile cap, against 0.86); the owner-restated CPU-time bound is met at 588 tiles but **exceeded at the cap in its first proposed form (0.5 ms mean: measured 0.54 to 0.97 ms)**; **5 as written for the whole host frame** (PR-2, six `GC.Alloc` samples per 300 frames, unchanged and accepted in Feature 003 D2) |
| **Not demonstrated (open)** | **The emulator rows 1, 2, 3 and 9** (60 FPS average, p95 bucket, 0 intervals >= 50 ms, phase-0 guest CPU) and point 3 of the T472 list. Four cold-booted sessions were run; three were wrecked by the owner's game client, and the fourth (game exited, host load 1.6 to 3.2) ran the emulator itself at about 44 FPS with B1 and with empty-run controls alike and a 30 FPS control reading 25 to 27 (section 4). **FR-025 and SC-007 are therefore undemonstrated on the emulator at B3**; they need one valid ABA session on a quiet host with a healthy emulator. |
| Indicative emulator signals (not verdicts) | **FPS: B3 43.4 against B1 44.6 under the same throttle (within the B1 spread).** **Guest CPU of the producing window: B3 40.8 % against B1 33.3 % (+7.5 points, allowance 3.5), not separable from the B1 spread of 8.7 points.** PSS +5.1 % on B1 (B2: +5.1 %). The CPU signal is the first thing a valid session must settle. |
| T472 points | 1 (SetPass), 2 (renderers, frame time), 4 (pinch GC), 5 (bare edge): measured (section 5). 3 (Android frame time at fit-all and `n_min`): captures exist but under a loaded or degraded host, not a verdict. |
| OPS-4 | done: Play Mode 2248 of 2248 on `ac813fd` and 2275 of 2275 on `b9221ca`, 545.8 s against 548.5 s; host not quiet |
| OPS-7 | done: clean revert, compile, `RenderParityTests` 18 of 18, `m_SpritePackerMode` 0 before and after (5 on today's main) |
| Physical device | not measured (FR-027), the gap stays open (section 9) |

## 1. Result against the frozen budgets (section 4 of `performance-budgets.md`)

| # | Frozen budget | B3 value | Status |
|---|---|---|---|
| 1 | Emulator average FPS >= 60 on W003-S producing, 60 s window, profiler-free, valid ABA session | **not demonstrated**: best session (4): B3 43.35 (42.52, 44.18) against B1 44.63 (42.52 to 47.13); B1 itself is below 60 and the 30 FPS control read 25 to 27 (invalid session; owner's game load in sessions 1 to 3) | **OPEN, accepted gap** (owner 2026-11-05, section 11); B3 = B1 under the same throttle |
| 2 | p95 present interval: no bucket above the 16.7 to 20 ms bucket (p95 lower bound <= 19 ms) | session 4: p95 bucket 44 to 46 ms for B3, 44 for B1 and for the empty-run controls (host-side periodic stall) | **OPEN, accepted gap** (owner 2026-11-05, section 11) |
| 3 | 0 intervals >= 50 ms per 60 s window | session 4: B3 11 and 11, B1 36, 4 and 14 | **OPEN, accepted gap** (owner 2026-11-05, section 11) |
| 4 | Windows host frame p95 not above the T122 range 0.02 to 0.04 ms (SC-008 ceiling 1 ms) | W003-S **0.029 ms**; W003-B 0.047; W003-R 0.060 (600 frames, development player, loaded host) | **PASS** for W003-S (inside the range); W003-B and W003-R 0.007 and 0.020 ms above the range, 5 to 6 % of the ceiling (noise-level on a loaded host) |
| 5 | 0 steady-state GC allocations per frame (warmed tests, 600-frame Windows-player run on W004-V) | advance path **0**; presentation paths **0** incl. pinch and the floor; 600-frame W004-V presentation run **0 in 600 frames**; whole host frame with the HUD refresh probe **6 samples per 300 frames (PR-2, unchanged)** | **PASS** for advance and presentation paths; **FAIL as written for the whole host frame** (PR-2, accepted in Feature 003 D2): **accepted (PR-2)**, section 11 |
| 6 | World-art texture memory <= 8 MB | build report: F004 atlases 3.05 MB (placeholder development art 1.76 MB not part of the restyled world); Editor runtime of the 5 atlas pages 6.10 MB (5.82 MiB) | **PASS** (both readings; no Profiler reading in the Windows player) |
| 7 | Atlas pages <= 1024 x 1024 | 5 pages: 512x1024, 256x512, 256x256, 128x256, 128x64 | **PASS** |
| 8 | SetPass per world-camera render on W004-V not above 0.86 (B2 0.78 + 10 %), 640x480 method | floor on, up to about 150 renderers: **1 pass per render**, 0.74 to 0.88 (same band with the floor off: 0.76 to 0.89); **desktop n=1: 1.74 to 1.76 (266 tiles) and 2.68 to 2.71 (588 tiles)** | **PASS** on every phone and emulator frame (within the method's noise); **restated by the owner 2026-11-05** (section 11): single-pass SetPass applies to phone frames and tablet portrait (measured, at most 173 renderers); above about 175 renderers (tablet landscape `n_min`: 2 passes; desktop n=1; the 2048-tile cap: 7 passes) SetPass is informational and a `camera.Render` CPU-time bound applies (588 tiles: 0.27 to 0.33 ms mean; **cap: 0.54 to 0.97 ms mean, above the 0.5 ms first proposed, see section 11**) |
| 9 | Phase-0 guest CPU regression <= max(10 %, 2 points) against the paired B1 | session 4 (invalid): B3 **40.8 %** (40.3, 41.3) against B1 **33.3 %** (29.6, 31.9, 38.3): **+7.5 points (+22 %)** against an allowance of 3.5 points; empty-run controls equal (27 to 28 %) | **OPEN, accepted gap** (owner 2026-11-05, section 11); indicative miss, not separable from the B1 spread (8.7 points); first item to settle in any later valid session |
| 10 | APK file size <= 150 MB | **46,391,439 B** (44.2 MiB; entry sum 46,310,503 B); T440 clean build owed to T444: measured | **PASS** (31 % of the cap) |
| 11 | Shader variants after stripping: no growth over 490 without explanation | **356** (176 snippets, 79 shaders), identical in the control build | **PASS** (fell by 134: 16 post-processing shaders gone since T457) |

Further rows asked for by the task:

| Metric | Value | Reading |
|---|---|---|
| T472 point 3 (Android frame time at fit-all and `n_min`) | captures under rising host load (40.5 and 35.8 FPS at fit-all, 32.7 and 38.4 at `n_min`) | **open, accepted gap** (owner 2026-11-05, section 11) |
| Pinch GC (T472 point 4) | warm 0; first gesture 157 to 723 once (pool growth) | steady state **PASS** |
| Bare edge (T472 point 5) | 0 bare frames in production order, positive control works | **PASS** in the Editor; real pointers not verified |
| PSS (no budget) | 525,694 KiB, +25,523 KiB (+5.1 %) on B1, 8,337 KiB below B2 | recorded |

## 2. Preflight and host load (R12, PF-9)

Per R12 and the workstation pitfalls, recorded for each Unity and emulator phase:

| Item | Evidence |
|---|---|
| Leases | `resource-lease.ps1 -Status`: `unity,emulator,gpu` held by `issue-562` (renewed at 09:58, 11:18, 11:37; never released by this task) |
| Unity processes | none running during any emulator window (every Unity run of this task, builds included, had exited; `Get-CimInstance Win32_Process` for `Unity*`, `PlayerWithTests`, `adb`); the Unity Hub `serve` process is not a Unity editor. No Unity process lingered after the last run. |
| Blender, ComfyUI | no application running; only the idle `blender-mcp` and `comfy-mcp` Python servers |
| Foreign adb or monkey | none: one `adb` server started from the main checkout directory with the emulator; Unity batch runs ended with "Killing ADB server" before each cold boot, so each boot started a fresh server |
| Emulator | cold boot with host GPU for every session: `emulator -avd Medium_Phone_API_36.0 -gpu host -no-snapshot-load`, started from `C:\Users\Domenik\ai-projects\idle-factory-game` (the main checkout) at 10:13, 11:18, 11:39 and 12:36; `adb devices` `emulator-5554 device`; 90 s settle |
| APK identity | sessions 3 and 4 printed the local and the installed `base.apk` SHA-256 prefix after every run (all equal: B1 `9c9ed43a`, B3 `267a50c0`, control `9c1d4d97`); the app was uninstalled before every install. In session 1 run 2 the hash was not checked and the run is void (section 4). |
| Disk | C: 49 GB free before the work, 30 GB during the Unity runs (limit 20 GB), 0 low-space errors |
| Settings noise | diffs archived and restored after every Unity run (section 3, section 7) |
| Leftover loops | my own wait loops and samplers were stopped; see section 10 |

**PF-9 deviation (host load), a cause of the open emulator rows.** Owner applications were not closed: **World of Warcraft (`Wow.exe`, started 09:44, 0.6 to 2.3 cores and the GPU, exited at about 12:33)**, the League of Legends launcher (`LeagueClient`, `LeagueClientUx`, about 1.3 cores), Battle.net (10 processes), Steam (9), Riot Client (7), Docker Desktop (6), Discord (6), iCUE, about 46 Chrome processes, idle MCP servers and other agent runtimes. Host busy threads (all-process CPU seconds per second, 8 logical CPUs, includes the emulator's own 1 to 1.5 threads): **4.2 to 6.5 inside the emulator windows of sessions 1 to 3** and of the second half of the zoom captures; **1.6 to 3.2 in session 4** (B2: 1.6 mean, B1 session 3: 2.8 to 3.4); 3.9 during the OPS-4 B1 run and 5.5 during the OPS-4 URP run (these include Unity itself, with the game running). The Windows-player timings of sections 5 and 6 and both OPS-4 durations were taken under the same load.

## 3. B3 build record (budgets 6, 7, 10, 11)

| Item | Value |
|---|---|
| Commit and recipe | `1838f22`, `scripts/f004-b1/run-build.ps1` (the B1/B2 recipe: IL2CPP, Android arm64, Development, GLES3, `-noProfilerAutoconnect`, build-report and shader-variant dump). Built in the issue worktree, 09:58 to 10:09 local. |
| Library state | **Warm, incremental** (Library of the T442/T475 builds; 0 "Rebuilding Library" lines). B2 was built from an empty Library; the difference is documented because it affects build time and the shader-variant callback only if shaders are served from the cache (not the case here: B3 and the 30 FPS control, built incrementally after it, report the same 356 variants). |
| B3 APK | `B3-1838f22.apk`, sha256 `267a50c0a0f6bad3a2a5a064c441046a34faa16e42d45571d371fad946250638`, **46,391,439 B** (44.2 MiB), 436 entries, compressed entry sum 46,310,503 B, uncompressed 144,205,933 B. The 80,936 B between file size and entry sum is container overhead; there is no dead space (the dead space of B1's incremental control APK did not occur: this build wrote into a fresh output path). |
| 30 FPS control APK | `B3ctl30-1838f22.apk`, sha256 `9c1d4d97956fcfdde2bcd0f5a345a14229b9692891755b4dba6b9bea309f4838`, 66,190,536 B (an incremental build into a warm Library carries dead space; its entry sizes equal B3). Same commit, the frame-rate line commented out for this build only, restored with `git checkout`. |
| Build time | B3 8 min 33 s (build report `totalTime`; warm Library); control 1 min 17 s. 0 errors, 0 warnings in both. |
| Profiler | profiler-free: log line `profiler autoconnect False`, build probe `B1BuildProbe.cs` copied in temporarily and removed by the script. |
| Shader variants after stripping | **356 variants in 176 snippets of 79 shaders**, identical in the B3 build and the control. B2 reference 490 (235 snippets, 95 shaders). The difference is fully explained: **16 post-processing shaders are no longer in the player** (`Hidden/Universal Render Pipeline/FinalPost` 64, `LensFlareDataDriven` 33, `SubpixelMorphologicalAntialiasing` 9, `TemporalAA` 7, `UberPost` 6, `LensFlareScreenSpace` 5, `Scaling Setup` 4, `LutBuilderHdr` 3, `Edge Adaptive Spatial Upsampling` 1, `LutBuilderLdr` 1, `Stop NaN` 1, `Bloom`, `BokehDepthOfField`, `CameraMotionBlur`, `GaussianDepthOfField`, `PaniniProjection` 0 each) = -134 variants; no shader is new and no shader gained variants. Cause: T457 set `m_PostProcessData` to null on `IdleFactory2DRenderer.asset`, so Renderer2D no longer references the post-processing shaders. Game-relevant: `Sprites/Default` 4, `Hidden/Internal-UIRDefault` 40. |
| URP global settings after the builds | The same single `m_SettingsList` entry as at B2 was dropped by the Android build (`rid: 3338746865761648669`, `RenderingDebuggerRuntimeResources`; diffs `global-settings-after-b3-build.diff` and `...-control-build.diff`). Variant counts are equal between both builds, so the stripping did not change. Restored with `git checkout`, not committed. This closes the T442 finding F5: the shader-variant count was measured on the build that carries the dropped entry, 356, and the entry has no shader. |
| ProjectSettings after the builds and runs | iOS icon-slot noise (134 lines), and after the graphical Editor runs `GraphicsSettings.m_LightsUseColorTemperature` and `QualitySettings.antiAliasing` toggles; all restored with `git checkout` and `scripts/check-settings-noise.ps1` run before the commit. |
| Untracked Unity output | `Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss` (+ `.meta`) existed untracked in the worktree during the builds (Unity writes it when the editor opens the project). It is a small default theme and not a committed asset; recorded as a build input deviation. |

### Size and texture budgets

| Metric | B1 | B2 | B3 |
|---|---|---|---|
| APK file size (clean-equivalent, fresh output path) | 37,345,615 B | 46,330,863 B | **46,391,439 B** (+60,576 B on B2; cap 150 MB: 31 % of it) |
| Compressed entry sum | 37,268,054 B | 46,252,640 B | 46,310,503 B |
| Uncompressed entries | 111,244,877 B (416) | 141,042,486 B (420) | 144,205,933 B (436) |
| Atlases in the build | none | none | **5 SpriteAtlas v2** (`machines`, `slots`, `cues`, `markers`, `floor`), 3,048,212 B in the build report |
| Texture2D total in the build (`packedAssets`) | 4,556,816 B (101) | 4,859,912 B (138) | 7,780,124 B (169): F004 atlases 3,048,212 B, F001/F003 development placeholder art 1,758,268 B, F004 non-atlas textures 154,228 B, Unity splash logo 2,796,376 B (not game art), packages 20,868 B |

Atlas pages (Editor runtime, Windows target, RGBA32 uncompressed; `B3-TEXTURE` lines of the Play Mode probe): `machines` 512x1024 (4,195,504 B), `slots` 256x512 (1,049,772 B), `cues` 256x256 (525,483 B), `markers` 128x256 (263,342 B), `floor` 128x64 (66,675 B): **5 pages, the largest 512x1024, all within 1024 x 1024; runtime sum 6,100,776 B (5.82 MiB)**. The Editor figure includes Unity's CPU-readable copy, which is why it is about twice the player build-report figure (3.05 MB); both readings are under the 8 MB cap. The build report contains the F001/F003 development placeholder textures that the restyled scene no longer draws (1.76 MB, not part of the world-art figure); a Profiler texture-memory reading in the Windows player was not taken (the Editor runtime sizes above stand in for it, and are the more pessimistic number).

APK growth levers: nothing was examined or changed (the budget is met by a wide margin; budget 10 is a 150 MB cap since the owner decision of 2026-11-04).

## 4. Emulator session (B3 against B1, 30 FPS control): no valid session was obtained

**Verdict for this section: the emulator rows of budgets 1, 2, 3 and 9 are NOT demonstrated and stay open.** Four cold-booted sessions were run with the B1/B2 protocol; none satisfies the R12 validity rule (30 FPS control reproduces about 30, quiet host, B1 reference at its known level). The reasons, in the order they occurred:

- **Sessions 1 to 3 (10:13 to 12:18) ran while the owner's game client was running** (World of Warcraft, started 09:44, 0.6 to 2.3 cores, plus the League of Legends launcher at about 1.3 cores and further launchers). The archived B1 itself read **38 to 40 FPS** (the single exception: the very first B1 run, 60.27 FPS, before the load rose) and the empty-run controls of every APK read the same 36 to 41 FPS, so the throttle was the host's. Host busy mean 4.2 to 6.1 threads (limit 4.5; B2 had 1.6). Owner applications were not closed (assignment rule). These sessions are archived and void (`session-invalid-loaded-host*/`, `zoom-invalid-loaded/`).
- **Session 4 (12:36 to 13:13, after the game had exited)** is the best record obtained and is reported below. No game process, host busy mean **1.6 to 3.2** (the 4.5 limit was never exceeded; B2 had 1.6), all APK hashes verified (the app was uninstalled before every install and the installed `base.apk` hash printed after every run), same boot, same sampler. But **the emulator ran well below its B2 level**: B1 itself read 42.5 to 47.1 FPS (B2 session: 60.9 to 61.3), the empty-run controls of every APK read 42 to 45 FPS, and the **30 FPS control read 24.7 to 26.6 FPS instead of about 30**. By the R12 rule this invalidates the session as a regression instrument. The shortfall is the same in the empty-run controls and in B1, so it is not caused by the restyled factory; its source was not identified (no game process; the League of Legends launcher stayed at about 1.3 cores; the GPU was not throttled when checked at 13:22: P0, no throttle reason; Windows power plan "High performance").

Protocol (all sessions): `scripts/android-emulator-journey.ps1 -Scenario F003 -Steps Preflight,Install,Launch,W003Push,ProducingFps,F003LogScan -LeaseOwner issue-562`, W003-S shipped set (SHA-256 verified on the device), 30 s warm-up, 60 s SurfaceFlinger TimeStats window between two fresh-run controls (`pm clear`), the Welcome-back sheet stays open as in B1 and B2; `sample-device.ps1` for guest CPU and host busy threads; `check-clean.py` after every run. Order B1a, B3a, B1b, 30 FPS control, B3b, B1c as at B2. Percentiles are bucket lower bounds.

### Session 4 (not valid; the best paired record)

| # | APK | Producing avg FPS | Frames / dropped | p50 / p95 / p99 ms | Intervals >= 50 ms | Fresh-run controls FPS | Guest CPU % of a core (controls) | Host busy mean |
|---|---|---|---|---|---|---|---|---|
| 1 | B1 | 44.25 | 2604 / 102 | 16 / 44 / 54 | 36 | 44.44, 44.77 | 38.3 (26.7, 27.5) | 1.58 |
| 2 | **B3** | **44.18** | 2599 / 91 | 16 / 44 / 48 | 11 | 44.27, 44.30 | **41.3** (28.0, 27.5) | 3.09 |
| 3 | B1 | 47.13 | 2768 / 113 | 16 / 44 / 46 | 4 | 45.67, 42.90 | 31.9 (27.4, 26.1) | 3.04 |
| 4 | control 30 FPS | 26.57 | 1583 / 1 | 31 / 62 / 66 | 114 | 24.73, 26.61 | 28.0 (18.3, 19.8) | 2.82 |
| 5 | **B3** | **42.52** | 2506 / 117 | 18 / 46 / 48 | 11 | 42.71, 42.03 | **40.3** (27.3, 27.3) | 3.17 |
| 6 | B1 | 42.52 | 2503 / 107 | 18 / 44 / 48 | 14 | 42.76, 40.40 | 29.6 (27.2, 27.1) | 3.18 |

| Summary | B1 (n=3) | B3 (n=2) | Difference |
|---|---|---|---|
| Producing average FPS | 44.63 (42.52 to 47.13) | 43.35 (42.52 and 44.18) | -1.28 FPS (-2.9 %), **inside the B1 spread of 4.6 FPS** |
| p95 bucket | 44 ms | 44 and 46 ms | none (both well above the 19 ms budget line, as are the empty-run controls) |
| Intervals >= 50 ms | 36, 4, 14 | 11, 11 | none |
| Fresh-run controls (empty factory) | 40.4 to 45.7 | 42.0 to 44.3 | none |
| **Guest CPU of the producing window** | **33.3 % (29.6 to 38.3)** | **40.8 % (40.3 and 41.3)** | **+7.5 points (+22 %)**; the budget allows max(10 %, 2 points) = 3.5 points |
| Guest CPU of the fresh-run controls | 26.1 to 27.5 % | 27.3 to 28.0 % | none |

How to read it. The median frame is 16 ms in every run (p50 bucket 16 to 18), and roughly 4 percent of frames are about 44 ms in **every** run, including the empty-run controls of B1 and of B3: a periodic host-side stall of the emulator, not the app. Within that throttle **B3 holds the same frame rate as B1** (43.4 against 44.6 FPS, difference smaller than the B1 run-to-run spread) and its empty-run controls match B1's. The 60 FPS rows (1, 2, 3) cannot be judged: B1 is below them too. **The guest CPU difference of the producing window is the one signal that points the wrong way**: B3 at 40 to 41 % of a core against B1 at 30 to 38 %, while the empty-run controls are identical (27 to 28 %). With the first B1 run (38.3 %, the run right after the boot) kept, the B1 spread of 8.7 points is larger than the B3 to B1 mean difference (7.5); without it B1 is 30.7 % and the difference is +10 points. A restyled producing factory does more per-frame work (state-cue overlays, accent animation, floor tiles, atlases), so a real rise is plausible, but **this session cannot separate it from the noise** and the 3.5-point allowance of budget 9 is neither shown met nor shown missed. It is the first thing a valid session must settle.

### Memory (outside any timed window, `pss-probe.ps1`, one `dumpsys meminfo` 45 s after the reconcile, W003-S, same process; session 4)

| | B1 | B2 (budgets file) | B3 | B3 against B1 |
|---|---|---|---|---|
| Total PSS | 500,171 KiB | 534,031 KiB | **525,694 KiB** | **+25,523 KiB (+5.1 %)** |
| Native Heap | 38,348 | 43,224 | 43,368 | +5,020 |
| Code | 105,184 | 95,636 | 85,008 | -20,176 |
| Private Other | 300,668 | 340,808 | 342,168 | +41,500 |

R12 names no PSS budget: the +5.1 % matches B2's +5.1 % and is recorded, not frozen. (The loaded session 3 gave 504,651 and 529,305 KiB, +4.9 %.) B1's PSS spread between sessions is about 1 %.

## 5. Floor layer: the T472 measurement points (Issue comment, escalation rule)

The floor is the pooled per-tile `SpriteRenderer` layer of T472 (`SlotGridView`, one 70 x 54 art-pixel tile per lattice cell, 2 art-pixel margin, hard cap 2048 tiles, re-placed only when the covered cell block changes, driven from `LateUpdate`).

Raw material: `artifacts/f004/B3-evidence/w004v-setpass/` (graphical Editor probe log), `player-probe/` and `player-probe2/` (Windows development player probe, results XML and `b3-lines.txt`), `bare-edge-editor2/` (frames and log). All probes are throwaway files (section 10); each number below is a log line of one of them.

### Point 1: SetPass per world-camera render with the floor on, against the 0.86 ceiling

Method of `baselines.md` section 3 and `performance-budgets.md` section 6: one graphical Unity 6000.6.0f1 Editor on the RTX 5070 Ti (Direct3D 12), the production `RunSession`, `SlotGridView` and sprite catalog (real F004 art), W004-V forced presentation (hash `1920dfaf...ecca6fc`, unchanged), 20 world-camera renders per "with" frame, 60 frames with and 60 without, value = (with - without) / 20 on the `SetPass Calls Count` recorder. "Floor on" sets the view's floor camera to the world camera; "floor off" gives it a perspective camera, which draws no tiles. Frames: the legacy fit-all and `n_min` presets of B2 (like-for-like with the 0.77 to 0.78 reference) and the production integer-scale model (`FactoryCameraController` at `n_min` and at its fit scale). Each frame was measured twice with the floor on; the floor-off value brackets them. Batches and draw calls still read 0 in this Editor/backend (as at B1 and B2).

| Screen, frame | Floor tiles | Slot renderers | SetPass per render, floor on (two readings) | Floor off | Passes per render (minimum of the with-frames, 21 = one per render) |
|---|---|---|---|---|---|
| 640x480 W003-S fit-all | 36 | 48 | 0.75, 0.85 | 0.76 | 1 |
| 640x480 W004-V legacy `n_min` | 100 | 47 | 0.77, 0.78 | 0.86 | 1 |
| 640x480 W004-V legacy fit-all | 36 | 47 | 0.81, 0.76 | 0.88 | 1 |
| 640x480 model `n_min` (n=1) | 100 | 47 | 0.86, 0.85 | 0.80 | 1 |
| 390x844 W003-S fit-all | 60 | 48 | 0.87, 0.75 | 0.85 | 1 |
| 390x844 W004-V legacy `n_min` | 102 | 47 | 0.83, 0.78 | 0.80 | 1 |
| 390x844 W004-V legacy fit-all | 60 | 47 | 0.87, 0.81 | 0.87 | 1 |
| 390x844 model `n_min` / fit (n=1) | 102 | 47 | 0.74, 0.77 / 0.78, 0.82 | 0.76 / 0.81 | 1 |
| 1280x720 W003-S fit-all | 48 | 48 | 0.86, 0.81 | 0.85 | 1 |
| 1280x720 W004-V legacy fit-all | 48 | 47 | 0.88, 0.85 | 0.83 | 1 |
| 1280x720 model fit (n=2) | 80 | 47 | 0.86, 0.83 | 0.85 | 1 |
| **1280x720 W004-V legacy `n_min` / model `n_min` (n=1)** | **266** | 47 | **1.75, 1.76 / 1.74, 1.75** | 0.81 / 0.77 | **2** |
| 1920x1080 W003-S fit-all | 96 | 48 | 0.86, 0.83 | 0.85 | 1 |
| 1920x1080 W004-V legacy fit-all / model fit (n=4) | 96 / 48 | 47 | 0.78, 0.82 / 0.76, 0.81 | 0.85 / 0.84 | 1 |
| **1920x1080 W004-V legacy `n_min` / model `n_min` (n=1)** | **588** | 47 | **2.71, 2.68 / 2.70, 2.68** | 0.89 / 0.83 | **3** |

Reading:

- **Up to about 150 renderers (floor plus slots) one world render is one SetPass call**, with or without the floor. The differential reads 0.74 to 0.88 with the floor on and 0.76 to 0.89 with it off; four floor-on readings (0.86 to 0.88) touch or exceed the 0.86 ceiling, and the same band appears with the floor off, so the ceiling is inside the noise of this method (about plus or minus 0.1), not a floor effect. The discrete minimum (21 SetPass calls over 20 renders plus one) is the unambiguous result.
- **The count steps up with the renderer count**: 266 floor tiles (313 renderers) give two passes per render, 588 tiles (635 renderers) three. The step lies between 147 and 313 renderers (not located more exactly); it is consistent with an SRP Batcher or sprite batch size limit (about one pass per 256 renderers), which is a hypothesis, not a measurement. The follow-up measurement below (2026-11-05, Architect conditions F-3 and F-5) pins the step down further: every phone frame and the tablet-portrait frame measured one pass, the tablet-landscape `n_min` frame (287 renderers) two, and the 2000-tile cap windows seven.
- **Budget 8 as frozen (at most 0.86) is therefore exceeded on the desktop lowest-zoom frames** (n = 1, a 1.0 px/pt window): 1.74 to 1.76 at 1280x720 and 2.68 to 2.71 at 1920x1080. It is met on every phone frame and on the tablet-portrait frame (follow-up table below); the tablet-landscape `n_min` frame (2 passes) is outside it.

#### Follow-up 2026-11-05 (Architect conditions F-3 and F-5): phone, tablet and tile-cap windows

Same graphical Editor, same real art, same W004-V forced presentation, same differential method (20 renders per "with" frame, 60 frames with and 60 without), but the world camera is the production `FactoryCameraController` at the lowest allowed scale (`n_min`) and at its fit scale, in a full-screen unobstructed rectangle (no HUD, so the tile counts are those of the bare camera; `n_min` does not depend on the HUD) with the device's pixels per point (`probes/B3SetPassProbeTests.cs.txt`, test `WindowSetPass`). Raw log `artifacts/f004/B3-evidence/arch-window-setpass2/`. In this run the per-render value was exactly discrete (the minimum of the with-frames is 20 calls per 20 renders for one pass), so the +/-0.1 noise of the table above did not appear; floor off was 1.0 everywhere.

| Window, px/pt | Scale | Floor tiles | Renderers (tiles + 47 slots) | SetPass per render, floor on (two readings) |
|---|---|---|---|---|
| Phone 1080x2400 (emulator), 2.63 | `n_min` 3 / fit 4 | 112 / 60 | 159 / 107 | 1.0, 1.0 / 1.0, 1.0 |
| Phone 2400x1080 (emulator), 2.63 | `n_min` 3 / fit 5 | 96 / 40 | 143 / 87 | 1.0, 1.0 / 1.0, 1.0 |
| Phone 720x1280 (320 dpi), 2.0045 | `n_min` 2 / fit 3 | 91 / 45 | 138 / 92 | 1.0, 1.0 / 1.0, 1.0 |
| Phone 1280x720 (320 dpi), 2.0045 | `n_min` 2 / fit 3 | 80 / 40 | 127 / 87 | 1.0, 1.0 / 1.0, 1.0 |
| **Tablet portrait 1536x2048, 2.0222** | `n_min` 3 / fit 6 | **126** / 35 | **173** / 82 | **1.0, 1.0** / 1.0, 1.0 |
| **Tablet landscape 2048x1536, 1.8667** | `n_min` 2 / fit 6 | **240** / 36 | **287** / 83 | **2.0, 2.0** / 1.0, 1.0 |
| Desktop-sized window 3840x1900, 1.0 | `n_min` 1 / fit 6 | **2016** / 70 | **2063** / 117 | **7.0, 7.0** / 1.0, 1.0 |

(The 3600x2000 window at `n_min` 1 has 1976 tiles and 2023 renderers and also read 7.0 passes in the first run of this probe, `arch-window-setpass/`.) Reading: one pass up to 173 renderers (the largest single-pass frame measured, tablet portrait at `n_min`), two at 287 and 313 (the earlier 1280x720 frame), three at 635, seven at about 2050. The step lies **between 173 and 287 renderers**; the pass count is roughly one per 256 to 300 renderers, which stays a **hypothesis** (SRP Batcher or sprite batch size limit), not a measurement. **So the single-pass claim holds on every measured phone frame and on the tablet-portrait frame, and does not hold on the tablet-landscape `n_min` frame (2 passes) or on any frame with more than about 250 renderers.**

**The floor disappears above the cap.** A window that needs more than 2048 tiles gets no floor at all (`SlotGridLayout.FloorCellsFor` returns the empty range): 3800x2000 and 3840x2160 at n = 1 drew 0 tiles in the probes (the camera background shows). The largest drawn windows were 3840x1900 (2016 tiles) and 3600x2000 (1976 tiles).

### Point 2: renderer count and frame time at the lowest zoom on the desktop player

Windows development player (`scripts/test-unity.ps1 -StandaloneWindowsPlayer`, Direct3D 12, Mono scripting backend), throwaway probe over the synthetic sprite catalog of `PresentationAllocationTests` (the real art catalog needs the authored scene, which the player test harness does not load; the draw structure is the same: one sprite per tile, the active URP 2D pipeline). 12 W004-V slots, camera rendering into an exact-size RenderTexture; `camera.Render()` timed with a Stopwatch (200 renders after 20 warm-up frames, floor on and off, each measured twice and averaged); the GPU-inclusive figure closes batches of 20 renders with a 1x1 readback; the live-loop figure is `Time.unscaledDeltaTime` over 600 frames with the camera rendering itself every frame and vsync off.

| Screen, scale n | Floor tiles | Renderers (slots 47 + tiles) | `camera.Render` CPU mean / p95 ms, floor on | floor off | GPU-inclusive mean ms per render, on / off | Live loop mean / p95 / p99 / max ms, floor on | floor off |
|---|---|---|---|---|---|---|---|
| **1920x1080, n=1** (lowest) | **588** | **635** | **0.265 / 0.393** | 0.109 / 0.140 | 2.06 / 0.70 | **1.62 / 6.44 / 7.83 / 11.28** | 1.77 / 6.60 / 8.17 / 13.27 |
| 1920x1080, n=2 (`n_min` at 1.5 px/pt) | 154 | 201 | 0.158 / 0.263 | 0.111 / 0.123 | 0.96 / 0.80 | 1.85 / 6.44 / 7.13 / 12.15 | 1.47 / 6.15 / 6.94 / 7.59 |
| 1920x1080, n=4 (fit) | 48 | 95 | 0.134 / 0.165 | 0.129 / 0.159 | 1.46 / 1.12 | 1.77 / 6.52 / 7.37 / 10.43 | 1.82 / 6.52 / 7.44 / 7.89 |
| 1280x720, n=1 | 280 | 327 | 0.205 / 0.281 | 0.150 / 0.217 | 1.45 / 0.93 | not run | |
| 1080x2400, n=3 (emulator `n_min`) | 112 | 159 | 0.199 / 0.264 | 0.159 / 0.189 | 1.73 / 1.26 | not run | |
| 1080x2400, n=4 (emulator fit) | 60 | 107 | 0.199 / 0.286 | 0.178 / 0.243 | 2.22 / 1.83 | not run | |

The floor adds 0.03 to 0.16 ms of CPU render submission per camera render at the lowest zoom (588 tiles); the live loop at n=1 is 1.6 ms mean with the floor on and 1.8 ms with it off, that is, no measurable difference, and every frame stays far below 16.7 ms. The p95 and p99 of 6 to 8 ms in the live loop are the same with the floor off and come from the player window and the loaded host (section 2), not from the floor. The GPU-inclusive figure contains the readback synchronisation and is only an upper bound; the Windows GPU is much faster than any Android target (FR-027).

**Tile cap (Architect condition F-3, measured 2026-11-05).** Same Windows development-player probe as the table above (`probes/F004B3ProbeTests.cs.txt`, test `Cap_FloorCost_AtTheTileCap`; synthetic sprite catalog, 12 W004-V slots, n = 1, 20 warm-up renders, then 200 `camera.Render()` timed with a Stopwatch; two runs, each floor on and off), at the 1920x1080 reference and at the two windows that come closest to the 2048-tile cap. Raw log `artifacts/f004/B3-evidence/arch-cap-player/`. The host was loaded (the owner's game client was running, section 2), which shows in the spread between the two runs.

| Window, n = 1 | Floor tiles | Renderers | Run | Floor on mean / p95 / max ms | Floor off mean / p95 / max ms |
|---|---|---|---|---|---|
| 1920x1080 | 588 | 635 | 1 ; 2 | 0.302 / 0.765 / 1.62 ; 0.327 / 0.753 / 2.35 | 0.115 / 0.161 / 0.27 ; 0.129 / 0.193 / 0.38 |
| 3600x2000 | 1976 | 2023 | 1 ; 2 | **0.965 / 1.918 / 12.17** ; **0.537 / 0.868 / 3.60** | 0.156 / 0.320 / 1.07 ; 0.155 / 0.381 / 1.04 |
| **3840x1900** | **2016** | **2063** | 1 ; 2 | **0.587 / 1.092 / 3.25** ; **0.551 / 0.991 / 3.16** | 0.140 / 0.260 / 1.08 ; 0.169 / 0.357 / 2.69 |
| 3800x2000 and 3840x2160 | 0 (above the 2048 cap: no floor drawn) | 47 | not timed | | |

**At the cap the floor costs 0.4 to 0.8 ms of CPU render submission per camera render (mean 0.54 to 0.97 ms with the floor on against 0.14 to 0.17 ms off): more than the 0.5 ms mean, and (in 3 of 4 runs) more than the 1.0 ms p95, that were proposed for the 588-tile window. The 588-tile repeat (mean 0.30 to 0.33 ms, p95 0.75) is also higher than the 0.265 ms and 0.393 ms of the first measurement above, which is the host load.** The cap is therefore not covered by the 0.5 / 1.0 ms bound; section 11 proposes a separate bound and flags it. The per-tile cost is about 0.2 to 0.4 microseconds of CPU, so the cap is a bounded cost (about 3 to 6 percent of a 16.7 ms frame), not a growth risk.

### Point 3: Android emulator frame time at fit-all and at `n_min`

**Not a verdict.** The world-visible captures need the Welcome-back sheet dismissed (T442 F4: the journey's fixed tap `0.85, 0.738` of the height lands on the upper edge of the Continue button), so the captures used an explicit tap at the button centre, `918,1815` device pixels, and the zoom control `-` at `68,2178` was tapped once to go from the fit scale `n_fit = 4` to `n_min = 3` on this 1080x2400 / 420 dpi screen (the same two scales as T442 section 7a). The helper (`b3-zoom.ps1`) is a scratch file, not committed; the two world frames are in `performance-b3/` (`emulator-fit-all-n4.png`, `emulator-n-min-n3.png`: restyled factory with floor, hit by one tap each). Windows of 30 s warm-up plus 60 s, B3 APK, the world visible, order fit-all, `n_min`, fit-all, `n_min`, after session 4 on the same boot:

| Capture | Avg FPS | Frames / dropped | p50 / p95 / p99 ms | Intervals >= 50 ms | Guest CPU % | Host busy mean |
|---|---|---|---|---|---|---|
| fit-all (n=4) 1 | 40.54 | 2391 / 149 | 29 / 46 / 50 | 26 | 42.3 | 3.44 |
| `n_min` (n=3) 1 | 32.71 | 1874 / 134 | 29 / 58 / 134 | 142 | 67.2 | 4.76 |
| fit-all (n=4) 2 | 35.77 | 2121 / 130 | 27 / 50 / 74 | 128 | 82.6 | 5.48 (contaminated) |
| `n_min` (n=3) 2 | 38.41 | 2273 / 122 | 27 / 48 / 58 | 85 | 77.2 | 5.27 (contaminated) |

The host load climbed during this set (3.4, then 4.8 to 5.5 busy threads: only the first capture is inside the 4.5 limit), and the numbers move with it, in both zoom directions (fit-all 40.5 and 35.8, `n_min` 32.7 and 38.4): **the load, not the zoom state, decides them, and no difference between fit-all and `n_min` can be read**. An earlier set under the game load (17 to 29 FPS) is archived and void. At the emulator's two scales the floor is 112 tiles (159 renderers) at `n_min` and 60 tiles (107 renderers) at fit-all (player probe, point 2), single-pass territory (point 1), and the Windows render cost of both differs by at most 0.1 ms of CPU; that is the only floor evidence for these two states. **A valid emulator reading at fit-all and `n_min` needs the same quiet-host session as the budgets above.**

### Point 4: GC allocation during a pinch gesture

Windows development player, `GcAllocationCounter` (the `GC.Alloc` recorder), the production `FactoryCameraController.PreviewPinch` / `EndPinch` path (the real pinch path: continuous preview, snap to the nearest allowed level on release) plus `SlotGridView.UpdateFloor()` and `SetProjectedPixelsPerCell` after every preview frame, as `LateUpdate` does. One cycle is 40 preview frames out, a snap, 40 preview frames in, a snap. The existing allocation test does not cover this (its floor case keeps the zoom level unchanged; its camera case never updates the floor).

| Screen (px/pt), scale range | Cold first gesture (pool growth) | Warm: 10 cycles = 800 preview frames | 20 button steps with floor | 300 pan frames with floor | Pool tiles before, after first gesture, after warm |
|---|---|---|---|---|---|
| 1280x720 (1.0), n 1 to 6 | 723 `GC.Alloc` samples | **0** | 0 | 0 | 40, 280, 280 |
| 1920x1080 (1.5), n 2 to 6 | 375 | **0** | 0 | 0 | 30, 154, 154 |
| 1080x2400 (2.63), n 3 to 6 | 157 | **0** | 0 | 0 | 60, 112, 112 |

**Steady state is 0.** The one-time cost is the growth of the tile pool during the first pinch-out to a zoom level whose block needs more tiles than the pool holds (new `GameObject`s and `SpriteRenderer`s: 157 to 723 allocations once per pool size). It is outside the "warmed" definition of the budget; a pre-grow of the pool at layout time (to the `n_min` block) would remove it and is offered as an option, not required by a budget.

Cost of the floor update itself (Windows development player, `Stopwatch` around `UpdateFloor()`, 300 calls per case): unchanged camera 0.0002 to 0.0004 ms; a pan with a block change in most frames mean 0.007 to 0.016 ms (max 0.46 ms); a zoom change every frame mean 0.012 ms (n=2, 154 tiles) to 0.077 ms (n=1, 588 tiles), p95 0.023 to 0.262 ms, **max 0.24 to 1.00 ms** (the largest values are pool-growth steps while the visible block grows); first call from an empty pool 1.7 ms (154 tiles) to 3.8 ms (588 tiles). No pan or pinch frame costs more than about 1 ms in the development player.

### Point 5: visible bare edge during a fast pan or pinch (A2, `LateUpdate` order against `FactoryCameraController`)

Reading of the code: the camera is moved from `NavigationInputAdapter.Update` (through `FactoryCameraController.HandleNavigationIntent`); nothing in the controller runs in `LateUpdate`, and `SlotGridView.LateUpdate` fits the floor afterwards, so the floor always sees the camera of the frame that is drawn (A2 holds by order).

Measurement (graphical Editor, real F004 art and catalog, camera background magenta, the frame read back after `WaitForEndOfFrame`, magenta pixels counted; coroutine steps run in the `Update` phase, the view's own `LateUpdate` fits the floor): at rest, at `n_min` and at the largest scale, on 1920x1080 (1.5 px/pt), 1080x2400 (2.63 px/pt) and 1280x720 (1.0 px/pt), screen pans of 4, 16, 64 and 256 device pixels per frame for 90 frames each (back and forth), and a 240-frame pinch preview (zoom out and in, 0.12 per frame, four cycles with snaps).

| Case | Result |
|---|---|
| Rest, every screen and both zoom ends | **0 bare pixels** |
| Production order, pans 4 to 256 px per frame, `n_min` and largest scale, three screens | **0 frames with a bare pixel** of 90 per speed |
| Production order, pinch preview and snap, three screens | **0 of 240 frames** |
| Positive control: floor frozen (view disabled) while the camera pans 64 px per frame at the largest scale | bare edges in 28 of 90 frames at 1080x2400 (up to 172,800 pixels) and 37 of 90 frames at 1280x720 (up to 467,712 pixels): the detector works |
| Forced one-frame lag (camera moved after `LateUpdate`, frame drawn without a floor update) | bare edge only at the largest scale and only for pans of 16 px per frame or more on the phone (2 of 90 frames, 9,600 px), 64 px per frame or more at 1280x720 (6 of 90 frames, 28,800 px; 4 of 90, 235,112 px at 256 px), none on 1920x1080 |

At `n_min` the camera cannot pan at all with the whole lattice in view (camera travel 0.00 world units on every screen), so a pan limit rather than the margin protects the lowest zoom. The 2 art-pixel margin (4 to 12 device pixels, depending on n) absorbs a one-frame lag of up to 2 art pixels, which a normal drag never exceeds. **Not verified: a real two-finger pinch or a real fast pan on the emulator or a device** (the shell user cannot inject multi-touch, T442 section 8; the pinch here is the controller's own preview API, not pointer input). The forced-lag and frozen-floor lines are controls, not product behaviour; two frames are kept in `performance-b3/` (`bare-edge-control-frozen-floor-1280x720-nmax.png`, `bare-edge-forced-lag-1280x720-nmax-pan256.png`: magenta is the camera background where the floor is missing).

### Escalation rule (recommendation only)

"If B3 shows a pan or pinch spike, do an incremental recycle of the changed rows and columns (P2). Move to a single tiled renderer only if SetPass or cull cost exceeds the budget."

- **No pan or pinch spike was measured** (point 4: steady state 0 allocations, the floor update at most about 1 ms in the worst frame, mean at most 0.08 ms): the P2 incremental recycle is not indicated.
- **The SetPass condition is met only at the desktop lowest zoom** (n=1 at 1.0 px/pt: 2 to 3 passes against the frozen 0.86, point 1); the measured cost of those extra passes is 0.03 to 0.16 ms of CPU and no measurable live-frame change (point 2), and the Android emulator and phone frames are not affected. The Architect and owner should choose between restating budget 8 for n=1 desktop windows and accepting the exception; moving to a single tiled renderer is not supported by this evidence. The cull cost was not measured separately (no profiler capture); the CPU render figures above include it.

## 6. Windows-player allocation and host frame (budgets 4 and 5)

Runs: `scripts/test-unity.ps1 -StandaloneWindowsPlayer -TimeoutSeconds 3000` on `1838f22` in the issue worktree (active target Windows after a batch `-buildTarget` run; Windows development player, Mono scripting backend, Direct3D 12). **Official run (no probe in the project): exit 0, 62 of 62 passed** (the tagged `StandalonePlayer` suite of T442; result XML and log in `artifacts/f004/B3-evidence/player-official/`). The throwaway probe runs (sections 5 and 10) added their own tagged cases to a second and third run (70 and 73 of 73 passed) and were deleted afterwards.

### Budget 4: Windows host frame p95 against W003 (official run, `W003-HOSTFRAME`, 600 frames, development player)

| Scenario | p50 ms | p95 ms | p99 ms | max ms | mean ms | T122 reference p95 | SC-008 ceiling |
|---|---|---|---|---|---|---|---|
| **W003-S** (the B3 workload) | 0.014 | **0.029** | 0.134 | 0.399 | 0.019 | 0.02 to 0.04 | 1.0 |
| W003-B | 0.014 | 0.047 | 0.166 | 1.110 | 0.024 | 0.02 to 0.04 | 1.0 |
| W003-R | 0.014 | 0.060 | 0.289 | 0.835 | 0.025 | 0.02 to 0.04 | 1.0 |

W003-S, the workload of this feature, is inside the T122 range. W003-B and W003-R are 0.007 and 0.020 ms above its upper edge, while the host was loaded (section 2: a game client and its launcher were running, mean 3.9 busy threads); both are 5 to 6 percent of the 1 ms ceiling and the Core and Application update path is untouched by Feature 004. Read as noise-level, not as a regression, but not as "unchanged" either.

### Budget 5: steady-state GC allocation per frame

| Path | Result | Source |
|---|---|---|
| Advance path (gate, `Advance`, hot read-model copies), 300 frames, W003-B/S/R | **0** `GC.Alloc` samples | official run, `W003-ALLOC` (`advancePathSamples=0`), positive control `controlSamples=1` |
| **Whole host frame including the HUD refresh probe**, 300 frames | **6 samples in 6 frames per scenario**, once per simulated second, one Overdrive status object (`overdriveStatusReads=9`) | official run, `W003-ALLOC` (`fullFrameSamples=6`); **unchanged since T122: PR-2 is not fixed** |
| Presentation hot paths (clock tick with accent renderers, cue refresh, floor pan at a fixed zoom level, camera snap during pan and zoom, reduced motion on and off), warmed | **0**, 8 tagged cases passed in the player | official run, `PresentationAllocationTests` |
| Pinch gesture with the floor (section 5, point 4) | **0** warm (800 preview frames, 20 button steps, 300 pan frames per screen) | probe |
| **600-frame W004-V presentation run in the player** (clock tick, `Render` alternating between the W004-V forced visuals and the all-occupied set, pan, pinch preview and snap, `UpdateFloor`, 1280x720, n 1 to 6, 400 warm-up frames) | **0 samples in 600 frames** (0 frames with a sample, pool 48 tiles) | probe, `B3-W004V-600` |

The W004-V fixture lives in the Play Mode test assembly and is not a production hook; the probe applied `W004VFixture.Apply` to a synthetic sprite catalog in the player, so the forced states and the floor ran but the sprites are the synthetic ones of `PresentationAllocationTests`, not the authored art. The Windows run has no real Core update behind it for the presentation loop (the Core path is the W003 host-frame tests above).

**Status of budget 5**: met for the advance path and for every presentation path (0 steady-state allocations, now including pinch and the floor); **not met as written for the whole host frame** because of PR-2, which Feature 003 decision D2 accepted. The budget text must be restated by the owner ("advance and presentation paths allocation-free; the HUD read path allocates one object per simulated second") or PR-2 fixed; this review does not fix it.

## 7. OPS-4: full Play Mode run, `ac813fd` (B1) against `b9221ca` (URP)

Closes the open item OPS-4 of `performance-budgets.md` sections 3 and 5 (owner decision 2026-10-29). Both runs used the detached worktree `t412-b2-build-b9221ca` (its `Library/` from the T412 builds), `powershell -NoProfile -File scripts/test-unity.ps1 -Suite PlayMode -TimeoutSeconds 3000` (the Play Mode suite only: "one full Play Mode run each"), leases `unity,emulator,gpu` held by `issue-562`, no other Unity process, 30 GB free on C:. Raw logs and result XMLs: `artifacts/f004/B3-evidence/ops4/`.

| | `ac813fd` (B1, Built-in) | `b9221ca` (URP, phase 0) | Difference |
|---|---|---|---|
| Result | exit 0, **2248 of 2248 passed**, 0 failed, 0 skipped | exit 0, **2275 of 2275 passed**, 0 failed, 0 skipped | +27 Play Mode tests (parity and configuration cases of T411) |
| Test-run duration (result XML `duration`) | **545.8 s (9 min 6 s)** | **548.5 s (9 min 9 s)** | **+2.7 s (+0.5 %)** |
| Script compile time in the log (`compile time=`) | 13,967 ms (Tundra 9.9 s, 71 items) | 35,048 ms (Tundra 28.4 s, 481 items) | see caveat |
| Initial asset refresh | 17.5 s | 74.1 s | see caveat |
| Run order and start state | second, started from the URP Library state of the first run | first, started from the Built-in state left by the OPS-7 revert run | |
| Host load during the run (all processes, including Unity itself, mean busy threads of 8, 15 s samples) | 3.9 | 5.5 | |

Findings:

- **No Play Mode slowdown from URP: 545.8 s against 548.5 s, within 0.5 percent**, on the same host at similar load. The "25 to 30 minutes" of the T411 QA figure is **not reproduced**: a full Play Mode suite takes about 9 minutes on both pipelines here. The earlier figure most likely included a cold or contended state (disk pressure from several Unity worktrees, a parallel session) rather than the render pipeline; this is an inference, not a measurement of that run.
- **Caveats.** The host was **not quiet** (section 2): a game client and its launcher ran during both runs, so absolute durations are upper bounds and the 0.5 percent difference is not a precise ratio (the load differed between the two windows, 5.5 against 3.9 busy threads, without a visible effect on the durations). The two compile times are not like-for-like: each run recompiled after a Library switch (URP run: from the Built-in state of OPS-7, 481 items; B1 run: from the URP state, 71 items). They bound a project switch (35 s for the URP assembly set, 14 s for the Built-in set), not a cold compile of an identical state, which was not run.
- Unity re-serialized `UniversalRenderPipelineGlobalSettings.asset` in the URP run (the same `m_SettingsList` rid drop as in the builds, `ops4/urp-run-global-settings.diff`) and `ProjectSettings/ProjectSettings.asset` in both; both were restored with `git checkout` in the worktree. The worktree ends detached at `b9221ca`, clean (untracked `.utmp/` only).

## 8. OPS-7: R01 rollback dry run on a scratch branch

Closes the open item OPS-7. Procedure in the detached worktree `t412-b2-build-b9221ca`: `git switch -c scratch/t444-ops7-rollback b9221ca` (the final URP merge, PR #583, parents `9aa01d8` and `1a5e533`), `git revert -m 1 --no-commit b9221ca` (the mainline parent is kept, so the revert removes the effect of the whole T411 branch, that is, the second parent with all its commits), Unity batch compile and the Play Mode test file `RenderParityTests`. The branch was never pushed; afterwards `git reset --hard`, `git switch --detach b9221ca` and `git update-ref -d refs/heads/scratch/t444-ops7-rollback` left the worktree detached and clean. Raw material: `artifacts/f004/B3-evidence/ops7/`.

| Step | Result |
|---|---|
| `git revert -m 1 --no-commit b9221ca` | **clean, no conflict**; 19 files changed (+18, -1,890): the URP assets under `Assets/IdleFactory/Rendering/` (URP asset, 2D renderer, default volume profile, global settings), `RenderPipelineConfigurationTests`, `ProjectSettings/ShaderGraphSettings.asset` removed; `Packages/manifest.json`, `packages-lock.json`, `GraphicsSettings`, `QualitySettings`, the scene, `RenderCaptureHarness.cs` and `RenderParityTests.cs` restored (`ops7/revert-files.txt`) |
| Compile | succeeded: `compile time=16,354 ms` (Tundra 14.4 s, 77 items), asset refresh 24.3 s, no compile error |
| `RenderParityTests` (`scripts/test-unity.ps1 -Suite PlayMode -TestFile .../RenderParityTests.cs`) | exit 0, **18 of 18 passed** (not 0 tests; 9.3 s) against the committed Built-in baselines, as in the T411 record |
| `EditorSettings m_SpritePackerMode` | **0 before the revert and 0 after it** (the revert does not touch `EditorSettings.asset`; the value at `ac813fd` is also 0). **On today's main (`1838f22`) the value is 5**: T430 (`b8b01d0`) enabled the sprite-atlas mode after T411, so a rollback of the pipeline does not undo the atlas mode and the F004 atlases stay active. |
| Branch hygiene | scratch branch never pushed, deleted locally, worktree detached at `b9221ca`, clean |

Additional read-only check (no Unity): the same revert applied to **today's main**, `git merge-tree --write-tree --merge-base=b9221ca 1838f22 b9221ca^1`, reports **3 conflicts**: `IdleFactory2DRenderer.asset` and `RenderPipelineConfigurationTests.cs` (deleted by the revert, modified later on main: T457) and a content conflict in `Fixtures/W004V/RenderCaptureHarness.cs`; `RenderParityTests.cs` and the scene merge automatically. R01 is therefore a one-command revert **only at the merge commit `b9221ca`**. **On today's main it needs three manual resolutions** (`IdleFactory2DRenderer.asset` and `RenderPipelineConfigurationTests.cs` modify/delete conflicts caused by T457, and a content conflict in `RenderCaptureHarness.cs`), then a compile and `RenderParityTests`. `m_SpritePackerMode` stays 5 (T430) after such a rollback, so the F004 atlases stay active. **The rollback on main was not attempted and is not verified**; only the clean revert at the merge commit was compiled and tested.

## 9. Physical-device gap and open risks

- **Physical-device gap (FR-027): open.** There is no measurement on a physical Android device, no battery, thermal or sustained-load measurement, and no GPU time measurement on a mobile GPU. The emulator runs on the workstation GPU through a GLES translator (host GPU makes frame time cheap), guest CPU and PSS are guest numbers, and the Windows figures are a desktop CPU/GPU. A physical low-end device could pass or fail every emulator row differently; the floor at the lowest zoom (up to about 160 renderers on the emulator's screen class, more on tablets) and the 5.8 MiB of atlas pages (against 0.9 MiB of placeholder textures at B2) are the first things to check there. Nothing in this record closes the gap.
- **Budget 8 (SetPass) exceeded above about 175 renderers** (2 passes on the tablet-landscape `n_min` frame, 2 to 3 at desktop n=1 with 266 to 588 floor tiles, 7 at the 2048-tile cap, against a frozen 0.86). No frame-time effect was measurable at 588 tiles (section 5); at the cap the floor costs 0.54 to 0.97 ms of CPU render submission per camera render. The restatement and its two-tier CPU bound are in section 11 (owner decision, Architect check at close-out).
- **Budget 5 whole-frame wording (PR-2)** is unchanged and accepted since Feature 003 D2; restating the text is cleaner than leaving a known failing row.
- **First-gesture pool growth** allocates once per pool size (157 to 723 `GC.Alloc` samples, 1.7 to 3.8 ms for the first fill); not a steady-state violation, a pre-grow at layout time would remove it.
- **Emulator rows open**: no valid ABA session (section 4). Needed: a quiet host, a healthy emulator (B1 at about 61 FPS and the 30 FPS control at about 30 before B3 is judged), then budgets 1, 2, 3, 9 and T472 point 3. The guest CPU signal (B3 +7.5 points on B1 in session 4) is the item most likely to fail.
- **URP global settings re-serialization** by Android builds and by test runs persists (one `m_SettingsList` entry, `RenderingDebuggerRuntimeResources`, no shader); the variant count is unaffected. Keep diffing the asset after every Unity run.
- **Rollback (R01)** is a one-command revert only at the merge commit; on main today it conflicts in three files (section 8).
- **Welcome-back sheet in the journey** (T442 F4): the scripted producing capture runs behind the modal sheet exactly as at B1 and B2 (same protocol, comparable); the world-visible captures of point 3 dismiss the sheet by an explicit tap.

## 10. What was run, what was not, and the probes

Run in this task (commit `1838f22` unless stated):

| Run | Where | Result |
|---|---|---|
| B3 and 30 FPS control builds | issue worktree | section 3, 0 errors |
| Graphical Editor SetPass probe (target switched to Windows by a batch `-buildTarget`) | issue worktree | 4 of 4 probe cases passed |
| Graphical Editor bare-edge probe, real art (twice; the first run showed no camera motion at `n_min` and no control, so it was extended with zoom modes, a travel measurement and a frozen-floor control) | issue worktree | 6 of 6 cases passed |
| `scripts/test-unity.ps1 -StandaloneWindowsPlayer`: official, with probe (compile error `FloorMarginPixels` fixed once), with probe plus the `UpdateFloor` timing | issue worktree | 62 of 62, 70 of 70, 73 of 73 passed |
| Emulator ABA sessions | emulator | four sessions, none valid (section 4); session 4 is the best record |
| Zoom captures (point 3) | emulator | two sets, the host load decides them, no verdict |
| OPS-4, two full Play Mode runs | `t412-b2-build-b9221ca` | 2275 of 2275 and 2248 of 2248 |
| OPS-7 rollback dry run | same worktree, scratch branch, deleted | clean revert, 18 of 18 |
| `git merge-tree` revert simulation on main | issue worktree, read-only | 3 conflicts |

**Not run:** a valid ABA session on a quiet host with a healthy emulator (rows 1, 2, 3, 9 and point 3); an emulator run of W004-V (the forced presentation is Editor and test code only, so `W004VFps` measures the plain restyled W003-S factory, as before; W004-V is covered by the Editor and Windows probes above); a real two-finger pinch or pan on the emulator or a device; the full Edit Mode suite and the full Play Mode suite on `1838f22` (T442 ran them on `701e8c8`; this task changed no code); a Profiler capture on the Windows player or the emulator (texture memory in the player is the Editor runtime figure); a cull-cost measurement separate from the CPU render time; a physical-device run; an identical-state cold compile for OPS-4; the emulator W003-B workload; the Android build from an empty Library (B2 style).

**Probes.** Two throwaway test files (one Editor SetPass, bare-edge and window probe, one Windows-player probe with the cost, live-loop, pinch GC, `UpdateFloor` timing, 600-frame W004-V and tile-cap cases) were copied into `Assets/IdleFactory/Tests/PlayMode/` for a run and deleted afterwards; the test manifest is unchanged. Their sources, the emulator drivers, SHA-256 values and the exact command lines are kept as non-compiling text under `performance-b3/probes/` (`README.md`). No code, test, scene, setting or script is committed under `Assets/` or `scripts/`.

## 11. Owner decisions 2026-11-05

Recorded as given by the owner through the coordinator after this review. No measured value in this document changed; only the status wording of the table in section 1 and of the T472 point 3 line.

1. **Budgets 1, 2, 3, 9 and T472 point 3 (emulator FPS, p95 bucket, intervals >= 50 ms, phase-0 guest CPU, Android frame time at fit-all and `n_min`): accepted as an open gap together with the physical-device gap (FR-027). No re-measurement in Feature 004.**
   - The indicative guest-CPU signal (B3 40.8 % against B1 33.3 % of a core, +7.5 points, inside the B1 spread of 8.7 points in session 4; empty-run controls equal) is the **first item to settle in any later valid session**.
   - Conditions such a session needs: a quiet host with no game client (World of Warcraft, League of Legends, Valorant, TFT or similar, and their launchers closed; host busy threads about 1.6 or less outside the emulator); a cold-booted emulator with host GPU (`-gpu host -no-snapshot-load`) started from the main checkout; a **30 FPS control reading within 1 FPS of 30** (session 4 read 24.7 to 26.6); B1 reproducing about 61 FPS before B3 is judged; leases `unity,emulator,gpu`, no Unity process, APK hashes verified after every install, and at least three B1 runs around the B3 runs (ABA).
2. **Budget 8 restated by the owner; exact scope, method and bounds (revised after the Architect review, with the measured 2048-tile cap).**
   - **(a) Phone and tablet class windows, where floor plus slots stay at or under about 150 to 175 renderers:** SetPass per world-camera render **at most 0.86**, read as **one pass per render** by the discrete minimum (20 or 21 SetPass calls over 20 renders; the differential method alone has about +/-0.1 noise). Measured (section 5, point 1 and its follow-up, graphical Editor, real art, W004-V): every phone frame (1080x2400 and 2400x1080 at 2.63 px/pt, 720x1280 and 1280x720 at 2.0045 px/pt, at `n_min` and fit; 87 to 159 renderers) and the tablet-portrait frame (1536x2048 at `n_min` 3: 126 tiles, 173 renderers) read exactly **1 pass**. **Provisional where not covered:** the tablet-landscape `n_min` frame (2048x1536, 1.8667 px/pt, `n_min` 2: 240 tiles, 287 renderers) read **2 passes**, so it is outside (a); tablets between 173 and 287 renderers are unmeasured. The step between one and two passes lies between 173 and 287 renderers; "one pass per about 256 to 300 renderers" remains a **hypothesis** (SRP Batcher or sprite batch size limit), not a measurement.
   - **(b) Any window above about 150 to 175 renderers (the tablet-landscape `n_min` frame, desktop windows at n = 1, the 2048-tile cap):** SetPass is **not budgeted** and is recorded for information (2 passes at 287 renderers, 2 at 313, 3 at 635, 7 at about 2050). **The budget is `camera.Render()` CPU time.** Workload: W004-V, 12 slots, n = 1. Screens: 1920x1080 (588 tiles, 635 renderers) and the 2048-tile cap (the largest windows that still draw a floor: 3840x1900, 2016 tiles, 2063 renderers, and 3600x2000, 1976 tiles). Method: development Windows player, Direct3D 12, floor on, 200 renders after 20 warm-up frames, Stopwatch, two runs.
   - **Bound, tier 1 (1920x1080, n = 1, 588 tiles): mean at most 0.5 ms and p95 at most 1.0 ms.** Evidence: 0.265 ms mean and 0.393 ms p95 in the first measurement; 0.302 and 0.327 ms mean, 0.765 and 0.753 ms p95 in the repeat on the loaded host (floor off 0.11 to 0.13 ms). Met.
   - **Bound, tier 2 (the 2048-tile cap): mean at most 1.0 ms and p95 at most 2.0 ms. FLAG: the 0.5 / 1.0 ms bound proposed by the Architect is exceeded at the cap.** Measured at the cap (3840x1900 and 3600x2000, two runs each): mean **0.54, 0.55, 0.59 and 0.97 ms**, p95 **0.87, 0.99, 1.09 and 1.92 ms**, worst single frames 3.2 to 12.2 ms (floor off: mean 0.14 to 0.17 ms). One run of the four (3600x2000, first run) has a mean of 0.97 ms and a p95 of 1.92 ms, which is within 3 to 4 percent of the proposed tier-2 bound and probably includes warm-up and host load; the margin is thin and a quiet-host repeat should confirm it. Rationale: 1.0 ms is 6 percent of a 16.7 ms frame; the per-tile cost is about 0.2 to 0.4 microseconds, so the cap (2048) is a hard upper bound of about 1 ms and a bound at 1.0 ms flags a per-tile cost regression without failing on the SetPass count, which is a batching artefact. The alternative that keeps the 0.5 / 1.0 ms bound is to lower the tile cap to about 1000 tiles; that would remove the floor from larger windows than today (above 2048 tiles the floor is already not drawn: 3800x2000 and 3840x2160 at n = 1 draw none), and is not proposed here.
   - Limits of the evidence: measured with a **synthetic sprite catalog** in the player (the player test harness has no authored scene), so **real-art atlas batching is not covered** by the CPU-time figures (the SetPass figures use real art); a development Windows player on one workstation, loaded by the owner's game client: **exploratory evidence from one workstation (FR-027); the physical-device gap stays open.**
   - **The frozen text of `performance-budgets.md` section 4 (row 8) changes in the close-out, with an Architect check. This task did not edit that file.**
3. **Budget 5, whole host frame: accepted (PR-2).** It is the known PR-2 finding (the HUD read path allocates one Overdrive status object per simulated second: 6 `GC.Alloc` samples per 300 frames), recorded in `specs/003-first-factory-prestige/reviews/performance-review.md` section 4.2 and accepted in Feature 003 decision D2 (`specs/003-first-factory-prestige/reviews/final-acceptance.md`, decision table row D2 and the PR-2 line of its findings list; `feature-review.md` line 125 for the review recommendation; `wiki/pages/w003-player-timing-baseline.md`). The advance path and every presentation path stay at 0.

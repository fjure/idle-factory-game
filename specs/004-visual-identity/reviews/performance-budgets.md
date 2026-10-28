# Frozen performance budgets and B2 record (T412)

Issue #524, Performance Engineer. Measured on `b9221ca` (T411 merge, the phase-0 render switch) against the paired B1 (`ac813fd`, [baselines.md](./baselines.md)). Evidence for FR-006, FR-025, FR-027, SC-006; research R12 and constitution VI. Every number here is **emulator or build-report evidence on one workstation (exploratory)**. There is no physical-device, battery or thermal measurement (FR-027); the physical-device gap stays open and is not closed by any row below.

Raw material (outside git): `artifacts/f004/B2-evidence/` (`session/` journeys, SurfaceFlinger dumps, sampler, `aba2.sh`; `build-b2/`, `build-control/` build logs, `build-report.tsv`, `shader-variants.tsv`, `report-dump.log`; `global-settings-after-builds.diff`, `projectsettings-after-builds.diff`).

## 1. Build record (OPS-1, OPS-2)

| Item | Value |
|---|---|
| Commit | `b9221ca`, built in a detached worktree (`t412-b2-build-b9221ca`), recipe `scripts/f004-b1/run-build.ps1` (same as B1) |
| Library state | **Empty `Library/`** for the B2 build ("Rebuilding Library because the asset database could not be found!" once in `build-b2/build.log`); 22:31:45 to 22:38:13. The 30 FPS control was built incrementally from the same Library afterwards (0 such lines in its log). |
| B2 APK | `B2-b9221ca-161d140b.apk`, sha256 `161d140b1b93cc5c...cba90`, **46,330,863 B**, 420 entries, 141,042,486 B uncompressed; Android arm64, IL2CPP, Development, GLES3, profiler-free (0 "Autoconnect Profiler" lines in all six producing captures) |
| Control APK | `B2ctl30-b9221ca-f8ec87ec.apk`, 46,373,284 B (same commit, frame-rate line removed for this build only) |
| Build time | B2 4 min 30 s (B1 5 min 39 s, both from an empty Library); 0 errors, 0 warnings (B1: 3 warnings) |
| URP global settings after the builds | One `m_SettingsList` rid was dropped by Unity: `3338746865761648669` in the second list (line 86). It resolves to `RenderingDebuggerRuntimeResources` (UI Toolkit panel settings and style sheets of the rendering debugger, no shader). Variant counts are identical to T411's own record (490 in 235 snippets of 95 shaders, `t411-urp-build`) and equal between the B2 build and the control, so the drop did not change shader stripping. The file was restored with `git checkout` after the builds. It is still a Unity-tooling re-serialization to watch (pitfall OPS-3/A8): diff the asset after every Android build. |
| ProjectSettings after the builds | iOS-icon slot noise (`projectsettings-after-builds.diff`), restored. Nothing under `ProjectSettings/` is committed by T412. |

## 2. Emulator session (B2, ABA with 30 FPS control)

Preflight (R12, recorded): leases `unity,emulator,gpu` held by `issue-524`; no Unity editor (only Unity Hub's `unity.exe serve`), no Blender or ComfyUI application (only idle `blender-mcp`/`comfy-mcp` python servers), no game client; emulator cold-booted once (`emulator -avd Medium_Phone_API_36.0 -gpu host -no-snapshot-load`), started together with adb from the main checkout path, 22:42:53 boot, 90 s settle; `adb devices` healthy (`emulator-5554 device`), no foreign adb or monkey; APK hashes checked before the session and each run (hash prefixes printed after every run); the journey's Install step is followed by the unscored Launch step in every run. Journey `-Scenario F003 -Steps Preflight,Install,Launch,W003Push,ProducingFps,F003LogScan`, workload W003-S (`w003-s-shipped` set, SHA-256 verified on the device), `check-clean.py` after every run (all six runs passed: no game process, mean host load far below the 4.5 limit).

Order (one cold boot, 22:45 to 23:18): **B1a, B2a, B1b, control 30 FPS, B2b, B1c** (three B1 runs as baselines.md section 7 asks, two B2 runs, so the B2 result is paired with B1 on both sides).

### Producing window (W003-S, 60 s capture after 30 s warm-up; percentiles are SurfaceFlinger bucket lower bounds)

| # | APK | Avg FPS | Frames / dropped | p50 / p95 / p99 ms | Intervals >= 50 ms | Guest CPU % of a core | Fresh-run windows FPS (before / after) |
|---|---|---|---|---|---|---|---|
| 1 | B1 | 61.33 | 3571 / 3 | 16 / 18 / 20 | 0 | 34.1 | 61.45 / 61.42 |
| 2 | **B2** | **61.45** | 3577 / 2 | 16 / 17 / 19 | 0 | 36.7 | 61.54 / 61.51 |
| 3 | B1 | 61.30 | 3569 / 9 | 16 / 19 / 20 | 0 | 35.1 | 61.49 / 61.41 |
| 4 | control 30 FPS | 30.33 | 1795 / 0 | 33 / 36 / 38 | 11 | 21.6 | 30.26 / 30.37 |
| 5 | **B2** | **61.29** | 3569 / 6 | 16 / 18 / 20 | 0 | 36.0 | 61.41 / 61.38 |
| 6 | B1 | 60.91 | 3548 / 10 | 16 / 19 / 33 | 0 | 36.5 | 60.94 / 60.74 |

Guest CPU: the same sampler as B1 (`sample-device.ps1`, `/proc/<pid>/stat` ticks every 5 s, window start sample as reference). The three fresh-run windows of each APK agree with the producing window within 2 to 5 CPU points (producing is the higher one).

| Summary | B1 (n=3) | B2 (n=2) | Difference |
|---|---|---|---|
| Producing average FPS | 61.18 (60.91 to 61.33) | 61.37 (61.29 to 61.45) | +0.19 FPS, inside the noise |
| p95 bucket | 18 to 19 ms | 17 to 18 ms | none above the 16.7 to 20 ms bucket |
| Intervals >= 50 ms | 0 | 0 | none |
| Guest CPU | 35.2 % of a core | 36.4 % of a core | **+1.1 points (+3.2 % relative)** |
| Fresh-run FPS (6 windows each) | 60.74 to 61.49 | 61.38 to 61.54 | none |

### Memory (outside any timed window, `pss-probe.ps1`, one `dumpsys meminfo` 45 s after the reconcile, W003-S, same process)

| | B1 | B2 | Difference |
|---|---|---|---|
| Total PSS | 508,123 KiB | 534,031 KiB | +25,908 KiB (+5.1 %) |
| Native Heap | 38,308 | 43,224 | +4,916 |
| Code | 110,564 | 95,636 | -14,928 |
| Private Other | 300,904 | 340,808 | +39,904 |
| "Unknown" (where the emulator accounts GL memory) | 154,206 | 173,188 | +18,982 |
| Graphics | 0 | 0 | not separately measurable on this emulator |
| Reconcile | 293 ms | 290 ms | none |

The B1 PSS of baselines.md section 4 (493,587 KiB) was one earlier sample; this session's B1 sample is 508,123 KiB, so the PSS noise is itself about 3 %. R12 names no PSS budget; the +5 % is recorded, not frozen.

### Validity and noise band

- **Control: valid.** 30.26 to 30.37 FPS in all three windows (T065: 30.31 to 30.36); the 11 intervals >= 50 ms are the control's normal pacing (B1 session 3 had 34 to 69).
- **B1 agreement: met.** Three B1 producing windows 60.91, 61.30, 61.33 (spread 0.42 FPS) against the +/- 5 FPS band recorded in baselines.md section 4. The recorded B1 failure of session 3 (58.05 and 48.18 FPS under host load) did not recur; the host was quiet (below).
- **Noise band for the regression judgement: B1 spread 0.42 FPS on the average and 1 bucket (1 ms) on p95; CPU spread of the B1 runs 34.1 to 36.5 (2.4 points); PSS spread about 3 %.** B1 drifts slightly downwards over the session (61.33, 61.30, 60.91) while the host mean load rises (1.59, 1.68, 1.92 busy threads); the last B1 run is the only one with p99 33 ms. The ABA bracket (B1 before and after B2) therefore holds, and the B2 result is judged against the mean of all three.
- **Session verdict: valid.** The conclusion "B2 shows no FPS regression against B1 on the emulator" is supported down to about 0.5 FPS; a CPU rise of 1 to 2 points is within the CPU noise and cannot be told apart from the B1 spread.

### PF-9 host load

Host busy threads (all-process CPU seconds per second, 8 logical CPUs, via `sample-device.ps1`, includes the emulator's own 1.0 to 1.5 threads): window means 1.59 (B1a), 1.67 (B2a), 1.68 (B1b), 1.20 (control), 1.56 (B2b), 1.92 (B1c); window maxima 2.79, 4.23, 3.32, 3.22, 2.85, 3.88 (single sample peaks; the 4.23 is one sample during B2a). 10 s load samples before and after runs 0.32 to 1.83. Before the boot: 0.32 and 0.47. No game process in any window (`games_max=0`). Still running in the background: Chrome, ChatGPT, Discord, iCUE, JBL Quantum, vmmemWSL (idle), idle MCP servers, Claude processes. The owner had confirmed a quiet host at about 22:25. Net: the host was quiet (about 0.3 to 0.6 threads outside the emulator), as in T124 (61.1 to 61.6), not loaded as in B1 session 3.

## 3. Build-report metrics, B2 against B1 (clean builds only)

| Metric | B1 | B2 | Difference |
|---|---|---|---|
| APK size (clean builds) | 37,345,615 B | 46,330,863 B | **+8,985,248 B (+8.99 MB, +8.57 MiB)** |
| Sum of compressed entries (`unzip -v`) | 37,268,054 B | 46,252,640 B | +8,984,586 B |
| Uncompressed entries | 111,244,877 B (416) | 141,042,486 B (420) | +29.8 MB |
| Shader variants after stripping | 693 (104 snippets, **52** shaders) | **490** (235 snippets, 95 shaders) | -203 variants; +131 snippets, +43 shaders (URP/Core hidden shaders, mostly 1 to 2 variants) |
| Texture2D in the build (`packedAssets`) | 101 entries, 4,556,816 B | 138 entries, 4,859,912 B | +303,096 B (URP SMAA `AreaTex` 268,920 B, `DebugFont` 16,508 B and small ones) |
| Atlases | none (0 `SpriteAtlas`, 100 Sprites) | none (same 100 Sprites, 45,900 B) | none |
| Build-report role "so" | 3 files, 82.3 MB | 4 files, 108.2 MB | +25.8 MB |

The largest consumers of the 52 shaders in B1 and 95 in B2 are the built-in and URP engine shaders; game-relevant shaders: B1 `Sprites/Default` 12 variants and the UI Toolkit shaders; B2 `Hidden/Internal-UIRDefault` 40 (B1 120). 490 is the same figure T411 recorded; 693 (B1) was the upper bound noted in #522 QA, so the variant count **fell** in the URP build. T411's unstripped run lists 2,262 variants, so stripping is active and the count is not an artifact.

### APK growth levers (OPS-5, examined, nothing changed)

Entry deltas (compressed): `libil2cpp.so` +5.37 MB (46.9 to 67.0 MB uncompressed), `global-metadata.dat` +1.64 MB (8.2 to 12.6 MB), `libunity.so` +1.56 MB, `globalgamemanagers.assets.split2/3/0/1` about +0.26 MB, `lib_burst_generated.so` +0.14 MB (new, 0.49 MB), everything else under 0.02 MB. So 95 % of the +9.0 MB is **IL2CPP-compiled managed code and its metadata from the URP and Core RP packages** (more assemblies in the player: ManagedLibrary 5 to 16 files, +3.1 MB; DependentManagedLibrary +0.02 MB), not textures or Burst. The "so" role growth in the build report (+25.8 MB uncompressed) is mostly that libil2cpp growth (+20.1 MB uncompressed) plus Burst and libunity. Levers, in order of expected gain: (1) managed stripping level (the project leaves `managedStrippingLevel` at the default per platform; a higher level may remove unused URP/Core code, risk: reflection and serialization use in URP, needs a build plus the full suite); (2) removing unused URP features and renderers from the build (SMAA `AreaTex` 0.27 MB compressed, 3 compute shaders 67 KB, post-processing and lens-flare shaders, 64 FinalPost variants); (3) Burst disabled for the player (saves at most 0.14 MB). The budget is met with 1.0 MB margin, so no experiment was run (the review item says experiments only on a miss).

### Phase-0 review section 6 follow-ups

- **OPS-1**: commit and Library state: section 1.
- **OPS-2 / A8**: URP global settings diff after builds: section 1; `ShaderGraphSettings.asset` was not separately diffed (the parent archived diffs for the two files above only), so that part of OPS-3 stays open.
- **OPS-4 / A7 / F3**: Play Mode suite duration, B1 against URP: **not measured**. No B1 Play Mode duration exists (T410 ran no full Play Mode suite), and a fair comparison needs two full Play Mode runs of 25 to 30 min each on a quiet host plus the editor lease, which T412 did not request (a Unity run was allowed only for an explicitly needed measurement). The 25 to 30 min QA figure remains unexplained; suggested owner: T413 follow-up or B3, one `scripts/test-unity.ps1 -TimeoutSeconds 3000` run on each of `ac813fd` and `b9221ca` with the compile time logged.
- **OPS-5**: levers: above.
- **OPS-7 / R01 rollback dry-run**: **not repeated**. Repeating it needs a Unity open, compile and a Play Mode run on a scratch branch (editor lease, 10+ min) beyond read-only git, and the T411 record already shows `RenderParityTests` 18/18 against the Built-in baselines after reverting `a3ca464`. Still not asserted: the sprite packer mode check (`EditorSettings m_SpritePackerMode`) and a revert of the final merge (both commits). Read-only check done: `git revert -m 1 b9221ca` is the procedure in the phase-0 record; the merge exists on `main` and has exactly two parents.

## 4. Frozen budgets

"Freeze" means the number below is the acceptance threshold for B3 (T444) and later. A miss is reported with the R12 options (atlas and batching fixes, fewer cue draws, removing Sorting Groups, lower animation fps, fewer overlay elements); any readability reduction needs an owner decision. All emulator numbers are exploratory (FR-027).

| # | Budget (frozen) | Source | B2 result | Status |
|---|---|---|---|---|
| 1 | Emulator **average FPS >= 60** on W003-S producing, 60 s window, profiler-free build, valid ABA session | R12; owner decision 2026-09-29 | B2 61.45 and 61.29 (mean 61.37); B1 mean 61.18 | **Met** |
| 2 | **p95 present interval: no histogram bucket above the 16.7 to 20 ms bucket**, i.e. the p95 lower bound is at most 19 ms | R12 | B2 17 and 18 ms (B1 18 to 19 ms) | **Met** |
| 3 | **0 intervals >= 50 ms** per 60 s window | R12 | 0 in both B2 windows (and in all B1 and fresh windows) | **Met** |
| 4 | **Windows host frame ms p95 unchanged from W003**: not above the T122 range 0.02 to 0.04 ms (SC-008 ceiling 1 ms) | R12; [W003 player timing baseline](../../../wiki/pages/w003-player-timing-baseline.md) (T122) | Not re-measured (the Core and Application update path is not touched by phase 0; a Windows player run needs a Unity player build and lease time T412 did not use) | **Deferred: measured at B3 (T444)** with the 600-frame Windows-player run |
| 5 | **0 steady-state GC allocations per frame** (warmed Edit and Play Mode tests: clock tick, cue refresh, camera snap during pan and zoom, reduced motion on and off; 600-frame Windows-player run on W004-V) | R12 | Not measurable at B2 (needs `GcAllocationCounter` tests and the W004-V player run). Known baseline is **not zero**: T122 saw 6 `GC.Alloc` samples per 300 frames from `RunShellPresenter.Refresh` (PR-2, one Overdrive status object per second); the advance path is 0 | **Deferred: measured at B3 (T444)**; risk: the budget as written fails today unless PR-2 was fixed since T122 (to be checked at B3) |
| 6 | **World-art texture memory <= 8 MB** (sum of atlas sizes from the build report plus Profiler texture memory in the Windows player) | R12 | Build report: project textures about 1.76 MB compressed (100 textures, B1) plus URP 0.30 MB; the 4.86 MB Texture2D total includes Unity's 2.80 MB splash logo; no atlas. Placeholder art is not the restyled art | **Deferred: measured at B3 (T444)** with the restyled art |
| 7 | **Atlas pages <= 1024 x 1024** | R12 | No `SpriteAtlas` exists at B2 | **Deferred: measured at B3 (T444)** |
| 8 | **Batch count on W004-V recorded and held at B3**: B1 reference SetPass per world-camera render 7.86 (W003-S fit-all), 13.90 (W004-V `n_min` and fit-all), method of baselines.md section 3 (graphical Editor probe, 20 renders per "with" frame) | R12; baselines.md section 3 | SetPass/batches not recorded at B2 (the T411 SRP Batcher changes what SetPass counts, so the comparison must use the same probe on `b9221ca`, which needs a graphical Editor run, not done here) | **Deferred: recorded at B3 (T444)**; freeze rule: B3 batches/SetPass may not exceed the B2-method value of `b9221ca` on the same fixture by more than 10 % |
| 9 | **Phase-0 guest CPU regression <= max(10 %, 2 percentage points)** against the paired B1 | R12 | +1.1 points (+3.2 %); allowed = max(3.5 points, 2) = 3.5 points | **Met** |
| 10 | **APK growth <= +10 MB against the paired B1** (clean builds, `unzip -v` entry sums as cross-check) | R12; baselines.md section 1 | +8,985,248 B (+8.99 MB) | **Met** (1.0 MB margin) |
| 11 | **Shader variants after stripping: no growth without explanation**, recorded B1 693 and phase-0 490 | baselines.md section 2 | 490 (T411: 490) | **Met** (frozen reference 490: any B3 count above 490 needs a written explanation) |

B2 therefore meets all five budgets it can measure (1, 2, 3, 9, 10) plus the variant-count rule (11), and defers four budgets (4 to 8) to B3.

## 5. Open items and risks

- Budget 5 has a known non-zero baseline (PR-2, T122): decide at B3 whether `RunShellPresenter.Refresh` is fixed first or the budget is restated by the owner.
- Budget 4 and 8 require a Windows player and a graphical Editor run at B3; both need the `unity` and `gpu` leases, and the batch-count probe has to run on `b9221ca` first so that B3 compares like with like (URP SRP Batcher).
- Emulator CPU and PSS are guest numbers; the host GPU makes frame time cheap, so a physical device may show a different outcome (FR-027). PSS grew 5 % (+25.9 MB) with URP: no budget, but a physical low-end device could be tight.
- Unity re-serialized the URP global-settings asset during the builds (section 1); keep diffing it after Android builds.
- The Play Mode duration comparison (OPS-4) and the rollback dry-run repeat (OPS-7) remain open (section 3).
- B1 sampled 3 runs and B2 2 runs in one session; a second cold-booted session would tighten the B2 numbers but is not needed for the verdicts above, because every margin is larger than the observed spread.

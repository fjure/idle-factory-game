# T040 Android ARM64-on-emulator performance evidence

**Status: COMPLETE for the T040 exploratory emulator profile.** Two comparable three-run sets were recorded from the same fixed checkpoint, APK and AVD. The baseline set (runs 1–3) covers CPU, memory, load, rendering buffers and package cost. The frame-pacing set (runs F1–F3) adds per-frame present timing through SurfaceFlinger. A single Unity Profiler probe adds Unity-side allocation, memory and render counters, but it is not a comparable set. This is emulator-only development evidence through ARM64 translation. It is not physical-device qualification, release evidence, an iOS result, or a mobile performance PASS. No mobile threshold is specified, so no threshold is inferred and the Windows W001 budgets are not applied.

Baseline runs 1–3 were observed on 2026-09-27 (Europe/Berlin) from branch `maqa/issue-40-mobile-performance`, based on integrated `main` commit `c6bd3b30fdb6bab0e269054aadf7f07df9c78bb1`, which contains T061 (cold-restore rendering) and T062 (live state badges). Frame-pacing runs F1–F3 and the profiler probe were observed on 2026-09-28 between 01:05 and 01:32 (Europe/Berlin), with the same installed APK (SHA-256 verified on the device before the runs) and the same fixed checkpoint.

## Target and build identity

| Item | Recorded value |
|---|---|
| AVD | `Medium_Phone_API_36.0`; Android 16 / API 36; fingerprint `google/sdk_gphone64_x86_64/emu64xa:16/BP22.250325.006/13344233:user/release-keys`; Android Emulator `35.5.10.0` build `13402964` |
| Guest ABI and translation | `ro.product.cpu.abilist=x86_64,arm64-v8a`; `ro.dalvik.vm.native.bridge=libndk_translation.so`; installed package `primaryCpuAbi=arm64-v8a` |
| Display and host renderer | `1080x2400`; emulator `gfxstream`; SurfaceFlinger/Unity renderer `Android Emulator OpenGL ES Translator (NVIDIA GeForce RTX 5070 Ti/PCIe/SSE2)`, OpenGL ES 3.1 (`4.5.0 NVIDIA 591.86`) |
| Unity | `6000.6.0f1 (f7f8ed4d1e24)`; one authored scene, `Assets/IdleFactory/Scenes/CoreGameFoundation.unity` |
| Player | Package `com.DefaultCompany.idlefactorygame`, Development Build, Autoconnect Profiler, IL2CPP, ARM64, stripping enabled, min API 26, target/compile API 36 |
| Graphics settings | Repository settings were automatic before and after the build. `BuildAndroidArm64DevelopmentGles3` temporarily selected GLES3 because Vulkan crashes through this AVD's ARM64 translation path. Runtime log confirms an OpenGL ES 3.1 device and `1080x2400` framebuffer. Quality uses its repository default, fixed DPI factor `1`, scaling mode `0`, and VSync `1` at the selected default quality. |
| APK | `CoreGameFoundation-t040-gles3.apk`; 36,066,804 bytes; SHA-256 `004b217a51ede39f94aba823e10561d371324cae957220f42ce56cdea514e50e`; AAPT reports native code only `arm64-v8a` |
| Build result | `Succeeded`, 0 errors, 0 warnings, 00:07:42.067. The build log confirms automatic Android Graphics APIs before and after the temporary override. |

The first sandboxed build attempts could not start Unity Package Manager because its database was read-only. The retained successful build was the single unsandboxed retry; it produced the APK above. Unity's known empty-icon and explicit-automatic-graphics serialization was restored before evidence was committed.

## Fixed checkpoint and comparable workload

Before changing device state, the five files in the existing development-emulator checkpoint directory were copied to ignored raw evidence. The run script force-stopped the app, restored those exact five files, cold-launched the installed APK, waited 30 seconds with no input, recorded the promoted checkpoint and factory state, reset the available counters, and captured the next 60 seconds. That sequence ran exactly three times. After run 3, the script restored the original five files; a subsequent pull verified all five SHA-256 hashes match their pre-run copies.

The fixed source checkpoint is generation `9`, checkpoint `43b3aaec-ad1d-4fbb-888a-80e88e9c0a1a`. Its stable factory state is:

- factory origin `(0,0)`, size `16x16`;
- one `dev.machine.basic`, instance `460df3d7-f9a7-491a-bbe2-f016bab2275f`, anchor `(0,0)`, orientation `0`;
- active, recipe `dev.recipe.basic`, progress `0` ticks;
- input `dev.input=2`; output `dev.output=1`, equal to the D001 output capacity, so it is output-blocked;
- no player input during warm-up or capture.

Each launch promoted the restored source to a new generation-10 checkpoint, so the checkpoint IDs and departure timestamps correctly differ. The factory-state SHA-256 intentionally excludes checkpoint metadata. It matches in all three runs, and every capture-start/end screenshot is byte-identical (`82f943ebcf9d6e1e2cb44b5de5d2c7e8738adab394841bc47877a568069be407`). Comparisons are therefore permitted.

| Run | Capture-start checkpoint | Reconciled from | Factory-state SHA-256 | Capture |
|---:|---|---|---|---:|
| 1 | `a8f6e81a-02ef-4789-9e5d-c04370d5a42b`, generation 10 | fixed source `43b3aaec-...` | `d989757b052c2f44aef9c1c6c11ce10c57dc6d945eaff175564ec7a1bf561bda` | 60.350 s |
| 2 | `bf1c822e-1edb-4497-9cea-2d78a424ad97`, generation 10 | fixed source `43b3aaec-...` | same | 60.388 s |
| 3 | `7f33c733-2e85-4622-926b-20f2da32c01a`, generation 10 | fixed source `43b3aaec-...` | same | 60.398 s |

The frame-pacing runs F1–F3 reached the same capture-start state: generation-10 checkpoints `6b4b19c1-…`, `cd47f257-…` and `4db16aff-…`, each reconciled from `43b3aaec-…`, with the same factory-state SHA-256 `d989757b…1bda`, and start/end screenshots byte-identical to runs 1–3 (`82f943eb…be407`). See [`t040-evidence/framepacing-state-records.txt`](t040-evidence/framepacing-state-records.txt).

**Workload character.** The fixed D001 checkpoint is output-blocked, and any D001 start state blocks within one 5 s cycle (input capacity 4, recipe 2 → 1, output capacity 1), so every 30 s warm-up ends blocked. While no machine can produce, `MachineInspectorPresenter` disables `FactoryRuntimeHost`. No simulation advance and no revision change happen during the capture windows, and T062's per-frame badge sync returns at its unchanged-revision check. These captures therefore profile an idle, fully rendered D001 scene: the steady presentation, HUD and polling cost. They do not measure production ticks.

[`t040-evidence/capture-start-identical.png`](t040-evidence/capture-start-identical.png) shows the no-input capture state: the one restored machine is visible with its non-color `△ OUT!` badge and the HUD reports a saved factory.

## Measurements

### CPU and process load

`adb shell top -b -d 5 -n 13 -p <pid>` supplied 13 process samples across each capture. Percentages are Android guest process CPU where 100% is one guest core; they include the ARM64 translation and emulator environment.

| Run | Mean | p50 | p95 | Min–max |
|---:|---:|---:|---:|---:|
| 1 | 49.24% | 46.2% | 65.3% | 40.4–65.3% |
| 2 | 46.62% | 45.2% | 65.3% | 40.4–65.3% |
| 3 | 47.23% | 43.6% | 81.4% | 38.0–81.4% |

Run 3's first sample is the 81.4% maximum; the remaining samples range from 38.0% to 59.4%. These values describe this AVD and host only.

**Correction to the first sample.** Toybox `top` computes its first sample over a short interval at the start of the window, not over 5 s and not since process start. A check on this AVD showed `surfaceflinger` at 4.0% in the first sample against a lifetime average of 8.2%. That short interval directly follows the start-boundary `dumpsys meminfo`, which triggers the explicit ART GC described below. The first sample therefore measures the instrumentation, and it is the maximum in all three runs. Without it (12 samples per run):

| Run | Mean | p50 | p95 | Min–max |
|---:|---:|---:|---:|---:|
| 1 | 47.90% | 45.4% | 56.6% | 40.4–56.6% |
| 2 | 45.07% | 44.6% | 52.0% | 40.4–52.0% |
| 3 | 44.38% | 43.6% | 59.4% | 38.0–59.4% |

The frame-pacing runs F1–F3 measured 30.48%, 31.18% and 30.97% mean guest process CPU without the first sample (p95 39.0%, 33.6%, 32.8%), with the same APK, checkpoint and screen state. Their `top` headers show lower guest system load than runs 1–3 (41–53% sys against 57–88% sys, excluding each first sample, with similar swap use of 0.86–0.97 GiB of 1.44 GiB). The host load differed between the two sessions, so the two sets are compared only within themselves, never with each other. The emulator guest has about 1.9 GiB of RAM and runs with heavy swap. Those guest conditions are part of this exploratory baseline and are unlike a physical device.

### Memory

`dumpsys meminfo` was recorded immediately before and after each 60-second window. Values are KiB. Android's PSS includes shared mappings and translation/runtime costs; it is not Unity managed-heap size.

| Run | Total PSS start → end | Total RSS start → end | Native-heap PSS start → end | `.so mmap` PSS start → end |
|---:|---:|---:|---:|---:|
| 1 | 431,212 → 420,921 | 543,672 → 530,044 | 39,708 → 36,476 | 61,160 → 58,379 |
| 2 | 466,633 → 474,385 | 580,660 → 584,356 | 39,664 → 36,496 | 79,198 → 80,236 |
| 3 | 483,663 → 470,330 | 599,828 → 587,544 | 39,680 → 36,652 | 90,852 → 82,507 |

Total PSS changed by -10,291, +7,752, and -13,333 KiB respectively. The three-run range and the large mapped-library variation are retained rather than averaged into a device claim.

### Allocations and garbage collection

A **comparable three-run** Unity managed-allocation measurement is **unavailable** on this host. ADB does not expose Unity's managed-allocation counter, and `dumpsys meminfo` is a heap snapshot, not an allocation rate. Attaching the Unity Profiler was attempted on 2026-09-28. With the editor running on the same host, the player's cold start slowed from "Fully drawn" +14.0–16.5 s to +32.9–47.7 s. With the editor connection open, the player stalled after scene load until the editor closed, so no run reached the fixed capture-start state. Five attempts were discarded for these reasons or because a parallel session was driving the same emulator. [`t040-evidence/profiler-probe-observation.txt`](t040-evidence/profiler-probe-observation.txt) records each attempt.

A **single profiler probe** from the same checkpoint and APK did deliver Unity counters. It is one observation under a profiler that slows the translated player by about 15× (main-thread frames of 493–621 ms), so it is qualitative. Counts and bytes are not time-scaled:

- **GC Allocated In Frame 23,488 B in 370 allocations.** 23,182 B and 352 allocations per frame come from `MachineInspectorPresenter.Update()` alone, with no machine selected and no simulation running. The Input System adds about 307 B (18 allocations) per frame. See finding **T040-F1** below.
- GC Used Memory 8.7 MiB of 12.9 MiB reserved; Unity Total Used Memory 152 MiB of 324 MiB reserved; System Used Memory 511 MiB.

For the Android runtime (ART) garbage collector, the baseline logs show the following. Each run's log contains one app `Explicit concurrent mark compact GC` immediately after the start-boundary `dumpsys meminfo` request and one after the end-boundary request. No app GC line occurs between those boundary events during the `top` capture interval. Because the measurement itself requests these explicit collections, they are retained as instrumentation evidence and are not treated as gameplay GC pauses. No `FATAL EXCEPTION`, `F DEBUG`, `SIGSEGV`, or `E Unity` line occurs in any capture log.

### Frame time, pacing, and rendering

`dumpsys gfxinfo ... framestats` gives no frame data for this player. The baseline protocol requested it in every run, but Unity renders through a `SurfaceView`, and all three dumps contain an empty `Profile data in ms:` section.

Frame pacing is observable through SurfaceFlinger instead. The frame-pacing runs F1–F3 repeat the baseline protocol exactly, adding only `capture-protocol.ps1 -CaptureFrameTimeStats`. That option enables and clears SurfaceFlinger TimeStats immediately before the 60 s window, and dumps and disables them immediately after it. The values below belong to the Unity layer `SurfaceView[…IdleFactoryGameActivity](BLAST)`. `present2present` is the interval between consecutive presented frames, as a SurfaceFlinger histogram in 1 ms buckets up to 34 ms and wider buckets above. Percentiles are bucket lower bounds.

| Run | Frames | Dropped | Janky | Avg FPS | p50 | p95 | p99 | Largest bucket | Frames ≥ 50 ms |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| F1 | 1,767 | 0 | 0 | 29.48 | 33 ms | 50 ms | 54 ms | 150 ms | 155 |
| F2 | 1,789 | 0 | 0 | 30.05 | 33 ms | 36 ms | 50 ms | 66 ms | 38 |
| F3 | 1,784 | 0 | 0 | 29.94 | 33 ms | 36 ms | 54 ms | 54 ms | 48 |

**Concurrent device use.** Afterwards, a parallel Claude session reported that its emulator test script (`scripts/android-emulator-journey.ps1`) ran on the same `emulator-5554` from 22:59 to 23:23 UTC on 2026-09-27. That window contains F1–F3 (23:07:47–23:12:38 UTC). The script ran `install -r`, `pm clear`, `monkey` launches, HOME, POWER, `force-stop` and rotation. The logcat captured for each F-run covers it continuously from launch to the end of capture. It contains only the protocol's own single activity start and routine display-timing messages: no other activity start, no HOME/POWER/wakefulness change, no rotation, no install, no data clear and no force-stop. The installed APK hash and the device checkpoint were verified immediately before F1, the capture-start state hashes and screenshots match, and each run kept one process ID. The device state was therefore undisturbed during F1–F3. Host-side load from the parallel script cannot be excluded, and it is one possible cause of F1's heavier tail. The five profiler attempts that the parallel session did disturb are discarded (see below).

The player paces at about 30 FPS (33 ms), which is half the 60 Hz display rate. The layer votes 60 Hz, and no project code sets `Application.targetFrameRate` or `QualitySettings.vSyncCount`. The 30 FPS cadence therefore matches Unity's default frame-rate cap on Android and is not a display limit. F2 and F3 agree closely. F1 has a heavier tail (155 intervals of 50 ms or more, one in the 150 ms bucket). The histograms do not show whether this comes from the host or the player, and a single run is not generalized. Emulator presentation timing through a host GPU translator is not a device frame-time measurement.

Unity-side CPU and GPU frame times are not reported. The profiler probe measured them only while slowing the player about 15×, so they are not representative.

The rendering evidence that is observable is consistent across all runs:

- runtime created an OpenGL ES 3.1 device and a `1080x2400`, RGBA8, depth-24/stencil-8 framebuffer with no MSAA samples;
- `gfxinfo` reports three 10,125 KiB `1080x2400` ViewRoot buffers, 30,375 KiB estimated total GraphicBufferAllocator allocation, and 30,396 KiB imported gralloc buffers;
- nine attached Android views and 11.32 KiB of Android RenderNodes; Unity scene rendering remains inside the SurfaceView;
- identical start/end screenshots prove the fixed camera/UI/factory state stayed visually unchanged during all windows;
- the single profiler probe reported 3 batches, 7 SetPass calls, 38 draw calls, 2,190 triangles and 5,090 vertices per frame, 231 textures using 25.6 MiB, 20.1 MiB of render textures, 0 meshes, 335 GameObjects and 2,139 objects. These are counts, so they do not depend on the profiler slowdown, but they come from one observation.

### Load behavior

Cold values are `am start -W` after force-stop and exact checkpoint restoration. Hot values bring the still-running activity back from HOME after the capture.

| Run | Cold `TotalTime` | Cold `WaitTime` | Hot `TotalTime` | Hot `WaitTime` |
|---:|---:|---:|---:|---:|
| 1 | 1,443 ms | 1,447 ms | 246 ms | 250 ms |
| 2 | 2,718 ms | 2,720 ms | 137 ms | 139 ms |
| 3 | 1,922 ms | 1,925 ms | 150 ms | 155 ms |

Median cold `TotalTime` is 1,922 ms; median hot `TotalTime` is 150 ms. The cold range is reported as observed and is not converted into a target.

### APK and asset cost

The APK contains 406 ZIP entries, 106,096,329 uncompressed entry bytes and 35,990,937 compressed entry bytes. The installed APK file is 36,066,804 bytes.

| Group | Files | Uncompressed | Compressed |
|---|---:|---:|---:|
| `lib/arm64-v8a/` | 6 | 81,195,664 B | 28,021,380 B |
| `assets/bin/Data/` | 18 | 16,674,357 B | 4,466,600 B |

The largest entries are `libil2cpp.so` (44,029,704 B uncompressed), `libunity.so` (35,385,240 B), and `global-metadata.dat` (7,546,294 B). These are package costs, not resident texture memory. Per-asset runtime residency is **unavailable**, because it needs a Unity Memory Profiler snapshot, which the editor/emulator contention described above prevented. The profiler probe gives only category totals: 231 textures using 25.6 MiB and 20.1 MiB of render textures. The 30,375 KiB graphics-buffer estimate above is retained separately.

## Evidence and reproducibility

- [`t040-evidence/capture-protocol.ps1`](t040-evidence/capture-protocol.ps1) is the exact restorable three-run protocol.
- [`t040-evidence/capture-metrics.csv`](t040-evidence/capture-metrics.csv) records the comparable numeric results.
- [`t040-evidence/state-records.txt`](t040-evidence/state-records.txt) records the full canonical state strings and checkpoint identities.
- [`t040-evidence/environment-and-build.txt`](t040-evidence/environment-and-build.txt) records device/build/package identity and original-profile restoration hashes.
- Full logs, APK, build logs, checkpoint copies, `top`, `meminfo`, `gfxinfo`, logcat, and all six screenshots remain under ignored `test-results/android/t040/` (64 files, about 38.1 MB before the post-run restoration verification).
- [`t040-evidence/framepacing-metrics.csv`](t040-evidence/framepacing-metrics.csv), [`t040-evidence/framepacing-state-records.txt`](t040-evidence/framepacing-state-records.txt) and `t040-evidence/framepacing-run-{1,2,3}-unity-layer-timestats.txt` record runs F1–F3. Their full raw output, including every TimeStats dump, `top`, `meminfo`, logcat and screenshot, is under ignored `test-results/android/t040-framepacing/`. After F3 the original device profile was restored, and a pull confirmed that all five files match their pre-run SHA-256 hashes.
- [`t040-evidence/profiler-probe-observation.txt`](t040-evidence/profiler-probe-observation.txt) records the profiler probe and the discarded profiler attempts. Those attempts are under ignored `test-results/android/t040-profiler/`. Afterwards the original device profile was restored again and verified 5/5, and the port forward was removed.

The configured full suite was run once after documentation: Edit Mode **561/561** and Play Mode **141/141**, 0 skipped, under `test-results/unity/20260927T202407979Z/`. Unity's known empty-icon serialization was removed after the run. The later additions change only this report, its evidence files and the evidence protocol script, not the player or the tests.

## Findings for remediation (outside T040's measurement scope)

- **T040-F1: time-based polling in `MachineInspectorPresenter` allocates heavily in an idle factory and scales quadratically with the machine count.**
  - **Evidence:** in the profiler probe, `MachineInspectorPresenter.Update()` allocated 23,182 B in 352 allocations per profiled frame, with no machine selected and no simulation advancing. That is about 99% of the frame's managed allocations.
  - **Cause (from the code):** `Update()` runs `RefreshRuntimeHostState()` and `CurrentRevision()` every 0.1 s of unscaled time (`InspectionPollIntervalSeconds`). Each read of `CoreGameFoundationBootstrap.Snapshot` builds a complete `FactorySnapshot` from a full state copy, and one poll reads it three times. `RefreshRuntimeHostState()` also calls `GetMachineInspection` for every building, and each call copies the whole factory state again. One poll therefore costs O(N) full copies of an N-building factory, which is O(N²).
  - **Scale:** at about 30 FPS this runs about 10 times per second, roughly 230 KB/s for the single D001 machine (estimated from the per-poll bytes). At W001 scale (1,000 machines) it would be about 1,000 full factory copies per poll. The W001 zero-allocation tests (Core advance, T062 view sync) do not cover this presenter path.
  - **Owner:** Gameplay Developer with Performance Engineer, Software Architect and Test/QA. It needs a change such as a revision-gated or allocation-free readiness query, plus a W001-scale allocation test of the presenter path. Traces to the plan's per-tick allocation goals. T040 records the finding and does not fix it.
- **T040-F2: a comparable Unity Profiler set needs a separate profiling host or a stand-alone profiler.** With the Unity editor and the emulator on the same Windows host, the attached player could not reach the fixed capture-start state (see above). Any later mobile performance work should profile from a second machine, a physical device, or a Unity Profiler stand-alone instance, and must keep other sessions off the emulator for the whole protocol.

## Disposition

T040's required Android ARM64-on-emulator exploratory baseline is recorded with three matching starts. A second set of three matching starts adds SurfaceFlinger frame pacing: about 30 FPS, a 33 ms median, and 0 dropped or janky frames on the Unity layer. CPU, process memory, load, graphics-buffer, package-size and stability evidence are comparable within each set, but not across the two sets, because the host load differed. Unity managed allocations, render counters and memory categories come from a single profiler probe and are qualitative. A comparable Unity-side set, Unity CPU/GPU frame time, and per-asset residency remain unavailable on this single-host setup (T040-F2). The probe exposes a real per-poll allocation hotspot (T040-F1), which needs a remediation task before any scale-sensitive mobile performance claim. No Windows budget, physical Android result, release conclusion or iOS result is asserted; Issue #39 remains the separate iOS work item.

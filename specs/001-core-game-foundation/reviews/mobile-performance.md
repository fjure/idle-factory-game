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

Frame pacing is observable through SurfaceFlinger instead. The frame-pacing runs F1–F3 repeat the baseline protocol exactly, adding only `capture-protocol.ps1 -CaptureFrameTimeStats`. That option enables and clears SurfaceFlinger TimeStats immediately before the 60 s window, and dumps and disables them immediately after it. The values below belong to the Unity layer `SurfaceView[…IdleFactoryGameActivity](BLAST)`. `present2present` is the interval between consecutive presented frames, as a SurfaceFlinger histogram in 1 ms buckets up to 34 ms and wider buckets above. Percentiles are bucket lower bounds. All percentiles in this report use the nearest-rank method: value number ⌈n·p⌉ of the n values sorted ascending, with no interpolation.

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
  - **Cause (from the code):** `Update()` runs `RefreshRuntimeHostState()` and `CurrentRevision()` every 0.1 s of unscaled time (`InspectionPollIntervalSeconds`). Each read of `CoreGameFoundationBootstrap.Snapshot` builds a complete `FactorySnapshot` from a full state copy, and one poll reads it three times. `RefreshRuntimeHostState()` also calls `GetMachineInspection` for every building, and each call copies the whole factory state again. One poll therefore builds three full snapshots plus N full state copies, where N is the number of buildings. `Any` stops only at the first machine that can produce, and in a fully blocked factory it visits all N. Each copy covers the whole N-building factory, so one poll costs O(N²).
  - **Scale:** at about 30 FPS this runs about 10 times per second, roughly 230 KB/s for the single D001 machine (estimated from the per-poll bytes). At W001 scale (1,000 machines) it would be about 1,000 full factory copies per poll. The W001 zero-allocation tests (Core advance, T062 view sync) do not cover this presenter path.
  - **Owner:** Gameplay Developer with Performance Engineer, Software Architect and Test/QA. It needs a change such as a revision-gated or allocation-free readiness query, plus a W001-scale allocation test of the presenter path. Traces to the plan's per-tick allocation goals. T040 records the finding and does not fix it.
  - **Status:** remediated by T063 (Issue #113); see [T063 remediation](#t063-remediation-of-t040-f1-inspector-polling-and-the-producing-path) below.
- **T040-F2: a comparable Unity Profiler set needs a separate profiling host or a stand-alone profiler.** With the Unity editor and the emulator on the same Windows host, the attached player could not reach the fixed capture-start state (see above). Any later mobile performance work should profile from a second machine, a physical device, or a Unity Profiler stand-alone instance, and must keep other sessions off the emulator for the whole protocol.
  - **Status:** addressed by T064 (Issue #114); see [T064](#t064-unity-profiler-standalone-process-set) below. The stall's most likely mechanism is the autoconnect build's own connection to the open editor.

## Disposition

T040's required Android ARM64-on-emulator exploratory baseline is recorded with three matching starts. A second set of three matching starts adds SurfaceFlinger frame pacing: about 30 FPS, a 33 ms median, and 0 dropped or janky frames on the Unity layer. CPU, process memory, load, graphics-buffer, package-size and stability evidence are comparable within each set, but not across the two sets, because the host load differed. Unity managed allocations, render counters and memory categories come from a single profiler probe and are qualitative. A comparable Unity-side set, Unity CPU/GPU frame time, and per-asset residency remain unavailable on this single-host setup (T040-F2). The probe exposes a real per-poll allocation hotspot (T040-F1), which needs a remediation task before any scale-sensitive mobile performance claim. No Windows budget, physical Android result, release conclusion or iOS result is asserted; Issue #39 remains the separate iOS work item.

## T040 independent reviews

| Lens | Verdict | Notes |
|---|---|---|
| Test/QA | **PASS** | Independent review of `e6580ca`, `4c57ded` and `cf5183f`. Every checkable figure was recomputed from the raw evidence and matched exactly: the APK size and SHA-256; CPU with and without the first sample; PSS/RSS/Native Heap; cold and hot `TotalTime`; all ten frame-pacing values per F-run from the Unity BLAST layer histogram; all six state hashes and twelve screenshot hashes; the restoration checks; and the Edit Mode suite (561/561). The T040-F1 code claims hold (`MachineInspectorPresenter.cs` lines 19/68/498/504/523, `CoreGameFoundationBootstrap.cs:99`, `FactorySession` state copies). The protocol switch is additive. The concurrent-device-use paragraph is supported: the combined F-run logcats cover the whole window, with a single own activity start and no foreign device events. Low, resolved: the percentile method is now named (nearest-rank). |
| Software Architect + Performance | **APPROVE** | The methodology is sound for an exploratory emulator baseline: deterministic restore/warm-up/capture, metadata-free state hashing, a correctly reasoned exclusion of the first `top` sample, and no comparison across sets with different host load. SurfaceFlinger TimeStats on the BLAST layer is a valid frame-pacing source, and the bucket lower-bound percentiles and the 30 FPS explanation are stated correctly. The single profiler probe is correctly labelled qualitative, and unavailable metrics are not reported as zero. The idle-workload note is verified against `RefreshRuntimeHostState`; T040 cannot speak to per-tick cost. T040-F1 is verified and, if anything, understated; the constant factor (three snapshots plus N copies per poll) is now stated. T040-F2 is supported by the discarded attempts. `cf5183f` is a transparency addition, and the verdict is unchanged. |
| Game Designer/Product Owner | **ACCEPT WITH FOLLOW-UPS** | Every T040 clause is satisfied as an exploratory, emulator-only baseline, and the report is honest about what is qualitative. Follow-ups: T040-F1 must become a tracked remediation task (Gameplay Developer with Performance, Architect and Test/QA) before any scale-sensitive mobile claim, and T040-F2 a lightweight infrastructure follow-up. T041 must characterize T040 as "exploratory, single emulator host, two non-comparable metric sets, one qualitative profiler probe, one unremediated allocation hotspot (T040-F1) and a profiling-infrastructure gap (T040-F2)", not as a clean mobile performance baseline, and must note that no mobile evidence yet exists for an actively producing factory. |

Omitted lenses: UI/UX Reviewer, Art Director and Technical Art, because nothing player-visible and no asset or import setting changes. Game Systems/Economy, because no production, offline or balancing rule changes. Security/Privacy, because the checkpoint format and trust boundary are unchanged; the evidence copies contain only development-scenario checkpoints. DevOps, because the build path is the existing `BuildAndroidArm64DevelopmentGles3` and the only automation change is the opt-in evidence-protocol switch.

## T063 remediation of T040-F1: inspector polling and the producing path

T063 (Issue #113) removes the per-poll factory copies from `MachineInspectorPresenter` and the per-tick snapshot capture from the runtime host's producing path. All measurements are Unity Editor measurements on the Windows host, in Edit Mode and Play Mode tests. No emulator or device was used, and the numbers are allocation counts, not device timings.

### Measurement method correction (T063-F1)

`GC.GetAllocatedBytesForCurrentThread()` returns **0** under the Unity Editor's Mono runtime, even for `new object[1024]`, while Unity's `GC.Alloc` profiler recorder counts that allocation. Every existing "zero managed allocation" assertion used that counter: W001 core `Advance` (T033), `WarmedPreparedAdvanceAllocatesNoManagedBytes`, T062's `CopyMachinePresentationStates` and T062's view sync. Those assertions could not fail. T063 adds `GcAllocationCounter` to both test assemblies. It is the mechanism behind the Test Framework's `AllocatingGCMemory` constraint, and it counts allocations, not bytes. All four assertions now use it, and the W001 report states allocation counts. Re-measured with the recorder, the claims themselves hold: W001 core `Advance` and the T062 copy allocate **0** times per call. A delegate's first recorded invocation can count one runtime allocation, so every T063 measurement records its delegate once before sampling.

### Baseline before the change (W001 confirmation)

Each figure below is the allocation count per call, recorded before any code change. [`t063-evidence/T063BaselineMeasurementTests.cs.txt`](t063-evidence/T063BaselineMeasurementTests.cs.txt) replays the pre-T063 poll call for call: three `GetFactorySnapshot()` reads, and `GetMachineInspection` for every building until one can produce. The raw output is in [`t063-evidence/baseline-prechange-measurement.txt`](t063-evidence/baseline-prechange-measurement.txt).

| Workload | Poll allocations | Poll time (editor) | `AdvanceSimulation` allocations per tick |
|---|---:|---:|---:|
| D001, 1 machine, producing | 362 | < 0.1 ms | 163 |
| D001, 1 machine, cannot produce | 366 | < 0.1 ms | 172 |
| W001, 1,000 machines, 500 producing | 206,447 | 23–30 ms | 100,657 |
| W001, 1,000 machines, none can produce | **47,253,857** | **5.1–5.3 s** | 105,907 |

The D001 row matches T040's profiler probe (352 allocations per poll in the player). The last row confirms the O(N²) cost: when no machine can produce, one poll copies the factory about 1,000 times. In the W001 shape with producing machines, `Any` stops at the first one. The producing path allocates in proportion to the machine count on every frame, because `FactoryRuntimeHost` called `AdvanceSimulation`, which captures a full `FactorySnapshot`, a change summary and one inspection per machine that the host never reads.

### Change

- `FactorySession.AnyMachineCanProduce()` / `ProductionCommands.AnyMachineCanProduce()`: reads each building's committed active flag, recipe and blocker counts through T062's `ReadPresentationState` without allocating. It is equivalent to the old predicate (active, has a recipe, no blockers), and a test proves it for every blocker case and at W001 scale.
- `ProductionCommands.CurrentRevision`: the revision without a snapshot.
- `FactorySession.GetMachineInspection`: captures only the inspected building (`ProductionSimulationSession.CaptureBuilding`, a binary search over the ID-ordered slots) instead of the whole factory. The result is field-for-field identical for all 1,000 W001 machines.
- `ProductionCommands.AdvanceActiveTime` / `FactorySession.AdvanceProductionInPlace`: the same admission, validation, advance and revision increment as `AdvanceSimulation`, returning only the outcome. `AdvanceSimulation` now delegates to it and keeps its full result for existing callers. `FactoryRuntimeHost` uses `AdvanceActiveTime`, because it reads only `Succeeded`, `Outcome` and `Message`.
- `CommandResult.Success()` returns one shared immutable instance.
- `MachineInspectorPresenter.PollInspection()` (called from `Update` every 0.1 s, as before) gates the host with `AnyMachineCanProduce()`, compares `CurrentRevision` with the last rendered revision, and re-renders only the selected machine and only on a change. An empty selection that is already rendered only records the new revision.

### Result after the change

| Path (W001 = 1,000 machines unless noted) | Allocations |
|---|---:|
| Poll, no selection, unchanged revision | **0** (20/20 samples) |
| Poll, no selection, revision changed | **0** (20/20 samples) |
| Poll, machine selected, unchanged revision | **0** (20/20 samples) |
| Poll, machine selected, revision changed | **89** at 1,000 machines and **89** at 1 machine: the inspector's own strings, independent of machine count |
| `AnyMachineCanProduce()`, some or no machine can produce | **0** |
| `GetMachineInspection` (one machine) | equal at 1 and 1,000 machines |
| Runtime host's per-frame `AdvanceActiveTime` | **0** (50/50 samples); `AdvanceSimulation` as a live control still allocates more than 1,000 |

Behavior is unchanged, and Play Mode tests in the authored scene cover it:
- the host is enabled only while a machine can produce, which the tests check through deactivation, reactivation, a full output buffer and freeing it;
- the selected inspection re-renders on every revision change, both from a direct poll and from the scene's own timed `Update`;
- existing inspector, lifecycle and host tests pass unchanged.

The idle D001 factory from T040 therefore has no per-poll managed allocation. The producing path's remaining per-frame allocation is not measured here in the player; T064's standalone-profiler set is the device-side check.

Tests: [`InspectorPollingQueryTests.cs`](../../../Assets/IdleFactory/Tests/EditMode/InspectorPollingQueryTests.cs) (5, Edit Mode) and [`InspectorPollingTests.cs`](../../../Assets/IdleFactory/Tests/PlayMode/InspectorPollingTests.cs) (4, Play Mode). The configured full suites passed: Edit Mode **566/566**, Play Mode **145/145**, 0 skipped.

### T063 independent reviews

| Lens | Verdict | Notes |
|---|---|---|
| Test/QA | **PASS** | Independent read-only review of `62fb78d`. The W001 baseline confirms the O(N²) cost before the change. The T063-F1 control line shows `GC.GetAllocatedBytesForCurrentThread` = 0 for `new object[1024]`. The four converted assertions are a strict fix, not a weakening. `AnyMachineCanProduce` is traced as equivalent to the old predicate, including buildings without production. `AdvanceProductionInPlace` keeps admission, validation, overflow, mapping and revision semantics. The shared success instance is safe because the type is immutable and no caller uses reference equality. Every document figure matches the result XMLs and the log line `1 machine=89, 1000 machines=89`. The `.meta` files are present, and the settings-noise check is OK. Minor: an empty selection showing the custom "no longer available" message still re-renders on each revision change, by design. Nit: `CaptureBuilding` relies on the ID-ordered slots and uses the same comparer as the sort. |
| Software Architect | **APPROVE** | The new queries live in `Core.Application`/`Core.Simulation`, and the presenter consumes only application contracts. Dependency direction is intact. `CaptureBuilding` reuses the committed buffers, and revision sequencing is unchanged. Low, cosmetic: `CurrentRevision` exists on both `ProductionCommands` and `PlacementCommands`, following the T062 pattern. `AdvanceActiveTime` could be named after the production domain instead. |
| Performance | **APPROVE** | T040-F1's cause is verified and removed. The producing-path fix is a caller swap onto the lean method, which is within scope. No full-copy caller remains on a per-frame path: the remaining `GetFactorySnapshot` callers are event-driven. The results are correctly scoped as editor allocation counts, and device profiling is deferred to T064. |

Omitted lenses: UI/UX Reviewer and Game Designer/Product Owner, because nothing player-visible changes and the inspector text, layout and interaction are identical. Game Systems/Economy, because production rules and revision semantics are unchanged. Art Direction and Technical Art, because no assets change. Security/Privacy, because persistence and the checkpoint format are untouched. DevOps, because the build and automation are unchanged.

## T064: Unity Profiler (Standalone Process) set

**Status: COMPLETE as exploratory, emulator-only evidence with no threshold.** T064 (Issue #114) implements T040-F2. The reproducible procedure is [`docs/integrations/unity-standalone-profiler-android.md`](../../../docs/integrations/unity-standalone-profiler-android.md). With the Unity Profiler (Standalone Process) attached while the scene loaded, the player reached the T040 capture-start state in all three runs, and one comparable Unity-side set was recorded. Every frame time below is measured under the attached profiler and is **distorted**. None is compared with T040 frame pacing or used for T065.

### Why the T040 attempts failed, and the resulting build requirement

A player built with `BuildOptions.ConnectWithProfiler` (T040's "Autoconnect Profiler" build) opens its own profiler connection to the build host at startup. During T064 the emulator guest held that connection to `192.168.2.172:34999`, which belongs to the **main editor**. The main editor and the standalone profiler both listen on `0.0.0.0:34999`, and the main editor must stay open, because the standalone profiler is its child process. With the autoconnect build the standalone profiler therefore received no player frame in four of six attempts. In the two exceptions it had presumably received the player's own connection. Removing the editor's `adb reverse tcp:34999` did not help: the player then connected directly through the emulator NAT. This is also the most likely mechanism of the T040 stall: the player was already connected to the editor when the editor opened a second, direct connection.

T064 therefore adds an opt-in `-noProfilerAutoconnect` switch to `PlatformBuildCommands.BuildAndroidArm64DevelopmentGles3` (`Development` without `ConnectWithProfiler`). The default build is unchanged. Edit Mode tests cover the build options and the exact argument match ([`PlatformBuildProfilerOptionsTests.cs`](../../../Assets/IdleFactory/Tests/EditMode/PlatformBuildProfilerOptionsTests.cs)). The procedure also records three Unity `6000.6` profiler behaviours:

- the profiler window owns the recording state, so `ProfilerDriver.enabled` alone records nothing;
- switching recording off and on inside one editor frame never reaches the player;
- a connection made before the player listens drops at once.

[`t064-evidence/discarded-attempts.txt`](t064-evidence/discarded-attempts.txt) lists all ten discarded attempts and trials.

### Protocol and identity

- **Player:** `CoreGameFoundation-t064-noautoconnect-gles3.apk`, SHA-256 `1feb2b16…0a1aa`, Development (no autoconnect), IL2CPP, ARM64, GLES3 emulator override. It was built from `origin/main` `87e73bb` (T063 included) plus this branch's editor-only build switch. The installed hash was verified before every run, and one unscored launch followed the install. See [`t064-evidence/environment-and-build.txt`](t064-evidence/environment-and-build.txt).
- **Host:** same AVD, emulator and host GPU as T040. One idle main editor ran with its MCP bridge stopped and no Play Mode. The standalone profiler (`role=profiler`) was driven through MCP for Unity `execute_code` with the committed snippets. No other session or `adb` client used `emulator-5554`.
- **Per run** ([`profiler-protocol.ps1`](t064-evidence/profiler-protocol.ps1) `-Phase Run`):
  - restore the five T040 checkpoint files and cold launch with `am start -W`;
  - wait for the player's listening line, then attach the standalone profiler with `adb forward tcp:34999 localabstract:Unity-com.DefaultCompany.idlefactorygame` and a direct connection, with recording off;
  - run the 30 s no-input warm-up, counted as in T040 from the return of `am start -W`, then check that the profiler still holds the player;
  - pull the checkpoint and record the state hash and screenshot;
  - switch recording on once, wait for the first frame, clear the buffer, and record **60 s**;
  - export per-frame counters and GC.Alloc sources, take a player Memory Profiler snapshot, and read per-asset residency;
  - record the end screenshot, `meminfo` and logcat, then force-stop.

### Capture-start state with the profiler attached

| Run | Profiler attached | "Fully drawn" | Capture-start checkpoint | Factory-state SHA-256 | Start = end screenshot | PID through run |
|---:|---|---:|---|---|---|---|
| 1 | 2.2 s after `am start -W` returned (≈ 8.7 s after launch) | +13.358 s | `f66b2af8-…`, generation 10, from `43b3aaec-…` | `d989757b…1bda` | `82f943eb…be407` | 7565 |
| 2 | 2.0 s (≈ 9.3 s) | +13.216 s | `bdb9d34c-…`, generation 10, from `43b3aaec-…` | same | same | 7774 |
| 3 | 2.3 s (≈ 8.6 s) | +12.612 s | `f6bce017-…`, generation 10, from `43b3aaec-…` | same | same | 8080 |

All three match T040's factory-state hash and screenshot, so the set is comparable within itself. The profiler was attached before "Fully drawn" in every run and still held the player at capture start. Without an attached profiler, T040 recorded "Fully drawn" at +14.0–16.5 s with the editor closed; with the in-editor profiler it recorded +33–48 s. T064's +12.6–13.4 s shows no slowdown, but T064 ran in another session with another host load, so it is not a controlled comparison. With the editor and the standalone profiler open, cold `am start -W` `TotalTime` was 6,295–7,322 ms, against 1,250 ms for the unscored launch with the editor closed. Each run's logcat has one own activity start, no `FATAL EXCEPTION`, `F DEBUG` or `SIGSEGV`, and one startup `E Unity` line (`AssetPackManager` `ClassNotFoundException`), which T040's pre-capture logs also contain. Full records: [`t064-evidence/state-records.txt`](t064-evidence/state-records.txt).

### Unity-side set (60 s window per run)

| Metric | Run 1 | Run 2 | Run 3 |
|---|---:|---:|---:|
| Profiled frames in the window (distorted) | 296 | 189 | 278 |
| GC allocated per frame, mean / p50 / p95 / max (B) | 208.1 / 204 / 238 / 306 | 287.7 / 306 / 323 / 347 | 218.1 / 221 / 255 / 323 |
| GC allocations per frame, mean / p50 / p95 / max | 12.23 / 12 / 14 / 18 | 16.90 / 18 / 19 / 20 | 12.82 / 13 / 15 / 19 |
| GC allocated per window (B) / allocations per window | 61,586 / 3,619 | 54,378 / 3,195 | 60,634 / 3,563 |
| GC used / reserved (B, last frame) | 8,740,864 / 10,080,256 | 8,720,384 / 10,088,448 | 8,716,288 / 10,145,792 |
| Unity total used / reserved (B, last frame) | 153,258,282 / 336,690,010 | 153,237,194 / 336,698,202 | 153,241,286 / 336,755,546 |
| System used (B, last frame) | 606,691,328 | 618,217,472 | 618,991,616 |
| Texture memory (B) / textures | 27,325,911 / 237 | same | same |
| Render-texture memory (B) / count | 21,473,280 / 2–11 | same | same |
| Batches / SetPass calls / draw calls | 3 / 7–8 / 38 | same | same |
| Triangles / vertices | 2,192–2,194 / 5,094–5,098 | same | same |
| GameObjects / objects | 335 / 2,144 | same | same |
| Distorted main-thread frame time p50 / p95 (ms); profiling context only, no baseline | 198.18 / 234.97 | 334.27 / 400.22 | 211.05 / 257.60 |
| Distorted GPU frame time p50 (ms); profiling context only, no baseline | 32.55 | 37.11 | 33.26 |

Per-frame values use the nearest-rank method of the T040 report. Machine-readable rows: [`t064-evidence/profiler-metrics.csv`](t064-evidence/profiler-metrics.csv). The `.data` captures, per-frame CSVs and `.snap` files are under ignored `test-results/android/t064/set/`.

**Managed allocations.** No allocation comes from game code: `MachineInspectorPresenter.Update()` no longer appears, against 23,182 B in 352 allocations per frame in the T040 probe. That is T063's remediation of T040-F1 confirmed in the player. Every remaining allocation comes from Unity packages ([`t064-evidence/gc-alloc-sources.txt`](t064-evidence/gc-alloc-sources.txt)):

- `NativeInputSystem.NotifyBeforeUpdate` allocates 17 B per call. It runs once per `FixedUpdate` (2,808–3,018 calls in the window, about 47–50 per second) and twice per frame (`PreUpdate` and `BeforeRender`).
- `UIElementsRuntimeUtilityNative.RepaintPanels` allocates 216 B in 9 allocations per window.

Because the fixed-step share runs per second and not per frame, the per-frame values grow as the profiler slows the frame. That is why run 2, the slowest, has the highest per-frame value. The per-window totals of 54–62 KB are stable. The Input System allocation belongs to the package and is recorded, not remediated.

**Frame time.** The attached profiler slowed the idle player to 3.2–4.9 recorded frames per second, against about 30 FPS unprofiled in T040, and the slowdown differs between runs. The main-thread times above therefore describe the profiling session, not the game. Profiler overhead is smaller than in the T040 probe (493–621 ms), but it is still substantial.

### Per-asset residency (Memory Profiler snapshot)

A player snapshot was taken in every run. It was read with the editor's built-in `PackedMemorySnapshot`, without the Memory Profiler package ([`t064-evidence/memory-residency.txt`](t064-evidence/memory-residency.txt)). All three runs report 2,144 native objects and identical per-type totals:

| Type | Bytes | Objects | Note |
|---|---:|---:|---|
| RenderTexture | 21,484,016 | 11 | **All profiler instrumentation**: `ProfilerScreenshotFull` 20,736,976 B plus ten `ProfilerScreenshotFinal*` of 74,704 B. The game has no resident render texture. |
| Texture2D | 5,312,479 | 220 | 21 project textures (`f001-*`) 522,419 B; the largest are the D001 ghosts at 66,224 B each. Two unnamed 2,097,944 B textures, 179 unnamed textures in total (4,336,438 B). Font Texture and UIR Shader Info 131,864 B each. |
| Shader | 1,347,339 | 17 | `Hidden/Internal-UIRDefault` 743,051 B |
| Cubemap | 526,792 | 3 | `Default-Skybox-Cubemap` 525,048 B |
| Material | 492,504 | 190 | |
| SpriteRenderer | 453,792 | 326 | |
| TextAsset | 382,281 | 1 | `icudt73l` (ICU data) |

This corrects the T040 probe's reading: its 20.1 MiB of render textures were the profiler's screenshot buffers, not game memory. The Unity counter "Texture Memory" (27,325,911 B) includes render textures. Minus the counter's 21,473,280 B of render textures it leaves 5,852,631 B. The snapshot's Texture2D, Cubemap and array textures total 5,841,895 B, which is 10,736 B less. The counter and snapshot render-texture totals (21,473,280 B and 21,484,016 B) differ by the same 10,736 B in the other direction. So the two sources split the same total differently: the counter is read at the last profiled frame, the snapshot at a later moment. The residual is not analysed further. Without the profiler, therefore, about 5.8 MB of textures are resident.

### Disposition

T040-F2 is resolved for this host. The standalone profiler is the profiling path for the Android emulator player, provided the player is built with `-noProfilerAutoconnect` and the documented procedure is followed. The comparable three-run Unity-side set is recorded. It confirms T063's zero game-code allocation in the idle D001 factory on the device side, and it reattributes the render-texture memory. The frame times remain distorted and are no evidence for T065's frame-rate target. T065 measures frame pacing unprofiled, as T040's F-runs did through SurfaceFlinger. When T065 uses this profiler, it should disable the Screenshots module before reading render-texture memory. The set is exploratory and emulator-only. It covers an idle, output-blocked factory, as in T040, with no threshold, so the producing path's per-frame cost on the device is still unmeasured. Physical-device qualification remains unverified.

The evidence scripts and snippets are committed as run, except for the added null checks on reflected members. Review finding T064-A1 added these checks after the scored set. They only change behaviour on a Unity version without those members.

### T064 independent reviews

| Lens | Verdict | Notes |
|---|---|---|
| Test/QA | **PASS** | Independent read-only review of `5bb7b48`. Every figure recomputed from the raw per-frame CSVs, GC sources, residency files and transcripts: per-frame nearest-rank statistics, per-window totals, last-frame counters, residency totals, the texture reconciliation, and the APK and screenshot SHA-256 values (certutil). All three runs meet the capture-start criteria: generation 10 from `43b3aaec-…`, hash `d989757b…1bda`, one PID per run. Test XMLs show Edit Mode 569/569 and Play Mode 145/145. `check-settings-noise.ps1 -Commit 5bb7b48` is OK, and the `.meta` file is present. Warnings: the "Autoconnected Player" label on a direct connection is explained in the procedure; the table's "same" shorthand is backed by the per-run CSVs. |
| Software Architect | **APPROVE WITH FINDINGS** | Editor-only, additive and opt-in; the default build and every existing call site keep autoconnect. The new helpers are pure and tested in Edit Mode. The deviation from T040's build is justified, documented and does not need a separate human decision: the default is unchanged, and the profiling path is the owner's decision. No snippet mutates assets or settings. Medium **T064-A1**: the reflection on internal profiler members was not version-guarded (fixed: explicit null checks naming 6000.6.0f1). Nit: the split log line. |
| Performance | **APPROVE WITH FINDINGS** | Workload identity with T040 is established by hashes. Attach timing, the one-switch recording and the buffer clearing are evidence-based. The 17 B-per-call FixedUpdate account explains the per-frame versus per-window numbers. The render-texture reattribution and the T063 confirmation are supported. Low: the 10,736 B residual between counter and snapshot was called a match (fixed: stated and explained). Low: the distorted frame-time rows lacked their own caveat (fixed). Take-aways for T065 are recorded above. |
| DevOps/Operations | **APPROVE WITH FINDINGS** | Preconditions, process inventory, hash-verified install and restore, guarded failure paths, the MCP role guard, and documented adb side effects are all reproducible. Medium: the pre-T064 device state was recovered by size and timestamp, not by hash, after the backup command was refused (fixed: stated explicitly in `environment-and-build.txt`, and the procedure now requires a separate, verified backup pull). Low: name the refusing guard (fixed). Low: the manual path lacked the MCP bridge note (fixed). Nit: fixed polling intervals are fine for a supervised procedure. |

Omitted lenses: Game Designer/Product Owner and UI/UX Reviewer, because nothing player-visible changes. Game Systems/Economy, because no production or balancing rule changes. Art Direction and Technical Art, because no asset or import setting changes. Security/Privacy, because no trust boundary or persisted format changes; the evidence contains development checkpoints only.
## T065: 60 FPS frame-rate target

**Status: TARGET REACHED on the Android emulator, emulator-only development evidence.** T065 (Issue #115) implements the owner's 2026-09-28 decision (spec Clarifications) to target an average of 60 FPS. With the explicit target, the Unity layer averaged 61.40–61.50 FPS in three matching-start runs, against 30.31–30.36 FPS in a same-session control at the old implicit 30 FPS cap. This is not physical-device, release, battery or thermal evidence.

### Change

- [`CoreGameFoundationBootstrap.ApplyFrameRateTarget()`](../../../Assets/IdleFactory/Unity/Bootstrap/CoreGameFoundationBootstrap.cs) runs in `Awake` before composition and sets `Application.targetFrameRate = 60` (`TargetFrameRate`). It sits at the Unity bootstrap boundary; no domain, simulation or application code changes.
- **Windows player choice:** the same explicit 60 FPS target, with `QualitySettings.vSyncCount = 0`. VSync takes precedence over `targetFrameRate` on desktop, and the repository's default quality level has VSync `1`, so without this the Windows player would follow the monitor's refresh rate instead of the target. Android ignores `vSyncCount`, so one policy covers both players.
- **Editor:** the VSync change is compiled only into players (`#if !UNITY_EDITOR`). A Play Mode change to `QualitySettings` would alter the Editor's quality level and could be re-serialized into `ProjectSettings/QualitySettings.asset`. The Editor keeps its quality settings; only `targetFrameRate` is set during Play Mode.
- **FR-017 is unchanged.** `FactoryRuntimeHost` converts cumulative active time into Core ticks. Frame count and render frequency do not enter the conversion, which the existing Play Mode test `FactoryRuntimeHostTests.CumulativeBoundaryConversionRetainsSubtickTimeAcrossFrames` covers: ten fractional frame intervals yield exactly six ticks, and the boundary does not drift. A higher frame rate only samples the same clock more often.

Tests: `CoreGameFoundationSceneTests.DevelopmentSceneAppliesSixtyFpsTarget` loads the authored scene with a prior target of 30 and asserts the target 60 and the unchanged Editor VSync. `FrameRateTargetPlayerTests.PlayerAppliesSixtyFpsTargetWithoutVSync` (category `StandalonePlayer`) asserts target 60 and `vSyncCount == 0` in the Windows test player.

### Protocol and identity

The T040 frame-pacing protocol was repeated unchanged: [`capture-protocol.ps1`](t040-evidence/capture-protocol.ps1) `-CaptureFrameTimeStats`, the T040 source checkpoint, 30 s no-input warm-up, 60 s SurfaceFlinger TimeStats window on the Unity layer, nearest-rank percentiles on the histogram's bucket lower bounds.

- **Builds:** both players come from the same base (`main` `ddb7dde`) with `BuildAndroidArm64DevelopmentGles3 -noProfilerAutoconnect`. The control has no frame-rate code; the target has the change above. An entry-by-entry comparison shows that only `libil2cpp.so` differs. Hashes, the verified installs and the note on the Editor-only refinement after the build are in [`t065-evidence/environment-and-build.txt`](t065-evidence/environment-and-build.txt). T040's F-runs used the Autoconnect-Profiler build, so T065 compares only within its own session.
- **Session:** freshly cold-booted emulator; no Unity editor, Unity CLI run, profiler or other `adb` client on the host during the captures; control set 13:49–13:54 UTC, target set 13:54–13:59 UTC on 2026-09-28. The task asked for at least one control run; the protocol recorded three.
- **Capture-start state:** all six runs restored the fixed source, promoted it to a generation-10 checkpoint reconciled from `43b3aaec-…`, kept one PID from launch to the end of capture, and reached factory-state SHA-256 `d989757b…1bda`. Every start and end screenshot is byte-identical to T040's (`82f943eb…be407`). Each capture log has one own activity start, no `FATAL EXCEPTION`, `F DEBUG` or `SIGSEGV`, and only the known startup `E Unity` `AssetPackManager` `ClassNotFoundException`. Per-run records and the full histograms: [`t065-evidence/state-records.txt`](t065-evidence/state-records.txt).

### Frame pacing (Unity layer, 60 s window per run)

| Set | Run | Frames | Dropped | Janky | Avg FPS | p50 | p95 | p99 | Largest bucket | Intervals ≥ 50 ms |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Control (30) | 1 | 1,805 | 0 | 0 | 30.36 | 33 ms | 36 ms | 36 ms | 54 ms | 11 |
| Control (30) | 2 | 1,802 | 0 | 0 | 30.31 | 33 ms | 36 ms | 44 ms | 54 ms | 16 |
| Control (30) | 3 | 1,803 | 0 | 0 | 30.32 | 33 ms | 34 ms | 36 ms | 54 ms | 14 |
| **Target (60)** | 1 | 3,596 | 4 | 0 | **61.48** | 16 ms | 18 ms | 19 ms | 36 ms | 0 |
| **Target (60)** | 2 | 3,592 | 5 | 0 | **61.40** | 16 ms | 17 ms | 19 ms | 36 ms | 0 |
| **Target (60)** | 3 | 3,597 | 2 | 0 | **61.50** | 16 ms | 18 ms | 18 ms | 36 ms | 0 |

The control reproduces T040's cadence (F1–F3: 29.48–30.05 FPS, p50 33 ms) and confirms that the old 30 FPS pacing was Unity's implicit Android cap. With the target, every run averages above 60 FPS, and the present interval distribution moves to 16–19 ms with no interval of 50 ms or more. SurfaceFlinger reports slightly more than 60 FPS. The cause, presumably the emulator's virtual display timing, was not investigated, and the values are reported as measured. SurfaceFlinger's `droppedFrames` counter recorded 2–5 of about 3,600 frames per run with the target and none in the control; no frame was classified as janky.

### CPU and memory against the control

| Metric | Control runs 1 / 2 / 3 | Target runs 1 / 2 / 3 |
|---|---|---|
| Guest process CPU, mean without the first `top` sample | 19.27% / 19.48% / 18.15% | 31.22% / 31.63% / 31.52% |
| Guest process CPU, p95 without the first sample | 20.2% / 20.6% / 19.4% | 31.8% / 32.0% / 32.2% |
| Total PSS start → end (KiB) | 433,132 → 401,447 / 464,485 → 467,575 / 404,468 → 386,707 | 416,041 → 401,458 / 483,299 → 472,293 / 480,742 → 462,515 |

Doubling the frame rate raised the player's guest CPU from a mean of 18.97% to 31.46%, i.e. +12.5 percentage points or about +66%, measured on one emulator core through ARM64 translation. Total PSS ranges overlap between the sets, so no memory change was detected within this sample of three runs per set; this is a weak null result, not proof that the target has no memory cost. Cold `am start -W` (1,081–1,218 ms) and "Fully drawn" (+2.33–2.51 s) did not change between the sets; they are much faster than T040's values because this session had a freshly booted emulator and no editor, and they are not compared with T040.

### Discarded first session

A first control and target set and one standalone-profiler run were recorded earlier the same day on an emulator that had run for hours, with up to three Unity editor processes on the host. Both sets measured 3.7–5.1 FPS with 200–250 ms median intervals, control and target alike, so they measured a degraded emulator and host and not the player. The emulator then stopped answering `adb` and was cold-booted with the owner's approval. The values are kept in [`t065-evidence/discarded-session1-metrics.csv`](t065-evidence/discarded-session1-metrics.csv) and are not used. Because the scored set reached the target, no profiler-based analysis of a frame-rate limit was needed.

### Disposition

The explicit 60 FPS development target is implemented and reached on the declared Android emulator. The cost is higher CPU use per second of play. The following remain **unverified**: battery drain and thermal behaviour on physical devices, physical-device frame pacing, the Windows player's achieved frame rate on a real display, and an actively producing factory (the checkpoint remains the idle, output-blocked D001 workload of T040). The evidence is exploratory, emulator-only, from one host and one session, with no threshold beyond the owner's average-60 FPS target. Carried forward for later work: the Windows player's VSync-off posture can tear on displays above 60 Hz and offers no way to re-enable VSync, which is acceptable for this development build but must be revisited before any non-development Windows build or a graphics settings screen; `ApplyFrameRateTarget()` should remain the single writer of the frame-rate and VSync settings.

### T065 independent reviews

| Lens | Verdict | Notes |
|---|---|---|
| Test/QA | **PASS** | Independent read-only review of `9583ad9`. Every frame-pacing, CPU and PSS figure recomputed from the raw SurfaceFlinger, `top` and `meminfo` files; the lineage, the factory-state hash, all twelve screenshot hashes and all APK hashes and sizes verified. Test XMLs show Edit Mode 569/569, Play Mode 147/147 (including both new tests) and the Windows player suite 2/2. `check-settings-noise.ps1 -Commit 9583ad9` is OK and the new `.meta` is present. The APK/commit note is an honest disclosure: an Editor-only conditional cannot change the player. |
| Software Architect | **APPROVE WITH FINDINGS** | The policy sits at the Unity bootstrap boundary with no domain or application dependency; FR-017 holds because production follows cumulative active time. Medium: VSync is off in every player, with no way to re-enable it on Windows; acceptable for the development bootstrap, recorded above for later settings work. Low: keep `ApplyFrameRateTarget()` as the single writer of this global state (recorded above). |
| Performance | **APPROVE WITH FINDINGS** | Workload identity, same-session control, SurfaceFlinger method and percentiles verified against raw data; "target reached" is supported. Low: the memory null result was stated too strongly (fixed: softened). Low: the target APK precedes the Editor-only refinement; accepted because the player path is identical and the disclosure is explicit. |
| UI/UX Reviewer | **APPROVE** | No code in `Assets/` uses `Time.deltaTime`, `Time.unscaledDeltaTime` or `Time.frameCount`, so camera, input and HUD behaviour is not frame-rate coupled; 60 FPS only renders the same state more often. Low: carry the Windows tearing caveat forward before any non-development Windows build (recorded above). |

Omitted lenses: Game Designer/Product Owner, because the owner decided the target and no player journey or acceptance rule changes. Game Systems/Economy, because no production or balancing rule changes. Art Direction and Technical Art, because no asset or import setting changes. Security/Privacy and DevOps/Operations, because no trust boundary, persisted format, build pipeline or runtime service changes.

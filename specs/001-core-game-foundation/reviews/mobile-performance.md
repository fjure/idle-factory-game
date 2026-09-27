# T040 Android ARM64-on-emulator performance evidence

**Status: COMPLETE for the T040 exploratory emulator profile.** Three comparable captures were recorded from the same fixed checkpoint on the named API 36 AVD. This is emulator-only development evidence through ARM64 translation. It is not physical-device qualification, release evidence, an iOS result, or a mobile performance PASS. No mobile threshold is specified, so no threshold is inferred and the Windows W001 budgets are not applied.

Observed 2026-09-27 (Europe/Berlin) from branch `maqa/issue-40-mobile-performance`, based on integrated `main` commit `c6bd3b30fdb6bab0e269054aadf7f07df9c78bb1`.

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

### Memory

`dumpsys meminfo` was recorded immediately before and after each 60-second window. Values are KiB. Android's PSS includes shared mappings and translation/runtime costs; it is not Unity managed-heap size.

| Run | Total PSS start → end | Total RSS start → end | Native-heap PSS start → end | `.so mmap` PSS start → end |
|---:|---:|---:|---:|---:|
| 1 | 431,212 → 420,921 | 543,672 → 530,044 | 39,708 → 36,476 | 61,160 → 58,379 |
| 2 | 466,633 → 474,385 | 580,660 → 584,356 | 39,664 → 36,496 | 79,198 → 80,236 |
| 3 | 483,663 → 470,330 | 599,828 → 587,544 | 39,680 → 36,652 | 90,852 → 82,507 |

Total PSS changed by -10,291, +7,752, and -13,333 KiB respectively. The three-run range and the large mapped-library variation are retained rather than averaged into a device claim.

### Allocations and garbage collection

Managed allocation bytes per frame are **unavailable**. ADB does not expose Unity's managed-allocation counter, and no Unity Profiler editor was attached during these fixed windows. `dumpsys meminfo` is a heap snapshot, not an allocation-rate measurement.

Each run's log contains one app `Explicit concurrent mark compact GC` immediately after the start-boundary `dumpsys meminfo` request and one after the end-boundary request. No app GC line occurs between those boundary events during the `top` capture interval. Because the measurement itself requests these explicit collections, they are retained as instrumentation evidence and are not treated as gameplay GC pauses. No `FATAL EXCEPTION`, `F DEBUG`, `SIGSEGV`, or `E Unity` line occurs in any capture log.

### Frame time, pacing, and rendering

Frame-time and frame-pacing percentiles are **unavailable**. The protocol reset and requested `dumpsys gfxinfo ... framestats` in every run, but Unity renders through a `SurfaceView`; all three dumps contain an empty `Profile data in ms:` section. They therefore provide no per-frame timestamps to summarize. The same limitation prevents reporting Unity CPU/GPU frame time, batches, draw calls, or GPU utilization. No number is inferred from the 60 Hz display mode.

The rendering evidence that is observable is consistent across all runs:

- runtime created an OpenGL ES 3.1 device and a `1080x2400`, RGBA8, depth-24/stencil-8 framebuffer with no MSAA samples;
- `gfxinfo` reports three 10,125 KiB `1080x2400` ViewRoot buffers, 30,375 KiB estimated total GraphicBufferAllocator allocation, and 30,396 KiB imported gralloc buffers;
- nine attached Android views and 11.32 KiB of Android RenderNodes; Unity scene rendering remains inside the SurfaceView;
- identical start/end screenshots prove the fixed camera/UI/factory state stayed visually unchanged during all windows.

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

The largest entries are `libil2cpp.so` (44,029,704 B uncompressed), `libunity.so` (35,385,240 B), and `global-metadata.dat` (7,546,294 B). These are package costs, not resident texture memory. Per-asset runtime residency is **unavailable** without a connected Unity Memory Profiler; the 30,375 KiB graphics-buffer estimate above is retained separately.

## Evidence and reproducibility

- [`t040-evidence/capture-protocol.ps1`](t040-evidence/capture-protocol.ps1) is the exact restorable three-run protocol.
- [`t040-evidence/capture-metrics.csv`](t040-evidence/capture-metrics.csv) records the comparable numeric results.
- [`t040-evidence/state-records.txt`](t040-evidence/state-records.txt) records the full canonical state strings and checkpoint identities.
- [`t040-evidence/environment-and-build.txt`](t040-evidence/environment-and-build.txt) records device/build/package identity and original-profile restoration hashes.
- Full logs, APK, build logs, checkpoint copies, `top`, `meminfo`, `gfxinfo`, logcat, and all six screenshots remain under ignored `test-results/android/t040/` (64 files, about 38.1 MB before the post-run restoration verification).

The configured full suite was run once after documentation: Edit Mode **561/561** and Play Mode **141/141**, 0 skipped, under `test-results/unity/20260927T202407979Z/`. Unity's known empty-icon serialization was removed after the run.

## Disposition

T040's required Android ARM64-on-emulator exploratory baseline is recorded with three matching starts. CPU, process memory, load, graphics-buffer, package-size, and stability evidence are comparable across the captures. Frame/pacing, Unity managed-allocation, detailed render-counter, GPU-time, and per-asset residency metrics are explicitly unavailable with the ADB-only instrumentation that was available. No Windows budget, physical Android result, release conclusion, or iOS result is asserted; Issue #39 remains the separate iOS work item.

# T040 Android and iOS Mobile Performance Evidence

**Status: BLOCKED.** No Android or iOS target device/build was available, so no mobile profiling or mobile baseline was produced. Windows Editor measurements, static estimates, and viewport calculations do not substitute for the required integrated-scene measurements on named devices. No mobile performance threshold is inferred.

## Scope and evidence reviewed

- Authoritative task: GitHub Issue [#40](https://github.com/fjure/idle-factory-game/issues/40), T040 in `specs/001-core-game-foundation/tasks.md`.
- Assigned baseline: `b57ce858ef8cf2d168e9e397bfc10eac6bbe3981`.
- Integrated scene under test: `Assets/IdleFactory/Scenes/CoreGameFoundation.unity` (integrated by T035).
- Unity version pinned by `ProjectSettings/ProjectVersion.txt`: `6000.6.0f1`.
- T038 environment evidence: `specs/001-core-game-foundation/reviews/windows-android-validation.md`, commit `4667e4cae37abaa77c850fd80fda0bc1510019bf`, observed 2026-09-25.
- T039 environment evidence: `specs/001-core-game-foundation/reviews/ios-validation.md`, commit `b780b0be0a8044ca995e77cd28ab8f69d60f4c5e`, observed 2026-09-25.
- T037 asset/rendering evidence: `specs/001-core-game-foundation/reviews/f001-placeholder-final-review.md`, commit `8a20234703fb2396d10f775f26115bfd67f48827`, dated 2026-09-25.
- Existing plain-Core performance evidence: `specs/001-core-game-foundation/reviews/w001-performance.md` (T033; Windows Unity Editor Edit Mode, measured 2026-09-24). Its values are not Android or iOS baselines.

## Target environment evidence

| Target | Evidence recorded by T038/T039 | Disposition |
|---|---|---|
| Android | T038 ran on Windows 11 `10.0.26200` with Unity `6000.6.0f1`. The Android Player module was absent. Android SDK/platform-tools were present, but `adb devices -l` listed no devices; Windows device enumeration returned Access denied. `ProjectSettings/EditorBuildSettings.asset` had an empty `m_Scenes` list, and the Windows player build failed because no scene was configured. | No named Android device or integrated Android player build exists. Android frame, memory, load, rendering, and asset measurements are unavailable. |
| iOS | T039 ran on Windows NT `10.0.26200.0`; `xcodebuild`, `xcrun`, `idevice_id`, and `adb` were not found. No macOS/Xcode runner, named iOS device, iOS version, or iOS build artifact was available. | No iOS build or device run exists. iOS measurements are unavailable. |

These reports establish environment limitations only. Their successful or attempted Unity Editor tests do not establish target-device performance.

## Workload and measurement protocol already specified

The plan, research, and quickstart define W001 as a **plain-Core `ProductionSimulationSession.Advance` Edit Mode workload**, not as an integrated mobile scene workload:

- 1,000 independent D001 machines: 500 active/unblocked, 250 active/output-blocked, and 250 inactive.
- Recipe `2 dev.input → 1 dev.output / 5 s`; each machine starts with `9,216` input and `4,608` output capacity.
- `100` untimed warmups and `500` independent samples for `100 ms` active advances and the capped `6.4 h` offline advance.
- Time only warmed `Advance`; prepare/reset and result capture/checksum remain outside the timed interval. A separate pass uses `GC.GetAllocatedBytesForCurrentThread()` for managed allocations on that thread.
- Record p50/p95 and raw timing samples. The existing W001 report uses a Windows Editor Mono runtime and explicitly labels the `10 ms` active / `100 ms` offline p95 and zero-allocation targets as Windows regression budgets.

W001 does not specify an integrated-scene mobile factory population, camera/render workload, Android/iOS player settings, or a frame-sampling duration. Its simulation intervals and sample counts cannot be silently repurposed as the mobile scene sampling protocol. The mobile thresholds are unspecified; the Windows budgets above are not applied to either mobile platform.

T037 reports a static decoded RGBA estimate of `581,632` bytes (`568 KiB`) for the placeholder images. It explicitly excludes Unity/runtime overhead and platform formats, so this is not a mobile build-size, texture-residency, or runtime-memory measurement. T037 also records the integrated scene's 640×360, orthographic-size-12 view as a required but uncaptured readability case at 15 px/cell; this visual acceptance evidence remains separate and unresolved.

## Mobile metrics

No target-device values were measured. No numeric mobile result or pass threshold is available for any row.

| Required metric | Android | iOS | Notes |
|---|---|---|---|
| Frame time and frame pacing, including p50/p95 | Unmeasured | Unmeasured | No target build, device, or approved integrated-scene sampling window. |
| Managed allocations and GC activity/pauses | Unmeasured | Unmeasured | W001 only counts current-thread managed allocations inside plain-Core `Advance`; it does not measure player-frame or native allocations. |
| Runtime memory, including managed and rendered-asset residency | Unmeasured | Unmeasured | T037's decoded-pixel estimate is not runtime residency. |
| Cold/warm scene load time | Unmeasured | Unmeasured | No platform player build or named device. |
| Rendering cost | Unmeasured | Unmeasured | No device frame/GPU/render capture; viewport calculations are not rendered performance evidence. |
| Asset cost in the player/runtime | Unmeasured | Unmeasured | No platform build-size or runtime texture/asset measurements. |

## Evidence required to resume

1. Provide a named physical Android device and a named physical iOS device, each with model and OS version. Android must be visible to ADB. iOS requires a named macOS runner with Unity `6000.6.0f1` iOS Build Support and Xcode.
2. Produce and identify an Android and an iOS build of the integrated scene. Resolve the empty player scene list reported by T038 before attempting a player build. Record each build's Unity version, backend, architecture, graphics API, quality/render-resolution settings, frame-rate/v-sync settings, and other target-specific build settings with its build identifier.
3. Declare the integrated-scene factory population/state and representative camera/render activity for each run, then record warmup and sampling-window details. W001's 1,000-machine fixture is specified for plain-Core simulation only; do not treat it as the scene/render fixture without an explicit approved mapping.
4. Capture raw device evidence for frame time/pacing, allocations/GC, memory, cold/warm scene load, rendering cost, and built/runtime asset cost. Summarize each platform separately and retain logs/captures and device/build identity. Keep the results as baselines; do not claim a mobile PASS until an explicit mobile threshold is approved.
5. Keep the T037 640×360/orthographic-size-12 readability capture as its own scene-render acceptance check; T040's performance report cannot close that missing visual evidence by calculation alone.

## Validation disposition

**T040 remains BLOCKED** until the named Android and iOS targets and platform builds are available and the required integrated-scene measurements above are captured. This task did not run Unity, build a player, access a device, or run a profiler because the required target environments are absent. No SDK/module was installed and no device was modified.

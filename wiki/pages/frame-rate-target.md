---
title: 60 FPS frame-rate target
type: decision
sources: [S001, S018]
updated: 2026-10-09
---

# 60 FPS frame-rate target

The owner's 60 FPS development target (see [Mobile performance targets](./mobile-performance-targets.md)) is implemented by T065 and reached on the Android emulator. It is a development target, not a shipping, physical-device, battery or thermal promise. (S001)

## Decision

- `CoreGameFoundationBootstrap.ApplyFrameRateTarget()` runs in `Awake` before composition and sets `Application.targetFrameRate = 60`. The policy lives at the Unity bootstrap boundary; no domain or application code knows about it (see [Unity presentation boundary](./unity-presentation-boundary.md)). Keep this method the single writer of the frame-rate and VSync settings. (S001)
- **Windows player:** the same target with `QualitySettings.vSyncCount = 0`, because VSync overrides `targetFrameRate` on desktop and the default quality level has VSync on. Android ignores `vSyncCount`. Rejected alternative: keep VSync on Windows, which would follow the monitor's refresh rate instead of the target. Known gap: VSync-off can tear above 60 Hz and cannot be re-enabled; revisit before a non-development Windows build or a graphics settings screen. (S001)
- **Editor pitfall:** setting `QualitySettings` during Editor Play Mode changes the Editor's quality level, which test runs can re-serialize into `ProjectSettings/QualitySettings.asset`. The VSync change is therefore compiled only into players (`#if !UNITY_EDITOR`); a `StandalonePlayer` test checks it in the Windows test player. (S001)
- **Production stays time-based (FR-017).** `FactoryRuntimeHost` converts cumulative active time into ticks, and no project code uses `Time.deltaTime` or `Time.frameCount`, so 60 FPS only renders and samples the same state more often. (S001)

## Emulator result (T065, 2026-09-28)

Same session, freshly cold-booted emulator, no editor on the host, T040 protocol with a same-session 30 FPS control. Builds differ only in `libil2cpp.so`. (S001)

| Set | Avg FPS | p50 / p95 / p99 | Intervals ≥ 50 ms | Guest CPU mean |
|---|---|---|---|---|
| Control (implicit 30) | 30.31–30.36 | 33 / 34–36 / 36–44 ms | 11–16 | 18.97% |
| Target (60) | 61.40–61.50 | 16 / 17–18 / 18–19 ms | 0 | 31.46% |

Doubling the frame rate cost about +12.5 percentage points of guest CPU; no PSS change was detected with three runs per set. Still unverified: physical devices, battery and thermal cost, the Windows player on a real display, and an actively producing factory. (S001)

A first session on a long-running, degraded emulator measured about 4.5 FPS for both builds and was discarded; see [Android emulator profiling](./android-emulator-profiling.md). (S001)

T124 measured the first actively producing factory on the emulator: 61.1 FPS average (controls 61.5/61.6), so the open producing-factory gap above is closed for the emulator only. See [Android emulator validation](./android-emulator-validation.md). (S018)

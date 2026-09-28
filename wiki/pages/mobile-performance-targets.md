---
title: Mobile performance targets
type: decision
sources: [S001]
updated: 2026-09-28
---

# Mobile performance targets

The desktop budgets for the plain-Core W001 workload (1,000 machines) are Windows regression targets, not mobile guarantees. Mobile evidence is recorded separately as exploratory emulator evidence, measured as in [Android emulator profiling](./android-emulator-profiling.md). (S001)

## T040 baseline (2026-09-27/28)

The D001 player on the emulator paces at about 30 FPS: a 33 ms median present interval, with 0 dropped or janky frames in three matching runs. No project code sets `Application.targetFrameRate`, so this is Unity's implicit Android cap, not a display limit. The fixed D001 checkpoint is output-blocked, and every D001 start blocks within one 5 s cycle. So the capture shows an idle, fully rendered scene: `FactoryRuntimeHost` is disabled and no production tick is measured. (S001)

## Decisions (owner, 2026-09-28)

- **60 FPS development target.** The build targets an average of 60 FPS, set explicitly instead of relying on the implicit cap. It is verified with the T040 frame-pacing protocol against a same-session 30 FPS control (T065). It is not a shipping, physical-device or battery promise. Render frequency still must not determine production. If the emulator cannot reach the target, the measured limiting cause is recorded instead of claiming the target. (S001)
- **Standalone profiler.** Emulator profiling uses the Unity Profiler (Standalone Process) (T064), because the in-editor Profiler stalled the player on the shared host (T040-F2). (S001)
- **No presentation allocation proportional to the machine count** (T063, from T040-F1). The idle poll of `MachineInspectorPresenter` allocated about 23 KB per poll. Its cost grows as O(N²) with the machine count; see [Unity presentation boundary](./unity-presentation-boundary.md). (S001)

## Order and gating

T063, then T064, then T065, all before the final acceptance T041. T064 profiles the corrected presenter, and T065 uses that profiler to find what limits the frame rate. T041 must describe T040–T065 as qualified, exploratory emulator evidence: one host, sets compared only within a session, and no mobile evidence yet for an actively producing factory. It must not present them as a clean mobile performance baseline. (S001)

The general state and simulation architecture behind these targets is in [Core architecture](./core-architecture.md). (S001)

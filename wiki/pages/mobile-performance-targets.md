---
title: Mobile performance targets
type: decision
sources: [S001, S018]
updated: 2026-09-29
---

# Mobile performance targets

The desktop budgets for the plain-Core W001 workload (1,000 machines) are Windows regression targets, not mobile guarantees. Mobile evidence is recorded separately as exploratory emulator evidence, measured as in [Android emulator profiling](./android-emulator-profiling.md). (S001)

## T040 baseline (2026-09-27/28)

The D001 player on the emulator paces at about 30 FPS: a 33 ms median present interval, with 0 dropped or janky frames in three matching runs. At the time no project code set `Application.targetFrameRate` (T065 added it), so this was Unity's implicit Android cap, not a display limit. The fixed D001 checkpoint is output-blocked, and every D001 start blocks within one 5 s cycle. So the capture shows an idle, fully rendered scene: `FactoryRuntimeHost` is disabled and no production tick is measured. (S001)

## Decisions (owner, 2026-09-28)

- **60 FPS development target.** The build targets an average of 60 FPS, set explicitly instead of relying on the implicit cap, and verified against a same-session 30 FPS control. It is not a shipping, physical-device or battery promise, and render frequency must not determine production. T065 reached it on the emulator (61.4–61.5 FPS); see [60 FPS frame-rate target](./frame-rate-target.md). (S001)
- **Standalone profiler.** Emulator profiling uses the Unity Profiler (Standalone Process) (T064), because the in-editor Profiler stalled the player on the shared host (T040-F2). It needs a build without profiler autoconnect; see [Unity standalone profiler on the emulator](./unity-standalone-profiler.md). (S001)
- **No presentation allocation proportional to the machine count** (T063, from T040-F1). The idle poll of `MachineInspectorPresenter` allocated about 23 KB per poll. Its cost grows as O(N²) with the machine count; see [Unity presentation boundary](./unity-presentation-boundary.md). (S001)

## T063 editor results (2026-09-28)

These allocation counts come from Unity Editor tests on the Windows host; they are not device timings. At W001 scale, polls with no selection or an unchanged revision allocate 0 times. A selected-machine poll on a revision change allocates 89 times at both 1 and 1,000 machines; the runtime host advance allocates 0 times, down from 105,907 per tick before T063. The idle-device result is reported under T064 below; the producing path has not been measured on the device. (S001)

## T064 results (2026-09-28)

- Three runs reached the T040 capture-start state with the standalone profiler attached. (S001)
- No game-code allocation remains in the idle D001 player, which confirms T063 on the device. The only remaining allocations come from the Input System (17 B per call) and a UI repaint. (S001)
- T040's roughly 20 MiB of render-texture memory were profiler screenshot buffers; the game keeps no render texture and about 5.8 MB of textures resident. (S001)
- The frame times are distorted, so T065 measures frame pacing unprofiled. (S001)

## Order and gating

T063, then T064, then T065 ran before the final acceptance T041. T065 reached the target, so the T064 profiler was not needed to find a frame-rate limit. T041 (2026-09-28) records all of T040–T065 as qualified, exploratory emulator evidence and not as a clean mobile baseline. It rests on one host, compares sets only within a session, and has no mobile evidence for an actively producing factory: every device capture used the output-blocked idle D001 checkpoint, and the producing-path result from T063 is editor-only. That gap stays open for future mobile work; see [Feature acceptance evidence](./feature-acceptance-evidence.md). (S001)

The general state and simulation architecture behind these targets is in [Core architecture](./core-architecture.md). (S001)

Feature 003 plans workload W003, a producing 12-slot factory, to close the producing-path gap; see [factory run simulation](./factory-run-simulation.md). (S018)

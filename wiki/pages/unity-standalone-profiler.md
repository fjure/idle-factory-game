---
title: Unity standalone profiler on the emulator
type: howto
sources: [S001]
updated: 2026-09-28
---

# Unity standalone profiler on the emulator

T064 made the Unity Profiler (Standalone Process) the profiling path for the Android emulator player. The full procedure and scripts are in `docs/integrations/unity-standalone-profiler-android.md` and `specs/001-core-game-foundation/reviews/t064-evidence/`. Device preparation and exclusive use are as in [Android emulator profiling](./android-emulator-profiling.md). (S001)

## Gotchas that cost a day

- **Build without autoconnect.** A `ConnectWithProfiler` player opens its own profiler connection to the build host at startup. It reached the open main editor on port 34999, and it streams frames only over that connection. The standalone profiler then got nothing. Build with `BuildAndroidArm64DevelopmentGles3 -noProfilerAutoconnect`; the default build keeps autoconnect. This is the most likely cause of the T040 stall. (S001)
- **The editor must stay open.** The standalone profiler is a child process of the main editor, and closing the editor closes it. Keep the editor idle. (S001)
- **Both processes load MCP for Unity** under the same instance ID and displace each other. Stop the main editor's MCP bridge first; the committed driver refuses to run unless the answering process has role `profiler`. (S001)
- **Recording is owned by the profiler window** in Unity 6000.6. `ProfilerDriver.enabled` alone records nothing. Use `ProfilerWindow.SetRecordingEnabled`. Switching it off and on within one editor frame reaches the player as no change. (S001)
- **Attach timing.** Connect only after the player logs "Started UDP target info broadcast" (about 2 s after launch). An earlier connection drops at once. After that line, the attachment holds through scene load. (S001)
- **Snapshots.** A hung Memory Profiler snapshot request blocks all later ones until the profiler restarts. The editor's built-in `PackedMemorySnapshot` reads per-asset residency without the Memory Profiler package. (S001)

## What the profiler distorts

- The attached profiler slowed the idle player to 3–5 recorded frames per second, against about 30 FPS unprofiled. Frame times are profiling context, never a baseline. (S001)
- Allocations that run per `FixedUpdate`, such as the Input System's 17 B per call, look larger per frame the slower the frame is. Report per-call sizes and per-window totals as well. (S001)
- The Screenshots module keeps about 21.5 MB of render textures in the player (`ProfilerScreenshot*`). Disable it before measuring render-texture memory. (S001)

Results and targets: [Mobile performance targets](./mobile-performance-targets.md). (S001)

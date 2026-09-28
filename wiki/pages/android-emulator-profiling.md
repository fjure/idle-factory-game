---
title: Android emulator profiling
type: howto
sources: [S001]
updated: 2026-09-28
---

# Android emulator profiling

This procedure produces exploratory performance evidence for the D001 scene on `Medium_Phone_API_36.0`. Results are emulator-only; they give no threshold, no Windows budget, and no physical-device or release claim. Build and install as in [Android emulator validation](./android-emulator-validation.md). The targets this evidence feeds are recorded in [Mobile performance targets](./mobile-performance-targets.md). (S001)

## Repeatable capture protocol (T040)

`reviews/t040-evidence/capture-protocol.ps1` runs three runs (S001). Each run:

1. force-stops the app and restores the five fixed checkpoint files;
2. cold-launches with `am start -W`;
3. waits 30 s with no input;
4. records the capture-start state;
5. captures 60 s.

The state record hashes the factory contents (origin, size, and for each building its ID, anchor, orientation, active flag, buffers, recipe and progress) without the checkpoint metadata, because every launch writes a new generation. Runs may be compared only when these hashes match. Afterwards, restore the original device profile and verify it by hash. (S001)

## Metric sources that work, and traps

- **Frame pacing:** Unity renders into a `SurfaceView`, so `dumpsys gfxinfo … framestats` stays empty. Use SurfaceFlinger TimeStats instead: enable and clear them (`dumpsys SurfaceFlinger --timestats -enable -clear`) right before the window, and dump and disable them right after it. Read the `SurfaceView[…](BLAST)` layer. Its `present2present` histogram gives frame intervals, and its percentiles are bucket lower bounds (the protocol's `-CaptureFrameTimeStats` switch). (S001)
- **CPU:** toybox `top`'s first sample covers only a short interval at the start of the window. That interval falls on the explicit ART GC that the start-boundary `dumpsys meminfo` triggers, so exclude the first sample. (S001)
- **Memory:** `dumpsys meminfo` PSS/RSS are process snapshots, including translation and shared mappings. They are not an allocation rate. (S001)
- **Host load:** guest `sys` load and CPU differed markedly between two sessions with the same APK, checkpoint and screen. Compare sets only within one session, and add a same-session control run whenever you want a before/after statement. (S001)

## Unity Profiler

The in-editor Profiler is not usable on the same Windows host. With the editor open, the player's "Fully drawn" time grew from +14–16.5 s to +33–48 s. With the editor's player connection open, the player stalled after scene load until the editor closed. The attached Profiler also slowed frames about 15×, so profiled frame times are not representative, although counts and bytes are. The chosen path is the Unity Profiler (Standalone Process), connected through `adb forward tcp:34999 localabstract:Unity-<package>` (T064). Closing the editor also stops the ADB server it started; restart it with `adb kill-server` and `adb start-server`. (S001)

## Exclusive device use

In T040 a parallel agent session reinstalled the APK, cleared app data and relaunched the app on the same emulator during profiling, which ruined several attempts (S001). Before measuring:

- check that no other session or `adb` client uses the device;
- verify the installed APK hash and the device checkpoint;
- do one unscored launch after each install, because the first launch is much slower;
- scan each run's continuous logcat for foreign activity starts, HOME/POWER events, rotations, installs, data clears and force-stops. (S001)

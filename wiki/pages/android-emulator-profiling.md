---
title: Android emulator profiling
type: howto
sources: [S001, S041, S055]
updated: 2026-11-05
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

- **Frame pacing:** Unity renders into a `SurfaceView`, so `dumpsys gfxinfo … framestats` stays empty. Use SurfaceFlinger TimeStats instead: enable and clear them (`dumpsys SurfaceFlinger --timestats -enable -clear`) right before the window, and dump and disable them right after it. Read the ``SurfaceView[…](BLAST)`` layer. Its `present2present` histogram gives frame intervals, and its percentiles are bucket lower bounds (the protocol's `-CaptureFrameTimeStats` switch). (S001)
- **CPU:** toybox `top`'s first sample covers only a short interval at the start of the window. That interval falls on the explicit ART GC that the start-boundary `dumpsys meminfo` triggers, so exclude the first sample. (S001)
- **Memory:** `dumpsys meminfo` PSS/RSS are process snapshots, including translation and shared mappings. They are not an allocation rate. (S001)
- **Host load:** guest `sys` load and CPU differed markedly between two sessions with the same APK, checkpoint and screen. Compare sets only within one session, and add a same-session control run whenever you want a before/after statement. (S001)
- **Degraded emulator:** in T065 a long-running emulator, with editors open on the host, measured about 4.5 FPS for both control and target, then stopped answering `adb`. Cold-boot before scoring and check that the control reproduces ~30 FPS; see [60 FPS frame-rate target](./frame-rate-target.md). (S001)

## F004 B1 addendum

Feature 004's B1 run measured a 58.05 versus 48.18 FPS ABA spread on the same build. Use about +-5 FPS as the noise band and repeat the control at least 3 times in one cold-booted session; see [F004 B1 Built-in baseline](./f004-b1-builtin-baseline.md). (S041)

## Unity Profiler

In T040 the in-editor Profiler stalled the autoconnect player after scene load, and its frame times were about 15× distorted. Use the Unity Profiler (Standalone Process) with a `-noProfilerAutoconnect` build, as described in [Unity standalone profiler on the emulator](./unity-standalone-profiler.md) (T064). Opening or closing the editor restarts or stops the ADB server; check `adb devices` afterwards. (S001)

## Exclusive device use

In T040 a parallel session reinstalled, cleared and relaunched the app on the same emulator during profiling, ruining several attempts (S001). Before measuring:

- check that no other session or `adb` client uses the device;
- verify the installed APK hash and the device checkpoint;
- do one unscored launch after each install, because the first launch is much slower;
- scan each run's continuous logcat for foreign activity starts, HOME/POWER events, rotations, installs, data clears and force-stops. (S001)

Unity command-line test runs (`scripts/test-unity.ps1`) also count as ADB use. When a checkout's active build target is Android, which is true of any `Library/` after an Android build, every batchmode run ends with "Killing ADB server". During T063 this broke a parallel session's emulator script: a device `ls` failed and was read as "no checkpoint", and the emulator went `offline`. Do not run Unity tests while another session drives the emulator, and restart the ADB server afterwards. (S001)

## F004 B3 addendum (batch 12)

An ABA is invalid when the host is loaded or the emulator is throttled: in B3 session 4, B1, B3 and empty-run controls all ran about 44 FPS and the 30 FPS control read 25 to 27, so the rows stayed an accepted gap (B3 43.4 vs B1 44.6 FPS; guest CPU 40.8 % vs 33.3 %, within the 8.7-point B1 spread). Another game client on the host wrecked three sessions. See [batch 12](./f004-batch-12-final-verification.md). (S055)

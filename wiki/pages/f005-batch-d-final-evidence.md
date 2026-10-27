---
title: F005 batch D final evidence
type: reference
sources: [S022, S029, S035, S036, S037]
updated: 2026-10-27
---

# F005 batch D final evidence

Numbers of record for the F005 closeout; accepted commit `4af2389` (code-identical to `cc0afd3`). (S022)

## Suites (T347, #412)
Edit Mode 2323, Play Mode 2230, StandalonePlayer 39, DevTools 68, BalanceFull 7: all passed, 0 skipped. (S035)
- `scripts/test-unity.ps1` has no assembly parameter, so the DevTools assembly runs through a direct `Unity.exe -runTests -assemblyNames IdleFactory.DevToolsTests` with a temporary `Assets/csc.rsp` defining `IDLEFACTORY_DEV`, deleted afterwards. (S035)
- F-1: `EventBase.PreventDefault()` in `ResearchPanel.cs` is obsolete in Unity 6000.6 (warning CS0618); replace it before a Unity update turns it into an error. (S035)

## Release scan (T350, #415)
- Windows 64 Mono and Android arm64 IL2CPP release builds both pass `scripts/check-release-build.ps1` with 0 findings; no `IdleFactory.DevTools` in either. (S036)
- Script fix: the gradleOut fallback for the backup-rules XML ran only without an APK; it now also runs when aapt2 is missing and still fails closed when nothing is found. (S036)
- `IdleFactoryBackupRules.androidlib/build.gradle` hard-codes SDK 36, min SDK 26 and build-tools 36.0.0; change them together with Player Settings. (S036)

## Performance (T349, #414)
- W005-L revision 3 on a quiet host: algorithm 273.5 ms, full ColdStart 312.1 ms against 500 ms; spreads 4.4 / 3.6 %. (S029)
- PF-10: W003-B with auto-deliver 219.24 ms against the re-baselined 222.28 ms (-1.4 %), met. (S029)
- Frame time with UI at W005-L: average 16.68 ms, p95 16.9 ms (60 fps cap). Acceleration host step p95 0.17-0.21 ms at x600 and Max against 1 ms. IL2CPP timing stays open. (S029)
- The earlier 548.5 ms and 376.8 ms were host load. The host ran about 1.5-2 cores of background load (Riot client, Docker, Chrome); record it as PF-9. (S029)
- `runInBackground=True` in desktop players is by design (LifecyclePlatform), distinct from the release-setting guard. See [desktop focus](./desktop-focus-lifecycle.md). (S022)
- The probe tooling was not preserved between tasks and was reconstructed; keep its source with the record. (S029)

## Emulator (T351)
Frame rate 60.59 average with W005-L producing against 60.8 controls; see [journeys](./f005-batch-d-journeys.md). (S037)

Related: [batch B measurements](./f005-batch-b-performance-and-measurements.md), [content revision 3](./f005-batch-c-content-revision-3.md).

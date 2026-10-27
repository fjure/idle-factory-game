---
title: F005 batch D player and emulator journeys
type: howto
sources: [S022, S037]
updated: 2026-10-27
---

# F005 batch D player and emulator journeys

## Windows player (T348, #413, PR #500)
- The StandalonePlayer suite now holds 39 tests: economy journey, a V2 to V3 round trip through the real store, fixture parity and log-failure isolation (a failing session-log writer cannot change the run). (S022)
- Editor-only composition seams do not reach players; drive the real components. See [player-level test gaps](./player-level-test-gaps.md). (S022)

## Android emulator (T351, #416, PR #503)
- `scripts/android-emulator-journey.ps1` gained the F005 scenario: W005-L push, Sell, auto-deliver, session-log export (7 lines, no file path inside, SEC-1), Home, Lock, cold restart and the 8 h capped return. (S037)
- Fix: Home, Lock and ColdRestart read `Checkpoints/v1`; they now read `Checkpoints/v2/run-checkpoint.*`. The Profile step still reads v1. (S037)
- Pitfall: a push into `IdleFactory/Checkpoints` created by `adb shell mkdir -p` before the first app run makes the game report `persistence.failed` (the directories are owned by `shell`). Launch once, then push. (S037)
- Pitfall: fixed-fraction taps inside a scrolled list are unreliable (fling distance varies); locate the button from the screenshot first. (S037)
- Pitfall: the AVD `/data` filled to 92 %; the script removes only the known stale test-runner package and fails below 400 MB free. (S037)
- Open: PF-7 flush calibration and the moved overlay need a DevTools plus GLES3 Android build variant that does not exist. (S037)

Related: [Android emulator validation](./android-emulator-validation.md), [batch D evidence](./f005-batch-d-final-evidence.md), [session log writer](./f005-session-log-writer.md).

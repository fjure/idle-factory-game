---
title: F004 emulator journey scenario
type: component
sources: [S045]
updated: 2026-10-28
---

# F004 emulator journey scenario

T443 (#561, PR #587, merge `203e66d`) added `-Scenario F004` to `scripts/android-emulator-journey.ps1`; the default, F003 and F005 scenarios are unchanged. Steps: `W004VPush` (after Launch), `F004Restyled`, `F004Crispness`, `W004VFps` and `F004LogScan`. (S045)

- `F004Crispness` takes an adb screencap and runs a block-uniformity analyzer (FR-016, SC-005); it was checked on synthetic crisp and blurred PNGs only. (S045)
- Validated without an emulator: parse check, unknown step and scenario rejection, and a lease conflict that stops before any adb call. Not run on a device; thresholds may need tuning in T444. (S045)
- QA notes: the analyzer `Add-Type` sits at top level; detection of `n` could pick a multiple of the true value; the Welcome-back tap is `0.85,0.738` for the 1080x2400 AVD; `W004VFps` measures plain W003-S until a build applies the W004-V forced presentation. (S045)

Related: [Android emulator validation](./android-emulator-validation.md), [F004 B1 Built-in baseline](./f004-b1-builtin-baseline.md).

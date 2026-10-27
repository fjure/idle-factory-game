---
title: Android Back and runInBackground
type: decision
sources: [S018, S019]
updated: 2026-10-10
---

# Android Back and runInBackground

T251 (Issue #350, PR #351 merge `ac4256c`, commits `5f37644` and `a6e3a33`) fixed a regression the first T133 emulator run found on `8a8e518`: with the Remove confirmation open, Android Back (key and two-swipe gesture) closed the task (`Transition CLOSE`, `onDestroy`, `Quitting process`) instead of cancelling. Owner decision D8: fix before acceptance. Evidence: `reviews/android-emulator-evidence.md`, section "T251". Emulator only; no physical device. (S018)

## Root cause and fix

- **Cause**: exactly one project setting, the Player setting `runInBackground`, which T250 (`ed49ca9`) turned on. Why it changes the GameActivity input path was not investigated; the isolation is the basis of the fix. (S018)
- **Fix**: the Player setting is 0 again for every platform. `LifecyclePlatform` (Bootstrap) sets `Application.runInBackground = true` at BeforeSceneLoad in desktop players only, using `Application.isMobilePlatform`. The editor is skipped because there the property writes the Player setting. Activity, immersive mode and gesture exclusion are untouched; `contracts/run-application.md` never mentioned the setting. (S018, S019)
- **Rejected**: a per-platform Player setting (`PlayerSettings.runInBackground` has none) and any activity change. (S018)

## Method: bisect with a single-setting variant and its inverse

| Build | Setting | Back on Remove confirmation |
|---|---|---|
| T247 baseline | off (older commit) | PASS |
| `8a8e518` base | on | FAIL, process restarted |
| Variant A: base with only `runInBackground: 0` | off | PASS, danger pixels 29,637 to 0, same PID |
| Fixed build | off plus runtime call | PASS for key and 2-swipe gesture |

The variant and its inverse (the base) differ in one setting, so no further builds for the T248 and T249 changes were needed. (S018)

## Guards

- Edit Mode `AndroidBackGestureTests` (3 new): the Player setting stays 0 (fail-first shown), desktop runs in background at runtime and mobile does not, and the runtime initializer exists and skips the editor. (S018)
- StandalonePlayer `RunInBackgroundPlayerTests` observes `LifecyclePlatform.BackgroundExecutionApplied` and `Application.runInBackground == !Application.isMobilePlatform`. **The property alone proves nothing**: the test-player harness already sets it true, so it read true with the call disabled. The test fails first with the call disabled only because it observes the initializer's own flag. Player suite 24 of 24. (S018)

## Open

A live Alt-Tab on a desktop player and an unfocused editor Play Mode were not observed after the fix. (S018)

Related: [Android back gesture](./android-back-gesture.md), [desktop focus](./desktop-focus-lifecycle.md), [F003 final acceptance](./f003-final-acceptance.md), [Android emulator validation](./android-emulator-validation.md).

---
title: Android back gesture
type: decision
sources: [S018]
updated: 2026-10-10
---

# Android back gesture

Decision for T247 (Issue #328, FR-033, SC-007, UX-2): in immersive fullscreen the Cancel path of a confirmation is the Back key or a **second** OS edge swipe. The first swipe only reveals the transient system bars. The owner accepted this on 2026-10-09 (option a); no immersive, activity or ProjectSettings change was made. Confirmation on a physical device is still open: all evidence is emulator-only (API 36 AVD, gesture navigation). (S018, `reviews/android-emulator-evidence.md` section "T247 back gesture", commit 6fb873f)

## Root cause

The player starts in fullscreen (`androidStartInFullscreen: 1`), which hides the navigation bar. SystemUI then reports `navbar_hidden` with `backGestureDisabled=true`, so the first edge swipe is swallowed and never reaches the app. Right after that swipe the flag is false for a few seconds (transient bars visible) and then disabled again. (S018)

## Disproved fixes

- Predictive back off (`enableOnBackInvokedCallback=false`): the manifest flag is ignored at target API 36, and the result equals the baseline. (S018)
- An `OnBackInvokedCallback` in `IdleFactoryGameActivity`: it registered but was never invoked, because SystemUI never sends the swallowed swipe. Not kept. (S018)

Rejected option (b): change the immersive start (bars visible or `BEHAVIOR_DEFAULT`). It would let the first swipe act, at the cost of visible bars and a layout and safe-area re-check, and T247 excluded it. (S018)

## Guard

Edit Mode `AndroidBackGestureTests` (3 tests) pin the decision: predictive back stays at the Unity default, and the activity adds no back handling (`OnBackInvoked`, `onBackPressed`, `KEYCODE_BACK`, `dispatchKeyEvent`), keeping it pure platform glue (R19, see [Android runtime rotation](./android-runtime-rotation.md)). A deliberate change needs a new emulator measurement of one and two swipes. (S018)

## Regression after T250 and its fix (T251)

The T133 emulator run found that T250's `runInBackground` Player setting made Back on an open confirmation close the app. T251 reverted the setting and applies it at runtime on desktop only; the decision above is unchanged and `AndroidBackGestureTests` now has 3 guards. See [Android Back and runInBackground](./f003-android-back-run-in-background.md). (S018)

## Behaviour with nothing to cancel

With the confirmation closed, one Back key or two swipes close an open Machine sheet. With nothing open the player stays resumed in the same process: Back does not leave the app. A single swipe with nothing open changes nothing. (S018)

## Open points

- A swipe starting at `x=3` belongs to the system and does not pan the camera, but a real finger just inside the edge region can still pan it. No exclusion region was added. (S018)
- A plain `input tap` on `Remove` right after a HOME and return with the Machine sheet open was ignored three times, while Close and the slot tap worked. Unexplained and not investigated; the journey script works around it. (S018)

How the gesture is injected and machine-checked is in [Android emulator validation](./android-emulator-validation.md). Related gap list: [Player-level test gaps](./player-level-test-gaps.md).

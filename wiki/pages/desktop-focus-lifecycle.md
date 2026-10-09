---
title: Desktop focus is not a departure
type: decision
sources: [S018, S019]
updated: 2026-10-10
---

# Desktop focus is not a departure

T250 (#338, `ed49ca9`, PR #341) splits lifecycle behaviour by runtime platform. Owner decision 2026-10-09 after the T126 playtest: on desktop, Alt-Tab, another window or minimizing must not stop the run, and offline credit applies only after a regular shutdown or an unclean exit and the next start. Phones and tablets keep FR-037 (focus loss and pause record a departure, the return reconciles). (S018)

## How it works

- **Seam**: `LifecyclePlatform` (`Unity/Bootstrap/`) decides the class from `Application.isMobilePlatform`, never from the `ShellDeviceProfile` input profile, so a touch laptop is a desktop and a keyboard tablet is not. Tests switch it through a disposable `Override`; production never does. The editor's Play Mode counts as desktop. (S019)
- **Desktop focus loss or pause**: no departure is recorded, the host keeps running and nothing reconciles. Exactly one plain safety checkpoint (`Autosave`) is written per focus loss, because the game has no periodic autosave; it is skipped while a prestige is pending or the run is not Running, is never published as a return, and credits exactly once after an unclean exit. A regular quit still records the departure. (S018)
- **Background execution is applied at runtime, not by the Player setting.** `LifecyclePlatform` sets `Application.runInBackground = true` before the first scene (BeforeSceneLoad) in desktop players only, so the player keeps simulating while unfocused. The editor is skipped: there the property writes the project's Player setting, and Play Mode already runs unfocused. `ProjectSettings` `runInBackground` is **0** again. (S018, S019, T251, `5f37644`, `a6e3a33`, PR #351)
> Superseded: T250 (`ed49ca9`) turned the Player setting on and claimed mobile was unchanged. That claim was wrong: with the setting on, Android Back on an open confirmation closed the task. Cause, bisection and guards: [Android Back and runInBackground](./f003-android-back-run-in-background.md). (S018)
- **Tests**: `RunDesktopLifecycleTests` (6) and the StandalonePlayer suite run the real desktop class; `RunInBackgroundPlayerTests` observes `BackgroundExecutionApplied` in a built Windows player. A live Alt-Tab on a desktop player and an unfocused editor Play Mode were not observed after T251. (S018)
- **Older tests**: Older Play Mode tests assume the mobile departure rules, so an editor-only `[SetUpFixture]` (`LifecyclePlatformTestDefault`) defaults them to the mobile class. (S019)

## Open follow-ups (QA, non-blocking)

- After an OS sleep while unfocused, one large live frame delta could be simulated live, outside offline efficiency and the 8 h cap. A clamp or reclassification of large single-frame deltas is to be decided with the Game Systems lens. (S018)
- The T131 review rates these advisories F-1 (no throttle), F-2 (no failing-write adapter test; an unguarded throwing flush could escape the callback), F-3 (no timer autosave while unfocused, an owner-visible consequence of this decision) and notes the untested quit-then-focus-lost order. Details and dispositions: [F003 security review](./f003-security-review.md). (S018)

Related: [Android runtime rotation](./android-runtime-rotation.md) (its rotation classifier applies to the mobile departure path; on desktop that path is no longer entered), [run bootstrap and lifecycle](./run-bootstrap-and-lifecycle.md), [run persistence](./run-persistence.md).

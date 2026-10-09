---
title: Desktop focus is not a departure
type: decision
sources: [S018, S019]
updated: 2026-10-09
---

# Desktop focus is not a departure

T250 (#338, `ed49ca9`, PR #341) splits lifecycle behaviour by runtime platform. Owner decision 2026-10-09 after the T126 playtest: on desktop, Alt-Tab, another window or minimizing must not stop the run, and offline credit applies only after a regular shutdown or an unclean exit and the next start. Phones and tablets keep FR-037 (focus loss and pause record a departure, the return reconciles). (S018)

## How it works

- **Seam**: `LifecyclePlatform` (`Unity/Bootstrap/`) decides the class from `Application.isMobilePlatform`, never from the `ShellDeviceProfile` input profile, so a touch laptop is a desktop and a keyboard tablet is not. Tests switch it through a disposable `Override`; production never does. The editor's Play Mode counts as desktop. (S019)
- **Desktop focus loss or pause**: no departure is recorded, the host keeps running and nothing reconciles. Exactly one plain safety checkpoint (`Autosave`) is written per focus loss, because the game has no periodic autosave; it is skipped while a prestige is pending or the run is not Running, is never published as a return, and credits exactly once after an unclean exit. A regular quit still records the departure. (S018)
- **`runInBackground: 1`** in ProjectSettings keeps the desktop player simulating while unfocused. iOS ignores it, and Android honours it only while the app is visible and unfocused, so mobile behaviour is unchanged. (S018)
- **Tests**: `RunDesktopLifecycleTests` (6) and the StandalonePlayer suite run the real desktop class. Older Play Mode tests assume the mobile departure rules, so an editor-only `[SetUpFixture]` (`LifecyclePlatformTestDefault`) defaults them to the mobile class. (S019)

## Open follow-ups (QA, non-blocking)

- After an OS sleep while unfocused, one large live frame delta could be simulated live, outside offline efficiency and the 8 h cap. A clamp or reclassification of large single-frame deltas is to be decided with the Game Systems lens. (S018)
- No throttle on safety checkpoints per Alt-Tab, and no adapter-level tests for focus loss during a pending prestige or a failing Autosave. (S018)

Related: [Android runtime rotation](./android-runtime-rotation.md) (its rotation classifier applies to the mobile departure path; on desktop that path is no longer entered), [run bootstrap and lifecycle](./run-bootstrap-and-lifecycle.md), [run persistence](./run-persistence.md).

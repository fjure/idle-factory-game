---
title: Run shell notices and gate fixes
type: component
sources: [S018, S019]
updated: 2026-10-07
---

# Run shell notices and gate fixes

How the [run shell](./run-shell-and-panels.md) shows notices, hints and pinned values after T192, and where each T139 shell-gate finding now stands. (S018, S019)

## Notices, hints and pins (T192)

- `ShowNotice(text, kind, sticky)` takes a `ShellNoticeKind` (Info, Warning, Error, Lock) and shows the Art Bible 9.4 icon with a left bar in the kind's colour, distinct from the detail line. A transient notice clears after `NoticeSeconds` (6 s), on a tab, sheet or slot change, on any UI press and on a world press. A sticky notice (gameplay unavailable) returns after a clear. `ReconcileLockNotice` drops the locked-slot notice once the slot unlocks. (S019)
- `SetEmptyHint` shows "Build a Miner: tap an empty slot." on a phone with the sheet closed while no slot is occupied. (S019)
- Pinnable strip values carry a small info glyph. The bootstrap calls `NotifyWorldPress()` on a world tap or pan to unpin the detail line, because world presses never reach UI Toolkit. (S019)
- A destructive confirm puts the warning icon on the Confirm button as its background image, since a text button cannot have children. The focus ring is clamped to the panel bounds. (S019)
- Detail, notice and hint bars are `picking-mode="Ignore"`; `RunShellDocument.TryGetUnobstructedScreenRect` starts the camera area below the lowest bar shown (see [UI Toolkit HUD layout pitfalls](./ui-toolkit-hud-layout-pitfalls.md)). (S019)

Each fix has a Play Mode test at every screen class and at 100 % and 200 % text. At 200 % text the Cash value can reach the glyph's margin, so the no-cover check applies only when the value fits its cell. (S018) Shell composition and its retry are in [run bootstrap and lifecycle](./run-bootstrap-and-lifecycle.md).

## Shell gate (T139)

The independent UI/UX shell gate passed T112 with ten non-blocking findings (F-1…F-10). T192 fixed F-1, F-2, F-3, F-5 (shell part), F-6, F-7 and F-8 (above); T191 fixed F-4 (text scale) and F-10 (panel scale), see [run shell device profile and text scale](./run-shell-device-profile-and-text-scale.md). F-9 (slot below 44 pt at the camera minimum) awaits a Game Designer/Product and Art Director decision; T128 re-checks all ten. (S018)

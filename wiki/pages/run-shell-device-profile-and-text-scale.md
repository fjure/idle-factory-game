---
title: Run shell device profile and text scale
type: component
sources: [S018]
updated: 2026-10-07
---

# Run shell device profile and text scale

T191 gives the [run shell](./run-shell-and-panels.md) a device profile and one text scale before panels T113–T118 add fixed sizes. Both are owner decisions of 2026-10-07 on the T139 shell-gate findings F-10 and F-4. (S018)

## Device profile

- `ShellDeviceProfile` is a plain value computed at the presentation boundary: primary input, pixel size, DPI and orientation. A mobile platform, or a touch screen without mouse or keyboard, counts as touch; DPI 0 means unknown. `Create/Touch/Pointer/Detect` build one without a device. (S018)
- The layout class still follows the panel-point size, so a landscape phone can get the tablet-landscape or desktop layout. The phone-landscape side sheet applies only where the size yields the phone class. (S018)
- The profile, not the layout class, decides touch behaviour. On touch, a tap pins the exact-value line, a 500 ms press shows it, and hover shows nothing. On a pointer profile, hover and focus show it and a mouse press never pins; touch or pen presses still pin, for hybrid screens. (S018)

## Physical size: 44 pt ≥ 7 mm

`ShellPanelScale` raises a touch profile with known DPI to a constant pixel scale of `dpi × 7 / 25.4 / 44` when the authored scale is lower. The raise is applied to the live `PanelSettings` at runtime and restored on teardown, so the `CoreGameFoundationPanelSettings` asset stays unchanged. A pointer profile keeps the authored scale. An unknown DPI also keeps it and is recorded as `UnknownDpiFallback`. (S018)

Example: 1080×2400 at 420 dpi becomes 2.63 px/pt, giving 410×912 pt (Compact portrait) or 912×410 pt (Compact landscape). As a pointer profile, the same device keeps 2.09 px/pt (portrait) or 1.69 px/pt (landscape). Its landscape form is 1422×640 pt, which classifies as Desktop. (S018)

There is no width cap: on small high-DPI screens the raise can push the panel below 360 pt, and the 7 mm rule wins. (S018)

## Text scale

`ShellTextScale` holds one factor from 100 % to 200 %, rounded to 10 % steps. On Android it comes from `Configuration.fontScale` (clamped, polled every 2 s); elsewhere it is 100 %. There is no in-game setting in Feature 003. The shell sets `run-text-scale--NNN` on its root. (S018)

Panel rule: never use a fixed `font-size`. Use `var(--run-fs-caption)` (12 pt), `--run-fs-body` (14 pt), `--run-fs-value` (16 pt) or `--run-fs-zoom`. Keep `min-height: 44px` without a fixed height, avoid `white-space: nowrap` unless a layout test proves it fits at 200 %, and adapt layouts with `.run-text-scale--200` descendant selectors. From 120 % the strip and tab bar wrap, and sheets scroll. (S018)

Testing at 200 %: build the panel in `ShellRig.Create(screen, null, 200)` for every `ShellScreens.All()` entry with worst-case content. Run `RunShellLayoutAssertions.AssertShellLayout`, check that the scroll content is clipped, and focus the first, middle and last control. `RunTextScaleTests` is the reference. (S018)

## Open evidence

The raise and the Android font-scale read are proven only in Edit/Play Mode. Three checks are deferred to T124 on a device: the 420 dpi raise, the font-scale read at 200 %, and the HUD sharing the raised live `PanelSettings`. See [UI Toolkit HUD layout pitfalls](./ui-toolkit-hud-layout-pitfalls.md). (S018)

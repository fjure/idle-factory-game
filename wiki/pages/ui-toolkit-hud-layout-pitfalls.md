---
title: UI Toolkit HUD layout pitfalls
type: howto
sources: [S001, S018, S019]
updated: 2026-10-09
---

# UI Toolkit HUD layout pitfalls

Three Unity 6000.6 UI Toolkit behaviours broke the Feature 001 HUD on a real phone while the editor viewport checks passed. They apply to any future UI Toolkit screen built on the [Unity presentation boundary](./unity-presentation-boundary.md). (S001)

## The UIDocument root has zero height

`UIDocument.rootVisualElement` resolves to the full panel width but zero height. An absolutely positioned child with `top: 0; bottom: 0` therefore collapses to its padding, and every panel draws over the next one. The T036 checks missed this because they sized the document root explicitly. The fix pins the root to the panel: `position: absolute` with all insets `0` (T053). (S001)

## Screen pixels are not panel points

`Screen.width`, `Screen.height` and `Screen.safeArea` are in pixels, while layout rules and screen classes are in panel points. Passing pixels classified a 1080×2400 phone as tablet portrait. `FactoryHudDocument.ApplyScreenLayout` converts with the panel's `scaledPixelsPerPoint` first. With `ScaleWithScreenSize` (1280×720 reference, match 0.5), the `sw411dp` phone runs at about 1.68 px/pt in either orientation, so tests must cover the scale the authored panel settings actually produce (T053, T059). (S001)

> ⚠ conflict: S001 says the `sw411dp` phone runs at about 1.68 px/pt in either orientation; S018 (T191, measured in a 1080×2400 panel) says `ScaleWithScreenSize` blends the width and height ratios linearly, giving 2.09 px/pt in portrait and 1.69 px/pt in landscape for 1080×2400.

On a touch device with known DPI the run shell now raises the live `PanelSettings` scale so 44 pt ≥ 7 mm. Any other `UIDocument` that shares that asset, such as the HUD, sees the raised scale while the shell is alive. See [run shell device profile and text scale](./run-shell-device-profile-and-text-scale.md). (S018)

## Without a theme, ScrollView does not lay out or clip

`CoreGameFoundationPanelSettings` loads no theme style sheet. The ScrollView therefore gets no default layout: its viewport does not clip, and the scroller collapses to zero height. The viewport is a grandchild of the ScrollView (inside `unity-content-and-vertical-scroll-container`), so a direct-child selector never matches. Project USS must supply the row layout, grow/shrink and `overflow: hidden` with descendant selectors. A control that receives focus while clipped is scrolled into view with `ScrollView.ScrollTo` (T059). (S001)

Layout-rectangle assertions passed while content escaped its viewport. Assert clipping and focus visibility, not only rectangle overlap. (S001)

## Frame the camera around the HUD, not the screen

Centring the orthographic camera on the 16×16 factory put the D001 machine outside the visible width in portrait, under the side panels. The camera now receives the largest uncovered screen rectangle from `FactoryHudDocument.TryGetUnobstructedScreenRect`, clamps pan limits against it and frames the occupied cells once after the HUD layout has resolved. It only reads factory state (T054). (S001) Overlay bars over the world must take no pointers (`picking-mode="Ignore"`) and must shrink that rectangle; the run shell does both for its notice, detail and hint bars, see [run shell notices and gate fixes](./run-shell-notices-and-gate-fixes.md) (T192). (S019)

## Text buttons and wrapped labels (T112)

A `Button` that shows text must have no child elements: a text node with children loses its text measurement, so the T112 focus ring is drawn as an overlay that follows the focused control. A wrapped `Label` placed directly inside a bounded `ScrollView` is clamped to the viewport height; wrap it in a container. See [run shell and panels](./run-shell-and-panels.md). (S019)

Panel-specific pitfalls (wrapped-label clamp, hidden-scroller wheel, layout jumps, non-ASCII literals, double arrow events) are in [run panels](./run-panels.md). (S019)

See [player-level test gaps](./player-level-test-gaps.md) for why these faults reached the player.

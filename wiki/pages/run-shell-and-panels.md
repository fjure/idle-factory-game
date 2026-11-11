---
title: Run shell and panels
type: component
sources: [S018, S019, S038, S053]
updated: 2026-11-06
---

# Run shell and panels

T112 adds the responsive run shell on top of [run bootstrap and lifecycle](./run-bootstrap-and-lifecycle.md). It follows the [Unity presentation boundary](./unity-presentation-boundary.md): views read run state and mutate only through `RunCommands`. (S018, S019)

## Layout

- `FactoryRun.uxml`/`.uss` (`RunShellDocument`): a phone bottom tab bar (Factory, Market, Contracts, Items, Research, Prestige) with bottom sheets at most 60 % of the safe height, a phone-landscape side sheet at most 50 % of the width, and a 360 pt side panel on tablet and desktop. Screen classes reuse `FactoryHudDocument.Classify`; safe area is applied in panel points. (S018, S019)
- `SlotGridView`: 12 slots described by the pure `SlotGridModel.Describe` (empty, locked with its unlock level, occupied with badge precedence and at most two markers). A slot tap opens the build sheet (empty) or machine panel (occupied); the Feature 001 placement flow is not reused. Zoom limits are the serialized `RunZoomSettings` (16/64, fit-all floor 24 px per cell). (S018, S019)

## Panel mount registry

Panels T113–T118 mount through `ShellPanelRegistry.Register(IShellPanel)`, which returns an `IDisposable` and rejects unknown or duplicate IDs. IDs: Build, Machine, Market, Contracts, Items, Research, Prestige. An unregistered panel shows a "not available" placeholder. `IShellPanel` exposes `Root`, `OnShown(context)`, `Refresh()` and `OnHidden()`; the shell never needs editing to add a panel. (S019)

## Shared services

- `RequestConfirmation`: a focus trap, initial focus on Cancel, focus return to the invoker, Escape and Android Back as Cancel, and rotation cancel with the notice (UX-2, UX-9). A second request closes the first as Replaced. (S018, S019)
- `AttachExactValue`: the exact value of a compact number on focus or hover, pinned by tap, or after a 500 ms long press (UX-8). (S018, S019)
- `RunRejectionCopy` maps all 24 rejection codes to player text. A shell composition failure disables start-up with `shell.unavailable` and re-shows the Feature 001 HUD error. (S019)

Panel authors must respect the [UI Toolkit HUD layout pitfalls](./ui-toolkit-hud-layout-pitfalls.md) and the text-scale panel rule in [run shell device profile and text scale](./run-shell-device-profile-and-text-scale.md).

The panels built on this registry (T113–T118) are listed in [run panels](./run-panels.md). Notices, the empty-grid hint, pinned values and the state of the T139 shell-gate findings are in [run shell notices and gate fixes](./run-shell-notices-and-gate-fixes.md).

## Batch 10 change: ore-vein cut

`TryGetUnobstructedScreenRect` now cuts the ore-vein overlay corner-aware (above or beside, larger fitting scale wins). See [batch 10 components](./f004-batch-10-lattice-zoom-ore-vein-art-import.md). (S053)

## Gotcha: Back only closes slot and tab sheets

`RunShellDocument.HandleBack` closes, in order, a confirmation dialog, a pinned host, and then a sheet only when a slot is selected or a non-Factory tab is active. A panel that is neither, such as the F004 Settings sheet (T433, [batch 11](./f004-batch-11-floor-settings-icons.md)), needs its own open-state flag and a `HandleBack` branch. Otherwise Escape and Android Back fall through. `ShellPanelIds.IsKnown` is a closed list, so a new panel id also needs its clause there. (S038)

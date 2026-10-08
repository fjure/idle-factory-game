---
title: Run panels
type: component
sources: [S018, S019, S021]
updated: 2026-10-08
---

# Run panels

T113–T118 add the player-visible surfaces on top of the [run shell](./run-shell-and-panels.md). Each one reads run read models and mutates only through `RunCommands` (see [Unity presentation boundary](./unity-presentation-boundary.md)). None of them edits the shell, the bootstrap or the scene: T194 wires them into the running game. (S018, S019)

## Panels and their mount points

| Task | Type | Mount | Notes |
|---|---|---|---|
| T113 | `BuildSheetPresenter`, `MachinePanelPresenter` | Registry IDs Build and Machine | UXML and USS; remove goes through the shell's destructive confirmation. (S019) |
| T114 | `MarketPanelHost` with `InventoryRatesPanel`, `PrioritiesPanel`, `ExpansionPanel` | Market tab | One host stacks the three sections, because the registry has one ID per tab. Drag only on pointer profiles. (S019) |
| T115 | `ContractsPanel`, `ItemsPanel` | Contracts, Items | Built in code with a panel USS, no UXML. (S019) |
| T116 | `ResearchPanel` hosting `IndustryPanel`; `OverdriveControl` | Research | There is no Industry tab, so Industry is a section under the graph. `OverdriveControl` matches the shell's top-strip captions. (S019) |
| T117 | `PrestigePresenter` | Prestige | Confirm, then "Saving…", then the idempotent `PromotePrestige`; a failure shows Retry. (S019) |
| T118 | `OfflineSummaryPanel`, `StartupStatusPanel` | Modal `StatusSurface` overlays | The registry rejects unknown IDs. `StatusSurface.Mount` works under the shell or on the document root. (S019) |

Every panel with a pending choice exposes `CancelForRotation()`, which the bootstrap registers as a transient action (UX-9). (S019)

## Shared conventions

- Soft-disabled controls stay focusable and show their reason. A press still sends the command, and the rejection copy appears next to the control, matching `contracts/ui-surfaces.md` › Rejections. (S018, S019)
- Panel USS uses only the `--run-fs-*` sizes. Every panel suite runs a worst-case layout test at all screen classes, rotations included, at 100 % and 200 % text. (S021)
- Panel tests that load assets through `AssetDatabase` sit inside `#if UNITY_EDITOR` (see [Unity player validation](./unity-player-validation.md)). (S021)

## Authoring pitfalls

- A sole wrapped `Label` in a short sheet is clamped to the viewport height. T115 fits it with a small geometry handler; T117 keeps a private copy, so the names do not collide. (S019)
- Non-ASCII literals such as `…` and `—` in `.cs` files broke the text in a T117 test run. Use `\u` escapes in C# and character references in UXML. (S019)
- One arrow press can arrive as both `KeyDown` and `NavigationMove`. The Research graph handles both in trickle-down, calls `PreventDefault` and de-duplicates per frame. (S019)
- A node or card larger than its viewport (200 % text on a 640x360 sheet) can only be aligned at its top-left corner. (S019)

## Known gaps

- With an unreadable active V2 generation and an intact previous generation, the cold start stays blocked with Retry as the only action. `RunPersistence.RecoverPreviousCheckpoint` needs a readable active generation. T195 closes this gap (see [run cold start and V1 import](./run-cold-start-and-v1-import.md)). (S018)
- After a live rotation from 360x640 to 640x360, the shell's panel title clips by about 3 pt. This is for the T128 review. (S018)
- The Art Bible 9.4 lock and warning icons beside locked types and rejections are not shown yet, so text carries the meaning. (S018)

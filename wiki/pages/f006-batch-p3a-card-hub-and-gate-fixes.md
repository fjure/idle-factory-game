---
title: F006 batch 4 machine card, Details view and Office hub
type: component
sources: [S060]
updated: 2026-11-13
---

# F006 batch 4 machine card, Details view and Office hub

## T534 docked MachineCard (#706, PR #748, 314f783)

- The card is one docked instance: a bottom band on phone portrait (at most 30 % at 100-140 % text, 45 % hard cap), a side column of about 40 % on phone landscape, a side panel on tablet and desktop. The machine panel became its **Details view**; without a registered card `OpenSlot` still opens the machine panel directly, so tests that register only the panel did not change. (S060)
- Upgrade happens in place and keeps focus. Keyboard order is persistent layer, locations by `SortKeyOf`, then the card; focus returns to the location. (S060)
- **ViewBounds**: `ScaleLayout.ViewBounds` and `FactoryWorldLayout.ViewBounds` (content, depot, deposit). Pan limits always use it; fit-all and centring use it only when it keeps the integer fit scale. Selection framing uses `MachineRectOf` (plate plus badge band). (S060)
- Measured unobscured world share with the card open: portrait 200 % text 0.42, landscape 0.52-0.57, tablet and desktop above 0.70; asserted floors 0.40, 0.50, 0.70. A fix round followed the Software Architect and UI/UX reviews. (S060)

## T553 empty tap closes Details and the card (#751, PR #755, 820f0ed)

`RunShellPresenter.HandleWorldTap` closed only while the card was open, so Details survived an empty-world tap (U-A2). It now closes Details and the card through the existing close path; marker-versus-machine tap priority is unchanged. (S060)

## T535 Office hub (#707, PR #756, b84078a)

- `HubPanel` (registry id `hub`) holds 8 cards: Market, Contracts, Items, Research, Industry, Overdrive, Prestige, Settings. Industry needed an additive `ShellPanelIds.Industry`; attention flags only on Contracts, Research, Prestige, Overdrive (`HubAttention`, `HubAttentionFeed`; `ContractsShellFeed` re-pointed to `SetContracts`). (S060)
- The hub reuses the scaffold by composition (`.scaffold.hub` override) and the shell's own sheet or side panel. `PanelScaffold` gained the `scaffold-sheet` mount point and the shared 48 dp `.scaffold-row`; the scaffold state card's Back follows `BackVisible`. (S060)
- **Back order**: hub, Industry and Settings are single frames of `ShellNavigationStack`; a system view opened from the hub replaces it, so one Back returns to the factory (FR-021). (S060)
- **Entry placement**: portrait gets its own 48 dp row; landscape from 120 % text an overlay beside the zoom column; other classes sit in the strip before the gear. Fix round U-1: the camera rectangle excludes the landscape overlay so the entry never covers the framed world; U-3: closing a hub-opened view returns focus to the Office entry. (S060)
- The entry glyph is still the contracts fallback (`HubCards.EntryIconKey`); the new `f006-ui-office` glyph is not imported because the scene's `PresentationSpriteCatalog` has no F006 rows yet (A-13, T527). When swapped it must stay distinct from the Contracts card icon. (S060)
- Card carried items closed here: A-1 and A-2 (`Details ›` takes the footer width, cause below it), A-3 (no blocker or second Switch on for a switched-off machine), A-4 (shortfall shows once, inside the button). (S060)
- Rebaselines: `AllPanelIds` 9 to 11 ids; 36 `F004-B2` render frames regenerated through the harness write mode, not hand-edited. The scene diff is two serialized fields set through a temporary editor script. `RunShellDocument.cs` (about 3,240 lines) grew by about 20; hub logic sits in `HubPanel.cs` and `RunShellDocument.Hub.cs`. (S060)

Related: [shell and panels](./run-shell-and-panels.md), [panel conventions](./run-panel-conventions.md), [gate and decisions](./f006-p3a-gate-and-owner-decisions.md), [P2 scaffold](./f006-batch-p2-art-and-presentation.md).

---
title: Run panels
type: component
sources: [S018, S019, S021, S022, S028]
updated: 2026-10-14
---

# Run panels

T113–T118 add the player-visible surfaces on top of the [run shell](./run-shell-and-panels.md). Each one reads run read models and mutates only through `RunCommands` (see [Unity presentation boundary](./unity-presentation-boundary.md)). None of them edits the shell, the bootstrap or the scene: T194 wires them into the running game. (S018, S019)

How the bootstrap composes and mounts them is in [run bootstrap and lifecycle](./run-bootstrap-and-lifecycle.md) › Panel composition. (S019)

## Panels and their mount points

| Task | Type | Mount | Notes |
|---|---|---|---|
| T113 | `BuildSheetPresenter`, `MachinePanelPresenter` | Registry IDs Build and Machine | UXML and USS; remove goes through the shell's destructive confirmation. (S019) |
| T114 | `MarketPanelHost` with `InventoryRatesPanel`, `PrioritiesPanel`, `ExpansionPanel` | Market tab | One host stacks the three sections, because the registry has one ID per tab. Drag only on pointer profiles. (S019) |
| T115 | `ContractsPanel`, `ItemsPanel` | Contracts, Items | Built in code with a panel USS, no UXML. (S019) |
| T116 | `ResearchPanel` hosting `IndustryPanel`; `OverdriveControl` | Research | There is no Industry tab, so Industry is a section under the graph. `OverdriveControl` matches the shell's top-strip captions. (S019) |
| T117 | `PrestigePresenter` | Prestige | Confirm, then "Saving…", then the idempotent `PromotePrestige`; a failure shows Retry. (S019) |
| T118 | `OfflineSummaryPanel`, `StartupStatusPanel` | Modal `StatusSurface` overlays | The registry rejects unknown IDs. `StatusSurface.Mount` works under the shell or on the document root. (S019) |

**Short-return suppression (T246, #327).** `OfflineSummaryPanel.ShouldOpenSummary(summary, usedFallback)` keeps the modal closed for a clean, applied return whose measured absence is below `ShortAbsenceThresholdMs` (60 000 ms, owner decision 2026-10-08, FR-033/UX-2) with nothing to report. It still opens for an FR-037 fallback, a backward or invalid clock, a forfeited credit, any expired effect, ended Overdrive phase or expired contract, and every return at or above the threshold. The reconcile still runs exactly once; the summary stays available as `LastGenuineSummary`, which Continue does not clear. The contract row is in `contracts/ui-surfaces.md`. The expired-effect and fallback cases are proven at `ShouldOpenSummary` level only, not through a full return. (S018, commit 70741aa)

Every panel with a pending choice exposes `CancelForRotation()`, which the bootstrap registers as a transient action (UX-9). (S019)

**Desktop pointer and stable layout (T248, T249).** The Research graph scrolls with the wheel, Shift+wheel and drag-to-pan on pointer devices at every screen class (OD-8 widened by the owner, spec Session 2026-10-09). The Machine panel keeps its controls fixed at a full store by placing the blocker section below the actions. Details in [run panel conventions](./run-panel-conventions.md). (S018)

## Conventions and pitfalls

Shared panel conventions (soft-disabled controls, text sizes, lock and warning icons, Editor-only test helpers) and the authoring pitfalls are in [run panel conventions](./run-panel-conventions.md). (S019, S021)

## Known gaps

- Resolved by T195: Use previous save now recovers a torn active generation at cold start (see [run cold-start recovery](./run-cold-start-recovery.md)). (S018)
- After a live rotation from 360x640 to 640x360, the shell's panel title clips by about 3 pt; T128 left it non-blocking. The final review's findings and open owner decisions (F-9, N-5, N-9) are in [F003 UI/UX final review](./f003-ui-ux-final-review.md). (S018)
- Resolved by T196 and T243: the Art Bible 9.4 lock and warning icons now sit beside locked items and every rejection text, including the Market tab's Inventory and rates (`value.limit` overflow caption included) and Priorities sections. The overflow caption has no dedicated test fixture yet. (S018, S021)
- Resolved by T196: `ItemsPanel.OnTarget` never activates a target the snapshot shows as unavailable; it shows the reason and keeps the Item. (S018)

Update (F005 batch A): Market sell rows, the ore vein and build price rows are new surfaces; see [F005 batch A economy and contracts](./f005-batch-a-economy-and-contracts.md) and [open decisions](./f005-batch-a-open-decisions.md) (locked rows hidden, UI findings F8-F10). (S022, S028)

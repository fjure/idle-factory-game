# UI surfaces: Feature 005 controls and F004 hand-off

This document extends `specs/003-first-factory-prestige/contracts/ui-surfaces.md`. The controls are functional, and F004 restyles them. F004 may change layout, art and placement, but it must keep:

- the command a control calls;
- the soft-disabled-with-reason rule;
- the exact-value rule;
- the accessible-name-with-state rule.

Shared rules for every control below (FR-038, `run-panel-conventions`):
- Controls are at least 44 pt and focusable when soft-disabled, and they show the reason next to the control.
- A rejection shows `RunRejectionCopy.For(code)` beside the control that was used.
- Text uses `--run-fs-*` and wraps at 200 % text, with no fixed heights.
- Numbers use `QuantityFormatter` (floor to 3 significant digits) with `AttachExactValue`.
- Focus follows reading order. After a control disappears, focus moves to a visible sibling.

| Control (stable name) | Location | Command | States | Reasons surfaced |
|---|---|---|---|---|
| `inventory-sell-step-<resourceId>` (selector, steps from content) and `inventory-sell-<resourceId>` (button) | Market › Inventory row, beside price and Auto-sell | `Sell` / `SellAll` | normal; soft-disabled (stock 0 or below the step: "Nothing to sell" / "Need N"); confirm when All drops below the active delivery contract ("Contract will be N short"); success notice "Sold N X: +$Y" with the exact value `N × $p = $Y` | `resource.locked`, `sell.quantity-invalid`, `stock.insufficient`, `value.limit` |
| `contracts-autodeliver` (toggle) | Active contract card, delivery objectives only | `SetAutoDeliver` | Off/On with the accessible name "Auto-deliver, on/off"; a hint line states that auto-deliver reserves the stock from auto-sell; hidden for production objectives and expired cards | `contract.none-active`, `command.invalid` |
| `contracts-offer-cooldown-<offerId>` (text) | Offer row | — (Accept stays soft-disabled) | "Available in mm:ss"; bonus offers "Bonus, ends in mm:ss" | `contract.cooldown`, `contract.bonus.unavailable` |
| `contracts-produce-progress` | Active production card | — | "Produced N of M" plus a bounded bar; auto-completes | — |
| `tab-contracts-badge` | Contracts tab button | — | Text "•" plus the accessible name "Contracts, deliverable" when any delivery contract is deliverable; absent otherwise | — |
| `build-price-<machineId>` | Build sheet | `Build(…, expectedPrice)` | Price (exact on inspect), "Owned: N", "Capped" when at cap; "Need $X more" when unaffordable | `cash.insufficient`, `build.price.changed`, `value.limit` |
| `goals-next` | Top of Market | — | "Next goal $X: $Y of $X (Z %)"; "Factory Sale available"; "All goals reached"; one-time notice on reaching a goal | — |
| `prestige-threshold` | Prestige panel | `ConfirmPrestige` | Shows the current threshold, the next threshold and the prestige index | `prestige.below-threshold`, `prestige.maxed` |
| `session-log-toggle`, `session-log-export`, `session-log-clear` | Settings section (placement decided in the plan review) | adapter, not a run command | Export: path notice or share; "Nothing to export"; failure reason with retry; the copy states "stays on this device" | adapter codes: `log.empty`, `log.export.failed` |
| `dev-timescale`, `dev-accelerated-marker` | DevTools overlay (development builds only) | `ITimeScaleSource` | ×1/×10/×60/×600/Max; a persistent "ACCELERATED ×N" marker when N ≠ 1 | — |

Each control needs Play Mode layout tests at every `ShellScreens.All()` entry at 100 % and 200 % text, plus keyboard focus tests and empty/error state tests (AGENTS.md UI rule).

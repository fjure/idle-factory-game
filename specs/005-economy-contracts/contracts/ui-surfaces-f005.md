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
| `factory-ore-vein` (with `factory-ore-vein-progress`, `factory-ore-vein-pulse`, `factory-ore-vein-reason`) | A screen-space overlay anchored to a safe-area corner of the Factory view, outside the slot rectangle, not moving with pan or zoom; hidden and out of the focus order when a sheet covers it | `Mine` | Click, tap, keyboard (KeyDown starts, KeyUp ends) and hold at the capped rate with pointer capture; a second pointer or leaving the vein ends the hold; one pooled pulse "+0.1" or "+0.1 ×n" with a static fallback; a subtle "max rate" cue; a progress indicator in tenths; "Store full" clears without a layout jump; excluded from exact-value gestures | `storage.full`, `value.limit`, `lifecycle.blocked` |
| `inventory-sell-step-<resourceId>` (selector, steps from content) and `inventory-sell-<resourceId>` (button) | Market › Inventory row, beside price and Auto-sell | `Sell` / `SellAll` | normal; soft-disabled (stock 0 or below the step: "Nothing to sell" / "Need N"); confirm when All drops below the active delivery contract ("Contract will be N short"); success notice "Sold N X: +$Y" with the exact value `N × $p = $Y` | `resource.locked`, `sell.quantity-invalid`, `stock.insufficient`, `value.limit` |
| `contracts-autodeliver` (toggle) | Active contract card, delivery objectives only | `SetAutoDeliver` | Off/On with the accessible name "Auto-deliver, on/off"; a hint line states that auto-deliver reserves the stock from auto-sell; hidden for production objectives and expired cards | `contract.none-active`, `command.invalid` |
| `contracts-offer-cooldown-<offerId>` (text) | Offer row | — (Accept stays soft-disabled) | "Available in m:ss"; bonus offers "Bonus, ends in m:ss" | `contract.cooldown`, `contract.bonus.unavailable` |
| `contracts-produce-progress` | Active production card | — | "Produced N of M" plus a bounded bar; auto-completes | — |
| `tab-contracts-badge` | Contracts tab label | — | The tab label itself reads "Contracts (n)" or "Contracts !" where space allows. Only the compact "•" is the separate `tab-contracts-badge` element in the tab corner; that element id exists only in the dot form. Accessible name "Contracts, n deliverable". It clears when nothing is deliverable, including after a sale or auto-sell lowers the stock. | — |
| `build-price-<machineId>` | Build sheet | `Build(…, expectedPrice)` | Price (exact on inspect), "Owned: N", "Capped" when at cap; "Need $X more" when unaffordable | `cash.insufficient`, `build.price.changed`, `value.limit` |
| `goals-next` | Top of Market | — | "Next goal $X: $Y of $X (Z %)"; "Factory Sale available"; "All goals reached"; one-time notice on reaching a goal | — |
| `prestige-threshold` | Prestige panel | `ConfirmPrestige` | Shows the current threshold, the next threshold (the same value once the maximum index is reached) and the prestige index | `prestige.below-threshold` |
| `session-log-section` with `session-log-toggle`, `session-log-export`, `session-log-clear`, `session-log-last-export` | Fourth `MarketPanelHost` section after Expansion (UX-6); F004 relocates it | adapter, not a run command | Export: path notice or share; "Nothing to export"; failure reason with retry; the copy states "stays on this device" | adapter codes: `log.empty`, `log.export.failed` |
| `dev-timescale`, `dev-accelerated-marker` | DevTools overlay (development builds only) | `ITimeScaleSource` | ×1/×10/×60/×600/Max; a persistent "ACCELERATED ×N" marker when N ≠ 1 | — |

Each control needs Play Mode layout tests at every `ShellScreens.All()` entry at 100 % and 200 % text, plus keyboard focus tests and empty/error state tests (AGENTS.md UI rule).

## Plan review additions (UX-1…UX-13)

- **Accessible names** (visible labels stay short):

  | Control | Name pattern |
  |---|---|
  | Ore vein | "Mine Iron Ore, +0.1 per click" (exact yield), plus ", store full" when blocked; nothing is announced per click, only state changes |
  | Sell button | "Sell 10 Iron Plate, available" or "Sell 10 Iron Plate, unavailable: not enough stock" |
  | Step selector | "Sell amount, 10" |
  | Build | "Build Assembler, $69, owned 3" plus a reason when unavailable |
  | Goal line | "Next goal $X, Y percent" |
  | Auto-deliver | "Auto-deliver, on/off" |
  | Log toggle | "Session log, on/off" |
  | Export | "Export session log", plus a reason when unavailable |
  | Marker | "Accelerated, factor N" |

- **Focus**:
  - Per row the order is step selector → Sell → Auto-sell.
  - Focus stays on a soft-disabled control, for example Sell after the stock is emptied.
  - It moves to a sibling only when a control is hidden.
  - Dialogs return focus to the invoker.
- **Sell row**:
  - A row with zero stock or a locked resource shows "Nothing to sell" or "Locked: unlocks after <condition>" instead of disappearing.
  - On a stale tap the row re-renders and focus is kept.
  - "All" asks for confirmation only when it would leave a contract short.
  - A remembered step above the stock shows "Need N more" and is never changed silently.
- **Auto-deliver**:
  - When it fires, a notice "Auto-delivered N X: +rewards" appears.
  - An expired contract with auto-deliver on shows "Auto-deliver was on; stock was short".
  - Production objectives show the one-line hint "Completes automatically".
  - After accept the toggle reads Off, and the hint says "applies to this contract only".
- **Cooldown and bonus**: the countdown updates on the existing once-per-second tick. When a bonus window ends while the tab is open, the row switches to "Bonus ended" without a layout jump. A rejected tap shows the `contract.cooldown`, `contract.bonus.unavailable` or `contract.capacity` copy next to Accept.
- **Build price**:
  - On `build.price.changed` the sheet re-renders and shows "Price changed to $Y", and nothing is charged.
  - The Remove confirmation states the rebuild price.
- **Session log**:
  - Clearing the log asks "Clear the session log? This cannot be undone" through `RequestConfirmation`, and rotation cancels it.
  - A persistent "Last export: <path>" line shows the path, which is selectable on Windows.
- **DevTools**: keyboard-operable. The marker is text, does not overlap safe areas or the HUD at 200 %, and its accessible name includes the factor.
- **Known limitation**: the goal line is visible in Market only. F004 decides whether it moves to the HUD.

## Invariants for F004

- The displayed price equals the charged price.
- Compact numbers floor and never overstate, and affordability compares exact values.
- Countdown ticks stay at once per second.
- Soft-disabled controls stay focusable and show their reason.
- Every rejection code has copy next to its control.
- The Market section order and the below-the-fold session-log section at 200 % are known layout limits.
- The F005 panel, layout and focus suites stay green.

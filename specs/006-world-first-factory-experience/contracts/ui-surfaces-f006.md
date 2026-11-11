# Contract: UI Surfaces (F006)

**Scope**: The persistent layer, the machine card and details, the hub and system views, the back stack, the build card and the shared visual system.
**Requirements**: FR-005, FR-007 to FR-010, FR-008a, FR-016 to FR-024, FR-032, FR-033.
**Research**: R10, R11, R13, R20.

These rules apply to every surface:

- Panel conventions: `wiki/pages/run-panel-conventions.md`.
- The text-scale rule: token font sizes, `min-height` instead of a fixed height, and no `nowrap` without a 200 % layout proof.
- The 44 pt / 7 mm touch raise.
- Mutations only through `RunCommands`.

## 1. Persistent layer (FR-007 to FR-010, FR-008a)

**Contents**:
- Cash and store fill, both with the exact-value behaviour.
- The hub entry. It is icon-only with an accessible name: the screen-reader label, plus a tooltip on pointer profiles. The name is chosen at OD-9 and is never "Systems". The entry carries an aggregate attention dot.
- The attention indicator (resource icon and count) when an `AttentionState` item exists.
- The manual-mining control.
- The Overdrive control, compact, with a visibly distinct "ready" state.
- Zoom controls, only on pointer profiles or when `showZoomButtons` is on.

**Removed**: the six-tab bar and the RP caption, which moves to the Research hub card.

**Guidance**: chrome at most 15 % of the safe area at 100 % text, and at most 20 % at 200 % (SC-001). The figures are measured by the R02 probe.

**Tests**:
- `PersistentLayerTests` (Play Mode, every class at 100, 150 and 200 %): contents present, no clipping, targets of at least 44 pt.
- The zoom visibility matrix across the touch and pointer profiles and the preference.

## 2. Machine card and details (FR-016 to FR-019)

**Card content**, top to bottom:
1. Name and level.
2. A state line in words.
3. A cause line with the resource icon.
4. Upgrade with cost, affordability and the shortfall line.
5. A Details chevron.

There is no Remove control on the card.

**Upgrade.** Upgrade updates the card in place. Focus stays on Upgrade (F004 N-3).

**Placement**:
- Phone portrait: docked at the bottom, at most 30 % of the safe height at 100 % text (guidance). The state line and Upgrade stay in the first view at 200 %.
- Other classes: docked beside the machine, or as a side panel.

**Visibility.** The selected machine's footprint is 100 % inside the unobstructed rect on every class and text scale. This is binding through FR-016 and is measured for SC-002.

**Details.** Details contains everything the F003–F005 machine panel had: rates, progress, the batch note, blockers with corrective actions, switch on/off, Items and boosts, and Remove.

**Remove.** Remove goes through `RequestConfirmation`: focus on Cancel, Back cancels, and a rotation cancels with a notice.

**Close and restore.** Tapping empty world, Back, Escape or Close closes the card. Tapping another machine switches the selection. The camera restores through `CloseSheet`, unless the player moved it.

**Stale state.** A removed or replaced machine closes the card with a notice. A state change updates the card in place. A stale action never executes: the command result is checked, and the rejection copy is shown.

**Tests**:
- `MachineCardTests` (Play Mode).
- `MachineDetailsParityTests`, which proves that every former panel capability is reachable.
- `CardCameraTests`, covering focus, restore and rotation.

## 3. Hub and system views (FR-020 to FR-023)

**Cards**: Market, Contracts, Items, Research, Industry (its own registry id `Industry`, which is additive), Overdrive, Prestige and Settings. Each card has an icon, a one-line status from existing data and an attention flag from `AttentionState.SystemFlags`.

**Navigation.** A system view opens through `ShellPanelRegistry` unchanged. Return to the factory takes at most 1 interaction.

**Containers.** Phone portrait uses a bottom sheet. Phone landscape and larger classes use a side panel that keeps the factory visible.

**Shared visual system.** One header pattern, primary and secondary button classes, spacing tokens and icon keys. There is one disabled state, conveyed by shape, text and contrast rather than by dimming alone (F004 U-4). Empty, loading and error states are provided for every view. The offline summary, notices, errors and confirmations use the same system.

**Tests**:
- `HubPanelTests` (Play Mode): reachability, attention flags, return in at most 1 interaction.
- `SurfaceConsistencyTests`, which asserts the shared USS classes on every registered panel.

## 4. Back stack and accessibility (FR-005, FR-021, FR-024)

**Back order.** `ShellNavigationStack` pops in this order: dialog, then pinned value, then card or details or build card, then hub or system view or settings, then the factory.

**Back semantics.** Android Back and Escape pop exactly one frame per frame-tick. Focus returns to the invoker.

**Preserved behaviours.**
- A rotation cancels pending confirmations with the existing notice.
- World replacement clears the stack.
- The stable-layout rule holds: blockers stay below the actions.

**Keyboard.** Focus moves location by location in `SortKeyOf` order and then into the card. Every action is keyboard-activatable.

**Tests**:
- `ShellNavigationStackTests` (Edit Mode).
- `BackOrderTests` (Play Mode, Escape and Android Back).
- `AndroidBackGestureTests`, re-baselined.
- `KeyboardTraversalTests`.

## 5. Build card (FR-032, FR-033)

**Opening.** A tap on an empty unlocked location opens the build card. The card lists machine types with icon, cost and affordability.

**Preview.** The view highlights available locations. The selected type shows a ghost preview: frame 0 at 60 % alpha at the location.

**Actions.** Confirm and Cancel sit side by side, each at least 44 pt.

**Insufficient Cash.** Confirm is disabled, the line "Need $X more" appears, and nothing changes.

**Success.** A build-dust effect and the foundation appear, then the build card is replaced by the new machine's card.

**Tests**:
- `BuildCardTests` (Play Mode): enough Cash, not enough Cash, cancel through Back, rotation while open.

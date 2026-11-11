# Contract: UI Surfaces (F006)

**Revision**: 2 (plan reviews, 2026-11-11)
**Scope**: Persistent layer, machine card and details, hub and system views, back stack, build card, shared scaffold.
**Requirements**: FR-005, FR-007 to FR-010, FR-008a, FR-016 to FR-024, FR-032, FR-033.
**Research**: R10, R11, R13, R20, R22.

## General rules

These rules apply to every surface:

- Follow `wiki/pages/run-panel-conventions.md`.
- Use font-size tokens and `min-height`. Do not use `nowrap` unless a test proves the layout at 200 %.
- Keep the 44 pt / 7 mm touch targets and the raise.
- Change game state only through `RunCommands`.
- World-facing copy comes from `reviews/world-copy.md` (FR-047).

## 1. Persistent layer (FR-007 to FR-010, FR-008a)

**Contents**:

- Cash and store fill (exact value on demand).
- The hub entry: an **icon plus a short visible text label**. The label is never "Systems", comes from `world-copy.md`, and is readable at 200 %. The entry carries the system-attention dot.
- The production attention indicator: resource icon and count.
- The manual-mining control.
- The Overdrive control, compact, with a distinct ready state.
- Zoom controls, only on pointer profiles or when `showZoomButtons` is on.

**Removed**: the six-tab bar, and the RP caption (it moves to the Research card).

**Layout per screen class**:

| Screen class | Arrangement |
|---|---|
| Phone portrait | Top strip plus a bottom row |
| Phone landscape | Left and right columns, no top bar |

**Guidance (SC-001)**: chrome takes at most 15 % of the screen at 100 % text and at most 20 % at 200 %, measured with `ShellGeometryProbe`.

**Tests**:

- `PersistentLayerTests` (Play Mode, every class at 100, 150 and 200 %, including 640×360 at 200 %): contents present, no clipping, targets of at least 44 pt, label not truncated.
- Zoom matrix: touch or pointer × preference × screen class.
- An FR-009 checklist test.

## 2. Machine card and details (FR-016 to FR-019)

**Docked only.** The card never floats.

| Screen class | Placement |
|---|---|
| Phone portrait | Bottom band. Up to 30 % at 100–140 % text, up to 45 % at larger text (hard cap). |
| Phone landscape | Side column of about 40 % width, with Upgrade pinned. |
| Tablet and desktop | Side panel. |

**Framing.** The band is known before the camera reframes. Framing stays driven by the selection: `OpenSheet(band, footprint)` when the card opens, `CloseSheet` when it closes.

**First view** (visible without scrolling at every text scale):

- name and level;
- the state line;
- Upgrade, with the cost and any shortfall inside the button.

**Below the first view**: the cause line with the resource icon, then the Details chevron.

**Not on the card**: Remove.

**After an upgrade**: the card updates in place, and focus stays on Upgrade.

**Details** keeps every capability of the F003–F005 machine panel. Remove sits in Details behind `RequestConfirmation`.

**Visibility.** The selected footprint is always 100 % inside the unobstructed rect. This is binding through FR-016.

**Stale state**:

- A removed or replaced machine closes the card and shows a notice.
- A stale action shows an inline card line with the rejection copy and never executes.

**Keyboard order**: persistent layer, then world locations in `SortKeyOf` order, then the card. Closing the card returns focus to the invoking location.

**Tests**:

- `MachineCardTests`: Upgrade visible without scrolling at 200 % with worst-case copy, on every class.
- `MachineDetailsParityTests`.
- `CardCameraTests`: dock, restore, rotation.
- `KeyboardTraversalTests`.

## 3. Hub and system views (FR-020 to FR-023)

**`HubPanel`** is built on `PanelScaffold`: a header with back, primary and secondary button classes, and the empty, loading and error templates. Art Bible §11 governs the scaffold.

**Cards**:

| Card | Notes |
|---|---|
| Market | |
| Contracts | Attention flag |
| Items | |
| Research | Status line shows the RP value; attention flag |
| Industry | Additive registry id |
| Overdrive | Attention flag |
| Prestige | Attention flag |
| Settings | |

Every card shows an icon and a one-line status. Only Contracts, Research, Prestige and Overdrive carry attention flags.

**System views** open through `ShellPanelRegistry` unchanged. Returning to the factory takes at most 1 interaction.

**Containers**:

| Screen class | Container |
|---|---|
| Phone portrait | Bottom sheet |
| Other classes | Side panel |

**Mutual exclusion**: opening the hub closes the machine card or build card and restores the camera.

**Shared states**: one disabled state, conveyed by shape, text and contrast, never by dimming alone. Shared empty, loading and error templates. Notices render above sheets. The offline summary, notices, errors and confirmations all use the scaffold.

**Tests**:

- `HubPanelTests`: every registered panel has a card, attention flags behave as specified, return takes at most 1 interaction.
- `SurfaceConsistencyTests`: scaffold classes and templates on every panel.

## 4. Back stack and accessibility (FR-005, FR-021, FR-024)

**Back order**: `ShellNavigationStack` pops in this order:

1. Dialog
2. PinnedValue
3. MachineCard, BuildCard or Details
4. Hub, SystemView or Settings
5. Factory

**Rules**:

- Android Back and Escape pop one frame per frame-tick.
- Focus returns to the invoker.
- Rotation cancels any open confirmation and shows a notice.
- A world replacement clears the stack.
- Stable layout is preserved.

**Migration**:

- Stack and sheet logic is extracted from `RunShellDocument` as a pure move.
- `ShellTab` stays as an internal shim until the UXML tab bar is removed in P3.
- `StartupStatusPanel.HandleBack` delegates to the stack.

**Tests**:

- `ShellNavigationStackTests`.
- `BackOrderTests`: mixed sequences (card, then hub, then dialog; build card, then Back; hub, then system view, then Back), plus rotation.
- `AndroidBackGestureTests` (re-baselined).

## 5. Build card (FR-032, FR-033)

**Opening**: tap an empty unlocked location. The build card is docked like the machine card, and the two are mutually exclusive.

**Content and preview**:

- Each machine type shows its icon, cost and affordability.
- Available locations are highlighted.
- The selected type shows a ghost preview (frame 0, 60 % alpha) and a text line "Selected: <type> at location N".
- The preview location is 100 % visible.

**Actions**: Confirm and Cancel sit side by side, each at least 44 pt.

**Insufficient Cash**: Confirm is disabled, the line "Need $X more" appears, and nothing changes.

**Success**: build dust plays, the foundation appears, and the card hands over to the new machine's card.

**Tests**: `BuildCardTests`: enough and insufficient Cash, cancel via Back, rotation, preview visibility.

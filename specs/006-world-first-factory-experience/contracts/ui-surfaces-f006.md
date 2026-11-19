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

**As built (T537).** Phone portrait: the strip (Cash, store, production attention, gear) on top and a bottom action row (hub entry "Office" with its attention dot, Overdrive); manual mining stays the overlay at the bottom right of the world, right above the action row. Phone landscape: the strip is the left column, the action bar (hub entry, Overdrive, gear) the right column, and both step aside while a sheet or the machine card is open. Tablet and desktop: the strip above the world with the hub entry before the gear, Overdrive in the side panel header. None of these is inside the world column on a phone, so the layer never covers the framed world. The zoom buttons show on pointer profiles, and on touch only with "Show zoom buttons" (`RunShellDocument.ShowZoomButtonsPreference`). Overdrive's ready state has a thicker accent border and a bold state word. `ShellTab` stays an internal shim of `SelectTab` (the hub cards use it); the tab bar, the RP cell and the tab badges are gone.

**As built (T555, owner decision 2026-11-17 "keep Cash and Overdrive visible").** On a phone in landscape, while the machine card, Details or a sheet (the hub included) is open, the action column (Office, zoom, gear) and the production attention indicator still step aside, but Cash with the store fill and the Overdrive state stay on screen: the strip leaves its column and becomes a one-row band at the top of the world column (`run-shell--status-band`; `PersistentLayer.SetSheetOpen` and `IsStatusBand` move the strip and the Overdrive control, the shell's `IsStatusBandShown` reports it). Overdrive keeps its states (Locked, Ready with the thicker accent border and bold word, On, Cooling down) and is tappable when ready, at least 44 pt. The camera rectangle starts below the band (the same rule as the strip on tablet and desktop), so nothing covers the framed world (OD-9b C1); `PersistentLayerTests` asserts, at 640x360 and 844x390 at 100 and 200 % for the card, Details, a sheet and the hub, that the controls are present, at least 44 pt, outside the camera rectangle, and that the whole factory fits that rectangle at fit-all. Closing the card or sheet returns the strip to the left column and Overdrive to the action column. Also T555: a system view or Details always opens scrolled to its top (a hold ends at the player's first scroll or key input, and the shell's own focus move into a new view no longer scrolls it past its header); the Research graph shows an arrow bar on each edge that has more behind it and its hint sits above the graph; a read-only value (Market Stock, Net rate, Sells for, the build Cost, the capacity lines) and an Industry card carry no button chrome (`scaffold-value`, `MarketPanelParts.NewValueButton`), and a disabled control's reason sits directly below the control in Industry and the build sheet, as in the Market and the hub.

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

**As built (T539).** `BuildCard` (`Assets/IdleFactory/Unity/UI/Panels/BuildCard.cs`, `.uxml`, `.uss`) is docked per screen class like the machine card: the bottom band in portrait, the 40 % column in landscape, the side panel on tablet and desktop. The shell keeps one card frame, so opening one card dismisses the other, and opening the hub closes the build card. Each type row shows icon, name, "Cost: $X" and a status word ("Affordable", "Need $X more", or the unlock reason), with a 3 px border and bold name for the selection, so colour is never the only cue; rows and buttons are at least 48 px (project rule, above the 44 pt minimum). The ghost (frame 0, 60 % alpha) and the 50 % outline of available locations come through `IBuildPreview`/`SlotGridView`; the preview location keeps the full selection frame and stays inside the unobstructed camera rectangle on every class and text scale (OD-9b C1). Confirm and Cancel sit side by side, Cancel on the left; keyboard order is types, Cancel, Confirm. Short of Cash, Confirm is soft-disabled and "Need $X more" shows below the actions and in the tooltip. Confirm passes the shown price to `RunCommands.Build`, so a price change rejects ("You were not charged") and the card stays open with the reason inline. Success plays `BuildDust` at the location and hands over to the machine card at the same slot (static highlight under reduced motion). Back, Escape and Cancel clear the preview. In landscape the status band keeps Cash and Overdrive on screen while the card is open (U-B1). The scene wires the card through `F006SceneWiring` step `buildcard`; with no card registered an empty location falls back to the build sheet and the bootstrap logs a warning.

**As built (T540, milestone banner).** `MilestoneBanner` is an absolute overlay at the top of the world column, placed by `RunShellDocument.PlaceMilestoneBanner` below the strip (when the strip or status band sits in the world column) and below the detail, notice and hint bars. It is not part of the unobstructed camera rectangle, so showing or hiding it never reframes the camera, and it never covers Cash, the store, Overdrive, the hub entry, the gear or the status band. It lasts at most 2.5 s, dismisses on tap (no focus change) or Back (first back frame), and is static under reduced motion. Copy B-01, B-01s (provisional, T547), B-02, B-03 and B-04 come from world-copy.md; a missing key shows "Milestone reached." and warns once. Offline milestones appear only in the offline summary's "Milestones" section. Known trade-off (UI/UX re-check 6a5f525): at 200 % text on small classes the banner is up to about 29 % of the height and may overlay the top of the framed world for its 2.5 s; OD-9b C1 for the selected machine or build ghost is verified on device in the T543 batch-C gate, with the fallback of a single primary line on compact height or large text.

**As built (T558, #771).** The closed-state camera frames the depot column: `FactoryWorldLayout.FitBounds` (content plus the depot sprite, the collector spine and the store-full and shortage marker boxes, not the vein) joins `Bounds` and `ViewBounds`, and `CameraScaleModel.ExtentFor` picks, in order, the whole view when it costs no scale, the depot column when it fits at or above `n_min` (fit-all steps down one whole-number scale instead of clipping it), else the content. `RunCameraFraming.FitAll` uses the same extent. Where the depot column would fall below `n_min` (tablet portrait, 200 % text on 360 x 640, 390 x 844 and 640 x 360) it stays pannable (Art Bible v1.1.1 AR-4, owner decision 2026-11-19); on the phone portrait 1080 x 2400 fit-all now sits at `n_min` (AR-3). The per-frame framing decision lives in `RunViewportDriver`: a card or sheet closed on a different screen than the one it was opened on, or after a fit ran while it was open, re-fits to the closed rectangle (and again next frame) instead of restoring a stale view (C-F2). The milestone banner shows only its main line on compact landscape or at 150 % text and above (`RunShellDocument.BannerPrimaryOnly`); the other lines stay in the accessible text, tooltip and offline summary, and the banner ends above the top machine row on every class (640 x 360 at 200 %: 72 pt, 20.0 %).

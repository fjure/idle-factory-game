# F006 test re-baseline record

F006 test re-baseline record; T515 adds the R19 per-file list and the old→new table.

## Settings

**Decision record (T513, FR-014, research R06): settings-noise exception for `ProjectSettings/TagManager.asset` only.**

- **What changed.** `m_SortingLayers` now reads WorldBase, WorldDeck, WorldLinks, Default, Overlay, Effects. WorldBase, WorldDeck and WorldLinks sit before Default; Effects sits after Overlay. The existing Default (uniqueID 0) and Overlay (1332175961) entries are unchanged. New uniqueIDs: 1713051201, 1713051202, 1713051203 and 1713051204.
- **Why.** R06 and FR-014 need separate sorting layers for the platform base, the deck, the links and the effects, so world art orders without per-sprite order hacks.
- **How.** Through Unity tooling: a temporary `-executeMethod` editor script edited `m_SortingLayers` through the TagManager `SerializedObject` in batch mode and saved. The script was deleted before the commit. No YAML was hand-edited. Other run noise (`ProjectSettings.asset`) was restored.
- **Basis.** Plan: settings-noise exception recorded for TagManager only (plan.md, phase P1). The commit is made with `IDLEFACTORY_ALLOW_SETTINGS_NOISE=1`.
- **Guard.** `Assets/IdleFactory/Tests/EditMode/F006/SortingLayerGuardTests.cs` asserts names, order, validity and the unchanged Default and Overlay ids, and does not rely on `NameToID != 0` (it returns 0 for unknown names, which is also Default's id).

## Core public API (T514)

`Assets/IdleFactory/Tests/EditMode/Fixtures/PublicApi/core-public-api.txt` (the `PublicApiSnapshotTests` baseline) changes only additively for `CompletedCycles` (research R08, OD-11). The file is sorted ordinally, and the diff is 1 changed line and 3 added lines:

| Type | Change | Line |
|---|---|---|
| `MachineStateView` | constructor gains a trailing parameter with default `0` (the fixture renders it as `= 0`) | `ctor public (..., System.Int64 batchFactor, System.Int64 progressPpm, System.Int64 completedCycles = 0)` replaces the 8-parameter line |
| `MachineStateView` | new read-only property | `property public System.Int64 CompletedCycles { get }` |
| `RunSession` | new read method | `method public System.Int64 CompletedCyclesOf(System.Guid instanceId)` |
| `RunSimulator` | new read method | `method public System.Boolean TryGetCompletedCount(System.Guid instanceId, out System.Int32 count)` |

No other line changed. The old 8-argument `MachineStateView` constructor is source compatible (the new parameter defaults to 0), but it is a different method signature for reflection, hence the replaced line. The save format, the golden trace `trace-r3.csv` and every balance fixture are unchanged.

## World layout seam (T512)

T512 (FR-037, FR-038, SC-011, research R04, R22) replaces the column count with a `FactoryWorldLayout` in the camera path. Every re-baselined existing test changed by an **API rename only**. The expected values are identical, the assertions are not touched, and no tolerance moved.

**API mapping.** `L` is `LatticeLayoutBuilder.ShippedForColumns(n)`, the cached F004 12-location layout (3 is portrait, 4 is landscape). Its `Bounds` equals `SlotGridLayout.Extent(n)` bit for bit.

| Old call | New call |
|---|---|
| `new ScaleLayout(w, h, ppp, rect, columns)` | `new ScaleLayout(w, h, ppp, rect, L(columns))` (also `LayoutId`, `Bounds`, `Margin` replace `Columns`, `Key`) |
| `CameraScaleModel.LargestFittingScale(rect, columns, art)` | `LargestFittingScale(rect, L(columns).Bounds, art)` (optional 4th argument `margin`, default `0.5`) |
| `CameraScaleModel.MarginFraction(rect, columns, n, art)` | `MarginFraction(rect, L(columns).Bounds, n, art)` |
| `RunCameraFraming.Layout(camera, columns, ppp, area)` | `Layout(camera, L(columns), ppp, area)` |
| `framing.FitAll(area, columns, ppp)` | `framing.FitAll(area, L(columns), ppp)` |
| `framing.FrameSlot(slot, columns)` | `framing.FrameSlot(slot, L(columns))` |
| `view.SetColumns(columns)` | `view.SetLayout(L(columns))` |
| `view.Columns`, `grid.Columns` | `view.Layout`, or `LatticeLayoutBuilder.ColumnsFor(view.Layout.Orientation)` where a number is needed |

**Per file (old → new).**

| File | Change | Expected values |
|---|---|---|
| `Tests/EditMode/SlotGridLatticeTests.cs` | none: it tests `SlotGridLayout`, which stays as the F004 reference | unchanged |
| `Tests/PlayMode/SlotGridTests.cs` | `view.SetColumns(columns)` → `view.SetLayout(L(columns))` (1 line) | unchanged |
| `Tests/EditMode/CameraScaleModelTests.cs` | the `Layout(...)` helper builds `ScaleLayout` from `L(columns)`; 5 `LargestFittingScale(rect, 3\|4\|columns, 24)` calls take `.Bounds` | unchanged |
| `Tests/EditMode/ScreenClassScaleTableTests.cs` | `Evaluate` and the oversized-HUD negative case: `LargestFittingScale` and `MarginFraction` take `L(columns).Bounds` | unchanged (the n_min, n_fit, levels and margin tables) |

Other callers moved by the same renames (compile only, no value touched): `Tests/PlayMode/ScreenClassScalePlayTests.cs`, `RunHudLayoutTests.cs`, `NavigationInputTests.cs`, `StateCueOverlayTests.cs`, `PresentationAllocationTests.cs`, `PixelCrispnessTests.cs`, `ReducedMotionTests.cs`, `ArtFallbackTests.cs`, `F006/BaselineRig.cs`, `Fixtures/W004V/RenderCaptureHarness.cs` (view and controller calls only; its `LegacyCameraFraming.FitAll(area, columns, ...)` calls keep their column argument).

**Not a pure rename, recorded here.** `Tests/EditMode/JsonContentAuthorityTests.cs`, `NoScriptableObjectInTheUnityAssemblyCarriesSerializedGameplayFields` (the FR-001 authority guard). `WorldCompositionSettings` (R22) is a ScriptableObject in the Unity assembly with serialized fields, so the guard failed on it. The guard now exempts a short explicit list of presentation tuning assets (`PresentationTuningAssets`, one entry) and asserts that every exempt type lives in `IdleFactory.Unity.Presentation`. Gameplay definitions still fail the guard. The exemption is in the test, not in the production code.

**Layout parity evidence.** `WorldLayoutParityTests` compares the seam against `SlotGridLayout` and against `tools/art-pipeline/f006/lattice-geometry.json` (extent, pivots, plate rects, hit diamonds, and `n_fit` for every point screen and device). A full Edit Mode run leaves that file byte-identical (`git diff` is empty), because `LatticeGeometryExportTests` rewrites it from the migrated code.

## Shell navigation seam and the R19 re-baseline list (T515)

**Helper (T515, SC-007, research R19).** `RunShellTestNavigation` in `Assets/IdleFactory/Tests/PlayMode/RunShellTestSupport.cs` has two members. `OpenPanel(shell, ShellTab)` delegates to `shell.SelectTab(tab)`. `EntryControl(shell, ShellTab)` delegates to `shell.TabButton(tab)`. Both behave exactly like the direct calls, so the helper changes nothing today.

**Decision: helper only, no call site migrated.** The edit scope of T515 is the support file and this record, and a migration would touch 33 test files that other work in the batch may also edit (conflict risk) for no behaviour gain. The replacement is mechanical (`X.SelectTab(t)` becomes `RunShellTestNavigation.OpenPanel(X, t)`, `X.TabButton(t)` becomes `RunShellTestNavigation.EntryControl(X, t)`), so the later hub issue can do it in one pass or just change the two bodies for the call sites that adopt it.

**Hub constraints for the later switch (OD-9b, UI/UX C3).** The hub label is "Office". The hub and the card are mutually exclusive and there is no duplicate path to a panel, so once `OpenPanel` navigates through the hub, a test must not also press a tab. Hub rows are 48 dp, so `EntryControl` returns the row and the 44/48 dp target assertions in `RunHudLayoutTests` and `RunTextScaleTests` move to that size. T515 implements no hub.

**Panel tests that use the tab seam directly.** 33 Play Mode files (counts: `SelectTab` calls / `TabButton` calls; `OpenTabAtSection` is not called by any test):

| File | SelectTab | TabButton |
|---|---|---|
| `ContractsAutoDeliverPanelTests` | 2 | 6 |
| `ContractsPanelTests` | 1 | 0 |
| `ExpansionPanelTests` | 1 | 0 |
| `F006/BaselineRig` (`ShowTab`) | 1 | 0 |
| `F006/InteractionCountJourney` | 0 | 2 |
| `F006/ShellGeometryProbeTests` | 1 | 0 |
| `GoalsLinePanelTests` | 9 | 0 |
| `IndustryPanelTests` | 1 | 0 |
| `InventoryRatesPanelTests` | 1 | 0 |
| `ItemsPanelTests` | 2 | 0 |
| `MachinePanelGuidanceTests` | 0 | 1 |
| `MarketPanelTestSupport` | 1 | 0 |
| `MarketSellPanelTests` | 2 | 0 |
| `OfflineSummaryPanelTests` | 0 | 2 |
| `OreVeinPanelTests` | 6 | 2 |
| `OverdriveControlTests` | 1 | 0 |
| `PanelTitleWrapTests` | 1 | 0 |
| `PrestigePanelTests` | 1 | 0 |
| `PrioritiesPanelTests` | 1 | 0 |
| `ResearchPanelScrollTests` | 1 | 0 |
| `ResearchPanelTests` | 1 | 0 |
| `RunDeviceProfileTests` | 1 | 1 |
| `RunHudLayoutTests` | 10 | 4 |
| `RunKeyboardFocusTests` | 4 | 17 |
| `RunPanelCompositionTests` | 0 | 6 |
| `RunShellFocusAndLabelsTests` | 3 | 0 |
| `RunTextScaleTests` | 4 | 4 |
| `SessionLogPanelTests` | 3 | 0 |
| `SettingsSheetTests` | 1 | 0 |
| `SlotGridTests` | 1 | 0 |
| `TextScaleLayoutTests` | 1 | 0 |
| `UiIconSurfaceRig` | 6 | 0 |
| `UiReviewFixesTests` | 7 | 2 |

Totals: 75 `SelectTab` lines and 47 `TabButton` lines (production `RunShellDocument` and the panel presenters are not counted). Counts come from `git grep` on the base commit.

**R19 per-file re-baseline list.** Status is `pending` until the owning issue records the old→new diff in the table below. T512 already recorded its renames above, T513 and T514 their own.

| Group | Files | Owner / status |
|---|---|---|
| Six tabs and `HandleBack` | `RunHudLayoutTests` (L171–186), `RunKeyboardFocusTests`, `RunPanelCompositionTests`, `GoalsLinePanelTests`, `UiReviewFixesTests`, `UiIconSurfaceRig`, `RunTextScaleTests`, `OreVeinPanelTests`, `ContractsAutoDeliverPanelTests`, the panel tests in the table above (about 35 files, via the `RunShellTestNavigation` seam), `AndroidBackGestureTests` | hub issue, pending |
| Zoom buttons | `NavigationInputTests`, `RunDeviceProfileTests`, `RunTextScaleTests`, `PixelCrispnessTests`, `DevToolsOverlayPlacementTests` | pending |
| 12 slots and columns | `SlotGridLatticeTests`, `SlotGridTests`, `CameraScaleModelTests`, `ScreenClassScaleTableTests`, `StateCueSetTests`, `F004AssetContractTests` | T512 (API renames recorded above), `StateCueSetTests` and `F004AssetContractTests` pending |
| Preferences | `DisplayPreferencesStoreTests` | pending |
| Core API | `core-public-api.txt` | T514, done (section above) |
| Emulator | `scripts/android-emulator-journey.ps1`: Default, F003, F004 and F005 scenarios move from bottom-bar fraction taps to a hub-entry tap helper; F006 is added to `$knownScenarios` | pending |

**Old→new table template (one row per changed test or assertion).** A re-baseline changes a test only through a recorded row; an expectation change needs a reason that cites the requirement.

| File | Test / line | Old | New | Reason (FR/SC/decision) | Expectation changed? | Issue |
|---|---|---|---|---|---|---|
| _example_ `RunHudLayoutTests.cs` | `Name` / L171 | `shell.TabButton(ShellTab.Market)` | `RunShellTestNavigation.EntryControl(shell, ShellTab.Market)` | R11, OD-9b | no | #nnn |

## Machine card, Details and camera view extent (T534)

T534 (FR-016 to FR-019, FR-024, SC-002, research R10) adds the docked `MachineCard`; the machine panel becomes its Details view. A machine tap opens the card when one is registered. Without a registered card, `OpenSlot` opens the machine panel directly as before, so every test that registers only `MachinePanelPresenter` is unchanged (`MachinePanelTests`, `MachinePanelGuidanceTests`, `BuildPricePanelTests`, `UiIconSurfaceRig`, the `ShellRig` panel suites).

**Re-baselined tests (the scene wires the card, so bootstrap-based tests now see it).**

| File | Test / line | Old | New | Reason | Expectation changed? | Issue |
|---|---|---|---|---|---|---|
| `RunPanelCompositionTests.cs` | `AllPanelIds` (4 tests: every id registered once, rebuild, failed panel, blocked import) | 8 ids | 9 ids, adds `ShellPanelIds.MachineCard` | the card is a registered panel (R10) | yes, count +1 | #706 |
| `RunPanelCompositionTests.cs` | `EverySlotTapOpensTheBuildSheetOrTheMachinePanel...` (2 lines) | `ActivePanelId == Machine` | `shell.IsCardOpen` | FR-016: an occupied location opens the card; the build sheet hands over to the new machine's card | yes | #706 |
| `RunPanelCompositionTests.cs` | `AVisiblePanelRefreshesWhenTheRunChanges` | `OpenSlot(0)` shows the machine panel | `OpenSlot(0)` then `OpenDetails(0)` | the panel is the Details view | no (same panel asserted) | #706 |
| `SlotGridTests.cs` | `ATapOpensTheBuildSheet...` (2 lines, 1 line removed) | machine panel active, root mounted in `PanelContent` | card open | as above; the panel is mounted only when Details opens | yes | #706 |

**New tests.** `MachineCardTests`, `MachineDetailsParityTests`, `CardCameraTests`, `KeyboardTraversalTests`, `MachineCardPlayerTests` (`StandalonePlayer`), `ViewBoundsTests`, `CardBackFrameTests`. `ShellNavigationStack` gained `HasCard`/`PopCard` as default interface members, so `ShellNavigationStackTests` is unchanged.

**Decisions.**
- **P2-F16 (scaffold root):** a docked variant by composition. The card root carries the `scaffold` class and `MachineCard.uss` overrides the full-screen fill with `.scaffold.mcard` (relative, no inset, surface background, no header). `PanelScaffold.*` is untouched. Buttons use the scaffold classes and `PanelScaffold.SetDisabled` with its reason label and tooltip (P2-F19).
- **P2-F19:** the shell notice bar lies in the world column and never overlaps the card (tested at every class at 200 %). Scaffold notices (up to 3) are not used by the card.
- **P2-F10, P2-F12, P2-F13:** the card owns one `MachinePresentationModel`, refreshes it every frame from cached snapshots, reads `LimitingResourceId` and `AttentionModel.ShortOnCopy` from it, and calls `ResetModels` (baseline reset, offline suppression); a session replace rebuilds the card with the panels. Tap priority between attention markers and machine shapes is not touched (no marker is a hit target yet): carried by T528.
- **Items and boosts (FR-018):** the F003 to F005 machine panel had no Item or boost entry point, so nothing is lost; none was added.
- **Effects (FR-034):** `EffectPool` is T529. The card calls the `IMachineEffects` seam; with no sink wired, and always under reduced motion, it shows a static one-second highlight (tested).
- **Keyboard:** one focusable location stop stands for the world locations (arrows move its cursor in `SortKeyOf` order, Enter or Space opens). On tablet and desktop Overdrive sits in the panel header and the tab bar after the card until T537 rebuilds the persistent layer.

**Camera view extent (P1-F2).** `ScaleLayout.ViewBounds` (default `Bounds`) and `FactoryWorldLayout.ViewBounds` (content, depot, deposit). Pan limits always use it; fit-all and centring use it only when it keeps the integer fit scale. Selection framing uses `MachineRectOf` (plate plus badge band), not only the plate footprint.

**Measured, SC-002 guidance (unobscured world share with the card open).** Portrait at 200 % text 0.42 (45 % hard cap), landscape 0.52 to 0.57 (the specified 40 % column), tablet and desktop above 0.70. Compact phones are below the 50 % and 70 % guidance by design of the cap and column; asserted floors are 0.40, 0.50 and 0.70. Depot and vein visibility per class is written by `CardCameraTests` as `T534-VISIBILITY` lines in the test result output (P2-F6 camera part; the composite proof stays with T527).

**Depot and vein visibility at fit-all (measured by `CardCameraTests`, P1-F2, P2-F6 camera part).** "Partly" is the share of the sprite extent inside the view; the player can still pan to both (pan extent). The composite proof stays with T527.

| Screen class | Text | n_fit | Depot | Vein |
|---|---|---|---|---|
| desktop 1280x720 | 100 % | 2 | in view | in view |
| desktop 1280x720 | 200 % | 2 | partly (89 %) | partly (94 %) |
| desktop 1920x1080 | 100 % | 4 | partly (67 %) | partly (70 %) |
| desktop 1920x1080 | 200 % | 4 | partly (60 %) | partly (61 %) |
| phone-landscape 640x360 | 100 % | 1 | in view | in view |
| phone-landscape 640x360 | 200 % | 1 | in view | in view |
| phone-landscape 844x390 | 100 % | 1 | in view | in view |
| phone-landscape 844x390 | 200 % | 1 | in view | in view |
| phone-portrait 360x640 | 100 % | 1 | partly (70 %) | partly (73 %) |
| phone-portrait 360x640 | 200 % | 1 | partly (40 %) | partly (39 %) |
| phone-portrait 390x844 | 100 % | 1 | partly (89 %) | partly (94 %) |
| phone-portrait 390x844 | 200 % | 1 | partly (59 %) | partly (60 %) |
| tablet-landscape 1024x768 | 100 % | 2 | partly (24 %) | partly (20 %) |
| tablet-landscape 1024x768 | 200 % | 1 | in view | in view |
| tablet-portrait 768x1024 | 100 % | 1 | in view | in view |
| tablet-portrait 768x1024 | 200 % | 1 | partly (70 %) | partly (73 %) |

## Hub ("Office"), back order and P3-A carried items (T535)

T535 (FR-020 to FR-022, FR-024, research R11, OD-9b C3 to C6) adds `HubPanel` (registry id `hub`), the additive `ShellPanelIds.Industry` id, the "Office" entry, and `HubAttention`. The six-tab bar stays until T537 removes it, so `SelectTab` and `TabButton` keep working and **no panel test was migrated to the hub**; the two `RunShellTestNavigation` bodies are unchanged (hub-then-row navigation is new in `HubPanelTests` and `BackOrderTests`). The hub and the tab bar are two entries to the same registry views.

**Re-baselined tests (old to new).**

| File | Test / line | Old | New | Reason | Expectation changed? | Issue |
|---|---|---|---|---|---|---|
| `Tests/PlayMode/MachinePanelTests.cs` | `TheActiveToggleSwitchesTheMachineOffAndOnAndKeepsProgress` | a switched-off machine lists the blocker "This machine is switched off." with a second "Switch on" button (`BlockerButtons.Count == 1`) | no blocker row and no second button; the one Switch on is `ActiveButton` | A-3 (U-A8) | yes | #707 |
| `Tests/PlayMode/MachineCardTests.cs` | `AnUnaffordableUpgradeShowsTheShortfallInsideTheButtonAndKeepsItsReason` | after a press the inline line read "Need $... more" (the same text the button already carries) | after a press it reads the rejection copy ("Not enough Cash."); before a press it is hidden | A-4 (U-A6): the shortfall shows once | yes | #707 |
| `Tests/PlayMode/RunPanelCompositionTests.cs` | `AllPanelIds` (4 tests: every id registered once, rebuild, failed panel, blocked import) | 9 ids | 11 ids, adds `ShellPanelIds.Industry` and `ShellPanelIds.Hub` | the bootstrap composes the Industry view and the hub (R11) | yes, count +2 | #707 |
| `Tests/PlayMode/Fixtures/Screenshots/F004-B2/*.png` (36 frames: `w003s-*` and `w004v-*`, world and shell, 9 screen classes) | `RenderParityTests.TheCapturedFramesMatchTheCommittedPostSwitchBaselines` | committed post-switch frames without the entry | frames regenerated through the harness' own write mode (`IDLEFACTORY_RENDER_BASELINE_WRITE=1`, no hand edit); the shell frames show the "Office" entry, the world frames follow the camera rectangle (the strip is 4 dp higher where it holds the 48 dp entry, and phone portrait has the entry row) | FR-020; the visible persistent layer changed | yes (images) | #707 |
| `Tests/EditMode/AndroidBackGestureTests.cs` | new `TheHubIsPoppedByTheSameShellHandleBackPathAsEveryOtherFrame` | none | pins that the hub is one more sheet frame of the single `HandleBack` path with no back handling of its own | R11, FR-021 | n/a, added | #707 |

No other existing assertion changed (the 36 world and shell frames were re-captured, not edited; the comparison tolerance is unchanged). In particular the layout suites (`RunHudLayoutTests`, `RunTextScaleTests`, `MachineCardTests` first view at 640x360 and 200 %, `CardCameraTests`) pass unchanged with the entry present, because of where the entry lives (below). `HubPlayerTests` checks both new ids in the packaged scene.

**Where the hub entry lives (and why).** Nothing in the strip could give up width or height without breaking a recorded invariant, so the one `hub-entry` button is re-parented by the shell (`PlaceHubEntry`, like the Overdrive control):
- phone portrait (the strip's four controls already fill 360 pt at the worst-case values): a 48 dp row of its own at the top of the world column; the camera frames below it like a notice bar (`HubEntryOnItsOwnRow`); it steps aside while a sheet is open, because the sheet has its own Close;
- phone landscape from 120 % text (the strip wraps, and a fifth control adds a whole line that the machine card cannot give up: Upgrade must stay in the first view at 640x360 and 200 %): an overlay at the bottom of the world column beside the zoom column, 48 dp, no flow;
- every other class and scale: in the strip, before the gear, 48 dp.

The entry always shows the icon and the word "Office" (tested on every class at 100 and 200 %). T537 rebuilds the persistent layer and chooses the final place. The camera's rectangle excludes the landscape overlay (OD-9b C1, fix round U-1): like the ore vein, it ends above the entry or starts right of it, whichever leaves the larger fitting scale (`TheLandscapeEntryNeverCoversTheRectangleTheCameraFrames`, 640x360 and 844x390 at 120 and 200 %). Closing a system view that a hub card opened returns focus to the entry (U-3, `ClosingASystemViewOpenedFromTheHubReturnsFocusToTheEntry`).

**Decisions.**
- **P2-F16 (scaffold root):** the hub uses the scaffold's classes and rows by composition, like the card: the hub root carries `scaffold` and `HubPanel.uss` overrides the full-screen fill with `.scaffold.hub` (relative, no inset, transparent). It is a registered shell panel, so the shell's own sheet (phone portrait) or side panel (other classes) holds it; the sheet header with Close belongs to the shell. `PanelScaffold.uss` changed only additively.
- **P2-F17:** `PanelScaffold` gained a sheet mount point (`scaffold-sheet`, `PanelScaffold.Sheet`) and the shared `.scaffold-row` class (`PanelScaffold.RowClass`, 48 dp, a `run-control`, so the shell focus ring applies). The hub rows use `.scaffold-row`. The mount point is exercised by `TheScaffoldStateCardOffersNoBackWhenTheRootHidesIt`; the hub itself mounts into the shell sheet.
- **P2-F19:** the scaffold state card's Back now follows `BackVisible`. The hub offers no Back at its root (the shell sheet has Close). Every disabled card passes its `reasonLabel` (visible text) and tooltip. A notice of each kind at 640x360 and 200 % leaves the layout, the cards and Close usable (`ThreeNoticesLeaveTheCardsAndTheCloseControlUsableAtTheWorstCase`; the shell shows one notice at a time, so the three bars are notice, detail line and hint).
- **Hub entry glyph (P2-F9, P2-F22; T554 approved the new "Office" glyph):** the icon is the single constant `HubCards.EntryIconKey`. **Fallback in use: the contracts glyph (`F003-UI-ICON-TAB-CONTRACTS`).** The new glyph (`art-source/f006/ui/f006-ui-office`) is not imported: the runtime icon lookup is the scene's `PresentationSpriteCatalog`, the F006 catalog rows are not registered in the scene yet (A-13, T527), and the F006 importer has no UI-icon family, so an import would need a new family, importer rules, atlas and contract-test changes in shared art tooling: out of proportion for this Issue. The swap is one constant plus a catalog row. **When it lands, the hub entry icon must stay distinguishable from the Contracts card icon** (the card keeps `F003-UI-ICON-TAB-CONTRACTS`, `HubCards.IconKeyOf`).
- **ContractsShellFeed re-point:** the feed writes `shell.Attention.SetContracts(n)` (the hub's Contracts card flag and the entry dot). Until T537 removes the tab bar the shell mirrors the count to the Contracts tab badge, so `UiReviewFixesTests` and `ContractsAutoDeliverPanelTests` are unchanged. Research, Prestige and Overdrive flags come from `HubAttentionFeed` (existing snapshots and the prestige preview only; no new rule, A-6). Unspent IP keeps the Research flag (the former tab dot).
- **Back order:** the hub, the Industry view and Settings are single frames of the existing `ShellNavigationStack` (sheet frame). A system view opened from the hub replaces it as the one sheet, so one Back returns to the factory (FR-021).
- **Scene:** two serialized fields (`hubPanelTree`, `hubPanelStyles`) were added to the bootstrap and set through a temporary `-executeMethod` editor script (deleted before the commit). The scene diff is those two lines.
- **`RunShellDocument.cs`:** it grew by about 20 lines of wiring (the partial class declaration, a few one-line hook calls, the unobstructed-rect bar). All hub logic is in `HubPanel.cs` and the new `RunShellDocument.Hub.cs`.

**P3-A carried items.**

| Id | Action |
|---|---|
| A-1 | The card footer is one column with `Details ›` first and the cause line below it in every class, so Details spans the footer width instead of being squeezed beside the cause line. Covered by `DetailsKeepsOnePlaceIsNeverASliverAndTheShortfallShowsOnce` on every class at 100 and 200 % (including 640x360 at 200 %). |
| A-2 | Same change: Details keeps its place relative to Upgrade when the cause line appears (the cause grows below it). |
| A-3 | The Details view no longer lists the "switched off" blocker nor its second "Switch on" button for an inactive machine (`MachinePanelPresenter`, one loop guard). |
| A-4 | Done: the shortfall shows once, inside the Upgrade button (FR-016); the inline line is only the rejection a press answers with. |

**New tests.** `HubPanelTests`, `BackOrderTests`, `HubPlayerTests` (`StandalonePlayer`), `HubTestSupport` (harness), and the cases named above.

## T536 (#708): panel consistency pass

**Re-baselined tests (old to new).**

| File | Test / line | Old | New | Reason | Expectation changed? | Issue |
|---|---|---|---|---|---|---|
| `Tests/PlayMode/PanelScaffoldTests.cs` | `NoticesLayerAboveTheBodyAndHoldAtEveryClassAndScale` | the notice layer overlapped the body (`Intersects(notice, body)` was true) | the layer takes its own room below the body, so a notice never covers a body control (`Intersects` is false); it is still the last sibling and still wins hit testing | P2-F19: up to three notices at 640x360 and 200 % must leave the primary action and Back usable | yes | #708 |

No other existing assertion changed. In particular the layout suites of every panel (`ContractsPanelTests`, `ContractsAutoDeliverPanelTests`, `ItemsPanelTests`, `ResearchPanelTests`, `IndustryPanelTests`, `PrestigePanelTests`, `SettingsSheetTests`, the Market suites, `OfflineSummaryPanelTests`, `StartupStatusPanelTests`, `TextScaleLayoutTests`, `RunTextScaleTests`, `MachineCardTests`, `HubPanelTests`) pass unchanged with the scaffold scope in place. One first run failed `ContractsAutoDeliverPanelTests.ABonusWindowCountsDownAndEndsWithoutAJumpInTheLayout` (the card shrank by 1.5 px when Deliver turned disabled, because the scaffold's disabled look carries 1 px top and bottom borders against the control's 1.5 px). That was a production defect of the new disabled look, fixed in `PanelScaffold.uss` (a hosted control keeps its top, bottom and right border widths and only the left bar grows); the test is unchanged.

**Decisions.**
- **One scope class, no per-panel edits of the look.** The shell adds `scaffold-scope` to every panel root it mounts (`RunShellDocument.MarkScaffoldScope`), to the confirmation dialog card and to the notice line; the two status surfaces carry it from construction. `FactoryRun.uss` imports `PanelScaffold.uss` (`@import`), so the scaffold rules reach every shell descendant and no bootstrap change or scene field is needed (the status surfaces import it from their own sheets because they can be mounted before the shell exists). A hosted button keeps its `run-button` look and spacing (destructive and primary fills included) and takes only the scaffold's 48 pt floor (`.scaffold-scope .scaffold-button`) and the one disabled look (`scaffold-button--disabled`, kept in step by `RunShellDocument.SetSoftDisabled`).
- **48 pt floor.** The value buttons of the Market inventory (`MarketPanelParts.NewValueButton`) and the status surfaces' controls are raised from 44 to 48 pt.
- **Empty templates.** Items and Contracts show the scaffold's empty template (`scaffold__state--empty`: marker, title "Nothing here yet", their own message) through `ScaffoldEmpty`; the hub and `PanelScaffold` already use it.
- **P2-F18.** The loading mark is three separate blocks, not a bar filled to 33 %; it is static in every motion mode (UI/UX C5), so reduced motion needs no other form.
- **P2-F19.** The scaffold notice layer is in flow below the body (at most 70 % of the stage), not an overlay. `SurfaceConsistencyTests.ThreeNoticesLeaveBackAndThePrimaryActionUsable` proves it on three screen classes at 100 and 200 % (640x360 at 200 % included). The shell's own notice line is one bar at a time (unchanged).
- **#555.** `StartupStatusPanel` watches the reduced-motion state it was given (every 250 ms, a read of the bootstrap's cached value) and calls `ApplyReducedMotion` on a change, so the spinner stops and resumes while the surface is open. `ReducedMotionTests.TheStartupSpinnerFollowsTheReducedMotionStateWhileTheSurfaceIsOpen` covers both directions.
- **HubAttentionFeed (T535 Architect F3).** The per-frame path is a revision compare; the Research and Overdrive reads walk the catalog and the state directly (no snapshot is built). Only the Prestige preview still allocates (the domain call returns a new object; a domain change is out of scope) and it is re-read at most once per timeline second.
- **Locked reasons (T535 Product Owner note).** The Overdrive card reads "Overdrive unlocks after <condition>." in the shared `UnlockCopy` wording, and the Prestige card reads "Not ready yet: needs Factory Value <threshold>".
- **A-6 (panel part).** Details opens at the top of the sheet scroll (`MachinePanelPresenter.OnShown`).

**New tests.** `Tests/PlayMode/F006/SurfaceConsistencyTests.cs` (scaffold scope, classes, 48 pt floor and disabled look on Market with inventory, priorities and expansion, Contracts, Items, Research, Industry, Prestige and Settings; the empty templates; the hub's scope and error state; the dialog and the notice line; the Offline summary and the start-up status; three notices at the worst case) and the #555 case in `ReducedMotionTests`.

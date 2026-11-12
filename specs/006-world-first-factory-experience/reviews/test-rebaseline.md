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

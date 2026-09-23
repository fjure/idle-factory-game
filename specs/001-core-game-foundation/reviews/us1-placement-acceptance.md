# US1 placement acceptance — T017

- Date: 2026-09-23
- Feature: `001-core-game-foundation`
- Issue: [#17](https://github.com/fjure/idle-factory-game/issues/17)
- Test environment: Unity `6000.6.0f1`, Windows Editor, repository-owned Play Mode/Edit Mode runner.

## Result

The authored development scene loads and initializes one application session. Automated Editor acceptance passes for mouse/touch gesture semantics, world/UI input ownership, valid placement, overlap and bounds rejection, empty selection, interruption cancellation, and view recreation without changing the authoritative factory snapshot. The existing application/domain tests cover persistent instance identity, independent mutable state, and alternate footprint bounds.

These results establish the development Editor path. No physical Android/iOS device or manual desktop Game View session was exercised here; device-specific safe-area, rotation, and suspend evidence remains assigned to T038. No mobile support claim is made from the Editor tests.

## Acceptance matrix

| Spec scenario | Result | Evidence |
|---|---|---|
| US1.1 — Pan and zoom with mouse and touch | PASS, automated semantics | `NavigationInputTests.MouseClickDragAndWheelProduceDistinctSemanticIntents`, `OneTouchTapAndPanUseTheSameWorldSemanticsWithoutConfirmingGameplay`, `SecondWorldTouchCancelsCandidateAndConsumesEveryPinchRelease`, and `CameraPanClampsForRepresentativeViewportAspects`. The scene test also verifies blank factory space is world-owned while the Build control is UI-owned. |
| US1.2 — Preview complete footprint and non-color validity | PASS | `NavigationPlacementTests.PlacementFlowConfirmsExactlyOnceAndInvalidOrCancelNeverConsumesAnId`, `PreviewBadgeUsesCompleteOrientedFootprintBoundsAndAuthoritativeCells`, and `UnsupportedOrientationRendersInvalidFeedbackWithoutMutation`. |
| US1.3 — Confirm one identifiable building and occupy its footprint | PASS | `CoreGameFoundationSceneTests.AuthoredSceneRunsPlacementInputAndViewRecreationWithoutChangingWorldOwnership` loads the actual scene, confirms placement at `(1,1)`, and observes one building plus all four occupied cells. `PlacementApplicationTests.ValidPlacementPublishesOnceAndReturnsStableDefensiveResult` covers application publication. |
| US1.4 — Reject overlap and out-of-area placement without mutation | PASS | The scene test rejects overlapping `(2,2)` and out-of-area `(15,15)` previews and verifies unchanged revision, building count, and occupancy. Domain coverage: `PlacementTests.OneCellOverlapRejectsEntirePlacement` and `CompleteFootprintCrossingAnyFactoryEdgeIsRejected`. |
| US1.5 — Same-type placements retain distinct identity and state | PASS, domain/application tests | `PlacementTests.SameDefinitionPlacementsUseIndependentIdsAndZeroInitializedMutableGraphs`. |
| US1.6 — Select and clear selection without simulation mutation | PASS | The scene test clears selection on an empty cell, checks the empty-selection feedback, and verifies the factory revision is unchanged. |
| US1.7 — Alternate area and footprint follow declared bounds | PASS, domain/application tests | `PlacementTests.AlternateNineBySevenFactoryAcceptsItsDeclaredThreeByOneFootprint`, `AlternateThreeByOneFootprintRejectsPartialOverlapWithoutMutation`, and `AlternateThreeByOneFootprintRejectsCrossingBoundsWithoutMutation`; `PlacementApplicationTests.PreviewUsesAlternateAndNegativeFixtureDefinitionsRatherThanD001Dimensions`. |
| Cancel, focus loss, and orientation change | PASS | `NavigationPlacementTests.PlacementFlowConfirmsExactlyOnceAndInvalidOrCancelNeverConsumesAnId`, `NavigationInputTests.FocusOrientationAndDisableInterruptWorldGestures`, and the actual-scene test's focus/orientation cancellation checks preserve the snapshot revision. |
| View recreation | PASS | The actual-scene test recreates `FactoryView`, retains selection by instance ID, and verifies revision/building/occupancy counts are unchanged. Existing coverage: `NavigationPlacementTests.FactoryViewUsesStableIdsBadgesSortingAndCleansStaleSelectionAndObjects`. |
| Responsive layout and keyboard-accessible controls | PASS, simulated viewport layouts | `NavigationPlacementTests.HudUsesAttachedAuthoritativeAssetsResponsiveClassesAndNavigationCallbacks` checks `360×640`, `640×360`, `390×844`, `768×1024`, `1024×768`, `1280×720`, and `1920×1080`, safe-area padding, focus, keyboard activation, and 44-pixel minimum controls. This is layout evidence, not physical-device evidence. |
| Invalid bootstrap references | PASS | `CoreGameFoundationSceneTests.MissingSerializedReferencesDisableBootstrapWithActionableError` verifies the bootstrap disables itself and records an actionable error. |

## Automated run

Command:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-unity.ps1
```

The complete suite ran with 190/190 Edit Mode and 36/36 Play Mode tests passing, no skips, and no runner-detected compiler or console errors. Raw results and logs are in `test-results/unity/20260923T185216226Z/` (ignored local test output).

Unity MCP was reachable in this session, but its reported project root was the main checkout. The T017 scene is in the isolated Issue #17 worktree, so scene verification used the repository-owned Unity CLI runner against that worktree.

## Remaining device evidence

Manual mouse and physical touch journeys, named Android/iOS devices, rotation and safe-area behavior on hardware, and suspend/lock/return are not claimed by this record. Track those separately under T038/T039. T017's automated scene, interaction, and Editor layout evidence is complete.

# Feature 004 re-baselined tests (SC-006)

Spec SC-006: F003 gameplay, content, save and UI suites pass unchanged in behaviour; re-baselined zoom tests are listed explicitly. This file is that list. It is the name-level diff of `baselines-b1-test-names.txt` (B1, main `ee92f1f`) against `final-test-names.txt` (final, this branch), per test case (a parameterised case counts once per parameter set). Both lists were compared without the result column; all final cases are `Passed`.

Totals: Edit Mode 2323 -> 2566 (246 added, **3 removed**); Play Mode 2230 -> 2572 (372 added, **30 removed**). 33 removed names in total: **33 mapped to a replacement, 0 without replacement**. The commits come from `git log -S"<old method name>"` on `Assets/IdleFactory/Tests`. The reason column is read from the old and new method names and parameters; the assertion-body details were not re-read.

Behaviour that is unchanged: no removed name is a deletion of coverage. Every removal is a rename or a re-baselined expectation of the same check, which is why the old name is missing from the final list.

## Edit Mode (3 removed)

| Removed (B1) | Replacement (final) | Reason | Commit / Issue |
|---|---|---|---|
| `PresentationSpriteCatalogTests.ARequiredF001KeyThatIsMissingFailsValidationAndNamesTheKey` | `PresentationSpriteCatalogTests.ARequiredRowWhoseKeyIsMissingFailsWithTheUnresolvedCodeAndNamesTheKey` | Renamed: per-key registry (F001 keys are now optional rows, a missing key of a required row yields the unresolved code). Same check. | df72571, #541 (T428) |
| `PresentationSpriteCatalogTests.F003KeysAreAcceptedAsExtraEntries` | `PresentationSpriteCatalogTests.F003KeysAreAcceptedWithTheirRegistryShape` | Renamed: F003 keys are validated against their registry shape instead of being "extra entries". | df72571, #541 (T428) |
| `PresentationSpriteCatalogTests.TheWrongApprovedSpriteForAKeyFails` | `PresentationSpriteCatalogTests.ASpriteOfAnotherNameWithTheRightShapeIsAccepted` (with `AWrongPivotFailsValidationAndNamesTheKey`, `AWrongWorldSizeFailsValidationAndNamesTheKey`) | Superseded: validation moved from the approved sprite name to the key's shape (pivot, world size), so a differently named sprite of the right shape is now accepted and the shape failures are asserted separately. | df72571, #541 (T428) |

## Play Mode (30 removed)

| Removed (B1) | Replacement (final) | Reason | Commit / Issue |
|---|---|---|---|
| `NavigationInputTests.CameraZoomIsAnchoredAndClampedToReplaceableLimits` | `NavigationInputTests.CameraZoomStepsWholeLevelsInstantlyAndStopsAtTheAllowedRange` | Re-baselined zoom test: continuous anchored zoom replaced by integer whole-level camera scale steps. | ddcda74, #539 (T427) |
| `SlotGridTests.ExtentsMatchTheArtBible` | `SlotGridTests.ExtentsMatchTheLatticeAtD24` | Renamed and re-baselined to the 70x54 isometric lattice with D = 24. | e4c8cbc, #592 (T456) |
| `SlotGridTests.FitAllReproducesTheArtBibleTableAndStaysAboveTheReadableFloor(...)`, 8 parameter sets | the same method name with the new expected values (see below) | Re-baselined expectation: the fit-all scale table changed with the lattice (last parameter, e.g. 129.0f -> 123.2f). Re-parameterised, so each old set shows as removed and each new set as added. | e4c8cbc, #592 (T456) |
| `SlotGridTests.HitTestResolvesEveryPointOfASlotAndTheGapsToTheNearestSlot(3)`, `(4)` | `SlotGridTests.HitTestIsFootprintOnlyOnTheDiamondAndNeverOverlaps(3)`, `(4)` | Re-baselined: footprint-only diamond hit shape replaces the nearest-slot rule. | e4c8cbc, #592 (T456) |
| `SlotGridTests.SlotsFollowReadingOrderFromTheTopLeft(3)`, `(4)` | `SlotGridTests.SlotsFollowReadingOrderFromTheTopLeftOnTheLattice(3)`, `(4)` | Renamed: same reading-order check on the isometric lattice. | e4c8cbc, #592 (T456) |
| `MachinePanelGuidanceTests.UsingAFixButtonMovesFocusToTheActiveToggleInsteadOfLosingIt(...)`, 16 parameter sets | `MachinePanelGuidanceTests.UsingAFixButtonMovesFocusToTheFr030TargetInsteadOfLosingIt(...)`, the same 16 parameter sets | Renamed and re-baselined: focus target follows the FR-030 focus order (machine panel hint by resource). | a9dbdd0, #556 (T450) |

`FitAll` parameter sets (old -> new expected last parameter): (1560, 1032, 4) 129.0 -> 123.2; (360, 536, 3) 45.0 -> 36.9; (390, 740, 3) 48.8 -> 40.0; (408, 976, 3) 51.0 -> 41.8; (640, 256, 4) 32.0 -> 33.0; (664, 720, 4) 63.2 -> 52.4; (844, 286, 4) 35.8 -> 36.9; (920, 672, 4) 84.0 -> 72.6.

Counts: Edit Mode 3 removed = 3 mapped. Play Mode 30 removed = 1 NavigationInput + 1 Extents + 8 FitAll + 2 HitTest + 2 ReadingOrder + 16 MachinePanelGuidance = 30 mapped. Total 33 mapped, 0 "no replacement", 0 unmapped.

## Added names by class (counts)

Edit Mode (246): DisplayPreferencesStoreTests 72, F004AssetContractTests 49, CameraScaleModelTests 37, PresentationAnimationClockTests 18, RenderPipelineConfigurationTests 16, StateCueSetTests 15, SlotGridLatticeTests 14, PresentationCatalogFallbackTests 10, ScreenClassScaleTableTests 8, PresentationSpriteCatalogTests 4 (the 3 replacements above plus `AMissingF001KeyIsLegalBecauseTheF001TableIsOptional`), UiIconIntegrationEditTests 3.

Play Mode (372): RunHudLayoutTests 60, UiIconIntegrationTests 48, StateCueOverlayTests 48, SlotGridTests 29 (13 replacements above plus 16 new floor, plate and tap-target cases), TextScaleLayoutTests 26, MachinePanelGuidanceTests 26 (16 replacements plus 10 new), RenderParityTests 21, ScreenClassScalePlayTests 19, SettingsSheetTests 17, NavigationInputTests 17 (1 replacement plus 16 new cases), ContractsPanelTests 16, PanelTitleWrapTests 12, PresentationAllocationTests 8, PixelCrispnessTests 6, OfflineSummaryPanelTests 5, ArtFallbackTests 5, ReducedMotionTests 4, ReducedMotionBootstrapTests 4, UiIconBlindNamingCaptures 1.

Reproduce: split each list by its `#` header, strip the `| Passed` column and `\r`, `sort`, then `comm -23 b f` (removed) and `comm -13 b f` (added).

# Feature 003 full-suite evidence

This record holds the suite evidence of the Feature 003 retirement and verification tasks. T119 adds the retirement inventory (QA-10). T120 adds the final full-suite and player evidence.

## Retirement inventory

**Task:** T119 (Issue #201), R16, GD-1, QA-10, FR-031.
**Base:** `origin/main` at `c7fb57a`. Unity `6000.6.0f1`, Edit Mode and Play Mode through `scripts/test-unity.ps1` in the issue worktree, Standalone build target.

T119 retires the Feature 001 runtime path. The Iron factory run (`RunSession`, `RunCommands`, `RunPersistence`, run shell and slot grid) is the only runtime the bootstrap composes. The Feature 001 domain, content v1, Save V1 and offline-reconciliation code stays as legacy import support with its Edit Mode suites. `RunAuthorityTests` proves the result.

### Suite counts

| Suite | Before (`c7fb57a`) | After (T119) | Change | Skipped before / after |
|---|---|---|---|---|
| Edit Mode | 1590 passed of 1590 | 1497 passed of 1497 | −93 | 0 / 0 |
| Play Mode | 1559 passed of 1559 | 1443 passed of 1443 | −116 | 0 / 0 |

Before: run `20261008T111848476Z`. After: run `20261008T114218347Z`. Both runs used the same worktree and Library. No test is skipped or ignored in the Editor runs. A first after-run (`20261008T113400293Z`, Play Mode 1442 of 1443) failed one new assertion in `RunLifecycleClassificationTests`: it compared two `RunCommandResult<T>` values by reference, but the type is a struct. The case now counts the publications of the two calls instead; the rerun above is green. The Unity logs of the final run contain no compiler error and no missing-script or null-reference exception. The Windows test player (`-StandalonePlayer`) is not part of this task; T120 runs it.

Edit Mode arithmetic: 1590 − 93 (six deleted classes) − 4 (`ApplicationContractTests` cases) + 4 (`RunAuthorityTests`) = 1497.
Play Mode arithmetic: 1559 − 123 (thirteen deleted classes) − 1 (`PlayerCheckpointRoundTripTests`, net) + 2 (`RunBootstrapTests`) + 2 (`StartupHudTests`) + 4 (`RunLifecycleClassificationTests`) = 1443.

### Retired production code

| File (under `Assets/IdleFactory/`) | Decision | Reason |
|---|---|---|
| `Core/Application/FactorySession.cs` | Deleted | Feature 001 gameplay session. R16 lists its gameplay use for retirement; nothing in the run, the V1 import or a retained suite uses it. |
| `Core/Application/PlacementCommands.cs` | Deleted | R16. Free placement is replaced by slots (T112). |
| `Core/Application/ProductionCommands.cs` | Deleted | R16. The run advances through `RunSession.Advance`. |
| `Core/Application/PersistenceCommands.cs` | Deleted | R16. `RunPersistence` owns the run's persistence. `ICheckpointIdSource` and `GuidCheckpointIdSource`, which `RunPersistence` uses, moved unchanged to `Core/Application/Ports.cs`. |
| `Core/Application/DevelopmentSessionFactory.cs` | Deleted | Part of the D001 development scenario (R16). |
| `Unity/Input/PlacementInputController.cs` | Deleted | R16. |
| `Unity/Presentation/PlacementPreviewView.cs` | Deleted | R16. |
| `Unity/Presentation/FactoryView.cs` | Deleted | Feature 001 world view. It depended on `PlacementCommands`. The run's world view is `SlotGridView` (T112), so no player-visible run behaviour depends on it. |
| `Unity/Presentation/FactoryViewRegistry.cs` | Deleted | Helper of `FactoryView` only. |
| `Unity/Bootstrap/ApplicationLifecycleAdapter.cs` | Deleted | Feature 001 lifecycle adapter over `PersistenceCommands`. The run uses `RunLifecycleAdapter`. `LifecycleReturnKind`, `LifecycleScreenConfiguration` and the one-second configuration-change window moved unchanged into `RunLifecycleAdapter.cs`. |
| `Unity/UI/MachineInspectorPresenter.cs` | Deleted | Feature 001 HUD panel (machine inspector). The run uses `MachinePanelPresenter` (T114). |
| `Unity/UI/PersistencePresenter.cs` | Deleted | Feature 001 HUD panel (Save and return, Reset D001). The run uses the start-up status and offline surfaces (T118). |

### Edited production code

| File | Change |
|---|---|
| `Unity/Bootstrap/CoreGameFoundationBootstrap.cs` | Removed the Feature 001 composition (`InitializeFeature001Runtime`, `OverrideCompositionDependenciesForTests`, the `JsonCheckpointStore` creation, `RecreateFactoryView`, the five Feature 001 serialized references and their public accessors). Start-up, recovery, panels and diagnostics of the run are unchanged. |
| `Unity/Bootstrap/FactoryRuntimeHost.cs` | Removed the `ProductionCommands` path. The run path is unchanged. |
| `Unity/Bootstrap/RunLifecycleAdapter.cs` | Received the moved lifecycle types (see above). No behaviour change. |
| `Unity/Presentation/FactoryHudDocument.cs`, `Unity/UI/CoreGameFoundation.uxml`, `Unity/UI/CoreGameFoundation.uss` | The Feature 001 HUD panels are retired: placement actions (Build, Confirm, Cancel, HUD zoom), machine inspector, Save and return. What remains is the start-up status panel, which reports a coded start-up failure and is hidden while the run shell is live, and the screen-class thresholds the run shell reuses. |
| `Core/Application/Ports.cs` | Received `ICheckpointIdSource` and `GuidCheckpointIdSource` unchanged. |
| `Core/Run/Application/RunPersistence.cs` | Doc comment only: it no longer names `PersistenceCommands`. |
| `Scenes/CoreGameFoundation.unity` | Changed through a temporary batch-mode Editor script, which was deleted afterwards. The script removed the six retired components (PlacementInputController, FactoryView, PlacementPreviewView, MachineInspectorPresenter, ApplicationLifecycleAdapter, PersistencePresenter), deleted the then empty `Factory View` and `Placement Preview` objects and renamed `Input and Placement Controller` to `Navigation Input`. The re-save dropped the five stale bootstrap references. The diff contains only these removals and the rename. |

### Retained legacy code (no change)

| Code | Reason |
|---|---|
| `IdleFactory.Domain` definitions, `DefinitionCatalog`, `FactoryState`, `FactoryPlacement`, `FactoryGeometry`, `Identifiers` | Legacy import support (R16): the Save V1 import maps a V1 factory through them. |
| `ProductionSimulator`, `OfflineReconciler`, `Contracts.cs`, `FactorySnapshot.cs` (`SemanticFactorySnapshot`) | Legacy import support (R16) and the retained contract, simulation and W001 suites. |
| Feature 002 version 1 loader (`GameplayContentLoader`, reader, validator, `JsonDefinitionCatalogBuilder`) | Reads the legacy import catalog for the V1 import. |
| `SaveV1Mapper.cs`, `SaveV1Dtos.cs`, `SaveV1CheckpointSource.cs`, Save V1 fixtures | V1 import (contracts/save-v2-and-v1-import.md). Byte-identical. |
| `JsonCheckpointStore.cs` | Kept as legacy, never composed by the bootstrap (GD-1). It is the Save V1 writer that the retained `CheckpointStoreTests`, `CheckpointGenerationFilesTests`, `V1ImportTests`, `PlayerCheckpointRoundTripTests`, `JsonContentPlayerTests` and the Play Mode V1 import fixture (`RunSceneRun.WriteV1Checkpoint`) use to produce V1 saves. `RunAuthorityTests` proves the bootstrap does not reference it. |
| `PresentationSpriteCatalog` F001 keys and the F001 sprites | The scene's sprite catalog and `F001AssetContractTests` and `PresentationSpriteCatalogTests` still cover them; removing art is outside R16. |
| `GridWorldMapper` | Covered by `NavigationInputTests`; the bootstrap no longer uses it. |

### Edit Mode test classes

| Class | Before | After | Decision | Reason |
|---|---|---|---|---|
| `PersistenceApplicationTests` | 31 | — | Deleted | Tested `PersistenceCommands` and the D001 scenario. Run persistence is covered by `SaveV2LifecycleTests`, `V1ImportTests` and `OfflineReturnDurabilityTests`. |
| `PlacementApplicationTests` | 17 | — | Deleted | Tested `PlacementCommands`. Placement geometry stays covered by `PlacementTests`; the run's slots by `RunCommandTests`. |
| `ProductionApplicationTests` | 11 | — | Deleted | Tested `ProductionCommands`. |
| `InspectorPollingQueryTests` | 5 | — | Deleted | Tested the Feature 001 inspector queries of `FactorySession` and `ProductionCommands`. |
| `MachinePresentationStateTests` | 4 | — | Deleted | Tested the presented state through `FactorySession` for the retired `FactoryView`. |
| `SimulationEquivalenceTests` | 25 | — | Deleted | Drove `ProductionSimulator` through `FactorySession` and `ProductionCommands`. `ProductionSimulationTests` (33) still cover the simulator directly; the run's equivalence is `RunSimulationEquivalenceTests`. |
| `ApplicationContractTests` | 9 | 5 | Kept, edited | The five cases on the retained contracts (snapshots, offline summary, typed outcomes, engine-free ports) stay. Removed: four cases on the `FactorySession` lifecycle graph, admission and state ownership. |
| `RunAuthorityTests` | — | 4 | Added | T119 final-green authority test (GD-1, R16). |
| All other Edit Mode classes (64) | unchanged | unchanged | Kept | Cover retained legacy code (`CheckpointStoreTests`, `CheckpointGenerationFilesTests`, `CheckpointMappingTests`, `DefinitionCatalogTests`, `FactoryStateTests`, `PlacementTests`, `ProductionSimulationTests`, `OfflineReconciliationTests`, `GameplayContent*Tests`, `JsonContent*Tests`, `JsonDefinitionCatalogBuilderTests`, `JsonDevelopmentContentTests`, `DevelopmentContentTests`, `DomainValueObjectTests`, `F001AssetContractTests`, `PresentationSpriteCatalogTests`, `GenerateSaveV1FixturesTests`, `W001PerformanceTests`) or the Feature 003 run. |

### Play Mode test classes

| Class | Before | After | Decision | Reason |
|---|---|---|---|---|
| `CoreGameFoundationSceneTests` | 6 | — | Deleted | Feature 001 scene composition, placement input, D001 framing and view recreation. The two run-relevant cases moved to `RunBootstrapTests`: the 60 FPS target and the missing-references error. |
| `ColdStartRestoreRenderTests` | 1 | — | Deleted | Rendered a V1 checkpoint through `FactoryView`. The run's cold start renders through the slot grid (`RunBootstrapTests`, `SlotGridTests`). |
| `HudLayoutOverlapTests` | 21 | — | Deleted | Layout of the retired HUD panels (inspector, Save and return, action bar). The run shell's layout is covered by `RunHudLayoutTests`, `RunTextScaleTests` and `RunDeviceProfileTests`. |
| `InitialCameraFramingTests` | 30 | — | Deleted | Pan limits against the retired HUD panels for the 16×16 Feature 001 factory. The run's framing is covered by `SlotGridTests` and `RunHudLayoutTests`. |
| `InspectorPollingTests` | 4 | — | Deleted | Retired `MachineInspectorPresenter`. |
| `ProductionInspectorTests` | 9 | — | Deleted | Retired `MachineInspectorPresenter`. The run's machine panel is covered by `MachinePanelTests`. |
| `JsonContentBootstrapTests` | 4 | — | Deleted | Feature 001 composition with version 1 content. Each content rejection, the exception-type-only diagnostic and null or empty content are covered on the run path by `RunBootstrapTests`. |
| `JsonContentPersistenceTests` | 3 | — | Deleted | Saved through `PersistenceCommands` and `DevelopmentSessionFactory`. The Save V1 store round trip stays covered by `CheckpointStoreTests` and `PlayerCheckpointRoundTripTests`. |
| `LifecyclePersistenceTests` | 13 | — | Deleted | Tested `ApplicationLifecycleAdapter` with `PersistenceCommands` and `PersistencePresenter`. The four return-classification cases were ported to `RunLifecycleClassificationTests`; the device-clock case moved to `PlayerCheckpointRoundTripTests`. |
| `NavigationPlacementTests` | 11 | — | Deleted | Placement flow, `FactoryView`, `PlacementPreviewView` and the Feature 001 HUD controls. The two HUD cases on retained behaviour moved to `StartupHudTests`. The sprite-catalog case duplicated `PresentationSpriteCatalogTests` (Edit Mode). |
| `PersistenceUiTests` | 17 | — | Deleted | Retired `PersistencePresenter`. |
| `ViewRecreationTests` | 2 | — | Deleted | Retired `FactoryView` and `FactoryViewRegistry`. |
| `ZoomReadabilityTests` | 2 | — | Deleted | Readability of the retired `FactoryView` sprites. The run's zoom readability is covered by `SlotGridTests` (pixels per cell). |
| `PlayerCheckpointRoundTripTests` | 5 | 4 | Kept, edited | Player-level Save V1 store and device-clock rules stay. Three cases that went through `PersistenceCommands` were removed; one direct store round trip replaces them, and the zero-offset device-clock case moved in from `LifecyclePersistenceTests`. |
| `FactoryRuntimeHostTests` | 3 | 3 | Kept, rewritten | The same three active-time accounting cases now drive the run (`ConfigureRun`) with a producing Miner, because the host lost its `ProductionCommands` path. |
| `RunBootstrapTests` | 8 | 10 | Kept, edited | The retired HUD buttons are no longer asserted; the start-up HUD must have no gameplay control instead. Two cases moved in from `CoreGameFoundationSceneTests`. |
| `JsonContentPlayerTests` | 4 | 4 | Kept | Doc comment only. |
| `StartupHudTests` | — | 2 | Added | Retained HUD behaviour from `NavigationPlacementTests`: responsive classes and safe-area padding, text-only states, no gameplay controls, missing-assets error state. |
| `RunLifecycleClassificationTests` | — | 4 | Added | Return classification of `RunLifecycleAdapter`, ported from `LifecyclePersistenceTests`. |
| All other Play Mode classes (23) | unchanged | unchanged | Kept | Feature 003 run, shell and panels, navigation input and the player frame-rate check. |

### Authority checks (`RunAuthorityTests`)

- `TheBootstrapHasExactlyOneGameplayContentInput`: the bootstrap serializes exactly `gameplayContent` and `legacyImportContent`; only `ReadContentBytes` reads the gameplay content and it goes through `GameplayContentDispatcher`; the legacy catalog is read only by `ReadLegacyImportBytes` for the V1 import; the only Editor composition seam is `OverrideRunCompositionForTests`; the authored scene assigns a version 2 document.
- `TheBootstrapNeverReferencesTheFeature001CheckpointWriter` (GD-1): no file under `Unity/Bootstrap/` names `JsonCheckpointStore` or `ICheckpointStore`, and no field, return type or parameter of a bootstrap type is one of them.
- `NoAssetReferencesARetiredGuid`: none of the 32 retired GUIDs appears in a text asset under `Assets/`, and neither the files nor their `.meta` files exist.
- `TheRetiredTypesAreAbsent`: the retired types exist in no IdleFactory assembly and no C# source under `Assets/` names them outside comments.

## Full suite (T120)

**Task:** T120 (Issue #202), SC-006, QA-10.
**Commit under test:** `origin/main` at `be29326`, the merge of PR #310 on top of T119 (`c1c329b`, PR #308) and T243 (`5b2d3e5`, PR #309). Unity `6000.6.0f1`, full `scripts/test-unity.ps1` (Edit Mode, then Play Mode) in the clean issue worktree `.maqa-worktrees/issue-202`, under the `unity,emulator` leases (owner `issue-202`). The worktree's `Library/` was seeded from the main checkout at `5129b86`, which predates T119.

### Result

| Suite | T119 inventory (after) | T120 (`be29326`) | Change | Skipped |
|---|---|---|---|---|
| Edit Mode | 1497 of 1497 | 1497 passed of 1497 | 0 | 0 |
| Play Mode | 1443 of 1443 | 1475 passed of 1475 | +32 | 0 |

Run `20261008T124805630Z`, exit 0, `UNITY_TEST_PASS` for both suites. No test is skipped, ignored or inconclusive, so there are zero unexplained skips.

Play Mode arithmetic: 1443 + 32 = 1475. T243 (Issue #307, merged after the T119 inventory was taken, with `c7fb57a` as its base) adds two parameterized cases. They are `InventoryRatesPanelTests.TheRejectionShowsTheWarningBesideItsTextAtEveryScreenClassAndClearsWithIt` and `PrioritiesPanelTests.TheRejectionShowsTheWarningBesideItsTextAtEveryScreenClassAndClearsWithIt`. Each runs over 8 screen classes × 2 text scales (100 %, 200 %), which gives 16 cases each and 32 in total. No other test file changed between `c7fb57a` and `be29326` apart from T119 itself (`git diff --stat 877cf1c be29326 -- Assets/IdleFactory/Tests`). Edit Mode is unchanged.

### First run (environment, not counted)

The first run, `20261008T124524105Z`, failed one Edit Mode case: 1496 of 1497 passed. The failing case was `RunAuthorityTests.NoAssetReferencesARetiredGuid`, with `AssetDatabase.GUIDToAssetPath` returning `Assets/IdleFactory/Core/Application/FactorySession.cs` for a retired GUID. The file and its `.meta` did not exist in the worktree (`git status` clean apart from settings noise). The start-up refresh in that session logged `deleted=32`, which are the 32 retired assets. The seeded pre-T119 `Library/` still mapped the GUID inside that editor session. The rerun in a new editor session on the same `Library/` and commit passed every suite (above). The production code is not at fault. One test-robustness finding goes back to the T119 owner (see "Findings").

Play Mode did not run in the first run, because the script stops after a failed suite.

### Unity console

The final run logs contain no compiler error (`error CS`), no missing-script or missing-reference message and no shader error. Every error-level entry in the logs is an expected diagnostic of a negative-path test. The Test Runner fails any case that logs an unexpected error, and all cases passed. The entries are:

- Edit Mode: `[IdleFactory.Persistence] … rejected at …` diagnostics from the injected I/O, structure and semantic faults of the Save V2 store and import suites, logged through `CheckpointDiagnostics.Report`.
- Play Mode: seven persistence diagnostics, `panel.prestige.failed` (2×, expected by `RunPanelCompositionTests`) and `panel.build.failed` (1×), each a deliberate missing-asset case that `RecordPanelFailure` reports.
- Editor environment: one `[Licensing::Module] Error: Access token is unavailable; failed to update`. This comes from the Hub licensing client and not from the project.

As on earlier runs on this host, the Play Mode run ended with `Killing ADB server`. The emulator lease was held for the run.

### Settings noise

`scripts/check-settings-noise.ps1 -Commit be29326` reports OK. The test run re-serialized `ProjectSettings/ProjectSettings.asset` in the worktree (130 lines). That change was reverted and is not committed.

### Findings

| ID | Severity | Owner | Finding |
|---|---|---|---|
| T120-F1 | Low (test robustness) | T119 (Issue #201) | `RunAuthorityTests.NoAssetReferencesARetiredGuid` asserts `AssetDatabase.GUIDToAssetPath(guid)` is empty. In an editor session whose `Library/` still knew a retired asset, Unity keeps returning the deleted path although the file and `.meta` are gone. The case then fails once in a fresh worktree seeded with a pre-T119 `Library/`. The file and `.meta` assertions alone already prove absence. A fix could, for example, accept a returned path only when `AssetDatabase.AssetPathExists` or `File.Exists` is true. Not blocking: the canonical run passes, and QA does not repair. |

### Verdict

PASS for SC-006 (suite part). Edit Mode and Play Mode are green on `be29326`. The counts reconcile with the T119 retirement inventory plus T243. There are zero skips, no relevant console errors and no settings noise. The Windows test player (T121), the W003 timings (T122) and the emulator run (T124) are separate tasks.

## Windows test player (T121)

**Task:** T121 (Issue #203), SC-001, SC-005, SC-006, FR-024, FR-028, QA-2.
**Base:** `origin/main` at `5f738e6`, Unity `6000.6.0f1`, clean issue worktree `.maqa-worktrees/issue-203` (`Library/` built fresh), under the `unity,emulator` leases (owner `issue-203`). The commit that carries this section is the T121 assignment commit (`MAQA-Assignment: issue:203:branch:maqa/issue-203-standalone-player-journey`).

### Commands and results

| Command | Result | Duration |
|---|---|---|
| `scripts/test-unity.ps1` (Edit Mode, then Play Mode) | Edit Mode 1499 of 1499, Play Mode 1480 of 1480, 0 skipped; `UNITY_TEST_PASS` for both | 8 min 25 s (incl. first import of the fresh `Library/`) |
| `scripts/test-unity.ps1 -StandaloneWindowsPlayer` | **11 of 11 passed**, 0 skipped, in the built Windows player (`-buildTarget win64`, `-testCategory StandalonePlayer`) | 1 min 23 s |

Counts reconcile with T120 (`be29326`: Edit Mode 1497, Play Mode 1475): Edit Mode +2 (`SaveV1PlayerFixtureParityTests`: two cases), Play Mode +5 (`RunJourneyPlayerTests` 1, `RunTransitionPlayerTests` 4). The player suite grows from 6 tagged tests (`FrameRateTargetPlayerTests` 1, `HarnessSmokeTests` 1, `JsonContentPlayerTests` 4) to 11. Between the Edit/Play run and the player run one unused field and one unused parameter were removed from `RunJourneyPolicy`; the player run recompiled that file and passed, and the change touches no behaviour.

As on earlier runs on this host, the player run ended with `Killing ADB server`; the emulator lease was held and no emulator or `adb` was used. No `PlayerWithTests.exe` process was left running. The logs contain no `error CS`.

### What the player tests prove

| Test (all `[Category("StandalonePlayer")]`) | Player time | Proves |
|---|---|---|
| `RunJourneyPlayerTests.TheIronChainContractItemResearchPrestigeAndIndustryJourneyMakesTheSecondRunFaster` | 0.33 s | SC-001 on real components: Iron chain (all four machines, a Gear produced), Contract delivered with an Item granted, the Item activated, Research purchased, Overdrive, a durable prestige (new run durable before live, award paid once), Industry purchased with the IP, run 2 eligible sooner than run 1 (editor log: run 1 at 2380 s, run 2 at 2340 s, 10 IP, one Industry purchase), and a restart that restores the saved second run exactly |
| `RunTransitionPlayerTests.ARestartWhileThePrestigeIsPendingKeepsTheOldRunAndThePrestigeIsAwardedOnceAfterwards` | 0.18 s | FR-024: pending prestige rejects a second confirmation and `SaveNow` (QA-4); a restart loads the old run with no award; the repeated prestige awards once |
| `RunTransitionPlayerTests.AnInterruptionAfterTheDurableWriteIsPublishedOnRetryWithoutASecondAward` | 0.22 s | the store reports a failure after the real durable write; the old run stays pending; `RetryPendingTransition` publishes it with no second award |
| `RunTransitionPlayerTests.ARestartAfterTheDurableWriteLoadsTheNewRunOnceWithTheAwardOnce` | 0.13 s | a restart after the durable write loads run 2 with the award once; a second restart changes nothing |
| `RunTransitionPlayerTests.TheV1ImportFromThePackagedFixtureSucceedsOnceAndTheV1DirectoryStaysByteIdentical` | 0.14 s | FR-028, SC-005: the packaged V1 `TextAsset` is published through the legacy protocol, imported (one Smelter, four Ore, import block with the source ID), a restart does not import again, and the V1 directory is byte-identical after the import and after the restart |

All five drive `RunSession`, `RunCommands`, `RunPersistence` and the real `JsonRunCheckpointStore` over an isolated `Application.temporaryCachePath` root that each test deletes; none uses `Application.persistentDataPath` or a scene.

### Fixtures and parity (QA-2)

The player copies live in `Assets/IdleFactory/Tests/PlayMode/Fixtures/SaveV1/Resources/RunPlayerFixtures/` as `TextAsset`s: `02-idle-placed-d001.json` (the V1 fixture), `development-catalog.json` (the V1 decode catalog) and `iron-factory-content.json` (the Iron run catalog; loading it from the authored scene would start the bootstrap against the real save directory). `Assets/IdleFactory/Tests/EditMode/SaveV1PlayerFixtureParityTests.cs` asserts, for every pair, that the copy sits in a `Resources` folder, imports as a `TextAsset` and hash-equals (SHA-256) its original both as file bytes and as `TextAsset.bytes`; a second case proves that a flipped byte or a mismatched pair fails. T122 can append its Save V2 pairs to `Pairs`.

### Findings and risks

| ID | Severity | Finding |
|---|---|---|
| T121-F1 | Low | A `Resources` folder is packaged into every player build, so the three small fixture copies (about 18 KB) also ship in a release build. This is the only scene-free way to give a test player a stand-alone asset. Options: accept, or have a later task move them behind a build-time exclusion. |
| T121-F2 | Info | The real prestige threshold ($10,000,000) is not reached by the reference policy within six simulated hours with content revision 1 (`balance-trace-r1.md`), so the journey uses the same scaled threshold ($100,000, IP divisor / 100) as the balance trace's scaled scenario. Commands, content and persistence are the shipped ones. T125 may revise the content; the journey adapts because it reads the packaged document. |
| T121-F3 | Info | The second run is faster by one to two looks (40 s of 2380 s, 1.7 %) because a single Industry purchase (Machine Calibration) is all that 10 IP buy. The result is deterministic, so the strict assertion is stable; it would need review if the Industry costs or curves change. |

### Settings noise

`scripts/check-settings-noise.ps1 -Commit <T121 commit>` is recorded in the Issue report. The test runs re-serialized `ProjectSettings/ProjectSettings.asset` in the worktree; that change was reverted and is not committed.

### Verdict

PASS for the T121 part of SC-006: Edit Mode, Play Mode and the tagged Windows test-player suite are green with the new tests. W003 timings (T122) and the emulator run (T124) are separate tasks.

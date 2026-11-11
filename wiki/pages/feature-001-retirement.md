---
title: Feature 001 runtime retirement
type: decision
sources: [S018, S019, S021]
updated: 2026-11-06
---

# Feature 001 runtime retirement

T119 (R16, FR-031) removes the Feature 001 runtime. The Iron factory run (`RunSession`, `RunCommands`, `RunPersistence`, the run shell and the slot grid) is now the only runtime the bootstrap composes (see [run bootstrap and lifecycle](./run-bootstrap-and-lifecycle.md)). Git history is the rollback path. (S018)

## Retired

- Core: `FactorySession`, `PlacementCommands`, `ProductionCommands`, `PersistenceCommands` and the D001 scenario (`DevelopmentSessionFactory`). (S018)
- Unity: `PlacementInputController`, `PlacementPreviewView`, `FactoryView` with `FactoryViewRegistry`, `ApplicationLifecycleAdapter`, `MachineInspectorPresenter` and `PersistencePresenter`. The run's world view is `SlotGridView` and its machine surface is `MachinePanelPresenter`, so no player-visible run behaviour depended on them. (S018)
- The Feature 001 HUD (Build, Confirm, Cancel, HUD zoom, inspector, Save and return). `FactoryHudDocument` and `CoreGameFoundation.uxml` keep only the start-up status panel, which still reports a coded start-up failure, and the screen-class rules the run shell reuses. (S018, S019)
- Six Edit Mode and thirteen Play Mode test classes with them; every deleted .meta went with its asset. (S018)

## Moved, not changed

`ICheckpointIdSource` and `GuidCheckpointIdSource` moved into `Core/Application/Ports.cs`, because `RunPersistence` uses them. `LifecycleReturnKind`, `LifecycleScreenConfiguration` and the one-second configuration-change window moved into `RunLifecycleAdapter.cs` (see [Android runtime rotation](./android-runtime-rotation.md)). (S018, S019)

## Kept as legacy import support

`IdleFactory.Domain`, `DefinitionCatalog`, `FactoryState`, `ProductionSimulator`, `OfflineReconciler`, the Feature 002 v1 loader, `SaveV1Mapper` and the V1 fixtures stay byte-identical with their Edit Mode suites. `JsonCheckpointStore` stays too: it is the Save V1 writer the retained V1 suites and the Play Mode V1 import fixture use, but the bootstrap never composes it (GD-1). The F001 sprites remain in the scene's sprite catalog; removing them is an Art Director and Technical Artist decision outside R16. (S018)

## Guard and evidence

`RunAuthorityTests` (4 cases) checks that the bootstrap has exactly one gameplay content input (the v2 sheet; the V1 catalog is read only for import), that no bootstrap code or signature names `JsonCheckpointStore` or `ICheckpointStore`, that none of the 32 retired GUIDs appears in a text asset under `Assets/`, and that the 16 retired types are absent from the assemblies and from comment-stripped source. (S018, S019)

The retirement inventory in `reviews/full-suite-evidence.md` (QA-10) lists every deleted, edited and retained test class with its reason. Edit Mode went from 1590 to 1497 and Play Mode from 1559 to 1443 tests, both reconciled class by class with zero skips. T120 re-ran the full suite on the merged result: Edit Mode 1497 of 1497 and Play Mode 1475 of 1475 (1443 plus the 32 T243 cases), zero skips. (S018)

## Pitfalls

- A port that moves a test away from a reference type must not compare `RunCommandResult<T>` by reference: it is a struct. The first T119 run failed this way. (S018)
- Removing scene components needs Unity tooling: T119 used a temporary batch-mode Editor script and deleted it afterwards (see [Unity serialized asset edits](./unity-serialized-asset-edits.md)). (S018)
- The Feature 001 layout and zoom suites (`HudLayoutOverlapTests`, `InitialCameraFramingTests`, `ZoomReadabilityTests`) have no one-to-one port; the inventory maps them to the run shell and slot grid suites, and the UI/UX review should confirm that coverage. (S018, S021)
- A worktree whose `Library/` was seeded from a pre-T119 checkout made `AssetDatabase.GUIDToAssetPath` return the deleted path for a retired GUID once, so the guard failed in the first session (T120-F1). T244 fixed the guard: a returned path is an offender only if `AssetDatabase.AssetPathExists` or `File.Exists` finds the asset. The text-asset scan and the per-file `File.Exists` checks are unchanged, so a restored asset or a live reference still fails. Edit Mode stayed 1497 of 1497. (S018, S021)

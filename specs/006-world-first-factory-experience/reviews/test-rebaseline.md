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

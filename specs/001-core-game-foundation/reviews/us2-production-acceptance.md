# US2 production acceptance — T022

- Date: 2026-09-24
- Feature: `001-core-game-foundation`
- Issue: [#22](https://github.com/fjure/idle-factory-game/issues/22)
- Implementation snapshot: `e93f814f94e6492b155c9813a680f4cffd0fef91` (accepted `origin/main`, including T018–T021)
- Test environment: Unity `6000.6.0f1`, Windows Editor, repository-owned Edit/Play Mode runner

## Result and evidence boundary

The committed Edit Mode and Play Mode evidence covers D001's first atomic cycle, production blockers and recovery, multi-cycle advancement, explicitly simulated stopped-time, per-instance command ownership, and live inspector refresh. The stopped-time cases advance deterministic simulation ticks; they do not wait for real elapsed hours or days. Viewport cases are Editor layout simulations, not physical-device runs.

Two composite success-criterion paths remain unproven by a single current automated case: SC-002's exact D001 sequence of completing its first cycle, removing the full output, then completing the second cycle; and SC-006's partial-cycle save/load roundtrip followed by equivalent continuation. Related tests cover the component invariants, as detailed below, but this record does not claim those exact end-to-end comparisons passed.

## Development fixture

`FoundationFixtures.CreateD001` defines one active development-only machine with `dev.input` quantity/capacity `4/4`, `dev.output` quantity/capacity `0/1`, and recipe `2 → 1` over `50,000,000` 100 ns ticks (`5 s`). `DevelopmentContentTests` and `FoundationFixtures.D001HasExactCanonicalDefinitionsAndInitializedState` verify the authored definition graph, development-only labels, recipe, capacities and initial state. The separate multi-cycle fixture has the same recipe and input `4`, with output capacity `2`.

## Acceptance matrix

| Requirement | Result | Evidence |
|---|---|---|
| US2.1 — 2.5-second checkpoint | PASS | `ProductionSimulationTests.D001HalfCycleChangesOnlyProgressThenCompletesExactAtomicExchange` asserts input `4`, output `0`, progress `2.5 s`, and unchanged source state after the first half-cycle. |
| US2.2 / SC-002 — one D001 cycle | PASS | `D001HalfCycleChangesOnlyProgressThenCompletesExactAtomicExchange` and `D001SingleFiveSecondAdvanceCompletesOneCycle` assert the exact exchange: input `2`, output `1`, progress `0`; completion reports one cycle and the full output blocks further progress. |
| US2.2 / SC-002 — repeat D001 after removing output | PARTIAL | `ResolvingBlockerResumesOnlyFromRetainedProgress` clears a full-output blocker and proves retained partial work completes atomically; `ProductionApplicationTests.AdvanceUsesOneRetainedSessionAndReturnsDetachedCompleteSnapshots` proves two identical cycles in the capacity-two fixture. `quickstart.md` section 2 documents that follow-up step as “remove output, advance `5 s`”, expecting input `0`, output `1`, and progress `0`; no current test asserts the complete D001 first-cycle → remove output → second-cycle sequence with those final values. |
| US2.3–2.5 — missing input, full output, blockers and recovery | PASS | `BlockedOrInactiveElapsedPreservesExistingProgressAndResources` verifies unchanged half-cycle progress and resources while blocked; `ResolvingBlockerResumesOnlyFromRetainedProgress` proves only the remaining productive duration is needed after resolution. `ProductionInspectorTests.SelectionNamesMachineOwnedBuffersAndShowsCapacityProgressAndEveryBlocker` shows all three simultaneous blockers without collapsing them. |
| US2.6 — inactive machine | PASS | `ProductionInspectorTests.DeactivationPreservesResourcesAndProgressAndStopsAdvancement` compares resources/progress before, during, and after inactive runtime; `ResolvingBlockerResumesOnlyFromRetainedProgress` covers retained progress in the core simulator. |
| US2.7 — rejected storage operations | PASS | `FactoryStateTests.BufferRejectsInvalidCandidatesAndFailedOperationsWithoutMutation`, `AggregateExchangeRollsBackWhenAnyLaterValidationFails`, and `ProductionApplicationTests.RejectedResourceCommandsPreserveFactoryRevisionAndPreparedSession` cover invalid, insufficient, and atomic rejection. UI rejection feedback is covered by `ProductionInspectorTests.EmptySelectionHidesMachineActionsAndRejectedCommandIsClearlyNotApplied`. |
| US2.8 — selected-machine inspection and live refresh | PASS for live production and accepted controls | `ProductionInspectorTests.InspectorPollsLiveRuntimeAdvancementWithoutLoadingCheckpointBehavior`, `ResourceAndActiveButtonsSupportKeyboardAndRefreshAcceptedCommands`, and `SelectionNamesMachineOwnedBuffersAndShowsCapacityProgressAndEveryBlocker` check rendered progress, process, active state, buffers, capacity and blockers. Checkpoint-load refresh is explicitly deferred to T028. |
| US2.9 — independent instances and explicit buffers | PASS | `ProductionInspectorTests.ControlsAffectOnlyTheSelectedMachinesOwnBuffers` supplies separate instances and verifies the other buffer remains unchanged. `ProductionApplicationTests.ResourceCommandsUseExplicitBuffersAndReturnRevisionQuantityAndCapacity` verifies commands address an instance and buffer role explicitly. |
| FR-007–010 — resource identity, development content and recipe | PASS | `DefinitionCatalogTests.ValidD001GraphResolvesTypedImmutableDefinitions`, `DevelopmentContentTests.CatalogContainsExactlyTheAuthoredRegisteredGraphWithoutMissingReferences`, and the D001 fixture checks verify distinct resource/recipe/machine identities and development labels. |
| FR-011–016 — production, atomic exchange, inspection, storage ownership and separation | PASS | The simulator blocker/completion cases, `FactoryStateTests.AggregateExchangeCommitsEveryInputAndOutputTogether`, `ProductionApplicationTests.SetBuildingActiveAndInspectionExposeEveryCurrentBlockerImmutably`, and the explicit-buffer/instance tests above verify the relevant rules. No automatic inter-machine material transfer is exercised or implied. |
| FR-017–020 — explicit time, determinism, partitioning and cycle boundaries | PASS | Direct `ProductionSimulator.Advance` tests do not depend on rendering. `PartitionedAdvanceMatchesSingleAdvanceAcrossCycleBoundaries`, `BulkAdvanceCompletesMultipleCyclesWithoutCycleReplay`, and `InputAndOutputBoundsLimitBulkCyclesAtCompletionBoundaries` cover partition equivalence and limits at each completion. The multi-cycle fixture yields input `0`, output `2`, progress `0` after `10 s`. |
| FR-021 — one owner for authoritative state | PASS | `ProductionApplicationTests.AdvanceUsesOneRetainedSessionAndReturnsDetachedCompleteSnapshots` checks revisions and detached inspection snapshots; live UI tests read through `ProductionCommands`/`MachineInspectorPresenter` and do not publish simulation state. |
| SC-003 — 1 h, 24 h and 7 d stopped time | PASS, simulated ticks | The parameterized `ProductionSimulationTests.BlockedOrInactiveElapsedPreservesExistingProgressAndResources` covers each duration for missing-input, full-output, and inactive D001 states: `36,000,000,000`, `864,000,000,000`, and `6,048,000,000,000` ticks respectively. Each keeps resources and half-cycle progress unchanged. These are explicit simulated intervals, not real-time waits. |
| SC-006 — time partitions and view recreation | PARTIAL | `PartitionedAdvanceMatchesSingleAdvanceAcrossCycleBoundaries` compares equivalent simulation partitions. `CoreGameFoundationSceneTests.AuthoredSceneRunsPlacementInputAndViewRecreationWithoutChangingWorldOwnership` verifies view recreation does not mutate revision, building count, or occupancy. `CheckpointMappingTests.D001RoundTripPreservesCompleteSemanticState` uses zero-progress D001; no current test proves a partial-cycle Save V1 serialize/load/continue comparison. |

## Inspector, accessibility and viewport evidence

The focused T021 Play Mode cases load the authored scene in Unity Editor. They verify selected machine identity; machine-owned input/output quantities and capacities; producing, missing-input, full-output, inactive and combined-blocker display; accepted supply/removal/active commands; rejected feedback; empty selection; and refresh after live simulation and commands. Keyboard submit/focus is exercised, and all three inspector buttons assert non-empty labels, focusability, and minimum resolved size `44×44`.

`ProductionInspectorTests.ResponsiveClassesSafeAreaAndControlAccessibilityCoverSupportedViewports` exercises `360×640`, `390×844`, `768×1024`, `640×360`, `844×390`, `1024×768`, `1280×720`, and `1920×1080`. It checks responsive classes, simulated safe-area padding, resolved UIDocument/HUD bounds, control/inspector safe-area containment, panel overlap, scroll reachability, and a positive compact-landscape scroll offset. This is automated Editor geometry evidence. It is not physical Android/iOS safe-area, touch-control, rotation, or suspend/resume evidence; no such device claim is made here.

Per the 2026-09-24 clarification in `spec.md`, T021/T022 cover live refresh after production, supply/removal and active-state changes. Checkpoint-load inspector refresh and stale-selection resolution remain T028 acceptance.

## Full validation run

The configured full-suite command ran successfully after Unity/UPM access was granted for the runner process:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-unity.ps1
```

`UPM_CACHE_ROOT` was set only for the PowerShell test process to this worktree's `Library/UPMCache` and restored in `finally`; no project configuration was changed. Unity `6000.6.0f1`, Windows Editor: Edit Mode **465/465 passed, 0 skipped**; Play Mode **48/48 passed, 0 skipped**; the repository runner exited `0`. Result XML and logs: `test-results/unity/20260924T171550267Z/`.

The two earlier sandbox-restricted startup attempts were **not executed** (no test discovery/XML): `test-results/unity/20260924T171041769Z/EditMode.log` and `test-results/unity/20260924T171350026Z/EditMode.log`. Both logged `unable to open database file` and a 30-second UPM IPC timeout. The second used the process-local cache root but remained sandbox restricted. The successful run above used the same process-local setting with the approved elevated Unity execution path.

For comparison, the parent-reported integrated validation on the identical source tree was also green at commit `877aa99a26d6fcb487bd350c8e2107dd9d37872c`: Edit Mode `465/465`, Play Mode `48/48`, zero skips, run directory `test-results/unity/20260924T164734655Z/`.

# Save V1 pre-migration fixtures

Captured at commit `51603f2` (origin/main, PR #137 merge) by `GenerateSaveV1FixturesTests.GenerateFixtures` (T070c), while the ScriptableObject development content path was still live. Re-running that test reproduces these files byte-for-byte from the unchanged `SaveV1Mapper` and Core domain types, and re-asserts the offline-return expectation below via the unchanged `OfflineReconciler`.

Every scenario below (other than 06 and the T106 import fixtures 07-10, which name their own catalog) uses the accepted `dev.*` catalog captured in `AcceptedFoundationCatalogFixture`: resources `dev.input`/`dev.output`, recipe `dev.recipe.basic` (2 dev.input -> 1 dev.output, durationTicks 50000000), building `dev.machine.basic` (D001, footprint 2x2, orientations [0], input capacity 4, output capacity 1), and offline policy revision 1 (4/5 efficiency, maxCreditedTicks 288000000000). Scenario 06 uses `FoundationFixtures.CreateW001`, which builds its own self-consistent catalog with the same `dev.*` resource/recipe IDs and values plus a distinct `test.machine.w001` building.

All factories use bounds origin (0,0), width 16, height 16, except scenario 06 (100x40, from FoundationFixtures.CreateW001).

By default, `GenerateSaveV1FixturesTests.GenerateFixtures` runs as a reproducibility regression check in the normal EditMode suite: it regenerates every fixture in memory and asserts each is byte-identical to the committed file, without writing anything to disk. To intentionally regenerate the committed files (and this README) after a deliberate change, set `IDLEFACTORY_REGENERATE_SAVEV1_FIXTURES=1` and run the test file directly:

```powershell
$env:IDLEFACTORY_REGENERATE_SAVEV1_FIXTURES='1'; scripts/test-unity.ps1 -TestFile Assets/IdleFactory/Tests/EditMode/GenerateSaveV1FixturesTests.cs
```

## 01-empty-factory.json

**Empty factory**

Scenario: An empty factory with no buildings, at generation 0, departure UTC 2026-01-01T00:00:00.000Z.

Expected outcome when loaded: Loading yields a FactoryState with zero buildings (FactoryState.Buildings.Count == 0) and Checkpoint.OfflinePolicyRevision resolves to the accepted revision 1 (4/5 efficiency, maxCreditedTicks 288000000000). No offline reconciliation is exercised.

## 02-idle-placed-d001.json

**Idle placed D001**

Scenario: A single freshly placed D001 (dev.machine.basic) at anchor (0,0), orientation 0: input buffer full (4/4 dev.input), output buffer empty (0/1 dev.output), production progress 0, active.

Expected outcome when loaded: Loading yields one building at Anchor (0,0), OrientationQuarterTurns 0, IsActive true, InputBuffer.Quantity(dev.input) == 4, OutputBuffer.Quantity(dev.output) == 0, Production.ProgressTicks == 0, GetProductionBlockers().Count == 0.

## 03-d001-mid-cycle-partial-progress.json

**D001 mid-cycle with partial progress**

Scenario: The same placed D001, mid-cycle: input partially consumed (2/4 dev.input), output still empty (0/1 dev.output), production progress at half the recipe duration (25000000/50000000 ticks), active.

Expected outcome when loaded: Loading yields InputBuffer.Quantity(dev.input) == 2, OutputBuffer.Quantity(dev.output) == 0, Production.ProgressTicks == 25000000 (< RecipeDefinition.DurationTicks 50000000), IsActive true, GetProductionBlockers().Count == 0 (2 >= recipe input amount 2, and the output buffer has free capacity for the recipe's output amount 1).

## 04-d001-blocked-output-full.json

**D001 blocked with output full**

Scenario: A placed D001 with input ready (4/4 dev.input) but its single-slot output buffer already full (1/1 dev.output), production progress 0, active.

Expected outcome when loaded: Loading yields InputBuffer.Quantity(dev.input) == 4, OutputBuffer.Quantity(dev.output) == 1 (at capacity), Production.ProgressTicks == 0, IsActive true; GetProductionBlockers() returns exactly one blocker: Kind InsufficientOutputCapacity, ResourceId dev.output, Required 1, Available 0.

## 05-checkpoint-departure-offline-return.json

**Checkpoint with a departure time for offline return**

Scenario: The same idle placed D001 (input 4/4, output 0/1, progress 0, active), checkpointed at departure UTC 2026-01-01T00:00:00.000Z, for exercising an offline return.

Expected outcome when loaded: Applying OfflineReconciler.Reconcile with returnUtc 2026-01-01T08:00:00.000Z (an offline interval of 08:00:00) against this checkpoint yields Status CandidateReady with MeasuredTicks 288000000000, CreditedTicks 288000000000 (capped at the offline policy's maxCreditedTicks 288000000000, exactly reached by this 8-hour interval), EffectiveTicks 230400000000 (CreditedTicks * 4 / 5). The resulting candidate checkpoint's single building has InputBuffer.Quantity(dev.input) == 2, OutputBuffer.Quantity(dev.output) == 1, Production.ProgressTicks == 0, IsActive == True, and InsufficientOutputCapacity on dev.output (required 1, available 0).

## 06-w001-scale-checkpoint.json

**W001-scale checkpoint**

Scenario: The FoundationFixtures W001 workload: 1000 test.machine.w001 buildings on the accepted dev.* recipe/resources (500 active unblocked, 250 active output-blocked, 250 inactive), each with input quantity 9216 and output capacity 4608, at generation 0, departure UTC 2026-01-01T00:00:00.000Z.

Expected outcome when loaded: Loading yields 1000 buildings with exactly the classification FoundationFixtures.CreateW001 produces; see FoundationFixtures.W001HasExactDeterministicWorkloadShapeAndClassifications for the complete field-by-field assertion (occupancy count 4000, bounds (0,0) 100x40, per-index anchor formula, and the unblocked/output-blocked/inactive counts and blocker kinds above).

## 07-over-capacity.json

**Over-capacity import source (T106)**

Scenario: Two dev.machine.basic buildings at anchors (0,0) and (2,0), each holding 1500 dev.input (3000 stock units in total), active, progress 0, at generation 0, departure UTC 2026-01-01T00:00:00.000Z. It uses the accepted dev.* catalog except that the dev.machine.basic input buffer capacity is 2000 (instead of 4), because the accepted catalog cannot hold more than 5 units per machine.

Expected outcome when loaded: Loading with that catalog yields two buildings. The V1 import fails with import.capacity.exceeded: the summed stock (3000) is above the level-1 storage capacity of the run content, and nothing is published.

## 08-over-slot.json

**Over-slot import source (T106)**

Scenario: Five dev.machine.basic buildings in a row at anchors (0,0), (2,0), (4,0), (6,0) and (8,0), each with 4 dev.input, active, progress 0, at generation 0, departure UTC 2026-01-01T00:00:00.000Z. It uses the accepted dev.* catalog.

Expected outcome when loaded: Loading yields five buildings. The V1 import fails with import.slots.exceeded: five machines are more than the four level-1 slots, and nothing is published.

## 09-unsupported-id.json

**Unsupported-ID import source (T106)**

Scenario: One building whose definition ID is dev.machine.unsupported instead of dev.machine.basic, at anchor (0,0) with 4 dev.input, active, progress 0, at generation 0, departure UTC 2026-01-01T00:00:00.000Z. It uses the accepted dev.* catalog with that one building definition renamed.

Expected outcome when loaded: Loading with that catalog yields one building. The V1 import fails with import.id.unsupported, and nothing is published.

## 10-corrupt.json

**Corrupt import source (T106)**

Scenario: The bytes of 02-idle-placed-d001.json without their last byte, so the document is truncated and is not valid JSON.

Expected outcome when loaded: Loading is rejected by the unchanged SaveV1Mapper (InvalidCheckpoint). The V1 import fails with import.v1.invalid, and nothing is published.


# F007 test re-baselining map

Purpose (QA-5, R19, FR-036): when Feature 007 moves, renames or removes a C# test, the old test id is
recorded here with its new id (or "removed"), the reason, the task and the reviewer. The script
`tools/test-ids/compare_test_ids.py` (T638) lists every `[Test]`, `[UnityTest]` and `[TestCase]` id under
`Assets/IdleFactory/Tests/` at a ref, compares it with `origin/main`, and fails unless every missing id
has a row below. `scripts/test-unity.ps1 -CheckTestIds` runs it before Unity starts.

## How the script reads this file

- Only the table whose header is exactly `Old id | New id or removed | Reason | Task | Status` is parsed.
  Escape a literal `|` inside a cell as `\|`. Backticks around cells are ignored.
- Id format: `<namespace>.<Class>[.<Nested>].<Method>`; a `[TestCase(args)]` id is
  `<...>.<Method>(<args as written, whitespace collapsed>)`. A row whose Old id is a method id without
  `(...)` covers every `[TestCase]` id of that method.
- Status values: `moved`, `removed`, `not yet moved`, `out of scope`.
- Only an exact-id row with status `moved` or `removed` satisfies a missing id. For `moved`, the New id must
  exist at head; for `removed`, column 2 must be the word `removed`. Class-level rows (Old id ending in
  `.*` or `::*`, or a file path) are candidates only: they never satisfy a missing id, whatever their
  status. When a class moves, replace its candidate row with one exact-id row per test id; run the script
  with `--emit-rows` for a paste-ready draft.
- Reviewer: the Test/QA reviewer who accepted the move is named in the Reason cell (`reviewer: <name>`)
  when the row is set to `moved` or `removed`.

## Map

| Old id | New id or removed | Reason | Task | Status |
|---|---|---|---|---|
| `CameraScaleModelTests.*` | - | QA-5 candidate (research.md R19); class moves with the F007 camera/scale model | T6xx (TBD) | not yet moved |
| `SlotGridLatticeTests.*` | - | QA-5 candidate; lattice geometry under the new look profile | T6xx (TBD) | not yet moved |
| `PixelCrispnessTests.*` | - | QA-5 candidate; pixel crispness rules under the new art direction | T6xx (TBD) | not yet moved |
| `StateCueSetTests.*` | - | QA-5 candidate; state cue set under the new look profile | T6xx (TBD) | not yet moved |
| `AtlasPageBudgetTests.*` | - | QA-5 candidate; atlas page budget for F007 assets | T6xx (TBD) | not yet moved |
| `PresentationSpriteCatalogTests.*` | - | QA-5 candidate; sprite catalog for F007 assets | T6xx (TBD) | not yet moved |
| `AttentionModelTests.*` | - | QA-5 candidate: F006 marker tests (class names resolved against the Tests tree) | T6xx (TBD) | not yet moved |
| `UpgradeMarkerTests.*` | - | QA-5 candidate: F006 marker test | T6xx (TBD) | not yet moved |
| `UpgradeMarkerSceneTests.*` | - | QA-5 candidate: F006 marker scene test (Play Mode) | T6xx (TBD) | not yet moved |
| `AttentionMarkerSceneTests.*` | - | QA-5 candidate: F006 marker scene test (Play Mode) | T6xx (TBD) | not yet moved |
| `DeckLayoutTests.*` | - | QA-5 candidate: F006 layout test | T6xx (TBD) | not yet moved |
| `WorldLayoutParityTests.*` | - | QA-5 candidate: F006 layout test | T6xx (TBD) | not yet moved |
| `WorldLayoutValidationTests.*` | - | QA-5 candidate: F006 layout test | T6xx (TBD) | not yet moved |
| `LayoutIndependenceTests.*` | - | QA-5 candidate: F006 layout test (named suite, must pass unchanged under every profile) | T6xx (TBD) | not yet moved |
| `DevelopmentStageTests.*` | - | QA-5 candidate: F006 stage test | T6xx (TBD) | not yet moved |
| `StageTriptychDeltaTests.*` | - | QA-5 candidate: F006 stage test | T6xx (TBD) | not yet moved |
| `StageDressingViewTests.*` | - | QA-5 candidate: F006 stage test (Play Mode) | T6xx (TBD) | not yet moved |
| `StageWiringSceneTests.*` | - | QA-5 candidate: F006 stage test (Play Mode) | T6xx (TBD) | not yet moved |
| `DevToolsOverlayLayoutTests.*` | - | QA-5 candidate: `DevToolsOverlay*Tests` | T6xx (TBD) | not yet moved |
| `DevToolsOverlayPlacementTests.*` | - | QA-5 candidate: `DevToolsOverlay*Tests` | T6xx (TBD) | not yet moved |
| `DevToolsOverlayPointerTests.*` | - | QA-5 candidate: `DevToolsOverlay*Tests` | T6xx (TBD) | not yet moved |
| `DevToolsOverlayTests.*` | - | QA-5 candidate: `DevToolsOverlay*Tests` | T6xx (TBD) | not yet moved |
| `tools/art-pipeline/tests/test_checks.py` | - | QA-5 candidate; Python (pytest), outside the C# id scan | T6xx (TBD) | out of scope |

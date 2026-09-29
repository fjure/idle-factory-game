# Post-migration SC-008 measurement — GameplayContentLoader.Load

**Task:** T076 (Issue #139) · **Date:** 2026-09-29 · **Commit measured against:** `42a193f` (origin/main, PR #154 merge, plus this task's uncommitted `JsonContentPlayerTests.cs`)

## What this measures

SC-008 requires the cold-start cost of content loading to be measured in a built Windows
player and compared with the pre-migration baseline
(`specs/002-json-content-authoring/reviews/pre-migration-baseline.md`, median
`DefinitionCatalogBuilder.Build` = 0.0320 ms). This records the post-migration measurement:
how long `GameplayContentLoader.Load` (the JSON reader, semantic validator and domain mapper,
in contract order) takes against the packaged development content.

## Method

- Test: `Assets/IdleFactory/Tests/PlayMode/JsonContentPlayerTests.cs`,
  `MedianGameplayContentLoaderLoadTimeIsMeasured`, tagged `[Category("StandalonePlayer")]`.
- Command: `scripts/test-unity.ps1 -StandaloneWindowsPlayer` (win64 test player).
- Sampling: 5 calls to `GameplayContentLoader.Load(bytes)` timed individually with
  `System.Diagnostics.Stopwatch`; the reported value is the median (3rd of 5 sorted samples),
  matching R10 and the pre-migration baseline's method. The first call is included in the
  sampled set, as in the baseline.
- **Content bytes in the built player:** `GameplayContentLoader.Load` takes a `byte[]` and has
  no dependency on Unity asset loading, so unlike the pre-migration baseline this measurement
  does not need scene loading or reflection at all. The bytes are a byte-for-byte copy of
  `Assets/IdleFactory/Content/Development/development-catalog.json` embedded as a C# string
  literal in the test file (Assets/ paths are not readable in a built player), verified
  byte-for-byte equal to the packaged `gameplayContent` TextAsset by the sibling test
  `PackagedSceneContentInBuiltPlayerBuildsCatalogEqualToAcceptedFixture` in the same file, which
  loads `CoreGameFoundation.unity` via the runtime `SceneManager` and reads the packaged
  TextAsset bytes off the bootstrap's private `gameplayContent` field through reflection.
- No production code was changed to make this measurement; only the new test file and this
  report were added.

## Result

Measured 2026-09-29 via `scripts/test-unity.ps1 -StandaloneWindowsPlayer` (win64 test player).

- Median `GameplayContentLoader.Load` time (5 samples): **0.0612 ms**
- Individual samples (ms, sorted): 0.0486, 0.0599, 0.0612, 0.1094, 0.4574
  (the 0.4574 ms outlier is the first call, consistent with JIT/first-call warmup seen in the
  pre-migration baseline; the median of 5 is robust to it)
- Unity log line (as reported by the test, decimal comma from the player's locale), taken from
  the NUnit result XML `<output>` for this test case:
  `SC-008 post-migration measurement: median GameplayContentLoader.Load over 5 samples = 0,0612 ms (sorted samples ms: 0,0486, 0,0599, 0,0612, 0,1094, 0,4574); 100 ms guard is measure-only (R10, clarification 2026-09-28).`
- Test result: `JsonContentPlayerTests.MedianGameplayContentLoaderLoadTimeIsMeasured` — Passed,
  part of a 6/6 passing `[Category("StandalonePlayer")]` run (the 4 tests in this file plus
  `PlayerCheckpointRoundTripTests`'s pre-existing 2)
  (`test-results/unity/20260929T001810673Z/PlayerPlayMode-results.xml`,
  `test-results/unity/20260929T001810673Z/PlayerPlayMode.log`; these result artifacts are
  produced by the test run and are not committed).

## Comparison with the pre-migration baseline

| Measurement | Median | Samples (ms) |
|---|---|---|
| Pre-migration `DefinitionCatalogBuilder.Build` (T070d) | 0.0320 ms | 0.0114, 0.0119, 0.0320, 0.0850, 7.6382 |
| Post-migration `GameplayContentLoader.Load` (T076) | 0.0612 ms | 0.0486, 0.0599, 0.0612, 0.1094, 0.4574 |

The post-migration median (0.0612 ms) is about 0.029 ms higher than the pre-migration median
(0.0320 ms) — both comfortably under the 100 ms guard, which per R10 and the 2026-09-28
clarification is measure-only and does not block acceptance regardless of the delta. The
increase is consistent with the JSON reader/validator doing more work (strict UTF-8/JSON
parsing, structural and semantic validation, JSON Pointer path tracking) than the prior direct
`ScriptableObject` field reads, and is far below any threshold that would be practically
noticeable. No Performance Engineer review is triggered.

## Notes

- This measurement should be re-taken and this file updated if `GameplayContentLoader.Load`,
  the packaged `development-catalog.json` document, or the reference measurement machine
  change materially.

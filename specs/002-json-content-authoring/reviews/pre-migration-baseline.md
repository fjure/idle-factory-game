# Pre-migration SC-008 baseline — DefinitionCatalogBuilder.Build

**Task:** T070d (Issue #138) · **Date:** 2026-09-28 · **Commit captured against:** `51603f2` (origin/main, PR #137 merge)

## What this measures

SC-008 requires the JSON content authoring migration not to regress catalog build time. This
records the pre-migration baseline: how long `DefinitionCatalogBuilder.Build` takes against the
still-live ScriptableObject `DevelopmentCatalog` asset, so the post-migration JSON loader can be
compared against it.

## Method

- Test: `Assets/IdleFactory/Tests/PlayMode/PreMigrationCatalogBuildBaselineTests.cs`,
  `MedianDefinitionCatalogBuilderBuildTimeIsMeasured`, tagged `[Category("StandalonePlayer")]`.
- Command: `scripts/test-unity.ps1 -StandaloneWindowsPlayer`.
- Sampling: 5 calls to `DefinitionCatalogBuilder.Build(catalogAsset)` timed individually with
  `System.Diagnostics.Stopwatch`; the reported value is the median (3rd of 5 sorted samples).
- **Catalog access in the built player:** `UnityEditor.AssetDatabase` is Editor-only and
  unavailable in a built standalone player, and the existing Editor-only harness
  (`LifecyclePersistenceTests`, which uses `EditorSceneManager.LoadSceneInPlayMode`) explicitly
  does not run outside the Editor (`Assert.Ignore` there). Instead, the baseline test loads
  `Assets/IdleFactory/Scenes/CoreGameFoundation.unity` — the sole, index-0 entry in
  `ProjectSettings/EditorBuildSettings.asset`, so it ships inside the standalone player build —
  with the ordinary runtime `UnityEngine.SceneManagement.SceneManager.LoadSceneAsync` API, then
  reads `CoreGameFoundationBootstrap.DevelopmentCatalog` off the loaded scene. That field is a
  serialized scene reference resolved purely by scene loading, so it is populated whether or not
  the bootstrap's own `Initialize()` succeeds in the player.
- No production code was changed to make this measurement; only the new test file and this
  report were added.

## Result

Measured 2026-09-28 via `scripts/test-unity.ps1 -StandaloneWindowsPlayer` (win64 test player).

- Median `DefinitionCatalogBuilder.Build` time (5 samples): **0.0320 ms**
- Individual samples (ms, sorted): 0.0114, 0.0119, 0.0320, 0.0850, 7.6382
  (the 7.6382 ms outlier is the first call, almost certainly JIT/first-call warmup; the median
  of 5 is robust to it — this is exactly why SC-008 asked for a median rather than a mean or a
  single sample)
- Unity log line (as reported by the test, decimal comma from the player's locale):
  `SC-008 pre-migration baseline: median DefinitionCatalogBuilder.Build over 5 samples = 0,0320 ms (sorted samples ms: 0,0114, 0,0119, 0,0320, 0,0850, 7,6382)`
- Test result: `PreMigrationCatalogBuildBaselineTests.MedianDefinitionCatalogBuilderBuildTimeIsMeasured` —
  Passed (1/1), part of a 3/3 passing `[Category("StandalonePlayer")]` run
  (`test-results/unity/20260928T201322232Z/PlayerPlayMode-results.xml`,
  `test-results/unity/20260928T201322232Z/PlayerPlayMode.log`; these result artifacts are
  produced by the test run and are not committed).

## Notes

- The catalog under measurement contains 2 resources, 1 recipe, 1 building, 1 offline policy
  (the accepted `dev.*` development content — see `AcceptedFoundationCatalogFixture`), matching
  what `DevelopmentContentTests.BuilderProducesExactCompatibilitySensitiveDevelopmentDefinitions`
  already exercises functionally.
- Because the catalog is tiny, this baseline is expected to be dominated by fixed overhead
  (dictionary allocation, snapshot copies) rather than per-entry cost; the post-migration
  comparison should account for that when judging whether a JSON-parsing path regresses SC-008.

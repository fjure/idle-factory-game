# Final Acceptance — JSON Content Authoring Migration (T081)

**Date:** 2026-09-29 · **Accepted code commit:** `c1aed9ffa2ad455610cb77f0dc92a22babfb360a` (PR #159, T079) · **Run commit:** `c83bf27` (`origin/main`, accepted code plus the T080 review docs; no production code differs from `c1aed9f`) · **Issue:** #54 (WU13) · **Runner:** Test/QA worker, Windows host, Unity `6000.6.0f1`, leases `unity,emulator` held as `issue-54` and released afterwards. No production code was changed; no failure occurred.

## Results per command

| Command | Result | Exit | Result XML (under `test-results/unity/`, uncommitted) |
|---|---|---|---|
| `scripts/test-unity.ps1` (full) — Edit Mode | 665 tests, 665 passed, 0 skipped | 0 | `20260929T112617579Z/EditMode-results.xml` |
| `scripts/test-unity.ps1` (full) — Play Mode | 160 tests, 160 passed, 0 skipped | 0 | `20260929T112617579Z/PlayMode-results.xml` |
| `scripts/test-unity.ps1 -StandaloneWindowsPlayer` | 6 tests, 6 passed, 0 skipped (4 `JsonContentPlayerTests`, `FrameRateTargetPlayerTests`, `HarnessSmokeTests`) | 0 | `20260929T112903839Z/PlayerPlayMode-results.xml` |

## Console inspection

Logs: `EditMode.log`, `PlayMode.log`, `PlayerPlayMode.log` of the runs above. Every `error`/`exception` match was one of the following, and none is a relevant error:

- Edit Mode: `[IdleFactory.Persistence] … rejected at …` diagnostics with their `CheckpointDiagnostics.Report` stack frames. These are the expected negative-path output of the checkpoint-store and Save V1 rejection tests, which all pass.
- Play Mode: three `Deserialize rejected at Semantic (KeyNotFoundException)` diagnostics (the T077 missing-ID case) and one stack frame from `NavigationPlacementTests.HudMissingAssetsProducesAttachedDisabledErrorState`, an expected error-state test.
- All three logs: `[Licensing::Module] Error: Access token is unavailable; failed to update`. This is Unity licensing telemetry unrelated to the project.
- Editor import lines for `ValidationExceptions.json` and package file names containing "Exception" are file names, not errors.
- No unhandled-log failure, no compile error and no content-loader diagnostic outside the rejection tests.

## Quickstart outcomes

| § | Outcome |
|---|---|
| §1 Validate without a scene | PASS. Each content test file run with `-TestFile`, exit 0 each: `GameplayContentAuthoringCheckTests` 1/1, `GameplayContentReaderTests` 28/28, `GameplayContentValidatorTests` 53/53, `GameplayContentSchemaConsistencyTests` 2/2, `GameplayContentOrderTests` 1/1, `JsonDefinitionCatalogBuilderTests` 2/2, `JsonDevelopmentContentTests` 3/3, `JsonContentCompatibilityTests` 17/17, `JsonContentAuthorityTests` 5/5. `PreMigrationCaptureTests.cs` is named in `tasks.md` but was retired by T079 (see `retirement-approval.md`, scope of T079), so the runner correctly reports the file as absent. Not a failure. The Edit Mode suite (665/665) passed as the second §1 command. |
| §2 Author a change | PASS (mechanical check). A scratch copy of `development-catalog.json` outside the repo, with one added resource `dev.scrap` and one recipe `dev.recipe.scrap`, passed `GameplayContentAuthoringCheckTests` via `IDLEFACTORY_CONTENT_CHECK_PATH` on attempt 1 (1/1). The scratch file is not committed. The doc-only SC-005 walkthrough by a reviewer who has not seen the implementation is recorded in `json-content-migration-review.md` (SC-005 walkthrough, 1 attempt); this run does not repeat it. |
| §3 Run from JSON | PASS. Full suites and player suite green (table above). D001 placement, two cycles with a blocked interval, save and load: `JsonContentBootstrapTests.PackagedContentPreservesD001PlacementTwoCyclesBlockingAndSaveLoad`. Offline return and unmodified pre-migration checkpoints: `JsonContentPersistenceTests` and `JsonContentCompatibilityTests`. |
| §4 Fail safely | PASS. `JsonContentBootstrapTests.RejectedDocumentsShowCodedErrorBeforeCheckpointAccess`, `NullSerializedAssetOnAuthoredCompositionUsesContentEmpty`, and in the built Windows player `JsonContentPlayerTests.CorruptedContentIsRejectedAndRealCheckpointStoreStaysUntouched`. Missing-ID against an existing checkpoint: `JsonContentPersistenceTests.JsonCatalogMissingReferencedBuildingIdRejectsExistingCheckpointWithoutFileOrLiveStateChange` (T077). |
| §5 Single authority | PASS. `JsonContentAuthorityTests` 5/5. `git grep -w` for `ResourceDefinitionAsset`, `RecipeDefinitionAsset`, `BuildingDefinitionAsset`, `OfflinePolicyAsset`, `DefinitionCatalogAsset` and `DefinitionCatalogBuilder` outside `specs/`, `docs/`, `wiki/` hits only `Assets/IdleFactory/Tests/EditMode/JsonContentAuthorityTests.cs`, which holds these names as its own retired-name and retired-GUID lists. The live type `JsonDefinitionCatalogBuilder` (and its mention in the schema description) is a different identifier and does not match as a whole word. Full and player suites pass. |
| §6 Cold start (SC-008) | Measured, guard not reached. T076 record (`json-content-load-measurement.md`): post-migration median `GameplayContentLoader.Load` **0.0612 ms** vs. pre-migration baseline (`pre-migration-baseline.md`) `DefinitionCatalogBuilder.Build` **0.0320 ms**, against the 100 ms guard. This run's player suite reproduced it: **0.0665 ms** (sorted samples 0.0531, 0.0561, 0.0665, 0.1636, 0.5103 ms; `MedianGameplayContentLoaderLoadTimeIsMeasured`). Both are about 1500 times below the guard. The absolute increase (about 0.03 ms) is a JSON parse plus validation on top of the definition mapping. No Performance review triggered. Measure-only. |
| §7 Android | Out of scope for T081; see the Android section. |

## FR and SC outcomes

Legend: PASS = evidenced in this run's green suites and the cited records; PASS-emulator-pending = passes except the Android emulator evidence owned by T082.

| ID | Outcome | Evidence |
|---|---|---|
| FR-001 | PASS | `GameplayContentReaderTests`, `GameplayContentSchemaConsistencyTests`; `Assets/IdleFactory/Content/Schemas/gameplay-content-v1.schema.json` |
| FR-002 | PASS | `GameplayContentSchemaConsistencyTests` (schema and `docs/content/json-content-pipeline.md` agree with reader constants); SC-005 walkthrough |
| FR-003 | PASS | Version rejection cases in `GameplayContentReaderTests`, `JsonContentBootstrapTests` |
| FR-004 | PASS | ID normalization/identity cases in `GameplayContentValidatorTests` |
| FR-005 | PASS | Pipeline doc "Future content families"; unknown-member rejection in `GameplayContentReaderTests` |
| FR-006 | PASS | Strict-parse rejection tests in `GameplayContentReaderTests` (28) |
| FR-007 | PASS | `GameplayContentValidatorTests` (53); Test/QA review found all 31 codes covered |
| FR-008 | PASS | Diagnostic-shape assertions in reader/validator tests; Security review (no paths, no player data) |
| FR-009 | PASS | `JsonDefinitionCatalogBuilderTests`, `GameplayContentOrderTests` |
| FR-010 | PASS | §1: Edit Mode file runs without a scene |
| FR-011 | PASS | `JsonDevelopmentContentTests` |
| FR-012 | PASS | Independent `AcceptedFoundationCatalogFixture`; `JsonDevelopmentContentTests`, `JsonContentPlayerTests.EmbeddedPackagedContentBuildsCatalogEqualToAcceptedFixture` |
| FR-013 | PASS (emulator-only) | Editor: `JsonContentBootstrapTests`, `CoreGameFoundationSceneTests`. Windows player: `JsonContentPlayerTests` (packaged scene content equals fixture). **Android: PASS on emulator (T082, #140), emulator-only; see the Android emulator section.** |
| FR-014 | PASS | §4 tests, incl. the built-player case against the real checkpoint store |
| FR-015 | PASS | `JsonContentAuthorityTests` (every presentation key resolves in the scene sprite catalog); unresolved-key rejection in bootstrap tests |
| FR-016 | PASS | `JsonContentCompatibilityTests`, `JsonContentPersistenceTests`; Save V1 tests unchanged in the 665 Edit Mode tests |
| FR-017 | PASS | `JsonContentPersistenceTests` (missing referenced ID rejected without file or live state change) |
| FR-018 | PASS | `SimulationEquivalenceTests`, `OfflineReconciliationTests`, `JsonContentCompatibilityTests` (D001, W001 outcomes identical) |
| FR-019 | PASS | Owner approval in `retirement-approval.md` (evidence commit `a4ddbc7`); retirement by T079; `JsonContentAuthorityTests`; §5 grep |
| FR-020 | PASS | Game Designer/PO and Game Systems findings in `json-content-migration-review.md` (values equal fixture, nothing added) |
| FR-021 | PASS | `json-content-migration-review.md`: Product Owner, Architect, Game Systems, Test/QA and Security verdicts on `c1aed9f`, each PASS-WITH-FINDINGS; UI/UX, Technical Art, Performance and DevOps omitted with reasons recorded |
| SC-001 | PASS | Parity fixture tests, `JsonDevelopmentContentTests`, player parity test |
| SC-002 | PASS | `JsonContentCompatibilityTests`, `JsonContentPersistenceTests`, `SimulationEquivalenceTests`, `OfflineReconciliationTests` |
| SC-003 | PASS | Named rejection tests for each edge-case class (reader 28, validator 53); Test/QA traceability in the migration review |
| SC-004 | PASS | `JsonContentBootstrapTests.RejectedDocumentsShowCodedErrorBeforeCheckpointAccess`; built-player `CorruptedContentIsRejectedAndRealCheckpointStoreStaysUntouched` |
| SC-005 | PASS | `json-content-migration-review.md` SC-005 walkthrough: valid document in 1 attempt (target at most 2); §2 mechanical check re-run here |
| SC-006 | PASS | `JsonContentAuthorityTests`; §5 grep; full suites green; console clean of relevant errors |
| SC-007 | PASS | `GameplayContentOrderTests` |
| SC-008 | PASS (measure-only) | §6: 0.0665 ms this run, 0.0612 ms in T076, 0.0320 ms baseline, 100 ms guard |

## Scope statements

- **Android:** the FR-013 Android row is closed by T082 (#140). Its evidence is emulator-only (ARM64 Development Player on the API 36 x86_64 AVD).
- **iOS:** deferred (Issue #39). Not exercised.
- **Physical Android devices and release qualification:** unverified.

## Android emulator (T082)

**EMULATOR-ONLY evidence.** Physical-device and release qualification are unverified. iOS is deferred (#39).

- **Code under test:** `c1aed9f` content (worktree base `e450e72`, no production changes).
- **APK build:** `Unity.exe -batchmode -quit -buildTarget Android -executeMethod IdleFactory.Editor.PlatformBuildCommands.BuildAndroidArm64DevelopmentGles3 -noProfilerAutoconnect`. Result: `Android result Succeeded, 0 errors, 0 warnings`, exit code 0, 00:02:54. APK 36,099,800 bytes, SHA-256 `52dbd6c41fe2bb80...` (not committed). The log states "Android Graphics APIs are automatic after the GLES3 emulator build". `ProjectSettings` noise (+134 lines) was discarded with `git checkout -- ProjectSettings`.
- **Device:** AVD `Medium_Phone_API_36.0` (API 36, 1080x2400), cold boot with host GPU (OpenGL ES 3.1, NVIDIA translator). `primaryCpuAbi=arm64-v8a` via `libndk_translation.so`. Development, IL2CPP, arm64-v8a player.
- **Journey** (`scripts/android-emulator-journey.ps1`, default steps): overall PASS.
  - Preflight: PASS (booted, AVD, ABI, GLES renderer).
  - Install: PASS (`adb install -r`, `pm clear`).
  - Launch: PASS (PID 2582, graphics device 4 s after process start, first display 3.54 s, OpenGL ES 3.1 device).
  - Rotation: PASS (sensor +x/-x/+y, lock/free; same PID throughout).
  - Home: PASS (departure checkpoint gen 11, return in the same process, return checkpoint gen 12).
  - Lock: PASS (device asleep, checkpoints gen 13 and 14, same process).
  - ColdRestart: PASS (process stopped, new PID 4375, reconciled from the last durable checkpoint gen 15, building set 1 -> 1, state hash `ab8889043bde1ac6` unchanged).
  - LogScan: PASS (0 FATAL/SIGSEGV lines in 15,266; 0 `[IdleFactory.Persistence]` lines).
- **Content-loading evidence:** the cold-restart screenshot (before any input) shows the HUD text "Development-only D001 factory is ready." and "Status: READY - a saved factory is available.", with the D001 machine rendered on the grid. The bootstrap ready status is a HUD string, not a logcat line. Logcat holds no `Development bootstrap` failure text and no `content.*`, `json.*`, `member.*` or `presentation.key.*` diagnostic. Every case-insensitive match of those patterns was an Android system line (`android.content.*`). The only Unity `E` lines are the two known noise lines (Play Asset Delivery `AssetPackManager` probe and the Autoconnect Profiler probe).
- **Noise observed:** the two known `E Unity` lines; `Failed to load native plugin '_burst_0_0'` (D level); ProjectSettings re-serialization (discarded).
- **Verdict:** FR-013 Android row PASS (emulator-only).

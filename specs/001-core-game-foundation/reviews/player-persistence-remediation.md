# T052 Player Persistence Remediation (Issue #81, WU40)

**Status: root cause proven and fixed. Windows Standalone (Mono) player: 4/4 PASS. Android ARM64 IL2CPP Development player on the API 36 emulator: 4/4 PASS (emulator-only evidence).** Physical Android devices were not tested, and this document makes no physical-device claim.

This task remediates T038 finding F1 ([windows-android-validation.md](windows-android-validation.md)). Traces: FR-023, FR-026, FR-027, FR-037, SC-004, SC-007.

Observed on 2026-09-25 (Europe/Berlin) on branch `maqa/issue-81-player-checkpoint-save` (base `988baea`). Unity `6000.6.0f1` was driven through MCP for Unity in the interactive editor.

## Root cause

`SaveV1Mapper.Serialize` rejected every checkpoint that the running game created. The mapper stores `departureUtcUnixMilliseconds` and deliberately refuses any instant it cannot represent losslessly (`GetUnixMillisecondsLossless`, covered by the existing test `SerializationRejectsSubMillisecondDepartureWithoutApproximation`).

The composition clock (`CoreGameFoundationBootstrap.DevelopmentUtcClock`) and `DeviceUtcClock` both returned raw `DateTimeOffset.UtcNow`. On Windows and Android, that value carries 100 ns ticks. `PersistenceCommands` passed the captured instant unchanged into the checkpoint, so `GetUnixMillisecondsLossless` threw `SaveSemanticException`. `Serialize` collapsed the exception into `InvalidCheckpoint` ("Save V1 semantic validation failed."), and `Publish` returned before it wrote any file. This explains the observed symptom exactly: the `v1` directory was created by the store's `Exists` probe, but it stayed empty, even without a `pending` file.

The existing tests missed the fault because every Edit Mode and Play Mode persistence test used millisecond-aligned fake clocks (`FromUnixTimeMilliseconds`) or `CompositionTestOverrides`. The Editor Play Mode run fails the same way with the real clock, so the fault is not specific to player builds.

**Stripping is not implicated.** The diagnostic identified the throw site `GetUnixMillisecondsLossless:770` in both players. No `link.xml` or `[Preserve]` was added. The T038 hypothesis (Newtonsoft reflection or managed stripping) is disproved.

### Reproduction evidence (before the fix, with diagnostics only)

| Player | Trigger | Log evidence | Checkpoint directory |
|---|---|---|---|
| Windows Standalone x64 (Mono, `test-results/t052/windows-prefix`, `IdleFactory.Unity.dll` SHA-256 `52bc5054…def63`) | Normal window close (`OnApplicationQuit` → `RecordSuspension`) | `[IdleFactory.Persistence] Serialize rejected at Semantic (SaveSemanticException) origin GetUnixMillisecondsLossless:770`, then `Publish rejected at Serialize` (`win-prefix-run1.log`) | `…\LocalLow\DefaultCompany\idle-factory-game\IdleFactory\Checkpoints\v1` stayed empty |
| Android ARM64 IL2CPP GLES3 Development (`android-prefix/CoreGameFoundation-gles3.apk`, SHA-256 `7965a7f6…8055`) on the cold-booted host-GPU emulator | `KEYCODE_HOME` | Same two warnings. The IL2CPP stack runs through `ApplicationLifecycleAdapter:RecordDeparture` → `PersistenceCommands:RecordSuspension` → `JsonCheckpointStore:Promote` → `SaveV1Mapper:Serialize` (`logcat-prefix-home.txt`) | `ls -la …/files/IdleFactory/Checkpoints/v1` listed only `.` and `..` |

The Editor Play Mode red run of the new regression suite failed the same way: 4 of 5 tests failed, and 3 of them showed the same diagnostic text. The clock test failed with `Expected: 0 But was: 6388` sub-millisecond ticks.

## Changes

| Path | Change |
|---|---|
| `Assets/IdleFactory/Unity/Persistence/CheckpointDiagnostics.cs` (new) | Privacy-safe diagnostic channel. Each report carries only a fixed operation and stage identifier, the exception **type name**, and a source `member:line`. It never includes exception messages, save bytes, IDs or paths. It raises the `Reported` event (used by tests) and a Unity warning (`LogToUnity`, default on). Listener exceptions are swallowed, so diagnostics cannot change persistence outcomes. |
| `Assets/IdleFactory/Unity/Persistence/SaveV1Mapper.cs` | `SaveStructureException` and `SaveSemanticException` record `[CallerMemberName]`/`[CallerLineNumber]`. Every catch that collapses into `InvalidCheckpoint`/`UnsupportedSaveVersion` now reports `Serialize` or `Deserialize` with a stage. Outcomes, messages and the catch filters are unchanged. |
| `Assets/IdleFactory/Unity/Persistence/JsonCheckpointStore.cs` | Each `InvalidCheckpoint`/`PersistenceFailed` exit in `Publish`, `RecoverPrevious` and `ConfirmDurable`, and in the write/read I/O boundaries, reports a named stage (for example `Publish.PendingReadbackMismatch` or `Write/Pending (IOException)`). Transaction logic is unchanged. |
| `Assets/IdleFactory/Unity/Persistence/DeviceUtcClock.cs` | **Fix.** `GetUtcNow()` floors to whole milliseconds with a zero offset (`FloorToMilliseconds`). |
| `Assets/IdleFactory/Unity/Bootstrap/CoreGameFoundationBootstrap.cs` | **Fix, required by the root cause.** The non-test path of `DevelopmentUtcClock` delegates to `DeviceUtcClock` instead of raw `DateTimeOffset.UtcNow`. The Editor test-override path is unchanged. No scene, prefab or ProjectSettings change. |
| `Assets/IdleFactory/Tests/PlayMode/PlayerCheckpointRoundTripTests.cs` (new) | Player-level regression suite that exercises the real `DeviceUtcClock`, the real `JsonCheckpointStore` (System.IO backend under `Application.temporaryCachePath`) and `SaveV1Mapper`. It uses no `CompositionTestOverrides` and no scene, so it runs in the Editor **and inside Windows and Android test players**. |
| `Assets/IdleFactory/Tests/EditMode/CheckpointMappingTests.cs` | `DeviceClockFloorProducesSerializableDepartureWithoutRounding`. |
| `Assets/IdleFactory/Tests/PlayMode/LifecyclePersistenceTests.cs` | `DeviceUtcClockReturnsZeroOffsetUtcTime` now expects a floored, millisecond-aligned reading. |
| `Assets/IdleFactory/Tests/PlayMode/CoreGameFoundationSceneTests.cs`, `SceneCompositionTestScope.cs` | Player-compile guards only (`yield break` after `Assert.Ignore`; the Editor-only override call is wrapped in `#if UNITY_EDITOR`). Without these guards, no Play Mode test player could be built (CS0161/CS0117), so the regression suite could not run in a player. Editor behavior is unchanged. |

### State ownership, compatibility and contracts

- **State ownership:** unchanged. `FactorySession`/`PersistenceCommands` own lifecycle and checkpoint identity. `JsonCheckpointStore` owns the fixed-slot transaction. The clock only supplies the UTC instant. `CheckpointDiagnostics` is stateless apart from its subscriber list.
- **Save compatibility:** no schema change. Save V1 bytes, field order, manifest layout, file names and validation strictness are identical. Saves written before this fix are compatible because none exist: the fault prevented any write in the running game. The pre-T051 fixture tests still pass.
- **Contract:** this change aligns with `data-model.md` ("Serialized UTC departure time uses Unix milliseconds") and `persistence-v1.md` ("UTC milliseconds must be a representable instant"). The mapper stays fail-closed on sub-millisecond instants. The device clock now produces only representable instants, as the contract assumed. No public command or port signature changed. The additive public API is `DeviceUtcClock.FloorToMilliseconds`, `CheckpointDiagnostics` and `CheckpointDiagnostic`.
- **Determinism:** flooring is monotonic and idempotent, so the nondecreasing departure rule (`max(source departure, captured return)`) still holds. Offline-credit resolution drops below one millisecond, which is well below the 100 ms simulation tick.
- **Error handling:** player-facing messages and `CommandOutcome` values are unchanged and generic. The new diagnostics expose only the type, stage and source-line identifiers. `SubMillisecondDepartureIsDiagnosedWithoutPlayerData` asserts that the diagnostic text contains no root path, checkpoint ID or definition identifiers. Unity logs the diagnostics as warnings, not errors, because the product already reports these outcomes explicitly.

## Player evidence

Evidence files are ignored and stored under `test-results/t052/`. Checkpoint files were decoded with a SHA-256 manifest/data verifier: it checks the manifest checksum, the data hash against the manifest, the generation, the `reconciledFromCheckpointId` prefix and the building count. Only ID prefixes are recorded.

### Windows Standalone x64 (Mono, project default), `windows-fixed` build: `Succeeded errors=0 warnings=0`, `IdleFactory.Unity.dll` SHA-256 `474f3caa…99a2`

| Check | Result | Evidence |
|---|---|---|
| Manual save writes a valid checkpoint | **PASS (command path in a player)** | The Windows **test player** (`PlayerWithTests.exe`, Mono, run with `-batchmode -nographics`; assembly path under `test-results/t052/windows-testplayer/…`) passed `ManualSaveWithDeviceClockWritesAndRestoresRealCheckpoint`: `PersistenceCommands.SaveNow` with the real clock and store, then a fresh-store read back (`runs/20260925T210226-StandaloneWindows64.xml`, 5/5 passed). The HUD Save button was **not** clicked on Windows, to avoid injecting synthetic mouse input into the owner's active desktop, where another fullscreen application had focus. The HUD path is covered by the Android row below. |
| Pause/background/quit writes a departure checkpoint | **PASS** | Game player run 1: a focus loss or window close wrote `manifest-a seq=1 → data-a gen=1`, with checksum and hash OK, `buildings=1`, and no diagnostics (`win-fixed-run1.log`). Run 2 quit wrote `manifest-a seq=3 → data-a gen=3`, with previous `gen=2`. |
| Relaunch restores it | **PASS** | Run 2 cold start: `manifest-b seq=2 → data-b gen=2 reconciledFrom=6346417d` (the gen 1 departure), with previous `data-a gen=1`. This is the promoted `ResumeAndReconcile` candidate, which exists only after the store read and strictly validated the departure checkpoint. No diagnostics (`win-fixed-run2-relaunch.log`). |
| Previous-generation recovery | **PASS (command path in a player)** | The Windows test player passed `PreviousGenerationRecoveryUsesTheRealStore`: `RecoverPreviousCheckpoint` promoted a generation above the second save, with `ReconciledFromCheckpointId` equal to the first save. |

### Android ARM64 IL2CPP, OpenGLES3-only Development APK (emulator only), `android-fixed/CoreGameFoundation-gles3.apk`, SHA-256 `7cc5b008…278b`

Environment: AVD `Medium_Phone_API_36.0`, cold-booted with `-gpu host -no-snapshot-load -no-snapshot-save`; SurfaceFlinger reported `OpenGL ES 3.1 (4.5.0 NVIDIA 591.86)`. Unity was the Android build target, with the GLES3 override restored afterwards. `pm clear` ran before the journey. Screen size is 1080x2400. The HUD panels still overlap (F2, owned by issue #82), but the tapped controls were reachable.

| Check | Result | Evidence |
|---|---|---|
| Manual save writes a valid checkpoint | **PASS** | HUD `Save now` tapped at `(84,168)`. The HUD showed "Checkpoint saved" (`screen-02-after-save-tap.png`). Result: `manifest-a seq=1 → data-a gen=1`, with checksum and hash OK. A second tap produced `manifest-b seq=2 → data-b gen=2`, with previous `gen=1`. |
| Pause/background/quit writes a departure checkpoint | **PASS** | `KEYCODE_HOME` (launcher resumed) → `manifest-a seq=3 → data-a gen=3 (8c3ce8a5)`, with previous `gen=2` (`pull-04-after-home`). |
| Relaunch restores it | **PASS** | `am force-stop` (PID gone), then launcher start (new PID `5316`). Result: `manifest-b seq=4 → data-b gen=4 reconciledFrom=8c3ce8a5`, with previous `gen=3`. The HUD showed the durable offline return summary (measured and credited 21.37 s, effective 17.1 s, output-capacity limit; `screen-05-cold-relaunch.png`). |
| Previous-generation recovery | **PASS** | HUD `Recover previous save` tapped at `(476,266)`. Result: `manifest-a seq=5 → data-b gen=5 reconciledFrom=8c3ce8a5`, with previous `gen=3`. The recovered generation supersedes gen 4 and derives from the retained previous generation (`pull-06-recover-previous`). |
| Diagnostics | none | `logcat-fixed-journey.txt` contains zero `[IdleFactory.Persistence]` lines. |

Android **test player** (IL2CPP ARM64, GLES3, installed and run by the Test Runner on the same emulator): `PlayerCheckpointRoundTripTests` 5/5 passed (`runs/20260925T211339-Android.xml`, assembly `/system/bin/arm64/IdleFactory.PlayModeTests.dll`).

## Configured suites (MCP `run_tests`, interactive editor, active target StandaloneWindows64)

- Edit Mode **546/546 PASS**.
- Play Mode **79/79 PASS**. New tests:
  - `PlayerCheckpointRoundTripTests.DeviceUtcClockReadingsAreRepresentableInSaveV1`
  - `ManualSaveWithDeviceClockWritesAndRestoresRealCheckpoint`
  - `SuspensionWritesDepartureCheckpointThatAFreshSessionRestores`
  - `PreviousGenerationRecoveryUsesTheRealStore`
  - `SubMillisecondDepartureIsDiagnosedWithoutPlayerData`
  - `CheckpointMappingTests.DeviceClockFloorProducesSerializableDepartureWithoutRounding`
- The editor console contains no errors.
- Environment note: in this interactive editor, `CoreGameFoundationSceneTests.AuthoredSceneRunsPlacementInputAndViewRecreationWithoutChangingWorldOwnership` fails with "Visible HUD controls must retain UI pointer ownership" whenever the Game view is at least 1366 px wide (Free Aspect 1468x714, 1920x1080 and 1366x768 all failed). It fails identically with this task's changes stashed. The suite is green at a fixed 640x480 Game view, which is also the batch-runner default. The failure depends on the HUD layout (F2 / issue #82), not on persistence.

## Serialized changes

Builds and target switches re-serialized `ProjectSettings/ProjectSettings.asset` (empty iOS/Android icon slots) and `ProjectSettings/EditorSettings.asset` (`m_EnterPlayModeOptions`). Both were reverted and are not committed. The temporary Graphics API override (Vulkan+GLES3 → GLES3) was restored. The active build target was switched back to StandaloneWindows64. A temporary editor-only test-result writer used for the test-player runs was deleted before commit.

## Residual risks

- Windows HUD button clicks were not exercised in the player (see above). IL2CPP on Windows was not built; the project's Windows player is Mono.
- Android evidence is emulator-only, through the ARM64 translation layer. Physical-device lifecycle timing (for example process death during a departure write) remains unverified.
- `CheckpointDiagnostics` logs warnings on every rejected load. This includes the many deliberate invalid-input tests and tampered saves. The warnings are privacy-safe but add log noise.

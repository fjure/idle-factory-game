# Android backup rules and release-manifest gate (T339, Issue #404)

**Task:** T339, plan step 12c, research F5-R18, SEC-3, SEC-4, SEC-6, OPS-4, OPS-6, decision D-M, FR-031.

**Status:** done for the backup rules, the release check, the GLES3 build and the emulator smoke. Two follow-ups are deferred and recorded below (PF-7 emulator calibration, DevTools overlay smoke).

**Emulator-only label.** Every emulator result is development evidence for AVD `Medium_Phone_API_36.0` (x86_64 host, arm64-v8a through `libndk_translation`, host GPU, cold boot, OpenGL ES 3.1 translator). It makes no physical-device, performance-qualification or release claim.

## What changed

- `Assets/Plugins/Android/AndroidManifest.xml`: `<application>` carries `android:allowBackup="true"`, `android:fullBackupContent="@xml/idlefactory_full_backup_content"` and `android:dataExtractionRules="@xml/idlefactory_data_extraction_rules"`. No FileProvider.
- `Assets/Plugins/Android/IdleFactoryBackupRules.androidlib/` (new): a resource-only Android library holding `res/xml/idlefactory_full_backup_content.xml` and `idlefactory_data_extraction_rules.xml`, plus a library `AndroidManifest.xml` and `build.gradle`.
- `scripts/check-release-build.ps1`: merged-manifest check (see below).

### Finding 1: `Assets/Plugins/Android/res` no longer exists in Unity 6

The first release build failed with `OBSOLETE - Providing Android resources in Assets/Plugins/Android/res was removed, please move your resources to an AAR or an Android Library`. The task text ("`res/xml` next to the existing manifest") is therefore not buildable. The rules now live in a `.androidlib` folder. Unity generates the library module itself, but AGP 9 then fails with `Namespace not specified`, so the folder carries a minimal `build.gradle` with `namespace "com.idlefactory.backuprules"`, `compileSdk 36`, `buildToolsVersion "36.0.0"`, `minSdk 26` and `targetSdk 36`. This is a library module file, not the app's `mainTemplate.gradle` or `launcherTemplate.gradle`, so the D-M "no Gradle template" rule still holds. Keep its SDK levels in step with Player Settings (the `**APIVERSION**` tokens of older Unity versions are no longer substituted). Owner note: this is a deviation from the literal task text.

### Finding 2: the persistence root is `<persistentDataPath>/IdleFactory`, not the bare root

On the emulator the files are at `/sdcard/Android/data/com.DefaultCompany.idlefactorygame/files/IdleFactory/SessionLog/segment-0.jsonl` and `.../IdleFactory/Checkpoints/v2/` (`SessionLogWriter.ApplicationDirectory = "IdleFactory"`; dev builds add `IdleFactory-Dev/`). Rules written for `SessionLog` and `Exports` at the domain root, as in the first draft, would not have excluded the real log. The final rules list, in domains `file` and `external` (Unity's `persistentDataPath` is `external` on this device, confirmed): `SessionLog`, `Exports`, `IdleFactory/SessionLog`, `IdleFactory/Exports`, `IdleFactory-Dev/IdleFactory/SessionLog`, `IdleFactory-Dev/IdleFactory/Exports`, `IdleFactory-Dev/SessionLog`, `IdleFactory-Dev/Exports`. The extraction rules apply the same list to `cloud-backup` and `device-transfer`. The release check requires the first four paths in both domains and every section.

## Release check on the real APK

Command: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-release-build.ps1 -BuildPath test-results/android/t339/release.apk` (release APK from `BuildAndroidArm64Release`, 28.5 MB, commit 6479801).

```
INFO   backup manifest source: APK merged manifest (aapt2)
check-release-build: PASS, 0 findings
```

aapt2 (SDK build-tools 36.0.0) reports in the APK: `allowBackup=true`, `fullBackupContent=@0x7f100001`, `dataExtractionRules=@0x7f100000`; the resource table maps them to `xml/idlefactory_full_backup_content` (`res/GU.xml`) and `xml/idlefactory_data_extraction_rules` (`res/Sy.xml`). The real `aapt2 dump xmltree` format matched the parser written in phase 1 without changes. The only provider in the merged manifest is `androidx.startup.InitializationProvider` (no FileProvider).

**Negative check.** Rule dumps taken from the real APK with every `Exports` exclude removed (real aapt2 text, through `ConvertFrom-Aapt2XmlTree` and `Test-BackupRuleNodes`) give findings such as `res/Sy.xml <cloud-backup> does not exclude Exports in domain 'file'` and the same for `full-backup-content`. Earlier hand-made manifests also failed for `allowBackup=false`, a FileProvider, a missing attribute and an `<include path=".">`. A negative run that rebuilds the APK with broken rules was not done (an IL2CPP rebuild per negative case); the real-format parser test above stands in for it.

## Builds

| Build | Method | Result |
|---|---|---|
| Release arm64 IL2CPP | `PlatformBuildCommands.BuildAndroidArm64Release` | success, `release.apk` 28.5 MB |
| GLES3 emulator (development, no profiler autoconnect) | `BuildAndroidArm64DevelopmentGles3` | success, `gles3.apk` 37.4 MB, automatic Graphics API restored |

Both were first run on a cold `Library/` and rebuilt after the rules changed (Finding 2).

## Emulator checks (GLES3 dev APK, cold-booted AVD, host GPU)

- `dumpsys package com.DefaultCompany.idlefactorygame`: `pkgFlags=[ DEBUGGABLE HAS_CODE ALLOW_CLEAR_USER_DATA ALLOW_BACKUP ]` (backup stays on; the rules limit what is backed up). The attributes themselves are not printed by `dumpsys`, the aapt2 dump above is the attribute evidence.
- Journey (`scripts/android-emulator-journey.ps1 -LeaseOwner issue-404 -Steps Preflight,Launch,Rotation,Home,Lock,ColdRestart,LogScan`): Preflight, Launch (OpenGL ES 3.1 graphics device, arm64-v8a), the five Rotation checks, Home and Lock return in the same process, ColdRestart new process, LogScan (0 FATAL/F DEBUG/SIGSEGV lines, 0 `[IdleFactory.Persistence]` lines) all PASS. The APK was installed with `adb install -r` by hand, because the emulator's `/data` was nearly full (see Findings below).
- Four journey checks FAIL, none caused by this task: `Home` and `Lock` departure/return checkpoint written (`none -> none`). `$deviceCheckpointDir` in the script is still `.../Checkpoints/v1`, while the game now writes `IdleFactory/Checkpoints/v2/run-checkpoint.*` (verified on the device). The script's checkpoint reader needs updating; it is outside T339 and was not changed.
- The session log is written on the device: `.../files/IdleFactory/SessionLog/segment-0.jsonl`.

### Other findings

- The emulator's `/data` was 92 percent used (505 MB free). `adb install -r` failed with `INSTALL_FAILED_INSUFFICIENT_STORAGE`, and so did the test-player install. Uninstalling our package and the leftover `com.UnityTestRunner.UnityTestRunner` (817 MB free) fixed it. `de.hamm.habittracker` was left alone. Clean the AVD before the next emulator batch.
- Unity batch runs end with `Killing ADB server`; adb had to be restarted before the journey, and the first journey run started while the device still read `authorizing`.
- Starting Unity with `Start-Process` without `-Wait` makes `$LASTEXITCODE` empty and lets a second command run against the same project; use `-Wait -PassThru`.

## PF-7 emulator calibration: not obtained (deferred)

`SESSIONLOG-FLUSH` is logged only by the Play Mode test `SessionLogAllocationTests` (category `StandalonePlayer`), not by the game APK. The attempt ran `Unity -batchmode -runTests -testPlatform Android -testFilter IdleFactory.Tests.PlayMode.SessionLogAllocationTests -testCategory StandalonePlayer` against the emulator. The test player built and installed, then the app died at start (`Force removing ActivityRecord ... app died` in logcat, right after `[Vulkan init]`), and the Test Framework ended with a remote-controller timeout. The test player uses Vulkan, which crashes in this emulator's translation layer (known, see the emulator setup notes); only `BuildAndroidArm64DevelopmentGles3` forces GLES3, and it builds the game, not the test player. A GLES3 test-player build would need new build code, which this task does not add. Result: the budget stays at 25 ms / 256 GC.Alloc samples, `SessionLogAllocationTests.cs` is unchanged, and no emulator ms or sample number is recorded. Windows reference stays 3.7 to 5.5 ms / 140 samples. Follow-up: a GLES3 test-player build method or a `-testPlatform Android` path in `scripts/test-unity.ps1` that applies the same GLES3 override.

## DevTools overlay smoke (#464 on #408): deferred to T351

No existing build method produces a DevTools plus GLES3 APK: `BuildAndroidArm64DevelopmentGles3` has no `IDLEFACTORY_DEV` define, and `BuildAndroidArm64DevelopmentDevTools` uses the project's default (Vulkan) Graphics API, which crashes on this emulator. Adding a combined method is new build code, so the smoke is deferred to T351. T340 (#405) also moves the overlay.

## Settings (OPS-6)

Android builds and the Unity batch runs re-serialized `ProjectSettings/ProjectSettings.asset` (about 136 changed lines). The noise was restored with `git checkout ProjectSettings/ProjectSettings.asset` before each commit, and `scripts/check-settings-noise.ps1` passed on the staged changes. The commit contains no `ProjectSettings/` change. The worktree's active build target was switched back to Windows after the builds.

## Residual risks

- No Android 11-and-lower device and no Android 12+ device with a real backup transport were tested; `fullBackupContent` and `dataExtractionRules` are verified as packaged and syntactically parsed, not by running `bmgr backup`.
- `build.gradle` hard-codes the SDK levels.
- The journey checkpoint steps need the `v2` path.

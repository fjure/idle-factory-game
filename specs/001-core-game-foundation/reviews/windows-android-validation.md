# T038 Windows and Android Validation

**Status: BLOCKED.** The Windows player build failed because the project build scene list is empty. Android Build Support and a named connected Android device are unavailable. The configured Unity test suites pass in the elevated run recorded below, but no physical-device acceptance is claimed. Touch, autorotation, safe-area, suspend/lock/return, and device persistent-path promotion/recovery remain unverified.

## Environment

Observed on 2026-09-25 in the T038 worktree:

| Check | Result |
|---|---|
| Host | Windows 11 Professional, version `10.0.26200`, as reported by Unity. |
| Unity | `6000.6.0f1`, revision `f7f8ed4d1e24`; editor at `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe`. |
| Windows player module | `Editor/Data/PlaybackEngines/WindowsStandaloneSupport` exists. |
| Android player module | `Editor/Data/PlaybackEngines/AndroidPlayer` does not exist. Android Build Support for the pinned Unity editor is unavailable; no module or SDK was installed for this task. |
| Android SDK | `ANDROID_HOME=C:\Users\Domenik\AppData\Local\Android\Sdk`; platforms `android-35` and `android-36`; build-tools `34.0.0`, `35.0.0`, `35.0.1`, `36.0.0`; platform-tools includes ADB `1.0.41` (`35.0.2-12147458`). `sdkmanager` is not on PATH and the SDK has no `cmdline-tools` directory. |
| ADB visibility | `adb` is not on PATH, but the SDK's `platform-tools/adb.exe` runs. `adb devices -l` reported `List of devices attached` with no device entries. Windows PnP enumeration returned Access denied, so no attached device could be independently named or inspected here. |
| Build scenes | `ProjectSettings/EditorBuildSettings.asset` has `m_Scenes: []`; no scene is configured in the player build scene list in this worktree. |

## Windows build attempt

The pinned editor and installed Windows player module were used for this batch build attempt:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe' `
  -batchmode -quit `
  -projectPath 'C:\Users\Domenik\ai-projects\idle-factory-game\.maqa-worktrees\issue-38-windows-android-validation' `
  -buildWindows64Player 'C:\Users\Domenik\ai-projects\idle-factory-game\.maqa-worktrees\issue-38-windows-android-validation\test-results\platform\windows\idle-factory-game.exe' `
  -logFile 'C:\Users\Domenik\ai-projects\idle-factory-game\.maqa-worktrees\issue-38-windows-android-validation\test-results\platform\windows\WindowsBuild.log'
```

The normal-context attempt exited with code `1` and produced no player executable. Its log first reports `unable to open database file`, then `Could not connect to IPC stream ... after 30.0 seconds` and `Could not establish a connection with the Unity Package Manager local server process`. Exact log: `test-results/platform/windows/WindowsBuild.log`.

After confirming there was no Unity or Unity Package Manager process and no other worker runner, one parent-authorized elevated retry used the documented process-scoped proxy bypass. Unity resolved packages and compiled the Windows player scripts, then failed at BuildPlayer: `'' is an incorrect path for a scene file. BuildPlayer expects paths relative to the project folder.` The log ends `Build Finished, Result: Failure` / `Failed to build player`; no executable was produced. Exact log: `test-results/platform/windows/WindowsBuild-elevated.log`. This confirms the empty `m_Scenes: []` build-settings list is a direct Windows build blocker. No project/build settings were changed.

An Android player build was not attempted because the pinned editor has no Android Player module. An Android SDK is present, but that does not substitute for Unity Android Build Support. No Android module, SDK component, package, or device software was installed or modified.

## Configured Unity suite

The configured command from `maqa-config.yml` was run in the normal context, then once more in the single parent-authorized elevated retry with the process-scoped proxy bypass:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-unity.ps1
```

The normal-context attempt exited with code `1` before Edit Mode produced result XML. Its log reports `Could not connect to IPC stream "Upm-15472" after 30.0 seconds` and `Could not establish a connection with the Unity Package Manager local server process`. Exact log: `test-results/unity/20260925T145243534Z/EditMode.log`; Play Mode was not reached in that attempt.

The elevated retry passed both suites on Unity `6000.6.0f1`: Edit Mode **545/545 passed, 0 skipped**; Play Mode **74/74 passed, 0 skipped**. XML and logs are `test-results/unity/20260925T145641587Z/EditMode-results.xml`, `EditMode.log`, `PlayMode-results.xml`, and `PlayMode.log`. This verifies the repository suites only; it does not substitute for Windows player or physical Android acceptance.

## Device acceptance disposition

No Android device was listed by ADB, and no device build could be installed. Therefore there is no device evidence for touch, autorotation, safe-area layout, suspend, screen lock, return/reconciliation, persistent-path checkpoint promotion, or previous-generation recovery. Editor/Play Mode behavior cannot be represented as physical Android evidence.

To resume T038, configure the intended scene in the player build settings, make Unity Android Build Support available for `6000.6.0f1`, and provide a named Android device visible in `adb devices -l`. Then capture exact Windows and Android build settings/results and exercise the named device through the required touch, rotation, safe-area, suspend/lock/return, and persistent-path promotion/recovery cases. Record device model and Android version, build identity, commands, logs, and per-case outcomes. T038 remains **BLOCKED** until both platform build evidence and required physical-device evidence are available and passing.

## Addendum — Android emulator discovered (2026-09-25)

The earlier empty `adb devices -l` result was a point-in-time check before starting the configured Android Virtual Device; it did not mean that no AVD was installed. A follow-up inspection confirmed:

| Check | Result |
|---|---|
| Android SDK | Installed at `C:\Users\Domenik\AppData\Local\Android\Sdk`; ADB is available at `platform-tools/adb.exe` (it is not on the current shell's `PATH`). |
| AVD | `Medium_Phone_API_36.0`, using `system-images/android-36/google_apis_playstore/x86_64`. |
| Emulator | Android Emulator `35.5.10.0`. |
| ADB after AVD startup | `adb devices -l` lists `emulator-5554` as `device` (`sdk_gphone64_x86_64`). |
| Guest OS | `sys.boot_completed=1`, Android release `16`, API level `36`. |
| Physical Android device | None is connected; the listed target is an emulator. |
| Unity Android Player | Still absent from `Editor/Data/PlaybackEngines`; the pinned Unity installation contains `WebGLSupport` and `WindowsStandaloneSupport` only. No Android Player build or app installation was performed. |

This AVD is available for exploratory ADB and UI-development checks. It does not close T038's physical-device acceptance: Unity 6.0 lists Android running in an emulator as unsupported Player hardware ([Unity 6.0 system requirements](https://docs.unity3d.com/6000.0/Documentation/Manual/system-requirements.html)), and the missing Unity Android Player module prevents a Unity Android build in this installation. No Android validation test or build was run for this addendum. T038 therefore remains **BLOCKED**; a named physical Android device and Android Build Support for Unity `6000.6.0f1` are still required for the specified device acceptance.

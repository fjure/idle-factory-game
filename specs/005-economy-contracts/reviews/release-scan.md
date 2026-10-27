# F005 release-build scan (T350, issue #415)

Batch D scan. Repeat once per later batch (append a new section).

| | |
|---|---|
| Build commit | `2b6b46e5661ddd6eec8b27e777ab60fe642a52a7` (origin/main after #504; the scan script fix is in the T350 commit on top) |
| Unity | `6000.6.0f1` (pinned), batch mode, `-quit`, one editor at a time under the `issue-415` unity+emulator leases |
| Android SDK | build-tools `36.0.0` (`aapt2` from `%LOCALAPPDATA%\Android\Sdk\build-tools\36.0.0`) |
| Output root | `test-results/release-scan/` (git-ignored, binaries not committed) |

## Builds

| Build | Method | Command (abridged) | Result |
|---|---|---|---|
| Windows 64 Mono, non-development | `IdleFactory.Editor.PlatformBuildCommands.BuildWindows64` (`BuildOptions.None`, no extra defines) | `Unity.exe -batchmode -quit -projectPath <wt> -buildTarget win64 -executeMethod ...BuildWindows64 -buildOutput test-results/release-scan/win64/IdleFactory.exe -logFile ...` | exit 0, `Succeeded`, 0 errors, 3 warnings, 101,198,230 bytes, 27.9 s |
| Android arm64 IL2CPP release APK | `IdleFactory.Editor.PlatformBuildCommands.BuildAndroidArm64Release` (`BuildOptions.None`, no DevTools define, no keystore) | `Unity.exe -batchmode -quit -projectPath <wt> -buildTarget Android -executeMethod ...BuildAndroidArm64Release -buildOutput test-results/release-scan/android/IdleFactory-release.apk -logFile ...` | exit 0, `Succeeded`, 0 errors, 3 warnings, 28,491,065 byte APK (Unity reports 568,111,042 bytes total build size), 3 min 47 s |

No earlier release-scan record exists for specs 003/004, so the commands above are taken from the `PlatformBuildCommands` doc comments.
"Killing ADB server" at Android shutdown is expected and no emulator was used.

| Artifact | SHA-256 | Size (bytes) |
|---|---|---|
| `test-results/release-scan/win64/IdleFactory.exe` | `a4c71b1bfc3e42d02f2f129fe04adef3ebf2343a662884e4b99598e9dfc90e7d` | player folder 101,198,230 |
| `test-results/release-scan/win64/IdleFactory_Data/boot.config` | `9ab9aadc392f9ad9dfb73e3d7ca91d2e96514ab739cadc320e94855576e718a2` | |
| `test-results/release-scan/android/IdleFactory-release.apk` | `01c6aa605c5219711100463b22728eac9eae9579df086ebc1807aa9138df7fd2` | 28,491,065 |

## Scans

Command: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-release-build.ps1 -BuildPath <path>`

| Scan | Output (verbatim) | Exit |
|---|---|---|
| Windows (`-BuildPath test-results/release-scan/win64`) | `check-release-build: PASS, 0 findings` | 0 |
| Android APK (`-BuildPath test-results/release-scan/android/IdleFactory-release.apk`) | `INFO   backup manifest source: APK merged manifest (aapt2)` / `check-release-build: PASS, 0 findings` | 0 |

Both scans include the ProjectSettings guards: `runInBackground: 0` and no `IDLEFACTORY_DEV` in `ProjectSettings.asset`.
Independent spot check: `global-metadata.dat` inside the APK contains 0 occurrences of `IdleFactory.DevTools`; the Windows `Managed/` folder has no `IdleFactory.DevTools*.dll`.
The Android project setting `AndroidMinSdkVersion: 26`; no explicit target API is stored (automatic), and the androidlib pins 36 (see below).

## Script fix (follow-up from #404/#416)

`scripts/check-release-build.ps1`, `Test-AndroidBackupManifest`: the gradleOut fallback for the `@xml/<name>` rules XML was guarded by `[string]::IsNullOrWhiteSpace($Apk)`, so with an APK given but aapt2 missing it could never run and the scan failed with `backup-rules-unresolved`. The guard is now `($null -eq $aapt2 -or [string]::IsNullOrWhiteSpace($Apk))`: the fallback runs when aapt2 is unavailable or no APK was given, but a stale gradleOut copy is never preferred when aapt2 can read the APK. `Resolve-RuleNodesFromGradleOut` still returns `$null` when nothing is found, so the finding remains and the scan fails closed. An INFO line reports a fallback read.

Verification (APK from this scan, aapt2 hidden for the child process only by clearing `ANDROID_SDK_ROOT`/`ANDROID_HOME` and pointing `LOCALAPPDATA` at an empty folder; no system change). `Temp/gradleOut` is deleted by Unity after the build, so a fixture gradleOut was assembled outside the repo from the committed androidlib rules XML plus a merged manifest with the same three attributes as the APK:

| Case | Result |
|---|---|
| Before fix, aapt2 hidden, fixture gradleOut | exit 1, 2x `FINDING [backup-rules-unresolved]` (bug reproduced) |
| After fix, aapt2 hidden, fixture gradleOut (manifest + rules) | INFO `aapt2 unavailable; using ...merged_manifests\release\AndroidManifest.xml`, two INFO `rules XML read from gradleOut`, `PASS, 0 findings`, exit 0 |
| After fix, aapt2 hidden, gradleOut with manifest only | 2x `FINDING [backup-rules-unresolved]`, exit 1 (fail closed) |
| After fix, aapt2 hidden, no gradleOut | `FINDING [backup-manifest] No merged manifest source ...`, exit 1 (fail closed) |
| After fix, aapt2 available | unchanged: PASS via aapt2, exit 0 |

## SDK-level note

`Assets/Plugins/Android/IdleFactoryBackupRules.androidlib/build.gradle` hard-codes `compileSdk 36`, `targetSdk 36`, `minSdk 26` and `buildToolsVersion "36.0.0"` (Gradle cannot read Player Settings). The decision was a doc note, not a guard or a gradle change (out of scope: Assets untouched). The note sits in the `check-release-build.ps1` header: change these values together with Player Settings > Android and the installed build-tools. This scan used build-tools 36.0.0, matching the file.

## Deviations and findings

- No findings on the real builds.
- `Temp/gradleOut` did not survive the build, so the fallback was verified with a fixture, not Unity's real output.
- The build rewrote `ProjectSettings/ProjectSettings.asset` (known settings noise); it was discarded with `git checkout`, and `scripts/check-settings-noise.ps1` passes.
- Untracked `.utmp/` appeared in the worktree (not created by this task's scripts); it is not committed.

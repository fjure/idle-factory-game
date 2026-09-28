# Unity Profiler (Standalone Process) for the Android emulator player

**Verified**: 2026-09-28, Europe/Berlin (T064, Issue #114)
**Scope**: the development GLES3 player on AVD `Medium_Phone_API_36.0` (`emulator-5554`), Unity `6000.6.0f1`, this Windows workstation

This is the profiling path for the Android emulator player (T040-F2, owner decision 2026-09-28). In T040 the in-editor Profiler slowed the player's cold start from "Fully drawn" +14–16.5 s to +33–48 s and stalled the player after scene load, so no run reached the capture-start state. With this procedure the player reached that state with the standalone profiler attached in all three T064 runs; see the [T064 section of `mobile-performance.md`](../../specs/001-core-game-foundation/reviews/mobile-performance.md#t064-unity-profiler-standalone-process-set).

All results are emulator-only development evidence. A frame time measured under the profiler is distorted and never a performance claim.

## Facts the procedure depends on

- **The player build must not autoconnect.** A `BuildOptions.ConnectWithProfiler` ("Autoconnect Profiler") player opens its own profiler connection to the build host at startup. On this host it reached `192.168.2.172:34999`, where the main editor and the standalone profiler both listen, through the emulator NAT (and through the editor's `adb reverse tcp:34999`). The player streams its frames over that connection. So an autoconnect player sent nothing to the standalone profiler whenever the main editor took its connection, and the main editor cannot be closed (next point). Build the profiling APK with `-noProfilerAutoconnect`. This is also the most likely mechanism of the T040 stall: the autoconnect player was already connected to the editor when the editor opened a second, direct connection. See [`t064-evidence/discarded-attempts.txt`](../../specs/001-core-game-foundation/reviews/t064-evidence/discarded-attempts.txt).
- **The standalone profiler is a child of the editor.** **Window > Analysis > Profiler (Standalone Process)** starts a second `Unity.exe` with `-ump-process-role profiler -editor-mode profiler`. Closing the main editor closes the profiler too (verified 2026-09-28). The main editor must stay open for the whole protocol. Keep it idle: no Play Mode, no build, no test run, no open in-editor Profiler window, and no asset import.
- **Both processes load MCP for Unity.** They register with the MCP server under the same instance ID and keep replacing each other's session, so an MCP call lands on either process. Stop the main editor's MCP bridge before you open the standalone profiler (step 3). `profiler-mcp.ps1` refuses every profiler snippet unless the answering process has role `profiler`.
- **The profiler window owns the recording state** in Unity `6000.6`. `ProfilerDriver.enabled = true` alone records no player frame. Use the toolbar's record button or `ProfilerWindow.SetRecordingEnabled(true)`. Switching recording off and on inside one editor frame reached the player as no change and recorded nothing. The protocol therefore connects with recording off, switches it on once, waits for the first frame, clears the buffer, and only then starts the 60 s window.
- **Connect only after the player listens.** Before the player logs `Player connection [...] Started UDP target info broadcast` (about 2 s after `am start -W` returns), adb accepts the forward and the connection drops at once. After that line the attach holds through scene load.
- **`DirectIPConnect` toggles.** Calling it again while already connected to the same address disconnects, and the profiler falls back to *Main Editor Process*. Check the returned identifier after every connect. A direct connection is labelled "Autoconnected Player" even for a player built without autoconnect.
- **A hung snapshot request blocks every later one** ("Cancelling snapshot, there is another snapshot in progress") until the standalone profiler is closed and reopened. Take snapshots only on a streaming connection.
- **The Screenshots module lives in the player.** `ProfilerScreenshotFull` (20,736,976 B) and ten `ProfilerScreenshotFinal*` render textures (74,704 B each) are profiler instrumentation. In T064 they were all of the player's render-texture memory.

## Host and device preconditions

1. **Exclusive emulator use.** No other session may use `emulator-5554` for the whole protocol: no parallel Claude or Codex session, no `scripts/android-emulator-journey.ps1`, and no Unity batch test run or Android build (these end with "Killing ADB server"). Check for foreign `adb`/`monkey` processes (`Get-CimInstance Win32_Process` command lines) and for running sessions, ask the user to pause them, and record the check.
2. **Processes.** Close every other Unity editor and every Unity batch process. Keep exactly: the emulator (`emulator.exe`, `qemu-system-x86_64.exe`), one `adb` server, one idle main editor on this checkout, the standalone profiler, and the MCP server (`mcp-for-unity`, `python`). Opening the main editor terminates a foreign `adb` server ("Multiple ADB server instances found…"); run `adb devices` afterwards and wait until `emulator-5554` is `device`.
3. **Emulator.** Cold boot with host GPU: `emulator -avd Medium_Phone_API_36.0 -gpu host -no-snapshot-load` (quick boot restores SwiftShader and breaks the ARM64 Unity player).
4. **Build** with the editor closed:

   ```powershell
   & 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe' -batchmode -quit -projectPath . -executeMethod IdleFactory.Editor.PlatformBuildCommands.BuildAndroidArm64DevelopmentGles3 -noProfilerAutoconnect -buildOutput test-results/android/<task>/CoreGameFoundation-noautoconnect-gles3.apk -logFile test-results/android/<task>/build.log
   ```

   The log must say `profiler autoconnect False`. Discard the known iOS icon-slot serialization in `ProjectSettings/ProjectSettings.asset` afterwards (`scripts/check-settings-noise.ps1`).
5. **Install and verify.** `adb -s emulator-5554 install -r <apk>`, then compare the installed `base.apk` SHA-256 with the local APK. Every scripted phase re-checks it with `-ExpectedApkSha256`.
6. **Device checkpoint.** Pull the device's current `Checkpoints/v1` directory with its own `adb pull` command and check that five files arrived before anything else touches the device. The fixed start state is the T040 source checkpoint (generation 9, `43b3aaec-ad1d-4fbb-888a-80e88e9c0a1a`, five files, SHA-256 in `t040-evidence/environment-and-build.txt`).
7. **One unscored launch after every install**: `profiler-protocol.ps1 -Phase Unscored`, discarded.

## Procedure

Scripts: [`specs/001-core-game-foundation/reviews/t064-evidence/`](../../specs/001-core-game-foundation/reviews/t064-evidence/). Run them from the repository root.

1. Open the main editor on this checkout and wait until it is idle.
2. `profiler-mcp.ps1 -Snippet main-editor-prepare`: runs in the main editor, closes any restored in-editor Profiler window, and stops the main editor's MCP bridge 5 s later.
3. **Window > Analysis > Profiler (Standalone Process)**; confirm the "3–4 seconds to launch" dialog. `profiler-mcp.ps1 -Snippet role` must print `role=profiler`.
4. For each run N = 1, 2, 3:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File specs/001-core-game-foundation/reviews/t064-evidence/profiler-protocol.ps1 -Phase Run -Run N -RawRoot test-results/android/<task>/set -ExpectedApkSha256 <sha256>
   ```

   - **Prepare**: restore the five checkpoint files and cold launch (`am start -W`). Wait for the player's listening line, then run `adb forward tcp:34999 localabstract:Unity-com.DefaultCompany.idlefactorygame` and `connect` (`ProfilerDriver.DirectIPConnect("127.0.0.1:34999")`, recording off). The profiler is attached while the scene still loads. After the 30 s no-input warm-up (counted from the return of `am start -W`, as in T040), check that the profiler still holds the player. Pull the checkpoint, compute the metadata-free factory-state SHA-256 with the T040 function, and take the capture-start screenshot.
   - **Capture**: record the host process list. Run `start-recording` (one off-to-on switch), wait for the first frame, run `clear-frames`, and record for 60 s. Then `stop-and-export` saves the `.data` capture, the per-frame counters and the GC.Alloc sources. `memory-snapshot` saves the player's `.snap`, and `snapshot-residency` reads per-asset residency with the editor's built-in `PackedMemorySnapshot`, without the Memory Profiler package.
   - **Finish**: capture-end screenshot, `dumpsys meminfo`, logcat scan (`FATAL EXCEPTION`, `F DEBUG`, `SIGSEGV`, `E Unity`, activity starts), force-stop.
5. `summarize-runs.ps1 -RawRoot test-results/android/<task>/set -OutCsv <csv>` writes one row per run.
6. Restore the device checkpoint from precondition 6 (`-Phase Restore -SourceCheckpoint <dir>`), pull it again and compare all five hashes, then `adb forward --remove-all`. Close the main editor, which also closes the standalone profiler and ends the stopped MCP bridge, and release the emulator.

A run counts only when its capture-start state matches the fixed checkpoint and the profiler still holds the player at capture start. The matching state is a generation-10 checkpoint reconciled from `43b3aaec-…`, with factory-state SHA-256 `d989757b…1bda` and the T040 screenshot `82f943eb…be407`. The run also needs one process ID through Prepare, Capture and Finish, and no foreign activity start in its logcat. If the attached profiler prevents that state, record the exact blocker instead of partial data.

## Manual equivalent (no MCP)

Use the same build and steps 1 and 3. After the player's listening line, create the forward and choose *Direct Connection > <Enter IP>* `127.0.0.1:34999` in the profiler toolbar. Selecting *Local > Devices > …@ADB:emulator-5554* makes Unity create its own forward. Press record once, wait until frames appear, clear, record the 60 s window, stop, and save the capture (**File > Save**). This records the same data, but the per-frame export and the snapshot residency need the snippets or the Memory Profiler package.

## Reporting rules

- Frame times under the profiler are **distorted**. In T064 the attached profiler slowed the idle player to 3–5 recorded frames per second, against about 30 FPS unprofiled. Report them only as labelled context, never against T040/T065 frame pacing or a target.
- Per-frame allocation values inherit that distortion when a source runs per `FixedUpdate`. Report per-call sizes and per-window totals next to per-frame values.
- Counts (batches, SetPass calls, draw calls, triangles, vertices, objects) and bytes are not time-scaled, but the profiler adds its own memory (screenshot buffers, profiler allocators).
- Compare runs only within one session, because host load differs between sessions.
- No threshold is implied. Physical-device qualification remains unverified.

# Android emulator journey - 2026-11-25 00:31

**Emulator only**: development evidence for this AVD and its ARM64 translation layer. No physical-device, performance-qualification or release claim.

| Environment | Value |
|---|---|
| Serial | `emulator-5554` |
| Avd | `IdleFactory_Test_API_36` |
| Fingerprint | `google/sdk_gphone64_x86_64/emu64xa:16/BP22.250325.006/13344233:user/release-keys` |
| Api | `36` |
| Abis | `x86_64,arm64-v8a` |
| NativeBridge | `libndk_translation.so` |
| Gles | `Google (NVIDIA Corporation), Android Emulator OpenGL ES Translator (NVIDIA GeForce RTX 5070 Ti/PCIe/SSE2), OpenGL ES 3.1 (4.5.0 NVIDIA 621.01)` |
| Display | `1080x2400` |
| Apk | `C:\Users\Domenik\AppData\Local\Temp\t542\t542.apk (46722286 bytes, SHA-256 62dbdb293f522fed2b7c63b87683028e188a991a11bb638577ef3ae2960d7c5f)` |
| UnityBuild | `Development, il2cpp, arm64-v8a` |

| Step | Check | Result | Detail |
|---|---|---|---|
| Environment | emulator lease | **INFO** | emulator: renewed for 'issue-714' (61 min) |
| Preflight | device booted | **PASS** | emulator-5554 |
| Preflight | expected AVD | **PASS** | IdleFactory_Test_API_36, API 36 |
| Preflight | arm64-v8a via translation | **PASS** | x86_64,arm64-v8a; bridge libndk_translation.so |
| Preflight | GLES renderer | **INFO** | Google (NVIDIA Corporation), Android Emulator OpenGL ES Translator (NVIDIA GeForce RTX 5070 Ti/PCIe/SSE2), OpenGL ES 3.1 (4.5.0 NVIDIA 621.01) |
| Install | /data free space | **PASS** | 3606 MB free (54 percent used), floor 400 MB |
| Install | adb install -r | **PASS** | Success |
| Install | pm clear | **PASS** | Success |
| Launch | process started | **PASS** | PID 12107 |
| Launch | time to graphics device | **INFO** | 2 s after process start |
| Launch | first scene loaded (playable shell) | **PASS** | 4 s after the graphics device |
| Launch | primary ABI | **PASS** | arm64-v8a |
| Launch | Unity build | **INFO** | Development, il2cpp, arm64-v8a |
| Launch | graphics device | **PASS** | Creating OpenGL ES 3.1 graphics device ; Context level  <OpenGL ES 3.1> ; Context handle -747085888 |
| Launch | time to first display | **INFO** | 1s261ms |
| Launch | display | **INFO** | ROTATION_0 1080x2400, requested SCREEN_ORIENTATION_FULL_USER |
| Launch | screenshot | **INFO** | 01-launch.png |
| W003Push | committed w003-s-shipped/run-checkpoint.data-a.json | **PASS** | SHA-256 4f7ed559fb1a396a |
| W003Push | committed w003-s-shipped/run-checkpoint.manifest-a.bin | **PASS** | SHA-256 781810f0d39f3069 |
| W003Push | device w003-s-shipped/run-checkpoint.data-a.json | **PASS** | SHA-256 4f7ed559fb1a396a on the device |
| W003Push | device w003-s-shipped/run-checkpoint.manifest-a.bin | **PASS** | SHA-256 781810f0d39f3069 on the device |
| F006Start | device set loaded | **PASS** | reconciled from 22222222-2222-2222-2222-000000000030 after 8 s; gen 1, id 498e77fa, run 1, cash 5681865, 12 machine(s), levels 331, prestige 0, data ec56d07f9859 |
| F006Start | UI ready before the first tap | **INFO** | surface present after 2 s |
| F006Start | portrait for the taps | **PASS** | 1080x2400; the F006 default taps are portrait fractions |
| F006Start | screenshot (after Welcome back) | **INFO** | 02-f006-start.png |
| F006Select | tap source: select machine | **INFO** | 0.5,0.45 of 1080x2400 -> tap (540,1080). World band middle: between the top strip and the bottom action row of the portrait shell (FactoryRun.uss .run-body); the camera frames the depot column (T558), so the middle row of the factory is near the centre. |
| F006Select | UI ready before the first tap | **INFO** | surface present after 2 s |
| F006Select | machine card docked (card band changed) | **PASS** | card band change 0.884 (floor 0.15). Screenshots: 03-f006-select-before.png, 04-f006-select-card.png |
| F006Upgrade | tap source: Upgrade | **INFO** | 0.5,0.815 of 1080x2400 -> tap (540,1956). Upgrade is the pinned button of the docked card in its first view (contracts/ui-surfaces-f006.md section 2), in the lower part of the bottom band, above the action bar. |
| F006Upgrade | UI ready before the first tap | **INFO** | surface present after 2 s |
| F006Upgrade | screenshot (after Upgrade) | **INFO** | 05-f006-upgrade.png |
| F006Upgrade | machine level raised (checkpoint level sum) | **PASS** | gen 1, id 498e77fa, run 1, cash 5681865, 12 machine(s), levels 331, prestige 0, data ec56d07f9859 -> gen 3, id 175d7834, run 1, cash 5682326, 12 machine(s), levels 332, prestige 0, data cbe79d6c438e |
| F006HubContracts | tap source: hub entry | **INFO** | 0.15,0.972 of 1080x2400 -> tap (162,2333). Persistent layer action bar, hub entry (see Get-HubTapMap). |
| F006HubContracts | UI ready before the first tap | **INFO** | surface present after 2 s |
| F006HubContracts | hub opened (sheet region changed) | **PASS** | change 0.791. Screenshots: 06-f006-hub-0-factory.png, 07-f006-hub-1-open.png |
| F006HubContracts | tap source: Contracts card | **INFO** | 0.5,0.565 of 1080x2400 -> tap (540,1356). Hub list row 2 (HubCards order), 48 dp rows (Get-HubTapMap). |
| F006HubContracts | UI ready before the first tap | **INFO** | surface present after 2 s |
| F006HubContracts | Contracts view opened (sheet region changed) | **PASS** | change 0.739. Screenshots: 07-f006-hub-1-open.png, 08-f006-hub-2-contracts.png |
| F006HubContracts | Back left the Contracts view (sheet region changed) | **PASS** | change 0.205. Screenshots: 08-f006-hub-2-contracts.png, 09-f006-hub-3-back.png |
| F006HubContracts | residual difference to the factory before the hub | **INFO** | 0.419 in the sheet region (Cash and production tick); the decisive check that the factory is back is the screenshot 09-f006-hub-3-back.png |
| F006HubContracts | same process | **PASS** | PID 12261 |
| F006Build | tap source: empty location | **INFO** | 0.5,0.30 of 1080x2400 -> tap (540,720). An empty unlocked location of the world band (the build card opens on a tap there, T539); the depot-column framing keeps it in the upper part of the band. |
| F006Build | UI ready before the first tap | **INFO** | surface present after 2 s |
| F006Build | build card docked (card band changed) | **PASS** | change 0.452. Screenshots: 10-f006-build-0-before.png, 11-f006-build-1-card.png |
| F006Build | tap source: type row, Confirm | **INFO** | 0.5,0.78 0.72,0.90 of 1080x2400 -> tap (540,1872); tap (778,2160). Build card (contracts/ui-surfaces-f006.md section 5): the first type row, then Confirm on the right of the Cancel and Confirm pair at the bottom of the band. |
| F006Build | UI ready before the first tap | **INFO** | surface present after 2 s |
| F006Build | screenshot (after Confirm) | **INFO** | 12-f006-build-2-confirm.png |
| F006Build | machine built (checkpoint machine count) | **FAIL** | gen 3, id 175d7834, run 1, cash 5682326, 12 machine(s), levels 332, prestige 0, data cbe79d6c438e -> gen 6, id 0eaf2403, run 1, cash 5683832, 12 machine(s), levels 332, prestige 0, data 2f2c0cc83199 |
| F006BuildRotation | UI ready before the first tap | **INFO** | surface present after 2 s |
| F006BuildRotation | screenshot (build card open, portrait) | **INFO** | 13-f006-buildrot-0-portrait.png |
| F006BuildRotation | rotated to landscape with the card open | **PASS** | ROTATION_90 2400x1080, requested SCREEN_ORIENTATION_FULL_USER |
| F006BuildRotation | screenshot (landscape, card open: docked in the side column, selected location inside the unobstructed rectangle) | **INFO** | 14-f006-buildrot-1-landscape.png |
| F006BuildRotation | screenshot (back in portrait) | **INFO** | 15-f006-buildrot-2-portrait.png |
| F006BuildRotation | same process | **PASS** | PID 12261 |
| F006BuildRotation | no coded diagnostics during the rotations | **PASS** | no return.blocked / save.v2.* / persistence diagnostic |
| F006BuildRotation | screenshot (after Back: card closed) | **INFO** | 16-f006-buildrot-3-closed.png |
| F006Autosave | UI ready before the first tap | **INFO** | surface present after 2 s |
| F006Autosave | UI ready before the first tap | **INFO** | surface present after 2 s |
| F006Autosave | change made | **INFO** | select and Upgrade taps (same fractions as F006Select and F006Upgrade); waiting 35 s with no lifecycle event (AutosavePolicy.DefaultIntervalMs 30000) |
| F006Autosave | periodic checkpoint written without a lifecycle event | **PASS** | gen 11, id 47727ab1, run 1, cash 5684714, 12 machine(s), levels 332, prestige 0, data d1573740decf -> gen 13, id a396c380, run 1, cash 5683537, 12 machine(s), levels 333, prestige 0, data 406402793cb1 |
| F006Autosave | same process while waiting | **PASS** | PID 12261 |
| F006Autosave | process stopped (no HOME) | **PASS** | PID 12261 gone |
| F006Autosave | relaunch restored the autosaved state | **PASS** | gen 13, id a396c380, run 1, cash 5683537, 12 machine(s), levels 333, prestige 0, data 406402793cb1 -> gen 14, id 7841faca, run 1, cash 5684059, 12 machine(s), levels 333, prestige 0, data 2d70f3c66b54 |
| F006Zoom | UI ready before the first tap | **INFO** | surface present after 2 s |
| F006Zoom | UI ready before the first tap | **INFO** | surface present after 2 s |
| F006Zoom | screenshot (Settings view) | **INFO** | 17-f006-zoom-0-settings.png |
| F006Zoom | Show zoom buttons | **INFO** | no -ZoomPrefTaps supplied (layout dependent); the preference is left as it is |
| F006Zoom | tap source: zoom-in | **INFO** | 0.063,0.86 of 1080x2400 -> tap (68,2064). Zoom column, "+" button (FactoryRun.uss .run-zoom; Get-HubTapMap). |
| F006Zoom | UI ready before the first tap | **INFO** | surface present after 2 s |
| F006Zoom | zoom-in changed the world band | **INFO** | world band change 0.374; buttons are hidden on touch without the preference, so 0 is expected. Screenshots: 18-f006-zoom-2-before.png, 19-f006-zoom-3-after.png. Pinch zoom stays Play Mode only. |
| F006LogScan | no crash signatures | **PASS** | 0 FATAL/F DEBUG/SIGSEGV line(s) in 7140 |
| F006LogScan | no coded diagnostics | **PASS** | 0 persistence/start-up diagnostic line(s) |
| F006LogScan | E Unity lines | **INFO** | 3, of which 0 outside the known benign AssetPackManager and Autoconnect Profiler probes |

Result: FAIL (1 check(s)). Raw evidence: this directory.

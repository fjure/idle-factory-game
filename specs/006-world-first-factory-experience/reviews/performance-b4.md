# B4 performance measurement, W006-V (T531 B4a, T544 B4b)

This record has two parts. **Part B4b (T544, below) is the final reading on the current main**; **Part B4a (T531) follows unchanged as history** (its numbers stand for the main of 2026-11-19, `b44b87d`). Evidence for FR-006, SC-008 and research R16. Budgets are the frozen F004 ones of `specs/004-visual-identity/reviews/performance-budgets.md` section 4 (row 8 as restated 2026-11-05 and 2026-11-19).

**Every number is exploratory evidence from one workstation (FR-027). There is no physical-device, battery or thermal measurement; that gap stays open.** Nothing in T531 or T544 changes production code. Art Bible 21.12 is confirmed by the owner (v1.1.2, worst-case ceiling 110); the census is also reported against the signed 95.

# Part B4b (T544, Issue #716)

Performance Engineer (Medium tier). Base `origin/main` 4d795384 (T560, T561 camera/marker/Back, T563 upgrade marker and the per-frame affordability mask of `RunShellPresenter` are in). Unity 6000.6.0f1, Windows 11 host (AMD Ryzen 7 7800X3D, RTX 5070 Ti, NVIDIA driver 621.01), emulator AVD `IdleFactory_Test_API_36` (GLES 3.1, host GPU, 1080x2400), method of Part B4a (its sections 1 to 8). Worktree `.maqa-worktrees/issue-716-t544-performance-b4b`. Raw evidence of this part: `performance-b4/b4b/` (Windows player log lines, device probe lines, SurfaceFlinger capture summaries, the autosave journey report, the GPU samples).

## B4b.1 Budget table (B4b)

W006-V as in B4a section 1 (card open = the real docked machine card of the packaged scene, build card = 11 machines). New in B4b: the upgrade marker (T563) is part of the shipped world, so SetPass and census are also read with the marker forced on all 12 slots.

| # | Budget (frozen) | B4b measured | Status | Method |
|---|---|---|---|---|
| 1 | Emulator avg FPS >= 60 (exploratory; **not on R16's list**, see B4b.5) | W005-L producing **46.3** (first session), 44.6 (ABA, session 2), 46.6 (cold boot); W003-S producing 46.7; fresh-run controls 44.4 to 47.9. **ABA with the B4a-era build (`b44b87d`) on the same emulator and host: 44.8 and 43.8** | **Not met in this session, by either build** (B4a read 61.4 in its session) | `android-emulator-journey.ps1 -Scenario F005 -Steps ...W005LFps` and `-Scenario F003 ... ProducingFps`, profiler-free dev APK, SurfaceFlinger TimeStats 60 s. Host: B4b.4 |
| 2 | p95 bucket <= 19 ms (emulator, exploratory; not on R16's list) | p50 16 ms, **p95 33 to 44 ms**, p99 46 to 48 ms (B4a-era build in the same session: p95 34 to 44) | **Not met in this session, by either build** | same captures |
| 3 | 0 intervals >= 50 ms per 60 s (exploratory; not on R16's list) | producing captures: 0, 0, 2, 3 (B4b) and 4, 4 (B4a-era build); controls 0 to 7 | **Not met reliably, by either build** | same captures (`fps-captures.tsv`) |
| 4 | Windows host frame p95 in 0.02 to 0.04 ms (ceiling 1 ms) | **W003 (the budget's workload): S 0.016, B 0.040, R 0.021 ms**. **W006-V** (new, C-69): synthetic world frame (clock, deck, links, deposit, effects; 600 frames) p50 0.018, **p95 0.022**, p99 0.025, max 0.072 ms; real scene `RunShellPresenter.Refresh` per frame: steady frames p95 0.052 to 0.060 ms, all frames p95 0.20 to 0.37 ms (the 18 to 31 % of frames that rebuild the snapshot), single worst frames 1.3 to 3.5 ms | **PASS** (W003 and the W006-V world frame inside 0.02 to 0.04; the real-scene Refresh including rebuilds is above that range, its p95 is below the 1 ms ceiling, and single rebuild frames reach 1.3 to 3.5 ms) | official player suite `W003-HOSTFRAME`, `B4B-W006V-600`, `B4B-REFRESH-REAL` |
| 5 | 0 steady-state GC allocations (advance path, presentation paths, 600-frame player run) | W006-V synthetic 600 frames: **0** (no contract; 540 non-rebuild frames with a contract: 0). **C-94: 600 `RunShellPresenter.Refresh` calls over an unchanged run: 0 allocations**, in the real packaged scene with the card closed (asserted) and with a card open (0 as well), in the Windows player and in Play Mode; moving cash without crossing a cost: 0 in Play Mode. Whole host frame in the real scene at 60 FPS: **steady about 20 per frame card closed, 17 to 21 card open, 15.5 build card** (B4a: 21.5, **403**, 280) | **PASS for the defined paths and for `Refresh`; whole real-scene frame still not 0** (about 20 per steady frame, unattributed, C-68). **The card-open cost of B4a (C-41 extension) is gone** (T560) | `GcAllocationCounter`, per-frame `GC.Alloc` recorder, player (B4b.3) |
| 6 | World-art texture memory <= 8 MB | build report **3.54 MB** (F004 pages 2.23, F006 world and FX 1.31; identical to B4a); player runtime of the pages in use 3.01 to 3.26 MiB, **3.39 MiB with the markers forced** (the 24x24 upgrade icon is its own texture); device 3.01 to 3.26 MiB | **PASS** | build report, `Profiler.GetRuntimeMemorySizeLong` |
| 7 | Atlas pages <= 1024 x 1024 | largest **512x512**, all RGBA32 (also the 512 and 1 MiB rule of Art Bible 21.9) | **PASS** | player, build report |
| 8 | SetPass per world-camera render, restated: F006 delta at most **+12 over the F004-only 34**, `camera.Render()` CPU tier bound | all **42.1** (landscape), 44.0 (portrait), 42.7 / 45.9 (card open), 38.6 (build card); F004 pages only in the same state 33.6, 34.2, 33.3, 35.5, 27.2: **F006 delta +8.5 to +11.4**. **With the upgrade marker forced on all 12 slots: 46.5 / 48.7; F004-only in that state 35.1 / 39.1; delta +11.4 / +9.7.** `camera.Render()` CPU 0.16 to 0.22 ms mean (p95 0.18 to 0.26; 0.24 with markers) | **PASS on the restated reading (delta over the F004-only value measured in the same state). Reading it against the fixed 34, the marker worst case is +12.5 (landscape) and +14.7 (portrait), over +12: owner reading needed (B4b.2)** | player SetPass differential, 20 renders per frame, `ProfilerRecorder` |
| 9 | Phase-0 guest CPU against a paired B1 (not on R16's list) | not measured | **Gap** (no paired B1 build or CPU capture; B4b.5) | - |
| 10 | APK <= 150 MB | **46,778,974 B** (44.6 MiB, 31 % of the cap; +11,484 B on the B4a build), sha256 `34e3a24d516516d123df25a23c9ebcd15767b6b71266407fe60f7e91e434a4f8` | **PASS** | `scripts/f004-b1/run-build.ps1`, dev IL2CPP GLES3 |
| 11 | Shader variants <= 490, no unexplained growth | **356** (176 snippets, 79 shaders), identical to B4a and B3 | **PASS** | `IPreprocessShaders` dump |

Player census (enabled sprite renderers plus 3 Tilemap renderers, 12 locations): synthetic portrait 80 / landscape 82 (B4a: 80 / 82); real scene natural 66 to 81; **worst cases in B4b.2**.

## B4b.2 Upgrade marker: census, SetPass (C-94)

T563 draws one marker renderer per location on the Overlay layer; it yields to the Item boost (at most two markers per location, ART-2). It is the F003 upgrade icon, **a single 24x24 texture of its own (`f003-ui-icon-upgrade_00`), not on the markers atlas**.

Worst-case census (all 12 slots at the overlay cap of 61 slot renderers, 12 pips, 3 FX, plus the 5 draining tails of the pool as design maximum), synthetic layouts:

| Variant | portrait (3 col) | landscape (4 col) | vs signed 95 | vs 21.12 ceiling 110 |
|---|---|---|---|---|
| boosted + Overdrive (T531 case) | 99 | 101 | +4 / +6 | -11 / -9 |
| boosted + Overdrive + upgrade marker on all 12 | **99** | **101** | +4 / +6 | -11 / -9 |
| Overdrive + upgrade marker on all 12 (2 markers each) | 99 | 101 | +4 / +6 | -11 / -9 |
| upgrade marker only | 87 | 89 | -8 / -6 | -23 / -21 |

**The marker on all 12 slots does not raise the worst case** (equal to the boosted + Overdrive case, as expected; asserted in `W006VWorldPlayerTests` and, in the real scene with the real catalog, in `W006VRealScenePlayerTests`). In the real packaged scene (3 FX active, natural pips, no draining tails) the same variants count 91 (landscape) and 88 to 89 (portrait); add the 5 tails for the design maximum: 96 and 93 to 94. Everything is below 110 and far below the 173 tier; against the signed 95 only the synthetic worst case is above it (+4, +6), as in B4a.

SetPass with the marker (real scene, player, forced W004-V visuals): the marker adds **+4.4 (landscape) and +4.7 (portrait) passes** to the all-state value (42.1 -> 46.5, 44.0 -> 48.7). Cause, from the per-texture deltas: the marker sprite is an unatlased texture, so its twelve renderers, interleaved with the slot and cue draws by sort order, each force a texture change (the icon's own delta reads 5.4 to 5.8 passes at 12 markers, 0.5 to 1.9 at the 4 to 10 markers of a natural W005-L run); on the device GLES3 the natural state shows +1.2 to +1.4 against B4a (the icon's own delta -0.4 to 1.1). The FX atlas delta is +1.2 to +3.7 (B4a +0.6 to +3.1, within the same +-1.5 run-to-run noise): C-33 stays "within noise". The world atlas delta is +7.0 to +9.4 (B4a +7.7 to +8.7). **Option (Performance role, not done): put the upgrade icon on the markers atlas (like the boosted and Overdrive markers); expected gain about -4 to -5 passes at the 12-marker worst case, about -1 in a natural run; complexity low; needs an Art/Technical Art decision. Recommend; the owner decides.**

## B4b.3 Allocations (budget 5, C-94, C-68)

Real packaged scene, Windows player, W005-L (12 running machines, Established), whole host frame per 600 frames at the production 60 FPS cadence (control with no scene: 1.8 per frame):

| State | per frame | structural frames (rebuild) | per structural frame | steady frames: per frame | B4a steady per frame |
|---|---|---|---|---|---|
| card closed, landscape / portrait | 76 / 73 | 114 / 109 | 314 / 306 | **20.7 / 21.7** | 21.5 |
| machine card open | 123 / 122 | 111 / 115 | 577 / 565 | **20.6 / 17.2** | **403** |
| build card open | 101 | 185 | 293 | **15.5** | **280** |

Uncapped (frame cost): 5 to 8 per frame, steady 2.4 to 2.6. The card-open steady cost moved from about 400 to about 20 per frame: `MachineCard.Refresh` now rebuilds only when a displayed value changes (T560). The remaining about 20 per steady frame at 60 FPS (about 2.5 uncapped, so a function of time) is C-68 and stays open: unattributed, a deep-profile Issue after the gates (owner decision).

C-94, `RunShellPresenter.Refresh` (the per-frame affordability mask loop over the cached visuals):
- `RunShellPresenterAllocationTests` (Play Mode, real scene, 12 locations, the mask loop with at least one affordable upgrade): 600 `Refresh` calls (one per frame) **0 allocations** with the cash still, and 0 with the cash moving by 1 every frame (no cost crossed, no rebuild).
- `W006VRealScenePlayerTests` (Windows player): `B4B-REFRESH-STILL` 600 consecutive `Refresh` calls over an unchanged run **0 allocations**, card closed (asserted), card open (0), mean 0.0008 to 0.0011 ms per call. `B4B-REFRESH-REAL` (one extra `Refresh` after each frame for 600 frames): the frames in which the presenter rebuilt its snapshot are structural (allocations as in the table); of the other frames 7 to 9 % allocate (50 to 90 allocations on such a frame, one in which a displayed value changed), the rest none. The mask itself allocates nothing.

## B4b.4 Emulator (exploratory), C-83, ABA

Host load (PF-9): the owner confirmed a quiet host. Processes: the emulator (`emulator.exe`, `qemu-system-x86_64-headless.exe`), one `adb` server, no Unity process during any timed capture (Unity CLI runs and builds finished first; the only Unity run during the emulator session was the older-build APK build, with no capture running); idle background applications (Discord, iCUE, Battle.net, CurseForge, Epic launcher, NVIDIA overlay, JBL QuantumENGINE, an idle Chrome; blender-mcp and comfy-mcp servers, idle); host CPU 0 to 17 % between captures (the emulator itself about 0.7 core). Windows power plan "Hoechstleistung" (high performance). The Windows player runs: CPU 7 % at the start, same applications.

- Cold-booted headless (`-no-snapshot-load -no-snapshot-save`, host GPU) twice: first session (B4b build, then F003 and the older build ABA), then again for the "cold B" capture. Dev APKs, profiler-free (`profiler autoconnect False`).
- **C-83 (ProducingFps about 40 on a loaded host): on the quiet host and cold-booted emulator it is about 46 to 47, not 60, in every capture, and the fresh-run controls read the same.** The present-to-present histogram is bimodal: the main mode at 15 to 16 ms, a second mode at 29 to 32 ms (about 850 of 2,700 intervals) and a tail at 42 to 48 ms (about 120); a frame that misses the 16.7 ms present waits for the next one. The device probe says the same: main thread mean 20.2 to 21.5 ms, p95 31.3 to 31.7 ms, deltas p50 16.67 ms, p95 33.3 ms (B4a probe: 60.0 FPS, main thread p95 17.8 to 18.7 ms). The guest is CPU-bound at about 20 ms per frame.
- **ABA (A = B4a-era main `b44b87d` plus the B4a probe, B = this main), same emulator, same session, W005-L producing, controls in brackets: A1 44.8 (47.9, 45.2), B 44.6 (45.0, 44.9), A2 43.8 (44.4, 44.0).** The F006 code of P3 and P4 (T560, T561, T563) therefore costs nothing measurable on this emulator; the drop from B4a's 61.4 is the session (host or emulator environment), not the code. What differs in the environment is not isolated: during the cold-boot capture the host GPU stayed in P8 at about 880 MHz and 8 % utilization (never above P8, `b4b/cold-B-gpu-samples.csv`); that is an observation, not shown to be the cause. The 61.4 of B4a is not reproducible today.
- Fresh-run controls: 46.9 / 46.9 (first), 45.0 / 44.9, 46.2 / 47.0 (cold boot), 46.6 / 46.0 (W003-S).
- Device probe (`B4bRuntimeProbe`, flag-gated, 1080x2400, natural W005-L, vein hold, 3 pops, 600-frame windows): card closed 46.5 FPS, natural markers (10 of 12 on) 49.5, machine card open 49.2, build card 49.5; 0 intervals >= 50 ms in three windows and 1 in the build-card window (max 50.0 ms); census 70, 65 (natural markers), 82, 67; textures 3.01 to 3.26 MiB; SetPass per render 37.2, 37.0, 37.9, 37.5 (B4a 35.8, 36.7, 36.1; **+1.2 to +1.4, the unatlased marker texture**), upgrade icon delta -0.4 to 1.1.
- Not covered: forced W004-V visuals on the device (a player has no such seam), a physical device, battery and thermal behaviour, a paired B1 build.

## B4b.5 Carried items

| Item | Result |
|---|---|
| **C-83** | B4b.4: 46 to 47 FPS on a quiet host, same for the B4a-era build and the fresh-run controls; not a loaded-host effect and not an F006 regression; a 60 FPS emulator session needs a changed environment (open). |
| **C-94** | B4b.2 and B4b.3: `Refresh` zero-alloc test added (Play Mode and Windows player), worst-case census with the marker on all 12 slots equals the boosted + Overdrive case. |
| **C-65** | Added `AutosavePoolThreadPlayerTests` (`StandalonePlayer`): the production `RunPersistence` over the real `JsonRunCheckpointStore`, packaged Iron catalog, isolated temp root, a clock the test moves and the default thread-pool executor; two periodic writes publish generations 2 and 3, the previous generation is retained and the files read back (the first write after the cold start already takes the pool thread; time to applied 32 to 39 ms; main-thread tick 2.7 ms first (includes first-call work), 0.09 ms second). **C-74's "no player-compatible rig" is resolved.** Device: `-Scenario F006 -AutosaveCheck` **PASS**: generation 11 -> 13 with no lifecycle event, same process, force-stop without HOME, relaunch restored generation 13 and its machines and levels. |
| **C-66** | Emulator, 60 s idle window against 60 s with 27 remove/rebuild commands (one every 2.2 s, each one autosave at the 2 s spacing, about 27 writes a minute): 49.9 vs 49.4 FPS, deltas p95 33.3 vs 33.3 ms, 1 frame above 33.4 ms (max 50.0 ms) vs 0, allocation samples not higher (363,897 vs 373,136). Bytes the process sent to storage (`/proc/self/io` `write_bytes`): **49,152 idle, 561,152 in the burst: about 512 KB in the minute, about 19 KB per write**. So a sustained burst of that rate is about 0.56 MB per minute (about 34 MB an hour), and the cadence of one write per 30 s while the run changes (about 120 writes an hour) is about 2.3 MB an hour. Emulator storage only; no physical-flash wear figure. `AutosaveWriteCostTests` asserts `mainMedian < 8 ms` on wall-clock time over 60 rounds; it is host-timing sensitive, as the test's own comment says, and passed in the final Edit Mode run; not changed. |
| **C-69** | Text fixes, see B4b.6. |
| C-33 | Closed as within noise (B4b.2). |
| C-68 | Open, owner: separate Issue after the gates (about 20 per steady frame). |
| Budget 9, ABA of 1 to 3 | The ABA of 1 to 3 was done against the B4a-era build (B4b.4). Budget 9 (guest CPU against a paired B1) was not measured: it needs a B1 build and a CPU capture, and is not on R16's list. |

## B4b.6 Corrections to the B4a text (C-69)

- **Budget 4 in B4a was read on W003, not on W006-V.** It still is the budget's definition (the W003 host frame, `W003-HOSTFRAME`); B4b adds the W006-V reading (B4b.1 row 4).
- **Budget 9 is outside R16's list** (R16 names #4, #5, #6, #7, #8, #10, #11); so are 1 to 3 as gates ("Emulator FPS stays exploratory"). The B4a statement "not measured" for 9 is a scope statement, not a failure.
- **The B4a probe's with/without labels were swapped** (its first schedule alternated one frame at a time against a recorder that lags two frames); B4a used the absolute values, which were right. `B4bRuntimeProbe` uses the block schedule of the player test (12 blocks of 10 frames, the first 4 readings dropped); its values are consistent with the player method.
- C-33 is closed as within noise.
- The B4a `perTexture` and "card open" reading stands as history; the card-open allocation of B4a section 5 (about 400 per frame) no longer holds on the current main (B4b.3).

## B4b.7 Tests, preflight, deviations

- Leases `unity,emulator,gpu` held by `issue-716` for the whole task. `Library/Bee` DAG files with foreign paths and `Library/Bee/Android` were deleted before each Android build; disk 19 to 22 GB free during builds. Emulator and adb server killed, game uninstalled at the end. Build outputs (both APKs) deleted after use.
- Tests (final test code, commit `19901958`): Edit Mode **3187 of 3187**, full Play Mode **3526 of 3526**, tagged Windows player suite **85 of 85** (the run of the same test code; a later edit of the editor-only `RunShellPresenterAllocationTests` for the fixed-slot-count guard changes no player code). A first player run failed three cases: the new autosave case (no checkpoint to read before a first save, fixed in the test), the real-scene worst-case census (the presenter re-rendered the natural visuals between the forced render and the count, fixed by counting in the render's own frame) and `HubPlayerTests` (a consequence of the failed real-scene case; it passed on every later run). `scripts/check-settings-noise.ps1 -Commit` run on the final commit.
- Deviations: SetPass and census with forced visuals only in the SetPass and census blocks (as B4a); the portrait case is a 540x960 window; **no B1 build, no physical device, no forced W004-V visuals on the device**; the device probe cannot grant cash (the setter is internal to Core), so the marker state on the device is the natural one (4 to 10 markers). The older-build APK was built by checking out `b44b87d` in this worktree (HEAD restored afterwards) with the B4a probe; its size 44.6 MiB.
- Probes: `probes/B4bRuntimeProbe.cs.txt` (device probe of this part, with the burst and the block SetPass schedule) with `probes/B4Probe.asmdef.txt`; copy both into `Assets/TempB4Probe/` for one development build and delete them afterwards. The B4a probe stays as history.
- Test code added (no production change): `RunShellPresenterAllocationTests`, `AutosavePoolThreadPlayerTests`, additions to `W006VRealScenePlayerTests` (Refresh runs, worst-case census with the marker, marker SetPass block, `B4B-*` log prefix) and `W006VWorldPlayerTests` (worst case with the marker, world frame time), the upgrade icon added to `DeckTestRig`'s flat-colour catalog.


# Part B4a (T531, Issue #704): history. B4a performance measurement, W006-V

Issue #704, Performance Engineer (Medium tier, high reasoning for the measurement design). Base `origin/main` b44b87d; worktree `.maqa-worktrees/issue-704-t531-performance-b4a`. Unity 6000.6.0f1, Windows 11 host (AMD Ryzen 7 7800X3D, RTX 5070 Ti), emulator AVD `IdleFactory_Test_API_36` (GLES 3.1, host GPU, 1080x2400). Evidence for FR-006, SC-008, research R16. Budgets are the frozen F004 ones of `specs/004-visual-identity/reviews/performance-budgets.md` section 4 (row 8 as restated 2026-11-05); the method is that of `performance-b3.md` with the deviations in section 8.

**Every number here is exploratory evidence from one workstation (FR-027). There is no physical-device, battery or thermal measurement; that gap stays open.** Nothing in this task changes production code. Art Bible 21.12 is a DRAFT pending owner confirmation against the signed 95 (OD-10); the census below is reported against both, and the Art Bible is not edited.

## 1. Fixture W006-V and what the card is

`Assets/IdleFactory/Tests/PlayMode/Fixtures/W006V/W006VFixture.cs`, definition hash `9496a6b3e3821c71a1167296eba95c746deb75d5bc605ed4e2f72add24026ac7` (pinned by `W006VWorldPlayerTests.TheFixtureDefinitionHasThePinnedHash`): W004-V (hash `1920dfaf...ecca6fc`) plus the Established-stage deck (stage 2), 12 branches with 12 running pips, the vein chip loop, 3 effects, the card open.

**The card is the real docked machine card of T534, not a stub.** It is opened through `RunShellPresenter.OpenSlot(0)` in the real packaged scene. The synthetic world (flat-colour sprites, real view code) has no shell, so it carries no card. In the shipped UI the ore-vein control is hidden while a card is open, so the real-scene cases measure "chip loop, card closed" and "card open" as two states of the one workload. The build card (C-41) is measured as a third case after removing the last machine (11 machines).

Two harnesses: (a) `W006VWorldPlayerTests`, the synthetic W006-V world (real `SlotGridView`, `FactoryDeckView`, `DepotLinkView`, `DepositView`, `EffectPoolView`, `WorldEffectsFeed` over a real `RunSession` with 12 running Miners and forced W004-V visuals); (b) `W006VRealScenePlayerTests` (Windows player only, `#if !UNITY_EDITOR`): the packaged scene with the committed W005-L checkpoint (12 machines, Established), real F004 and F006 art, real shell. In (b) the forced W004-V visuals are applied only inside the SetPass and census blocks; the allocation and frame runs show the natural W005-L states. The existing persistence folder is moved aside and restored.

## 2. Budget table

| # | Budget (frozen) | Measured | Status | Method |
|---|---|---|---|---|
| 1 | Emulator avg FPS >= 60 (exploratory) | **61.41** (3576 frames, 4 dropped), W005-L producing (Established, 12 branches, deck, Welcome back open as in B1 to B3); fresh-run controls 61.43 and 61.60 | **Met on this session; not an ABA** | `android-emulator-journey.ps1 -Scenario F005 -Steps ...W005LFps`, profiler-free dev APK, SurfaceFlinger TimeStats 60 s. No B1 or B3 APK run in the session (card closed, no probe) |
| 2 | p95 bucket <= 19 ms | p50 16, **p95 19**, p99 22 ms (controls 17 and 18) | **Met** (at the line) | same capture |
| 3 | 0 intervals >= 50 ms per 60 s | **0** (controls 1 and 0) | **Met** | same capture |
| 4 | Windows host frame p95 in 0.02 to 0.04 ms (ceiling 1 ms) | W003-S **0.016**, W003-B 0.039, W003-R 0.018 ms | **PASS** | official player suite, `W003-HOSTFRAME`, 600 frames. Quiet host not guaranteed |
| 5 | 0 steady-state GC allocations (advance path, presentation paths, 600-frame player run) | **W006-V synthetic 600 frames: 0** steady, 0 in the 540 non-rebuild frames of the contract case; advance path 0. **Whole host frame in the real scene: not 0** (section 5) | **PASS for the defined paths; FAIL for the whole real-scene frame** (card closed about 21 per steady frame, card open about 400, build card about 270). `W003-ALLOC` fullFrame 6 samples per 300 frames (PR-2) unchanged | `GcAllocationCounter` and per-frame `GC.Alloc` recorder, player |
| 6 | World-art texture memory <= 8 MB | build report **3.54 MB** (F004 pages 2.23 MB, F006 world and FX 1.31 MB); player runtime of the pages in use 3.13 to 3.38 MiB | **PASS** | build report, `Profiler.GetRuntimeMemorySizeLong` in the player |
| 7 | Atlas pages <= 1024 x 1024 | 7 pages, largest **512x512**; all RGBA32 | **PASS** (also the 512 and 1 MiB rule of Art Bible 21.9; a 512x512 page is 1,048,576 B of pixels) | player and build report |
| 8 | SetPass per world-camera render <= 0.86, read as one pass per render (restated row 8 (a)) | **about 42 to 44 passes per render** at W006-V on D3D12 (landscape 42.1, portrait 43.8, card open 41.7 and 43.8, build card 37.2); GLES3 emulator about 36. F004 pages alone in the same method: 33.5 to 33.9; F006 adds **+6 to +10** (about +18 to +30 %). `camera.Render()` CPU 0.16 to 0.22 ms mean, p95 0.19 to 0.27 ms | **FAIL as written; not comparable with the B3 reading** (section 3) | player SetPass differential, 20 renders per frame, `ProfilerRecorder` |
| 10 | APK <= 150 MB | **46,767,490 B** (44.6 MiB), 31 % of the cap (+376,051 B on B3) | **PASS** | dev IL2CPP GLES3 APK, `run-build.ps1` |
| 11 | Shader variants <= 490, no unexplained growth | **356** (176 snippets, 79 shaders), identical to B3 | **PASS** | `IPreprocessShaders` dump |

Budgets 9 (guest CPU against a paired B1) and the ABA reading of 1 to 3: not measured, no B1 APK was installed in the session.

## 3. SetPass (budget 8, C-33, A-10)

Method: the real packaged scene in the Windows development player (D3D12, 1920x1080 and 540x960), forced W004-V visuals, ore-vein hold, 3 pops, 12 running pips. 20 extra `camera.Render()` calls into an exact-size RenderTexture in half of the frames; 12 blocks of 10 frames, the first 4 readings of each block dropped (the recorder lags two frames); passes per render = (mean with minus mean without) / 20. Atlas deltas: all renderers of one atlas texture disabled.

| State | all | F004 pages only | F006 atlases only | atlas delta world / FX |
|---|---|---|---|---|
| landscape, card closed | 42.08 | 33.47 | 10.00 | +7.70 / +0.63 |
| landscape, machine card open | 41.69 | 31.54 | 10.28 | +8.2 / +3.1 |
| portrait 540x960, card closed | 43.79 | 33.85 | 8.00 | +8.5 / +1.4 |
| portrait, machine card open | 43.82 | 33.84 | 9.12 | +8.4 / +2.7 |
| build card open (11 machines) | 37.18 | 27.42 | 8.65 | +8.7 / +2.5 |

Run to run the "all" value moved between 40.4 and 45.2 (three runs), so the per-atlas deltas carry about +-1.5. Reading:
- Texture changes interleaved by sort order make every renderer group a pass: removing the slots atlas lowers the count by about 25, the machines atlas by about 24, both far above their renderer counts. Passes are not additive per atlas.
- The F006 world atlas costs about +7.7 to +8.7 passes, the FX atlas (pips on Overlay between cue draws, pops) +0.6 to +3.1: it is inside the noise, so the C-33 "+1 for the FX atlas" is neither shown nor excluded.
- **The B3 reading (one pass per render, 0.74 to 0.88) came from a graphical Editor probe whose batch and draw-call counters read 0 (performance-b3.md section 5). In the player on D3D12 and on the GLES3 emulator the same differential gives 36 to 44 passes, and F004 content alone already gives 28 to 34. The two methods are not comparable.** I did not re-run the Editor probe: it needs a foreground graphical Editor session, which the owner's no-foreground-window rule of 2026-11-17 excludes for gate agents.
- Verdict: budget 8 (a) fails as written (not one pass per render), but the failure is the method, not F006: the F006-attributable delta is +6 to +10 passes per render. CPU is not the cost: `camera.Render()` costs 0.16 to 0.22 ms mean (tier 1 bound 0.5 mean / 1.0 p95 applies above 175 renderers; the W006-V world has at most 101).
- Options and recommendation (Performance role): (1) restate budget 8 on the player method with the F004 baseline in the same method (F004 pages only: 34) and a ceiling on the F006 delta, for example <= +10 passes or <= +35 %; (2) if passes matter on devices, sort the world sprites by atlas inside each layer (large gain, per-location y-order would need an owner decision). Recommend (1); the owner decides. Expected gain of (1): a measurable gate where today's is unreadable; complexity low.

The A-10 re-measure (machines atlas now two pages): the second page (256x256, 2 to 6 renderers) adds about 0 to 3 passes; included above.

## 4. Renderer census (C-8, C-9)

Counted as enabled sprite renderers with a sprite plus the Tilemap renderers (3), at 12 locations. Reported against the signed 95 (Art Bible 2.2a) and the draft 105 (21.12), neutrally; tier 173.

| Case | Renderers | vs 95 | vs 105 | vs 173 |
|---|---|---|---|---|
| W006-V synthetic, portrait 3 columns (slots 47, pips and depot 13, edges and props 12, FX 3, deposit 2, 3 Tilemaps) | **80** | -15 | -25 | -93 |
| W006-V synthetic, landscape 4 columns | **82** | -13 | -23 | -91 |
| Real scene W006-V, landscape / portrait | 74 to 82 / 75 | -21 to -13 / -20 | -31 to -23 / -30 | about -95 |
| Worst case: all 12 slots at the overlay cap (61 slot renderers), 12 pips, 3 FX + 5 draining tails, portrait / landscape | **99 / 101** | **+4 / +6** | -6 / -4 | -74 / -72 |
| Allocated (enabled or not) | 134 / 136 | | | |

The slot cap of 61 holds. The worst case is above the signed 95 by 4 (3 columns) and 6 (4 columns) and below the draft 105; edge pieces (7 or 9) are the part the 95 did not count, as C-8 said. All are far below the 173 tier. Tiles: 63 across 3 Tilemaps. Layouts measured: the two shipped ones (12 locations, 3 and 4 columns); the six, twenty and L test layouts have fewer or more locations than the 12-machine session and were not counted.

## 5. Allocations (budget 5, C-20, C-21, C-41)

**Synthetic W006-V, 600 frames, Windows player (`W006VWorldPlayerTests`)**: no contract: 600 steady frames, **0 allocations**, 225 effects spawned, 12 pips and 3 FX active in every frame. With a production contract: 60 feed rebuilds in 600 frames (at most one per simulated second), **226 allocations per rebuild frame (structural-event allocations, not steady state)**, the other 540 frames **0**.

**Real scene, whole host frame, per 600 frames at 60 FPS (Windows player; control with no scene: 1.8 per frame, the test runner)**:

| State | allocations per frame | structural frames (presenter rebuild) | allocations per structural frame | steady frames: per frame | bytes per frame (approx) |
|---|---|---|---|---|---|
| card closed (vein hold, 3 pops) | 76 | 113 to 116 of 600 | 311 | 21.5 | 4.9 KB |
| machine card open | 458 | 110 to 117 | 694 | **403** | 27.5 KB |
| build card open (C-41) | 366 | 178 | 572 | **280** | 20.9 KB |

Uncapped (frame cost, same 600 frames): card closed 5.8 per frame, card open 382 to 392, build card 271. Rebuilds scale with wall time (about 11 to 12 per second at 60 FPS; 6 to 11 per 600 frames uncapped), the per-frame card cost does not.
- C-20: the shell presenter rebuilds the factory snapshot about 11.6 times per second in the W005-L state (113 to 117 per 600 frames at 60 FPS), about 310 allocations each (about 174 for the snapshot); the world feed rebuilt 0 times in the real scene. Structural-event allocations, not steady state. The ask to share one snapshot between the presenter and the feed is supported by the rebuild count.
- **C-41 confirmed and wider: `MachineCard.Refresh` has the same cause as `BuildCard.Refresh`.** Both call `RunSnapshots.Factory` and `RunSnapshots.Rates` and re-render on every `QuantityRevision` change, which moves nearly every frame while machines run: about 400 allocations (27 KB) per frame with the machine card open, 280 with the build card. This breaks FR-006 and the SC-008 reading "zero managed allocations per frame in steady state on the reference workload" for the card-open state. Not fixed (measurer). Option: refresh and render only when a displayed value changes (affordability, Cash display, the card's own machine), cache the snapshot; expected gain card open about 400 to about 20 per frame; complexity low to medium; recommend a follow-up Issue before the P4 gates.
- Card closed, steady about 21 per frame at 60 FPS: not attributed (UI Toolkit HUD updates and the per-second Overdrive status of PR-2 are candidates); needs a deep-profile follow-up.
- C-21: the world model's `VisualState` and `QuantityRevision` are as old as the last rebuild (up to 1 s) in the world feed; the feed uses only deltas and `ActivityFps`. Noted; no later reader may rely on them. Not measured.
- Device: the emulator probe (below) shows the same shape on GLES3: card closed 77 per frame, card open 459, build card 370.

## 6. Emulator (exploratory, FR-027)

Cold start of the session: emulator booted headless, `adb` from the main checkout directory, no other Unity or foreign adb process (the owner's idle `unity.exe serve` Hub helper and MCP server only), host busy about 1.3 to 2.3 threads. APK `B4a.apk` sha256 `e99750f5b993852b71cf2a589a6c636e40845886524764fdde218b9785010faa`, development, profiler autoconnect False.
- FPS: section 2 rows 1 to 3. This is the first valid emulator session since B2: the controls read 61.4 and 61.6.
- Probe (`probes/B4RuntimeProbe.cs.txt`, temporary, gated by a flag file; W005-L, vein hold, 3 pops, 1080x2400): 600-frame windows at the 60 FPS cap: card closed 60.0 FPS, card open 59.8, build card 59.9; **0 intervals >= 50 ms**, one 33.3 ms frame in two windows; main thread p95 17.8 to 18.7 ms, max 25 to 27 ms. Census 63, 67 and 63 renderers (natural states, no forced visuals). Textures 3.25 MiB RGBA32, 6 pages, largest 512x512. SetPass per render about 35.8, 36.7, 36.1 (the probe's with and without labels are swapped: it used the first, one-frame-alternating schedule; its raw per-render and delta values are negative and are the absolute values quoted); atlas deltas: world +7.5, slots +22, machines +15 to +16, FX +0.9 to +2.1.
- Not covered: forced W004-V visuals on the device, a B1 or B3 ABA, guest CPU (budget 9), physical device.

## 7. Carried items

| Item | Result |
|---|---|
| C-6 | `PresentationCatalogInitOrderTests` (Edit Mode): every placed F006 row is registered and required, and the declaration order of `PlacedF006Keys`, `PlacedF006LinkKeys` before `Registry` is asserted from the source. Passes. |
| C-8, C-33, A-10 | section 4 and 3. |
| C-9 | W006-V, budgets 1 to 11 where measurable (9 not), census per shipped layout, SetPass card closed with 3 FX: done. |
| C-20, C-21, C-41 | section 5. |

## 8. Preflight, deviations, what was not run

- Leases `unity,emulator,gpu` held by `issue-704`, renewed by the parent. Unity CLI runs ended before the emulator booted. The emulator was killed, the game uninstalled, the adb server killed afterwards; the only leftover was the emulator's own `-kill` helper.
- Disk: C: had 6.6 to 9 GB free; the build ran with a guard (minimum observed 6.6 GB). `Library/Bee` DAG files with foreign paths were deleted and `Library/Bee/Android` before the APK build.
- PF-9 host load: the owner's machine was in use (browsers, Hub, MCP servers, other agent sessions); the Windows timings are not quiet-host values. Player frames were measured uncapped and at the 60 FPS cap.
- Deviations: SetPass measured in the player, not in a graphical Editor (section 3); the real-scene allocation and frame runs use natural W005-L states, the forced W004-V visuals only in the SetPass and census blocks; no B1 ABA; the portrait census of the real scene used a 540x960 window; the player window default 1920x1080 stands for "landscape".
- Tests: Edit Mode, full Play Mode (3450 of 3450 passed; the one skipped test of that run was removed by making the real-scene class player-only), the tagged player suite (82 of 82, the last run on the final code). Numbers in this record come from the player runs of the final test code except the first-run values quoted as run-to-run spread.
- Probes: `probes/B4RuntimeProbe.cs.txt` and `B4Probe.asmdef.txt` are the temporary device probe; copy them to `Assets/TempB4Probe/` for one development build and delete them afterwards. Nothing under `Assets/` besides the fixture and the tests is committed.

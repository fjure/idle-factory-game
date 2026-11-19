# B4a performance measurement, W006-V (T531)

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

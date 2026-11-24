# Play-Tester batch-C report (T543, issue #715)

**Status: proposal for the owner.** Two channels: the deterministic balance runner and a touch-only player session on the dedicated emulator. Neither is a human playtest. This report approves nothing and turns no feel statement into a threshold. Feel statements are marked **[inference]**. Screenshots are in `playtest-agent-f006-C/` (names in brackets). The independent UI/UX review is a separate report.

| Item | Value |
|---|---|
| Build commit | `15deea2c72b61ee24cbb72004e99c52986a13def` (detached, gate worktree `f006-p3b-playtest`) |
| Batch under test | T555 (gate-B fixes), T527 deck/edges/props, T529 effects, T556 cue contrast, T528 depot states + tap priority, T557 world attention markers, T539 build card + ghost, T540 stages + milestone banner |
| Channel A | `IDLEFACTORY_WRITE_STRATEGY_BASELINE=1; scripts/test-unity.ps1 -Suite EditMode -Category BalanceFull`: `UNITY_TEST_PASS suite=EditMode tests=7 passed=7`, exit 0 (`test-results/f006-p3c/balance.out`) |
| Channel B | Android development APK of 15deea2 (`BuildAndroidArm64DevelopmentGles3 -noProfilerAutoconnect`, IL2CPP ARM64 GLES3, 44.5 MB) on AVD `IdleFactory_Test_API_36`, headless, input by `adb shell input`, screenshots by `adb exec-out screencap`. No host mouse, keyboard or window input. Classes: phone portrait 1080x2400 (100 %, 200 %), `wm size` 360x640, 640x360 and 844x390 at density 160 (200 % text), phone landscape 2400x1080 (200 %, by rotation with the build card open) |
| Not covered | Windows player (not run, owner rule), tablet class, reduced motion, TalkBack/accessibility names (the Android accessibility tree shows only a `SurfaceView`, see C-F9), pinch, pixel-shimmer zoom (item 12), grayscale check, real-time offline longer than 9 s (only the 8 h fixture return), prestige, contracts delivered by hand, level 5 |
| Leases | `unity,emulator` held by `issue-715`, not released by me; no UnityMCP |

## 1. Channel A: economy diff

The regenerated `specs/005-economy-contracts/reviews/strategy-baseline-r3.md` differs from HEAD only in the build-commit cell, the measured wall time (129.9 s against 116.0 s; host load) and the two projection lines. Every measured number and gate is unchanged, so batch C changes no economy value. Diff: `test-results/f006-p3c/baseline.diff` (untracked). The baseline and `ProjectSettings/` were restored; `git status` shows only `.utmp/` (pre-existing) and the report files.

## 2. Channel B: session record

Method. Fresh install, then a hand-played start; the economy at x1 (no time-scale control on touch) is too slow to reach factory levels 3 and 4, so I pushed the committed Save V3 fixture W005-L (level 5, run 3) through the journey script's recipe (manifest rebuilt from the template) and a derived level-2 variant (factory level 2, 3 Miners, 1 Smelter, 5.0M Cash) to trigger real level-ups through the Market "Expand factory" button. The first variant (slots/levels beyond the level) was rejected with `save.v2.invariant` and the damaged-save dialog [c20, c21]; that is expected behaviour, not a finding.

Taps from a closed world (portrait 100 %):

| Goal | Taps now | Batch B | Note |
|---|---|---|---|
| Build a Miner | 2 (empty slot, Confirm) [c03-c05] | 3 | build card opens on the slot with Miner preselected on a fresh run; the machine card opens after Confirm with Upgrade focused |
| Upgrade from the card | 2 (machine, Upgrade) | 2 | unchanged |
| Ore auto-sell | 3 + scroll (Office, Market, scroll to Iron Ore, Auto-sell) [c10] | 3 | unchanged; Market is a long scroll |
| Expand factory | 3 + long scroll (Office, Market, scroll to the end, Expand) [c23] | n/a | 8 to 14 swipes at 200 % |
| Overdrive | 1 [i06] | 1 | pink pips appear on all machines; "On 0:30" |
| Attention chip to affected machine | 1 [i04] | 1 | selects the Smelter, "Short on Iron Ore" |

Session observations: the start screen [c01]; the organic start showed only 4 open slots with locked slots numbered by unlock level (an improvement over batch B's plain locks); a 25 s long-press on Mine gave +0.1 x 251 ticks [c06]; with a sheet open the world reframes to the whole factory in the early run [c09, c11] but not in the Established fixture [c19] (C-F5).

U-B13 (save loss after force-stop): **confirmed as designed-but-risky.** After about 5 minutes of play with a built Miner, `am force-stop` and relaunch gave a brand-new run (Cash 37, "Build a Miner") because `Checkpoints/v2` was empty [c13]. After `KEYCODE_HOME` the checkpoint files appeared (data-a, manifest-a, pending) and a later `force-stop` plus relaunch restored the Miner with a "Welcome back" for 9 s away [c14, c15]. So a checkpoint is written on pause only; there is no periodic autosave [inference: a crash or swipe-away without pause loses the session].

## 3. Findings

Severity: Medium = an acceptance scenario or checklist item fails on this build, or the player loses information; Low = rough edge or copy; Info = limit or positive result. The disposition is my proposal; the owner decides.

| ID | Observed behaviour | Evidence | Sev | Proposed disposition |
|---|---|---|---|---|
| C-F1 (mandatory C1) | **The milestone banner covers the top row of machines in every compact class tested.** The level-up can only be triggered from the Market sheet, which clears the selection and card, so a selected top-row machine or ghost cannot be under the banner by player action; but the world column is small while a sheet is open, and the banner (strip-to-world, 2 to 3 lines at 200 %) hides the whole top machine row and its markers for the banner lifetime. Portrait 200 %: all three top-row machines and their markers; 640x360: all four; 360x640: the top two rows; 844x390: the upper half of the top row. At 100 % portrait with the Market open the banner covers the marker tops only [c24-c]. Dismissal: a tap on the banner appears to dismiss it (gone 0.8 s after the level-up in g05 [inference]); otherwise it is gone by about 3 s (visible at +0.6 s, gone at +3.2 s) [c24-e, e09]. Text wraps without truncation at 200 % | d08, e08, e10, f04, g04, c24-c | Medium | Fix before batch D: the review's proposal (primary line only on compact height or large text, B-04 in the a11y text and offline summary) or place the banner over the sheet |
| C-F2 | **After the build card is closed with Back in landscape (rotated from portrait with the card open, 200 %), the world camera stays shifted: the factory is almost entirely off screen** (only the bottom fence is visible, rest black), also after 3 s. Opening and closing the Office hub re-fits the camera [h09]. Rotation itself kept card, ghost and the selected type [h05] | h05, h06, h07, h09 | Medium | Fix before batch D (re-fit the camera when the card closes after a rotation) |
| C-F3 | **Store-full markers are per machine, not capped.** With the store full, every Miner shows a yellow store-full marker (6 markers in the 12-machine fixture [c17], 3 in the level-2 variant [c22]) and a newly built Miner gets one at once [j04]. The checklist expects "at most 2 shortage + 1 store-full". The strip chip counts something else (6 store-full in the first case, 1 shortage in the second). The red shortage marker shows a generic down-arrow (input missing), not the Iron Ore icon the checklist expects at the trunk junction (the Ore icon appears on the trunk [c23] and in the chip). **[inference]** these may be the T528 machine badges rather than T557 attention markers; UI/UX to confirm which layer draws them | c17, c22, c23, j04 | Medium (marker cap/identity unclear) | T542/T547 or UI/UX call |
| C-F4 | **Tapping the red shortage marker in the world selected nothing** (two taps on the icon centre, portrait 100 %) [i02, i03]; the strip chip tap selected the Smelter and opened its card ("Short on Iron Ore") [i04]; a machine tap and a far-world tap (closes the card) worked [i05]. Checklist 7a and 7c fail on this channel | i02, i03, i04, i05 | Medium | Fix before batch D, or confirm that markers are non-interactive by design |
| C-F5 | **With a sheet open in the Established fixture the world does not re-fit** (only the top machine row is visible above the Market sheet and the depot with its FULL state is hidden) [c19], while in the early run the same sheet re-frames to the whole factory [c09, c23]. The player cannot see the depot FULL sprite; in portrait the Mine/depot are never visible together with the store-full state. Checklist 6 only partly covered | c19 vs c09 | Medium | T542/T547 |
| C-F6 | **Hard-edged deck and black void.** At fit-all the deck shows as an axis-aligned rectangle with a visible hard edge, black bands above (about 13 % of the portrait height, roughly 320 px) and beside it [c12, c17, d09 landscape h09]. The Early stage has the railing only (no lit ring) as designed; Partial adds lit tiles and a wall piece at the right and bottom [c24-c, e09]; Established adds the gantry, S3 depot and ring [c17, e11]. **[inference]** the stage progression reads well, but the rectangular deck cut-off looks unfinished next to the isometric slots | c12, c17, c24-c, h09 | Medium (Art/UI call) | Art Director / owner |
| C-F7 | The Mine button overlaps the world in compact and build-card states: portrait 200 % with the build card open it covers the neighbouring empty slot [h02]; 100 % with a card it covers part of the bottom row [j01]; at 640x360 it covers the lower-right fence but no machine [e02] | h02, j01, e02 | Low | Keep-out for Mine against empty slots, T547 |
| C-F8 | 360x640 at 200 %: the bottom-right Overdrive button text "Ready" is cut off at the screen bottom and collides with the "Development Build" label (dev only) [g02]. At portrait 200 % "Overdrive" touches the right edge [d02]. The 844x390 hub row "Research" breaks mid-word ("Resea/rch") [f02] | g02, d02, f02 | Low | T547 text-scale polish |
| C-F9 | **Android accessibility tree is empty.** `uiautomator dump` shows only a `SurfaceView` (no UI Toolkit nodes), so checklist 11 (strip indicator accessible name) and TalkBack cannot be verified on device and the player screen reader cannot work on this build | uiautomator dump | Info (known R-7 carried) | later feature |
| C-F10 | Offline return: the 8 h fixture return summary lists cash, produced, used, contract auto-delivery and waiting machines [c16, c21]; no milestone line because no level-up happened while away (checklist T540-6 not exercised). A 9 s gap still shows a full "Welcome back" modal ("Away for 9 s", Cash +$0) [c14] | c14, c16 | Low | threshold for the modal (owner copy) |
| C-F11 | Hand-over and camera: building from the build card opens the machine card at once and the world re-centres vertically by about 120 px between the build card and the machine card [c03 vs c05]. **[inference]** this is the "world jump" the review feared; it is a reframe, not a pan | c03, c05 | Low | UI/UX decision |
| C-F12 | A first Android build from the warm worktree produced a 64 MB APK that hung at `SetGameState isLoading: false label: 0` with `Loading.AsyncRead` at 100 % CPU (the T552 signature); the Gradle step had also failed on a stale `compressed_assets` folder. After deleting `Library/Bee/Android` the clean rebuild (44.5 MB) loaded in 3.4 s. The batch-B APK also loaded in 3 s on the same AVD. **[inference]** a stale incremental build artefact, not a game regression, but QA of later batches should check the APK size | build.log, build2.log | Info | note for the next gate |

Counts: **Medium 6** (C-F1 to C-F6), **Low 4** (C-F7, C-F8, C-F10, C-F11), **Info 2** (C-F9, C-F12); 12 rows.

## 4. Checklist results

Marker checklist (T557):

| # | Result |
|---|---|
| 1 Empty state | Not covered (no all-running state reached) |
| 2 Marker within about 1 s, caps, no text | Partly: shortage marker present at load; caps exceeded by store-full markers (C-F3); no digits or text in the world, pass |
| 3 Keep-out vs machines, cues, selected card band | Pass for the card (Smelter, row 2, card below) [i04]; banner and Mine cover them (C-F1, C-F7); not tested for first/last row |
| 4 Tone under marker, grayscale, n_min | Not covered; markers read clearly on the dark deck [c22] (feel) |
| 5 Draw order | Pass: marker above cue and overdrive pips; the pip sits left of the marker and stays readable [i06] |
| 6 Store full: depot FULL vs badge | Fail/partial (C-F5): the depot FULL sprite not visible with the world framed; the badge is a per-machine marker |
| 7 Taps a to d | a/c fail (C-F4); b (machine) pass; d (far world closes the card) pass |
| 8 Reduced motion | Not covered |
| 9 Rotation | Pass with a defect: card, ghost and type kept; old layout at about 150 ms [h03], new layout by 2 s [h05]; h04 not inspected; the camera defect C-F2 |
| 10 Recovery | Not testable: the new Miner was blocked by a full store [j04] |
| 11 Accessible name | Fail on this channel (C-F9) |
| 12 Pixel shimmer | Not covered |
| 13 logcat | Pass: no marker-related errors; only the harmless `AssetPackManager ClassNotFound` and the expected `save.v2.invariant` diagnostics from my invalid fixtures |
| U1, A10 | Pass in 640x360 and 844x390: Cash, Store, chip, Overdrive stay visible with the Office hub and Market sheet open [e03, f02] |

T539 build card: 1 pass at portrait 100 % and 200 % (ghost visible above the card, type rows visible and scrollable, Confirm/Cancel >= 48 pt) [h02, j01]; front/middle/back locations only middle tested. 2 landscape 2400x1080 200 %: Cash/Store/chip/Overdrive visible, ghost visible in the world column, card docked at the right [h05]; 640x360 not covered for the card. 3 tablet not covered. 4 ghost reads teal at reduced opacity, selected slot has a blue ring, other empty slots a ring too [j01]; occupied and locked slots distinguishable. 5 shortfall not tested (always affordable). 6 build with Cash: hand-over to the machine card, Upgrade focused [c05]; no dust effect captured. 7 Back closed the card (C-F2 follow-up), rotation kept it, hub open while card open not tested. 8, 9 not covered.

T540: 1 C1 fail (C-F1). 2 banner below the strip on phone portrait and landscape; no camera jump seen; two milestones in a row: level 3 then level 4 in 640x360 each replaced the other without stacking [e08, e10]. 3 B-01 wording "Level 3. 2 new locations open. Deck extended." wraps at 200 % without truncation. The level-4 banner reuses "Deck extended." [e10]. 4 gone by about 3 s; reduced motion not covered. 5 not covered. 6 offline summary shows no milestone line (not exercised). 7 stages: Early (railing only) [c22], Partial after level 3 (lit deck tiles at the right and bottom, wall piece) [c24-c], Established at level 4 [e11] and at level 5 [c17] (gantry, S3 depot, full ring); the lit ring grows from the depot side; no floating prop seen; a wall prop appears in the top-right corner [c17] but not in the keep-out at 640x360.

Batch-B regressions: B-F1 (landscape Cash/Overdrive) **fixed** (e03, f02, h05). B-F5 (Mine/Overdrive gap) improved: Overdrive moved to the bottom row; Mine sits above it with clear space in 100 % portrait [j01]. B-F8 (hub Contracts row copy) **fixed**: "Locked: unlocks after you produce Iron Plate, Gear or Iron Bar" [c07]. B-F9 (Back returns to hub) not tested. B-F10 (Research clip) not covered. R-2/R-3 (Details scroll, landscape Details covering columns) not covered. A-F2 (cause line) still category-level: "Short on Iron Ore" for the Smelter [i04], though the underlying store is full (an artefact of my fixture).

## 5. Feel statements **[inference]**

The Established factory is the strongest visual moment: dense, readable, clearly richer than the Early stage. The early run looks sparse and slightly empty because of the black void. The build flow feels faster (2 taps) and the ghost is clear. The banner is informative but too large for small screens. Store-full pins on every Miner look like noise when the whole factory is blocked.

## 6. Go/no-go proposal

**Conditional go for batch D**, with a fix-first list: C-F1 (banner covers machines on compact screens, a spec-required mandatory check), C-F2 (camera stuck after the card closes in landscape), C-F4 (marker tap does nothing), then C-F3 and C-F5 (marker cap and the depot FULL visibility) as T542/T547 scope. Economy is unchanged. Not covered: Windows, tablet, reduced motion, accessibility, prestige. A human playtest on a real phone remains needed for landscape feel and for the banner timing. The owner decides.

## 7. Environment notes

- Emulator `IdleFactory_Test_API_36` booted headless, used with `adb` only, killed (`adb emu kill`, `adb kill-server`); no `emulator`, `qemu` or `adb` process remains. `font_scale` 1.0, `wm size` 1080x2400, density 420, auto-rotate on, user rotation 0 restored. The game stays installed; no other app touched.
- Unity builds re-serialized settings; restored with `git checkout`; the Gradle `compressed_assets` and `Library/Bee/Android` folders (generated) were deleted in the gate worktree; the strategy baseline was restored.
- Scratch: `test-results/f006-p3c/` (APKs, logs, fixture sets, helper scripts), untracked. The derived level-2 fixture and its recipe (`mkset.py`, inline patch) live there; the committed fixtures were not modified.

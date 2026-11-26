# Play-Tester final report (T543 final gate, issue #715)

**Status: proposal for the owner.** Two channels: the deterministic balance runner and a touch-only player session on the dedicated emulator. Neither is a human playtest. This report approves nothing and turns no feel statement into a threshold. Feel statements are marked **[inference]**. Captures are in `playtest-agent-f006-final/` (names in brackets). The independent UI/UX review is a separate report that I did not read.

| Item | Value |
|---|---|
| Build commit | `437cb46c` (origin/main, merge of #789 T544; contains T542, T561, T563, T562, T544) |
| Channel A | `IDLEFACTORY_WRITE_STRATEGY_BASELINE=1; scripts/test-unity.ps1 -Suite EditMode -Category BalanceFull`: `UNITY_TEST_PASS suite=EditMode tests=7 passed=7`, exit 0 |
| Channel B | Development APK of 437cb46c (`BuildAndroidArm64DevelopmentGles3 -noProfilerAutoconnect`, after deleting `Library/Bee/Android` and the Bee state files; 44.6 MB, first display after 1.4 s), AVD `IdleFactory_Test_API_36`, headless, input by `adb shell input` only, screenshots by `adb exec-out screencap`. Native 1080x2400 at 420 dpi, font scale 1.0 (never changed). Emulator FPS is about 45 on this host for any build (environmental, T544); I did not read FPS and file nothing about it |
| Fixtures | Fresh install (hand-played start); committed `device/empty-location` (journey `F006` default); the W005-L set built by the journey script; the derived level-2 variant (factory level 2, untracked recipe from the gate-C scratch folder, loads) |
| Not covered | Windows player, tablet, landscape, 200 % text, reduced motion, TalkBack (known empty accessibility tree, R-7), grayscale, prestige, real-time offline longer than 20 s, a hand-played contract delivery (see 2.6), every screen class other than 1080x2400 portrait. These belong to the UI/UX gate and to a human playtest |
| Leases | `unity,emulator` held by `issue-715` (parent), not released by me; no UnityMCP, no host input |

## 1. Channel A: economy diff

The regenerated `specs/005-economy-contracts/reviews/strategy-baseline-r3.md` differs from HEAD only in the build-commit cell, the measured wall times (111.1 s against 116.0 s) and the two projection lines. No measured economy number or gate line changed, so the batches since the previous gate (T542, T561, T563, T562, T544) change no economy value. Diff: `test-results/t543pt/baseline.diff` (untracked scratch). The baseline and `ProjectSettings/` were restored; `Assets/UI Toolkit/` deleted.

## 2. Channel B: session record

### 2.1 Fresh run, hand-played
| Step | Taps / time | Capture |
|---|---|---|
| Start: Cash 37, hint "Build a Miner: tap an empty location." 4 open slots, locked slots numbered by unlock level | 0 | a01 |
| Tap an empty slot: build card opens, Miner preselected (Cost $30, Affordable), other types show "Unlocks at factory level N" | 1 | a02 |
| Confirm: machine card opens, "Miner - level 1, Running", Upgrade "Need $29" | 2 total | a03, a04 |
| Close the card (tap the far world), long-press Mine 25 s: Store 6 % | 1 + 25 s hold | a05-a07 |
| Office, Market, long scroll to Iron Ore, Auto-sell on | about 4 taps + 6 swipes (my first tap missed, the row sits low) | b01-b04, c03-c05 |
| Cash 174 about 40 s later; the upgrade marker appears above the Miner | | c05, d01 |

The first upgrade is reachable about 2 to 3 minutes into a hand-played run, but only after the player finds Auto-sell in a long Market scroll [inference: the Market scroll is the weakest step of the first minutes; it was the same in gate C].

### 2.2 Upgrade-available marker (T563, FR-027)
- **Appears when affordable:** white chevron above the Miner at Cash 174 and Upgrade $36 [c05, d01, d02]; also above the three clean top-row Miners in the Established fixture [e02] and above six machines in the W005-L set [h02].
- **Gone when spent:** four upgrades in a row (cost 36, then rising to 72). At Cash 64 and Upgrade $61 the marker is still shown [d05]; after the next upgrade (Cash 11, "Need $61") it is gone [d06].
- **Gone under a problem badge:** in the Established fixture the machines under a red Missing-input badge show no marker, the clean Miners do [e02]. Later a freshly built Assembler short on Iron Plate (Missing-input badge, Cash 4.99M, Upgrade $68 affordable) shows no marker [l02]. Machines under the yellow store-full badge show none either [h02].
- Hidden on boosted machines: not tested (owner accepted).
- The marker is never red and never covers the badge; it sits left of the badge column [e02, h02].

### 2.3 Attention marker: resource icon and tap (T561)
- The shortage marker is the resource icon at the trunk junction: Iron Ore [e02], and the Iron Plate icon after the Assembler was built [l02]. The red arrows on the machines are the per-machine Missing-input badges, not the marker.
- A tap on the Iron Ore icon centre (982,964): the first affected machine (a Smelter) is selected and its card opens with "Short on Iron Ore / Auto-sell is selling it" [f01]. Pass.
- The strip chip cycles between categories on its own ("4 +2" with an ore icon, later "6" with a contract icon) [e02, h05].

### 2.4 Back on the Contracts sheet (C-85)
Hub, then Contracts (locked state in the empty-location fixture, "Contracts are locked ... Iron Plate, Gear or Iron Bar" [g03]): one device Back (`KEYCODE_BACK`) closed the sheet at once [g04]; a second Back with nothing open did nothing and no crash [g05]. In the W005-L set: the Contracts sheet with offers [h03], Accept on the Plate contract, then one Back closed the sheet [h05]. The journey step `F006HubContracts` also passed with its real state check (`test-results/t543pt/journey-hub/journey.md`). **Fixed on this build.**

### 2.5 Build-card hand-over recentre (D7)
Fresh run: the empty slot sits at the same row before the card opens (a01), with the build card (a02) and with the machine card after Confirm (a03, a04). Established fixture: the same slot at the same place with the build card [l01] and after Confirm [l02]. **0 px by eye in both runs; no hand-over jump.** (No cross-correlation run; T561 recorded 0 px with it.)

### 2.6 First contract, hub, stage change, offline return
- **First contract:** a fresh hand-played run cannot reach it within this session (needs a Smelter at factory level 2 and a produced bar). On the W005-L set the Contracts sheet lists Gear, Plate and "Produce Iron Plate" offers with Reward lines [h03]. Accept on the Plate contract shows "Accepted." and the other offers become disabled with "Finish or abandon the active contract first." [h04]. After Accept the sheet scrolls so the new active contract is half out of view, focus stays on the disabled Accept (F-5). Delivery by hand was not played; the offline summary credited an auto-delivery.
- **Hub:** every row reachable in 2 taps; Contracts row copy "Locked: unlocks after you produce Iron Plate, Gear or Iron Bar" [b01]; Factory Sale "Not ready yet: needs Factory Value 10.0M" [b01]; Overdrive row "Locked" with the hint "Overdrive unlocks after the factory reaches level 2." [b01].
- **Stage change:** level-2 variant, Market, Expand factory ($2,430): the banner "Level 3. 2 new locations open. Deck extended." appears below the strip within 0.7 s and covers the tops of the markers of the top machine row at 100 % text with the Market sheet open [j01]; it is gone by +6 s [j03]. Two new numbered slots opened and the deck moved to the Partial stage (lit tiles, right and bottom) [k01]. Market now says "The factory is now level 3: 8 locations." and the next level costs $6,885 [j01].
- **Offline return:** a W005-L cold start (synthetic 26,579 h gap, 8 h credited) shows the full "Welcome back" summary: time, cash and stock, contract auto-delivery, limits [h01]; the level-2 variant lists the waiting machines (store full, an input missing) and "Contract still active: deliver 165 Gear." [i02]. A real 20 s Home gap returned with no modal [k02] (accepted behaviour: none under 60 s). Not covered: a real 60 s to 8 h gap.
- **Error state seen once:** a rejected fixture produced the damaged-save dialog with Retry, Use previous save and Discard damaged save, focus on Retry [i01]. Expected, not a finding.
- logcat over the whole session: 0 `FATAL EXCEPTION` or `SIGSEGV`; the only E line is the known `AssetPackManager ClassNotFoundException`.

## 3. Findings

Severity: Medium = an acceptance scenario or checklist item fails on this build, or the player loses information; Low = rough edge or copy; Info = limit or positive result. The disposition is my proposal; the owner decides.

| ID | Observed behaviour | Evidence | Sev | Proposed disposition |
|---|---|---|---|---|
| F-1 | **Store-full markers are still per machine and uncapped.** With the store full, six machines carry a yellow store-full marker (W005-L) and all three top-row Miners in the level-2 variant. The gate-C marker checklist expected at most 2 shortage and 1 store-full marker (gate-C C-F3; I found no disposition for it in the follow-up files I read). The depot also shows its own store-full icon, so the player gets both. Unchanged since gate C | h02, h05, i04 | Medium (open from gate C) | Owner or Architect disposition: cap them, or accept per-machine pins as designed |
| F-2 | **The deck still ends in a hard-edged rectangle with a black void above and below** at fit-all, in every stage seen (Early, Partial, Established). Same as gate-C C-F6, unchanged | a01, k01, e02 | Medium (Art/owner call) | Art Director / owner |
| F-3 | **The milestone banner covers the marker tops of the top machine row** with the Market sheet open at 100 % text on 1080x2400. The compact cases of gate C (200 %, 640x360) were not re-captured here | j01 | Low at this class; open for compact classes | UI/UX gate to re-check compact classes |
| F-4 | **The world reframes when the card closes** in the early run: closing the machine card moved the factory down by about 44 px (a04 to a05) because the world re-fits when nothing is selected. Opening a card does not move it (D7) [inference: a small, readable settle, but a visible shift] | a04, a05 | Low | UI/UX decision |
| F-5 | **Accepting a contract scrolls the Contracts sheet** so the new active contract is half out of view, and focus stays on the disabled Accept button | h04 | Low | UI/UX or later polish |
| F-6 | **The Upgrade button keeps a white focus ring after taps** (after repeated upgrade taps and on the new Assembler card), while the first open of a machine card showed none. This is the C-86 focus-ring item for the UI/UX gate; noted only as seen | d05, d06, l02, d02 | Info | UI/UX gate (C-86) |
| F-7 | **The first minutes depend on a hidden step:** the first upgrade needs Auto-sell or manual Sell from the Market, found by a long scroll; the Mine long press fills the store but yields no Cash by itself [inference] | a07, b03, c05 | Low | later hint or copy |
| F-8 | **The offline summary is a long text block** (about 25 lines in the W005-L case) [inference: informative and legible, but dense] | h01, i02 | Low | owner copy decision |
| F-9 | Positive: upgrade-marker lifecycle, resource-icon marker and tap, Back on Contracts, D7 0 px recentre, build in 2 taps, banner timing and wording, stage change to Partial, offline summary content and no modal under 60 s | section 2 | Info | none |

Counts: **Medium 2** (F-1, F-2, both carried unchanged from gate C), **Low 5** (F-3, F-4, F-5, F-7, F-8), **Info 2** (F-6, F-9). No new Medium and no regression against gate C. Gate-C Mediums C-F2, C-F4 and C-F5 were not seen as defects in the cases I played (marker tap works, no stuck camera in portrait, the depot store-full icon is visible [h05]); C-F1 only in its portrait 100 % form (F-3).

## 4. Checklist results

| Item | Result |
|---|---|
| Upgrade marker appears when affordable / gone under a badge / gone when spent | Pass (2.2) |
| Marker never covers the badge, never red | Pass |
| Shortage marker shows the resource icon; tap selects the first affected machine | Pass (2.3) |
| Back closes the Contracts sheet | Pass (2.4) |
| Build-card hand-over without a world jump | Pass, 0 px by eye (2.5) |
| Build flow tap count | 2 (empty slot, Confirm) |
| Marker caps (2 shortage, 1 store-full) | Fail, unchanged (F-1) |
| Milestone banner vs machines | Low on the phone at 100 % (F-3); compact classes not covered |
| Reduced motion, 200 % text, landscape, tablet, TalkBack, grayscale | Not covered (UI/UX gate) |
| logcat | Pass |

## 5. Feel statements **[inference]**

The Established factory with its row of white upgrade chevrons reads as "things to do" at once, and the chevron is calmer than the red badges next to it. The shortage icon at the trunk reads better than the old generic arrow. Building feels fast, and opening a card does not move the world, which feels steady. The first minutes are quiet and slow before the first upgrade. I did not judge frame rate (about 45 fps on this host for any build).

## 6. Go/no-go proposal

**Go for F006 final, with notes.** No economy change, no new Medium, and the items the gate named (upgrade marker, shortage icon and tap, Contracts Back) plus D7 hold on a fresh APK of the merged main. Two Mediums stay open from gate C and need an owner disposition (F-1 marker cap, F-2 deck edge). Still needed and not covered here: compact-class and landscape banner checks (UI/UX gate), a human playtest on a real phone for feel and banner timing, and real-time offline gaps. The owner decides and keeps the veto.

## 7. Owner questions

1. F-1: cap the store-full markers or accept per-machine pins as designed?
2. F-2: accept the hard-edged deck and black void for F006, or schedule an art task?
3. F-4: is the 44 px settle when a card closes acceptable?
4. F-7 / F-8: any hint or copy change for the first upgrade path and the long offline summary, now or later?

## 8. Environment notes

- Emulator `IdleFactory_Test_API_36` booted headless, used with `adb` only, then killed (`adb emu kill`, `adb kill-server`). `wm size` 1080x2400, density 420 and font scale 1.0 were never changed. The game stays installed; no other app touched. The balance run ran while the emulator booted, which only restarts the adb server.
- Unity runs re-serialized settings; restored with `git checkout`; `Assets/UI Toolkit/` deleted; the baseline restored. The APK was deleted; `Library/` kept. Scratch in `test-results/t543pt/` (untracked): logs, journey reports, fixture copies, the `h.sh` helper.
- The first Android build after moving the warm Library succeeded with a 44.6 MB APK (no stale-Gradle hang; Bee state files and `Library/Bee/Android` were deleted first).

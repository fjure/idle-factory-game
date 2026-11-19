# Play-Tester batch-B report (T543, issue #715)

**Status: proposal for the owner.** Two channels: the deterministic balance runner (reviewed policy r2, unchanged) and a player session on the Android emulator (touch only). Neither is a human playtest. This report approves no content revision or balance number and turns no feel statement into a threshold. Every finding is open for owner decision (AGENTS.md; `docs/agents/roles-and-reviews.md`, Play-Tester). Feel statements are marked **[inference]**. Measured values name their source. The independent UI/UX review is a separate report.

| Item | Value | Source |
|---|---|---|
| Build commit | `b386a82a620018e66637c6fd32768c9a212b9a9c` (detached, gate worktree `f006-p3b-playtest`) | `git rev-parse HEAD` |
| Batch under test | T535 (Office hub, back order, card fixes), T536 (PanelScaffold pass), T537 (slim persistent layer) | task brief |
| Channel A | `$env:IDLEFACTORY_WRITE_STRATEGY_BASELINE=1; scripts/test-unity.ps1 -Suite EditMode -Category BalanceFull`: `UNITY_TEST_PASS suite=EditMode tests=7 passed=7`, exit 0 | `test-results/f006-p3b/balance.out` |
| Channel B | Android development APK of b386a82 (`BuildAndroidArm64DevelopmentGles3 -noProfilerAutoconnect`, IL2CPP ARM64, GLES3, 44.3 MB, build exit 0), installed on the dedicated AVD `IdleFactory_Test_API_36` (API 36 x86_64, 1080x2400 at 420 dpi, headless `-no-window -gpu host`, cold boot, `/data` 7.2 GB free). Input by `adb shell input` only, screenshots by `adb exec-out screencap`. The host mouse, keyboard and windows were not used by the Android session. Classes: phone portrait 1080x2400 (about 410x914 pt), phone landscape 2400x1080, and `wm size 640x360` with `wm density 160` (compact landscape, 640x360 pt). Text scale 100 % and 200 % by `settings put system font_scale 2.0`. `font_scale`, `wm size`, `wm density` and rotation restored to 1.0, 1080x2400, 420, auto-rotate on | `build-apk.log`, `adb` outputs |
| Earlier Windows attempt | About 3 min of the Windows development player at 1280x720 before the owner stopped host input. Three PNG kept (`00-fresh`, `03-miner-card`, `04-office-hub`); its findings are folded in below as B-W items | `player1.log` |
| `contentRevision` | 3 (unchanged; no content change in this batch) | channel A |
| Strategy (B) | hand-played new-player tinkerer: hand mining, three Miners, Ore auto-sell, factory level 2 (480 Cash), Overdrive once, Smelter (paused by auto-sell), hub navigation (Market, Contracts, Items, Research, Settings). Not a casual-pace calibration | section 2 |
| Wall time | About 20 min of wall time on the emulator across portrait, landscape and 640x360, of which about 15 min at x1 game speed (no time scale on touch); the earlier Windows run added about 3 min | wall clock |
| Not covered | The Windows 1280x720, 390x844 and 640x360 player sessions (owner chose emulator-only because the host was in use), keyboard Tab order, screen reader, pinch zoom (no `adb` pinch), rotation with an open card, first prestige and long horizons | section 6 |
| Leases | `unity,emulator` held by `issue-715` (not released by me); no UnityMCP | `resource-lease.ps1 -Status` |

## 1. Channel A: strategy runs, economy unchanged

`git diff` of the regenerated `strategy-baseline-r3.md` against `HEAD` differs only in the build-commit line (`b386a82a6` now), the wall-time cells, the "Measured" line (133.9 s of history time and 134.1 s matrix wall time against 116.0 s and 116.1 s committed; the host was busy) and the two projection lines (2.5 min and 4.6 min against 2.2 and 3.9; ceiling 36.6 min against 32.1). Every measured number and gate is identical (filtered every line without `ms (offset`), so batch B changes no economy value. The diff is at `test-results/f006-p3b/baseline.diff` (untracked). `strategy-baseline-r3.md`, `ProjectSettings/` and the URP global settings asset were restored; there is no `Assets/UI Toolkit`.

## 2. Channel B: session record (portrait and landscape on the emulator)

Core loop on the touch build, counted as taps from a closed world (screens in section 5):

| Goal | Taps now | Batch A (report A and re-run) | Note |
|---|---|---|---|
| Build a Miner | 3 (slot, Build Miner; the world, no extra tap) | 2 | the same as before; the build list opens on the slot |
| Upgrade from the card | 2 (machine, Upgrade) | 2 | unchanged; Upgrade keeps focus and the card stays open |
| Sell (Market) | 3 (Office, Market, Sell or Auto-sell, after a scroll) | 2 (tab, Sell) | +1 for the Office entry |
| Take a contract | not reached: Contracts stay locked until Iron Plate, Gear or Iron Bar is produced (B-F11) | n/a | Office, Contracts = 2 taps to read the lock |
| Research | not reached: 0 RP; Office, Research = 2 taps to read the screen | 1 | +1 for the Office entry |
| Overdrive | 1 (bottom row, always on screen in portrait) | 1 | unchanged in portrait; 2 in landscape and 640x360 only when no sheet or card is open (B-F1) |

Overdrive states (portrait, bottom row, 1080x2400): Locked = dimmed bolt with a lock, "Overdrive / Locked"; Ready = bright cyan outline, bold bolt, "Ready"; On = ring icon, "On 0:30" with a pink marker over each boosted machine (3 Miners); Cooldown = clock icon, dim, "Cool 4:51". All four are distinguishable at arm's length in the screenshots; "Cool" is an abbreviation of "Cooling down" (the hub row says "Cooling down, 4:19", with "Overdrive is cooling down" below it).

## 3. Findings

Severity: Medium = a spec acceptance scenario or SC is not met on this build, or a player loses information that matters; Low = rough edge or copy; Info = limit of this run or a positive result. Disposition is my proposal; the owner decides. Feel statements are marked **[inference]**.

| ID | Observed behaviour | Evidence | FR/SC | Sev | Proposed disposition |
|---|---|---|---|---|---|
| B-F1 (watch 1) | **Phone landscape: opening a card, Details, the hub or any sheet hides the left column (Cash, Store, attention) and the right column (Office, Overdrive, gear).** While upgrading in landscape the player sees neither Cash nor Overdrive, nor the Office entry. The compact card shows "Upgrade: $36" with no "Need" line when affordable and no cash figure; Cash returns only after the card closes. At 640x360 and 200 % the same happens (hub open: Cash and Store gone). **[inference]** the Upgrade button state (blue or dim) carries affordability, so a player is not lost while upgrading, but a player cannot read Cash or Overdrive's cooldown without closing the card. In portrait the top strip and the bottom row stay on screen over sheets, so this is landscape only | `m21-land-card.png`, `m22-land-details.png`, `m31-640-200-card.png`, `m35-640-200-hub.png` against portrait `m03`, `m04` | FR-003, FR-017 | Medium | Owner decision. Option: keep a one-line Cash or Overdrive state in the card header on landscape, or keep the right column visible over the card. Carrier T537 follow-up or a UI/UX call |
| B-F2 (watch 2) | **640x360 pt at 200 %: the whole factory is in view at fit-all** (12 slots; the 3 Miners and 9 slots all visible). Chrome: left column about 92 px (14 %), right column about 125 px (20 %), so the world is about 66 % of the width (about 420 of 640 px). Mine (about 125x90 px) sits in the lower right of the world and covers no machine. With a card open the world is shifted right and still fully visible (`m31`). **[inference]** chrome is heavy but tolerable. The numbers I measured differ from the review's 55.8 % chrome (measured on a different screen layout); I did not compute the area ratio | `m30-640-200-closed.png`, `m31-640-200-card.png` | FR-017, SC-009 | Info (acceptable) | none |
| B-F3 (watch 3) | **The tab bar is gone and the Office entry costs one tap** (table in section 2). The Office entry is easy to find: icon, the word "Office" and, when something is new, a small blue dot (seen top right of the button after the factory reached level 2, `m42`). The hub groups Market, Contracts, Items, Research, Industry, Overdrive, Prestige, Settings with a one-line state each. **[inference]** a new player finds Office quickly because it is the only text button in the bottom row; the dot draws the eye to news | `m04`, `m42`, `m50` | FR-003, FR-017 | Info | none |
| B-F4 (watch 4) | Overdrive Locked, Ready, On and Cooldown are readable at a glance in portrait (section 2). The hub row says "Locked" with the line "Overdrive unlocks after the factory reaches level 2." and, in cooldown, "Cooling down, 3:01" with "Overdrive is cooling down". Minor: "Cool 4:51" in the bottom row is an abbreviation | `m42`, `m43`, `m45`, `m04`, `m50` | FR-003 | Info (positive), one Low copy nit | Low: write "Cooling" or "Cooldown" (T536 copy) |
| B-F5 (watch 5) | **Portrait: Mine and Overdrive are stacked** with a gap of about 24 px (about 9 dp, from the screenshot: Mine bottom at y 2250, Overdrive top at y 2275 of 2400) between two targets of about 48 dp. At 200 % the gap is about the same. Overdrive is a consequential tap (30 s boost, then a 5 min cooldown). Mine is a long-press target (I held it for 25 s), so a thumb that slides down from Mine can reach Overdrive. I did not mis-tap in about 12 taps near them. **[inference]** a risk, not a defect | `m01`, `m15` (200 %) | FR-003, SC-009 | Low | T537 follow-up: add vertical clearance or move Mine up when Overdrive is Ready |
| B-F6 (watch 6) | **Production attention indicator**: with Ore auto-sell on, a built Smelter paused; the top strip gained a yellow-edged chip with the Ore icon and "1" (`m47`). One tap on the chip **selects the affected machine and opens its card** ("Smelter - level 1 / Paused / Upgrade: $11 / Details / Short on Iron Ore", `m49`). The world also shows a red marker over the Smelter. The card cause line is still **category-level** ("Short on Iron Ore"; auto-sell as the real cause only in Details), as in batch A's A-F2/A-5. The chip stayed at "1" after I switched Ore auto-sell off in the Market (my toggle may have missed; not verified). **[inference]** the chip leads to the cause machine in one tap, which is good, but the new player is told "Short on Iron Ore" while Ore is being sold | `m47-smelter-card.png`, `m49-attention-tap.png` | FR-016, FR-018, SC-003 | Low (A-5 is already a carried item) | A-5 stays with T528/T537 copy; owner copy decision |
| B-F7 (watch 7) | **Text-scale change with Details open (portrait): the open Details view closes and the card is not shown; the world is not shifted.** After `font_scale` 2.0 the camera framing is unchanged (grid at the same rows), the selected slot shows a white focus box and the sheet is gone. I did not reproduce the R-5 camera shift. At 200 % the portrait card is clean (Upgrade fully visible, Mine no longer touches it) and the Details sheet reaches about 52 % of the height with the Upgrade button on its bottom edge above the persistent row; the sheet is not overlapped by Mine. At 640x360 and 200 % the card fits and `Details ›` is fully visible (no clipping: R-1 fixed for the no-cause-line case, not tested with a cause line) | `m15`, `m17`, `m18`, `m31`, `m33` | FR-016, FR-017 | Low (state loss on text-scale change); Info (positive for R-1, R-5) | Low: a text-scale change drops the open card or Details; acceptable or an idea |
| B-F8 (watch 8) | Copy seen: Contracts sheet "Contracts are locked / Contracts unlock after you produce Iron Plate, Gear or Iron Bar."; Items sheet "Nothing here yet / Earn them from contracts."; Overdrive "Locked / Overdrive unlocks after the factory reaches level 2."; Prestige "Not ready yet: needs Factory Value 10.0M"; Market lock copy ("Locked: unlocks after the factory reaches level 3."). All read clearly. **But the hub row for Contracts says "Nothing ready to deliver" while the Contracts screen says the system is locked**; and the Items row says "0 owned" while the sheet says "Earn them from contracts". The hub row is more optimistic than the sheet | `m50-hub-l2.png`, `m51-contracts.png`, `m53-items.png` | FR-017, SC-010 | Low | T536 copy follow-up: show "Locked" on the hub row until contracts unlock |
| B-F9 (watch 9) | **Back order on touch.** From a card: Details, card, closed in order (landscape `m22` to `m24`: Back once from Details gives the card with focus on `Details ›`, Back again closes it). **Back from a system screen opened through the Office hub (Market) closes everything and does not return to the hub** (portrait, `m06`: Back once from Market showed the closed world). The F006 brief reads "hub → system view → back"; I read it as back-to-hub; the owner may have intended the other behaviour. Settings (gear) closes with one Back. Android Back by `KEYCODE_BACK` was reliable; keyboard Escape was not tested on this channel | `m06-back1.png`, `m22`, `m23`, `m24` (landscape back order) | FR-017, FR-045 | Low (owner decision) | Owner question: should Back return to the hub? Verify against `BackOrderTests` |
| B-F10 | **Research screen text is clipped at the right edge in portrait at 100 %**: the right column of the tree shows "Output x2 on Min", "Sale price x1.1 or", "Locked: needs Miner Tuning Lv 3" cut off at the screen edge (cards run under the scrollbar edge). Left column cards fit | `m52-research.png` | FR-017, SC-010 | Low-Medium (text loss on a main screen) | T536 follow-up: make the tree scroll horizontally or wrap; check at 200 % |
| B-F11 | **Contracts stay locked for a long time with the cause hidden.** After 15 min with 3 Miners, a Smelter paused by Ore auto-sell and factory level 2, Contracts remained locked (they unlock when Iron Bar, Plate or Gear is produced). The lock copy is clear, but the Smelter was paused by the player's own auto-sell, so a new player who followed "Build a Miner" and turned on auto-sell never reaches Contracts without finding Details. Also Research, Items and Contracts (RP and items) depend on it. The "tinkerer" on Windows in batch A hit the same Smelter pause (A-F2) | `m47`, `m51`, `m52` | SC-003, US2 | Low (known A-F2 area) | Owner decision with A-5 |
| B-F12 | Fixes verified from batch A: **A-F3** (shortfall shown twice) gone: a single "Upgrade: $36 / Need $29" in the button (`03-miner-card.png` from the Windows run; the portrait card with a shortfall was seen at start); **A-F4** (`Details ›` moves) fixed: `Details ›` stays below Upgrade and the cause line sits under it (`m47`); **A-F8/R-11** zoom buttons: no zoom buttons on touch and the Settings row reads "Adds zoom buttons to the factory view. Pinch zoom still works." (`m12`); **A-F7**: not verified on this channel (I did not read logcat for the first launch); **A-F1/U-A2/U-A3**: Back closes Details and card in order. Still present: **A-F5** stale hint under an open card (Windows 1280x720 run), **R-2** Details opens with the previous scroll offset (`m33`: the 640x360 Details opened scrolled down), **R-3** landscape Details covers the third and fourth columns (`m22`) | `m12-autosell.png` (Settings), `m22`, `m33` | various | Info | A-F5, R-2, R-3 as carried items |
| B-F13 | **Positive, portrait**: the card is a bottom band that leaves the machine visible; Mine moves up above the card; the bottom row (Office, Overdrive) and the top strip (Cash, Store, gear) stay on screen over every sheet; Upgrade in place keeps focus; the hub opens with focus on Market (white ring) | `m03`, `m04`, `m13` | FR-016, FR-017 | Info (positive) | none |
| B-F14 | Windows 1280x720 (about 3 min): the side panel keeps "Factory" with "Overdrive Locked" at its top right; the DevTools time-scale row (development only) sits over the top-left of the world. The Market panel opened from the hub showed no visible Back control (title only). A wheel scroll once left the player at a 0x0 client area, not reproduced | `00-fresh.png`, `04-office-hub.png`, `player1.log` | none | Info | none, dev-only |

Counts (table rows): **Medium 1** (B-F1), **Low 8** (B-F5, B-F6, B-F7, B-F8, B-F9, B-F10, B-F11, plus the B-F4 copy nit), **Info 5** (B-F2, B-F3, B-F4 positive, B-F12 and B-F13 positive, B-F14), 14 rows in total.

Feel statements, all **[inference]**: the portrait layout feels calm and the actions are always in the same places; landscape feels stripped while a card is open because the player cannot see the cash for the next upgrade; the hub is easy to learn and its one-line states are useful, except where a row says "Nothing ready" for a locked system.

## 4. Regressions of the batch-A findings (A-F1 to A-F8, R-1 to R-11)

| ID | State |
|---|---|
| A-F1 Escape/Back for card and Details | **Fixed on touch** (Back order Details, card, closed). Escape on a keyboard not tested here |
| A-F2 category-level cause | **Still present** (B-F6) |
| A-F3 duplicate shortfall | **Fixed** (B-F12) |
| A-F4 `Details ›` moves | **Fixed** (B-F12) |
| A-F5 stale hint | Present on Windows landscape; not seen on the portrait card |
| A-F6 two "Switch on" buttons | Not re-checked (no switched-off machine) |
| A-F7 first-launch log | **Not verified** (logcat not read) |
| A-F8 / R-11 zoom buttons on touch | **Fixed**: hidden on touch; Settings row explains pinch zoom |
| R-1 `Details ›` clipped at 640x360 / 200 % | **Fixed without a cause line**; not tested with a cause line |
| R-2 Details keeps the scroll offset | **Still present** (`m33`) |
| R-3 landscape Details covers the fourth column | **Still present** (`m22`) |
| R-4 tab labels clip at 200 % | **Moot** (tab bar removed); Mine no longer overlaps a column in portrait 200 % |
| R-5 camera shift after a text-scale change | **Not reproduced** (B-F7) |
| R-6 immersive Back swipe | Not tested (KEYCODE_BACK only) |
| R-7 TalkBack | Not tested (carried to a later feature) |
| R-8 APK size | The 44.3 MB APK built this time; the install needed an AVD with more free space (the old AVD failed with `INSUFFICIENT_STORAGE`): `extractNativeLibs=true` extracts 107 MB on install. T541 |
| R-9 strip order changes with the label width | Overdrive now sits alone in the bottom row; **moot** |
| R-10 target sizes | Cards and Upgrade measured at about 48 dp in the screenshots (the hub rows about 48 dp high); not re-measured pixel by pixel |

## 5. Evidence index (`playtest-agent-f006-B/`)

| File | Shows |
|---|---|
| `00-fresh.png`, `03-miner-card.png`, `04-office-hub.png` | Windows 1280x720: fresh start, Miner card, Office hub |
| `m01-start.png` | portrait fresh start: top strip, "Build a Miner" hint, Mine above the bottom row (Office, Overdrive Locked) |
| `m03-miner-card.png` | portrait card: Upgrade shortfall |
| `m04-hub-portrait.png` | portrait Office hub with locked and Prestige rows |
| `m06-back1.png` | Back from Market closed the world (B-F9) |
| `m13-after-builds.png` | three Miners built |
| `m15`, `m17`, `m18` | portrait 200 %: after the scale change with Details open, the card, Details |
| `m20`, `m21`, `m22` | landscape 100 %: columns, card (columns hidden), Details |
| `m30`, `m31`, `m33`, `m35` | 640x360 pt at 200 %: closed, card, Details (scrolled), hub |
| `m12-autosell.png` | Settings sheet (zoom buttons row) |
| `m43`, `m45` | Overdrive On 0:30 and Cooldown |
| `m47`, `m49` | Smelter paused, attention chip, card after the chip tap |
| `m50`, `m51`, `m52`, `m53` | hub in cooldown, Contracts locked, Research tree (clipped), Items empty |

## 6. What I could not observe

- Windows 1280x720 and 390x844 sessions beyond about 3 min (owner chose emulator-only because the host was in use); keyboard Escape and Tab order; the 390x844 text-scale sequence in a desktop window.
- Pinch zoom (no pinch through `adb input`), rotation with a card open, Android immersive-mode swipes, TalkBack.
- Taking a contract and spending RP (locked in this session: B-F11), prestige and long horizons (batch B changes no economy; channel A shows it unchanged).
- The card with a cause line at 640x360 and 200 % (R-1 with a cause line).
- A casual-pace calibration (my run is not one).

## 7. Open owner questions

1. B-F1: keep Cash and Overdrive visible over a card or sheet in landscape (a header chip), or accept?
2. B-F9: should Back from a system screen return to the Office hub?
3. B-F8, B-F11, B-F6: hub rows for locked systems ("Locked" rather than "Nothing ready"); card cause line (A-5).
4. B-F10: the Research tree clips in portrait at 100 %; fix now (T536 follow-up) or later?
5. B-F5: give Mine and Overdrive more clearance in portrait?

## 8. Summary for the owner and recommendation (proposal)

Channel A: the economy is unchanged on b386a82. On the emulator, the slim layout works well in portrait: the six-tab bar is gone, Office and Overdrive are always one tap away in the bottom row, the cash and store strip stays over sheets, the card is a clean bottom band, the attention chip leads to the stalled machine in one tap, Overdrive Locked, Ready, On and Cooldown read at a glance, and several batch-A findings (A-F3, A-F4, A-F7, A-F8, R-1, R-5) are fixed. The costs: one extra tap to reach Market, Contracts, Items and Research; in phone landscape a card or sheet hides Cash, Store and Overdrive (B-F1, the one Medium); the Research tree clips text in portrait; the Contracts hub row says "Nothing ready" while the system is locked; Back skips the hub; and the card cause line still says "Short on Iron Ore" when auto-sell is the cause. Coverage is partial: no Windows desktop, 390x844 window or keyboard session. **Proposal for batch C: go, with a short fix-first list** (B-F1 decision, B-F10 Research clipping, B-F8 hub copy) as T537 or T536 follow-ups, and a human playtest on a real phone for landscape feel. The owner decides.

## 9. Environment notes

- Emulator `IdleFactory_Test_API_36` was booted headless, used with `adb` only, and then killed (`adb emu kill`, `adb kill-server`); no `emulator`, `qemu` or IdleFactory process remains. The game stays installed on the AVD (package `com.DefaultCompany.idlefactorygame`); no other app was touched. The earlier `Medium_Phone_API_36.0` AVD was not started again.
- Windows dev save folder restored (`IdleFactory-Dev` original back; session kept as `IdleFactory-Dev.t543b-session1`).
- Unity builds (Windows player, Android APK) re-serialized ProjectSettings and left `.utmp`; restored with `git checkout` and removal. The gate worktree's active build target is Android now (expected). `git status` shows only this report and its PNG folder.
- Logs, `baseline.diff`, the APK and the player build stay untracked under `test-results/f006-p3b/`.

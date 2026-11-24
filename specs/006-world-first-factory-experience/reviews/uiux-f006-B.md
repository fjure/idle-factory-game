# UI/UX review, F006 P3 batch B (T543, issue #715)

**Status: independent UI/UX Reviewer report (read-only, Medium tier).** I did not write the reviewed UI and did not take verdicts from the Play-Tester (`playtest-agent-f006-B.md`); I read it first and re-checked each of its items on the device. Findings and the batch-C line are proposals for the owner.

## 1. Header

| Item | Value |
|---|---|
| Build | main `b386a82` (detached), the Android development APK already installed on the AVD (no reinstall, no Unity run) |
| Device | Dedicated AVD `IdleFactory_Test_API_36`, headless, host GPU, cold boot, 1080x2400 at 420 dpi (about 411x914 pt portrait). Input only through `adb shell input`, captures through `adb exec-out screencap`. No host mouse/keyboard/foreground window use |
| Classes and text | Phone portrait 100 % and 200 %, phone landscape (2400x1080) 100 % and 200 %, `wm size 640x360` at density 160 with 200 % text. 150 % not run |
| Save state | Existing save: 3 Miners, then Smelter and factory level 2 built by me during the session (see U-B13 for what the relaunch restored) |
| Evidence | `uiux-f006-B/` (about 4.4 MB, 49 PNG; captures are 900 px on the long side, crops are native pixels) |
| Cleanup | `font_scale` 1.0, `wm size` and `wm density` reset (1080x2400, 420), auto-rotate on, "Show zoom buttons" toggled on and off again, `adb emu kill`, `adb kill-server`; no `emulator`, `qemu` or `adb` process remains. Gate worktree `git status` shows only `playtest-agent-f006-B.md`, its PNG folder, this report and its folder. Leases untouched (renewal was not needed) |

## 2. Verdict for the batch-B surfaces

**ACCEPT_WITH_NOTES.** No blocker. One major (U-B1, landscape persistent layer hidden while a card, Details or the hub is open) needs an owner decision before the batch is called closed, because FR-008 says the layer is persistent and the contract's "As built" records a deviation that no owner decision covers. Everything else is minor or info.

What holds up, checked on device:

- The six-tab bar is gone. In phone portrait the whole 12-slot factory is in view at fit-all and the chrome is a thin top strip (about 5.5 % of the height) and a bottom row (about 5.5 %), so about 11 % in total at 100 % (within the SC-001 guidance of 15 %). No chrome sits over the framed world (C1). In landscape the columns take about 18 % of the width at 100 % and the world stays whole.
- The hub opens as a bottom sheet in portrait and a side panel in landscape, re-frames the factory into the free area (the whole factory stays in view in both), shows icon and one-line state per row, and shows an "Attention" pill on Overdrive when it is ready. Focus lands on the first row and returns to the Office button after a hub-opened view closes (`m-closed-l2.png`).
- Overdrive reads at a glance in all four states (Locked: lock icon and text; Ready: thick cyan outline and "Ready"; On: pink markers over the boosted machines and the hub row "On, 0:28 left"; Cooling: clock icon and "Cool m:ss"); disabled states are not conveyed by dimming alone. A hub row can start Overdrive and the hub stays open and updates (`o-od-from-hub.png`).
- Production attention chip (resource icon and count) in the top strip: tap reopens the affected machine's card, with the red marker in the world and the Details status "Paused: Missing Iron Ore (auto-sell)" plus the warning line (`q-smelter-card2.png`, `r-chip-tap.png`, see also A-2 carried item).
- Zoom buttons are absent on touch in every capture; with "Show zoom buttons" on they appear bottom left above the Office button at about 44 dp and cover no machine (`y-zoom-on-closed.png`, FR-008a met).
- R-1 is fixed with a cause line too: at 640x360 pt and 200 % the Smelter card shows Upgrade, `Details ›` and the cause stacked, nothing clipped (`s-640-card.png`; the Play-Tester could not test this).
- Remove confirmation uses the scaffold dialog with the destructive action styled apart and the focus on "Keep machine"; Back cancels it and returns focus to Remove (`u-remove-confirm.png`).
- Touch targets: Office, Overdrive, Close, rows, Upgrade, Details about 48 dp or larger in every capture (not measured pixel by pixel except the bottom row crops: Office about 117 px high at 2.625 px/dp, so 44.5 dp).

## 3. Findings

Severity: blocker = gate cannot pass; major = a MUST/SC not met or a player loses information that matters; minor = rough edge or copy; info = observation or positive. Proposed owner: a named task, "before batch C", or "later feature". "PT" is the Play-Tester's ID.

| ID | Sev | Screen / state | Expected vs observed | Evidence | Condition | Proposed owner |
|---|---|---|---|---|---|---|
| U-B1 (PT B-F1) | major (owner decision) | Phone landscape 100 % and 200 %, 640x360 at 200 %: machine card, Details or hub open | Expected: Cash, store fill, Office and Overdrive stay on screen (FR-008 "persistent layer", FR-009). Observed: the left column (Cash, Store) and the right column (Office, Overdrive, gear) both disappear. The compact card says "Upgrade: $36" with no cash figure, so a player cannot see Cash, the store or the Overdrive state while upgrading, and Overdrive and Office are gone until the card closes. Portrait keeps the top strip and bottom row over every sheet. The contract's "As built (T537)" records this ("step aside"); I find no owner decision that approves it. **Agree with the PT** | `e-land.png` vs `e-land-card.png`, `e-land-details.png`, `e-land-hub.png`; `f-land200.png` vs `f-land200-card.png`; compare portrait `d-card.png`, `b03-hub.png` | FR-008, FR-009, FR-003, OD-9b C6 | Owner decision, then a T537 follow-up (keep a one-line Cash and Overdrive chip in the card or side panel header, or keep the right column visible) before batch C |
| U-B2 (PT B-F5) | minor | Phone portrait, bottom right: Mine above Overdrive | Expected: a consequential tap (Overdrive, a 30 s boost then a 5 min cooldown) is not within easy slide distance of the long-press Mine control. Observed: gap between Mine and Overdrive is about 24 px = 9 dp at 100 % and about the same at 200 % (crop measured), between two targets of about 48 dp. No mis-tap in my taps. **Agree**, risk not defect | `h-p200-bottom-crop.png`, `m-bottom-ready-crop.png`, `b02` (see `d-card.png`) | FR-003, SC-009, OD-9b C4 | T537 follow-up (clearance or Overdrive moves aside) |
| U-B3 (PT B-F8) | minor | Office hub rows Contracts and Items while the systems are locked | Expected: the row state matches the system. Observed: Contracts row says "Nothing ready to deliver" while the Contracts view says it is locked (until Iron Plate, Gear or Iron Bar is produced; seen as the PT recorded, I did not unlock it); Items "0 owned" vs the view "Nothing here yet. Earn them from contracts." The Overdrive row ("Locked" plus the reason line) is the better pattern. **Agree** | `b03-hub.png`, `c-items.png` (Contracts view not captured with the lock text; see PT `m51-contracts.png`) | FR-020, FR-018, SC-010 | T536 copy follow-up |
| U-B4 (PT B-F9) | info (owner question) | Office hub, tap Market, press Back | Observed: one Back goes from the hub-opened Market straight to the closed factory (`b09-back-from-market.png`), not back to the hub. Expected by the specification: FR-021 back order "dialog, pinned value, card or detail, system view, then factory", and T535's recorded order "a system view opened from the hub replaces it as the one sheet, so one Back returns to the factory" (`reviews/test-rebaseline.md`). **I disagree that this is a defect: it matches FR-021 and the recorded design.** The contract's test line "hub, then system view, then Back" (§4) reads either way, and the Office button stays in the bottom row as a one-tap return to the hub (`b03` pattern). The owner may still want hub-first for muscle memory | `b09-back-from-market.png`; one Back on the hub closes it (confirmed) | FR-021 | Owner question only; nothing to fix if the recorded order stands |
| U-B4a | info | Back after a scroll gesture in Market | Observed once: the first `KEYCODE_BACK` after scrolling Market did not close it (`b06-back1.png` still shows Market); the same sequence repeated twice closed it in one press. Probably the known immersive-mode first-edge behaviour (R-6, "reveals the system bars"); not reproduced | `b06-back1.png` | FR-021 | None; real-device pass |
| U-B5 (PT B-F10, partly disagree) | minor | Research view, phone portrait 100 % | Expected: lock reasons readable. Observed: the tree is two columns wider than the screen; the right column's text is cut at the screen edge ("Output x2 on Min", "Locked: needs Miner Tuning Lv 3") with no scroll affordance. **It is not lost: a horizontal swipe pans the tree** (`k-research-clip-crop.png`, taken after the pan), but nothing hints that it can pan. At 200 % the tree stacks into one column and nothing clips (`j-p200-research.png`). So the loss is discoverability at 100 %, not the 200 % case the PT worried about | `c-research.png`, `k-research-clip-crop.png`, `j-p200-research.png` | FR-017, SC-010, OD-9b C1 | T536 follow-up (wrap into one column at portrait 100 % like 200 %, or add an edge fade / scrollbar) |
| U-B6 (PT R-2, refined) | minor | Details opens already scrolled; sheets inherit scroll | (a) Stale offset: **the T536 reset works**. After scrolling Details to its bottom and selecting another machine, the next Details opens at the same offset as a fresh one (`g-d2-first.png` vs `g-d3.png`). (b) But in phone landscape at 200 % that standard offset hides the machine name and Status line: Details opens with "Makes:" first and Upgrade focused at the bottom (focus scroll-into-view), `f-land200-details.png`, `g-d2-first.png`; Status and name are one scroll up. (c) Other system sheets also open with a leftover partial offset: Industry (title "Industry" half clipped), Prestige ("Not ready to sell yet" cut), Market at 200 % (first line cut) (`c-industry.png`, `c-prestige.png`, `j-p200-market.png`) | listed | FR-017, OD-9b C1 | T536 follow-up: scroll to top when a sheet opens or the view changes; do not focus-scroll past the header |
| U-B7 (PT R-3) | minor | Details and build card, camera framing | Landscape Details (50 % width) covers machine columns 3 and 4 and the camera does not pan (`e-land-details.png`); the hub, by contrast, re-frames the whole factory into the free area (`e-land-hub.png`). Portrait Details and the build card hide the bottom rows (`d-details-scrolled.png`, `p-build.png`: rows 3 and 4 gone) while the hub keeps all four in view (`b03-hub.png`). The selected machine stays visible in every case. **Agree**, with the contrast to the hub as the reason it looks inconsistent | listed | FR-017, OD-9b C1 | T535/T537 camera pass, or accept as pannable; owner note |
| U-B8 | minor | Scaffold, "one disabled language with a reachable reason" (B-1) | Mostly met: a disabled control always has a reachable reason (Industry: warning line above "Upgrade for 1 IP"; build list: "Unlocks at factory level 3." with a lock above "Build Assembler"; Market: "Nothing to sell" below "Sell 1"; card: "Upgrade: $36 / Need $29" in the button; hub: yellow reason line). Inconsistent in three ways: (a) the reason sits above the button in some views and below in others; (b) disabled buttons differ in width (Industry upgrade half width, Market and build full width); (c) **read-only values use button chrome**: Market "Store 0 / 1,000", "Stock", "Net rate", "Sells for", build "Cost: $78", Industry rows look like tappable buttons although they do nothing, which undercuts the single disabled/enabled language | `c-industry.png`, `p-build.png`, `b04-market.png`, `c-overdrive.png` | OD-9b scaffold, B-1, SC-010 | T536 follow-up: a distinct read-only style (no border and fill) and one reason placement |
| U-B9 | minor | Office entry attention dot and production chip | The Office dot is about 8 px = 3 dp, and the only signal that Contracts, Research, Prestige or Overdrive needs attention (`m-bottom-ready-crop.png`); the hub row's "Attention" pill is clear but needs the hub open. The production chip is an icon and a count, no word (a new player must learn "ore icon, 1"). Accessible names cannot be verified on this channel (R-7) | `m-bottom-ready-crop.png`, `n-hub-l2.png`, `r-chip-tap.png` | FR-020, FR-007 | T537 follow-up (larger dot or pill), copy decision for the chip |
| U-B10 | minor | Hub rows, reason line placement | The yellow line "Overdrive unlocks after the factory reaches level 2." / "Overdrive is already on" sits between the Overdrive and Prestige rows with no card edge, so it reads as belonging to Prestige; a tap on a locked row does nothing visible except the focus ring (`c-overdrive.png`) | `c-overdrive.png`, `o-od-from-hub.png` | OD-9b scaffold | T536 copy and layout follow-up (put the line inside the row) |
| U-B11 (PT B-F4) | minor | Overdrive label | "Cool 4:57" abbreviates "Cooling down"; at 200 % portrait the two-line label "Overdrive / Locked" touches the button's top and bottom borders (`h-p200-bottom-crop.png`). The hub row spells it out ("Cooling down, m:ss"). **Agree** | `o-bottom-on-crop.png`, `o-bottom-cooldown-crop.png`, `h-p200-bottom-crop.png` | FR-007, C1 | T536 copy |
| U-B12 | info | Chrome share at 200 % | Measured by width, not by `ShellGeometryProbe`: phone landscape 200 % about 27 % of the width (columns 13 % + 13 %); 640x360 at 200 % about 38 % (closed state; with a card open the columns step aside, so the world is not narrowed further); portrait 200 % top strip and bottom row about 11 %. Guidance is at most 20 % at 200 % (SC-001 guidance): met in portrait, exceeded in landscape width. The PT measured 34 % at 640x360; I did not recompute area | listed captures | SC-001 guidance | Parent: run `ShellGeometryProbe`; owner note |
| U-B13 | info | Persistence (outside the UI scope) | After `am force-stop` and relaunch the save came back without the factory expansion and the Smelter I had built about 4 to 5 minutes earlier ("Away for 6 min 38 s", cash from 2,390). So a purchase may not be saved until a later autosave or a pause event, and a process kill loses minutes of play. Not a UI defect and not verified in code | `w-strip.png` | not a UI condition | Parent or Play-Tester: confirm autosave cadence; later feature if intended |
| U-B14 (PT R-5, no repro) | info | Camera after a runtime display-size change | After `wm size reset` the factory sat about 13 px left of centre until the next selection (`t-p-closed.png`, not kept). `font_scale` change alone did not shift it (PT B-F7 agrees) | n/a | FR-017 | None; an emulator artefact more than a player case |
| U-B15 | info | Sheet heights | Sheets differ in height (hub tall, Items short), so the world re-frames by a different amount for each view; the offline summary and dialogs are centred overlays with the world dimmed. Consistent, but the world "jumps" when moving hub to Items | `c-items.png` vs `c-research.png` | OD-9b C3 | None |

## 4. The Play-Tester's items, one by one

| PT item | My result |
|---|---|
| B-F1 landscape columns hidden | **Confirmed, major** (U-B1), at 100 %, 200 % and 640x360 |
| B-F5 Mine vs Overdrive | **Confirmed**, 9 dp gap (U-B2), minor |
| B-F8 Contracts row copy | **Confirmed** (U-B3), minor |
| B-F9 Back skips the hub | **Behaviour confirmed, defect disputed**: it is FR-021 and the recorded T535 order (U-B4) |
| B-F10 Research clip at portrait 100 % | **Confirmed, but the tree pans horizontally** and the 200 % layout stacks; I rate it a discoverability problem (U-B5) |
| R-2 scroll offset | **Stale offset is fixed** (T536 claim holds); the open Details still hides the name and Status in landscape 200 % (U-B6) |
| R-3 landscape Details covers column 4 | **Confirmed** (U-B7) |
| B-F2, B-F3, B-F4, B-F6, B-F12, B-F13 | Consistent with what I saw; B-F6 (category-level cause "Short on Iron Ore" on the card, true cause only in Details) still present and is carried as A-5 |
| B-F7 (text-scale change drops the open card) | Not retested |

## 5. Persistent layer checked against contracts §1

| Item | Result |
|---|---|
| Contents per class | Portrait: strip (Cash, store, chip, gear) plus bottom row (Office, Overdrive), Mine above the row. Landscape: left Cash and Store column, right Office, Overdrive and gear column, Mine in the world's lower right (it overlaps a slot partially with a card open, `e-land-card.png`). Matches the contract; the hide-on-sheet behaviour in landscape is U-B1 |
| Office entry | Icon, word "Office", dot (U-B9); accessible name not checkable (R-7) |
| Overdrive states | Pass (section 2) |
| Production attention | Pass, leads to the machine; the chip is word-less (U-B9) |
| Zoom hidden on touch unless on | Pass (`y-zoom-on-closed.png`; hidden in all other captures) |
| 44/48 pt targets | Pass for the bottom row, rows and card buttons; dot is below (U-B9) |
| Focus return | Pass for Market via Office; gear gets the ring on Settings open (`c-settings.png`) |
| No chrome over the framed world | Pass in portrait and landscape closed state; the zoom buttons when enabled sit in the lower-left margin |
| Whole factory in view at fit-all | Pass in portrait, landscape and 640x360 at 200 % (closed); not in portrait with Details or build card open (U-B7) |

## 6. T536 scaffold consistency, by surface

| Surface | Seen | Notes |
|---|---|---|
| Market | yes (portrait 100 %/200 %, landscape, 640x360) | read-only value tiles look like buttons (U-B8); "Session log" lives here (export, clear, no confirm seen) |
| Contracts | row only (locked), view from the PT | U-B3 |
| Items | yes, empty state (`c-items.png`: info icon, title, one line of action copy) | good empty template |
| Research | yes | U-B5; 0 RP shown in the view header with "Earn RP from contracts." |
| Industry | yes | disabled with reason, U-B6(c) and U-B8 |
| Prestige | yes | text heavy but clear; "Missing Factory Value: 9.99M" in yellow with a lock |
| Settings | yes | one clean sheet, radio and checkbox with text state "(selected)", "Off" |
| Offline summary | yes (twice) | clear sections; Back dismisses it; no icons per line |
| Notices and confirmations | confirmation yes (`u-remove-confirm.png`); notices not seen | |
| Startup status and loading mark | **not captured**: it was shorter than my 1 to 1.5 s capture interval on this emulator | not covered |

## 7. Not covered

- 150 % text, tablet and desktop classes, pinch zoom (no `adb` pinch), keyboard or Escape or Tab order, TalkBack (R-7, not re-reported as new), rotation with an open card, startup status and loading mark, error states, the Contracts and Prestige views open with data, claim flows, a notice toast, real-device feel.
- `ShellGeometryProbe` numbers (I measured by width from captures).
- Sheet scroll offsets on portrait 150 %.

## 8. Counts

Blocker 0. Major 1 (U-B1). Minor 9 (U-B2, U-B3, U-B5, U-B6, U-B7, U-B8, U-B9, U-B10, U-B11). Info 6 (U-B4, U-B4a, U-B12, U-B13, U-B14, U-B15). 16 rows in total.

## 9. Proposal for batch C (owner decides)

**Go**, with a short fix-first list that does not need a new task type: (1) the owner decision on U-B1 (landscape layer) and its T537 follow-up; (2) the T536 follow-ups in one pass: scroll-to-top on sheet open (U-B6), read-only vs button styling and reason placement (U-B8), the Contracts/Items hub row copy (U-B3), the Research tree at portrait 100 % (U-B5). Items U-B2, U-B9 to U-B11 can ride with that pass. U-B4 is an owner question, not a fix. U-B13 should go to the parent or Play-Tester to confirm autosave cadence. A human playtest on a real phone remains open for landscape feel and the immersive Back gesture.

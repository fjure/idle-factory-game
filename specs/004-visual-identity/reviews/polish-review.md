# Polish review: T450-T452 (Feature 004, US8)

**Task**: T453 (#559). **Role**: independent UI/UX Reviewer (read-only; not the author of T450-T452). **Covers**: SC-010.
**Reviewed**: PR #578 (T450, `a9dbdd0`, `507941d`), PR #579 (T451, `7ae49da`), PR #580 (T452, `21935da`), as merged on `main` at `b9221ca`.
**Verdict: accept with two minor follow-ups (N-1, N-2 below). No blocker, no regression of FR-029 to FR-033.**

## 1. Method and evidence limits

| Channel | What | How |
|---|---|---|
| Player | Windows development player with `IDLEFACTORY_DEV` (`PlatformBuildCommands.BuildWindows64DevelopmentDevTools`, Mono, build log: `Succeeded, 0 errors, 2 warnings`), windowed 640x480 (client area 640x480 confirmed) and 390x844 (boot only) | Real mouse input by user32 `SetCursorPos`/`mouse_event`, screenshots by `CopyFromScreen` of the client area. The DevTools time scale (x60) was used once to earn cash. The existing dev save folder was moved aside for the session and restored afterwards. |
| Rig capture | `ShellRig` off-screen panel, all 9 sizes (the 8 `ShellScreens.All()` classes of Feature 003 contracts/ui-surfaces.md plus 640x480) at 100 % and 200 % text | Temporary Play Mode capture test (not committed). `panelSettings.clearColor = true` set right after rig creation (workstation pitfall), several frames settled, `ReadPixels`. Scroll positions 0, 30, 50, 70 and 100 % of the sheet. 225 raw PNGs are in `artifacts/f004/t453/rig/` (outside Git); the cited ones are copied to `polish-review/`. |

Limits: the Windows player has no text-scale setting (the shell reads the Android system font scale, `ShellTextScale`), so **200 % is evidenced by rig captures only**. Only 640x480 was walked in the player; the other classes are rig captures. Not re-run here: the T450-T452 test suites (QA evidence on each Issue); I read their diffs and tests.

## 2. FR-029 to FR-033

| Item | Verdict | Evidence |
|---|---|---|
| FR-029 (R-23) hint only when no owned machine produces the resource, decided by resource through the catalog, returns when the last producer is removed | **Accept** | Code: `OwnsProducerOf(resourceId, factory)` walks owned machine types and their recipe outputs through `session.Catalog` (read only). Player: Smelter with a Miner standing shows "Missing Iron Ore: auto-sell is selling it ..." and no hint (`player-02`); after the Miner is removed the same Smelter shows "Missing Iron Ore: needs 2, has 0." plus "Build a Miner to make Iron Ore." (`player-04`). Rig 640x480: `rig-machine-hint-640x480-100-scrolled` vs `rig-machine-nohint-640x480-100-scrolled`; 200 % at all classes shows the hint without clipping (`rig-machine-hint-1280x720-200` and the raw set). Tests: `TheHintIsDecidedByResourceNotByMachineTypeAndReturnsWhenTheProducerIsRemoved`, the multi-output content test. |
| FR-030 (R-20) focus order: Upgrade if visible and enabled, else Close, empty selection to the shell default | **Accept** | Player: after a real click on "Stop auto-selling Iron Ore" the row disappears and the focus ring sits on "Upgrade to level 2" (`player-03`), not on the Switch off toggle. Rig: same at 100 % and 200 % (log "focus after fix: machine-upgrade/Upgrade to level 2"); `rig-machine-autosell-focus-on-fix-640x480-100-scrolled` shows the ring on the fix button before, `rig-machine-autosell-after-fix-640x480-100` the ring on Upgrade after. Empty selection: Remove the machine with focus on Switch off, panel reads "There is no machine in this slot." and the ring is on Close (`rig-machine-empty-after-640x480-100`). Not shown in the player: Close/unaffordable-Upgrade fallbacks (covered by `FocusAfterAFixGoesToTheCloseControlWhenUpgradeIsNotEnabled`, `FocusAtMaxLevelGoesToTheCloseControl`). |
| FR-031 (R-02) "+$<amount> at run start" | **Accept** | Seed Capital card reads "Next level: +$500 at run start" (`rig-industry-seed-640x480-100-mid50`). The effect is an Industry upgrade; the Research tab has no money effect, so the spec's "research starting-money effect" means this copy. |
| FR-031 (R-31) idle line omitted below one displayed second | **Accept** | `rig-offline-idle-1000ms-640x480-200` keeps the line at 1 s ("Machines could work for 1 s at 80 % efficiency, but finished no cycle."); the 400 ms capture shows headline, "Away for 45 s." and "Credited: 45 s." only (raw `offline-idle-400ms-640x480-100.png`). Player: not reachable (needs a real return after absence). |
| FR-031 (R-06 b) reason line for every non-acceptable live offer, neutral icon for normal states | **Accept** | Rig: with Plate active, Gear shows "Finish or abandon the active contract first." with the neutral info icon (`rig-contracts-active-640x480-100-mid30`); a storage-too-small offer shows its text with the warning icon (`rig-contracts-capacity-640x480-100`). Player: `player-05` shows the Contracts tab; only one offer is unlocked at the start, so the second-offer reason was not reachable in the player. |
| FR-032 focus restoration on offer re-sort covered by an automated Play Mode test | **Accept** | `FocusStaysOnTheFocusedOfferCardWhenItMovesThroughTheSort` and `FocusFollowsAFocusedCardThatLeavesTheLiveOffersForTheEndOfTheList` in `ContractsPanelTests`; both assert the order change and the focused element. |
| FR-033 (F-1) no `PreventDefault()` in presentation code | **Accept** | `grep PreventDefault Assets --include=*.cs` returns nothing; the development build log has no CS0618 from `ResearchPanel.cs` (the remaining CS0618 warnings are `DevToolsOverlay` and test code). The replacement `canvas.focusController?.IgnoreEvent(evt)` is Unity's documented replacement for the focus-suppression use; no player check of the pan gesture was possible with synthetic mouse input (see section 5). |

## 3. Feature 005 findings

| Finding | Verdict | Note |
|---|---|---|
| R-20 | Closed | Leftover (a) from the F005 review (Active toggle flipped by repeated Enter) is gone: the target is Upgrade or Close. See N-3 for Upgrade side effect. |
| R-23 | Closed | The F005 leftover (type versus resource) is fixed; the multi-output test proves it. |
| R-02 | Closed | Remaining "Cash" uses are balance labels and rejection copy, both allowed by the owner rule. |
| R-31 | Closed | Threshold is 1000 ms, which equals "formats as 1 s or more". |
| R-06 | Closed | (a) card jump on re-sort needs a real-touch device check, unchanged; (b) reason line fixed. |

## 4. QA notes, judged from the player's point of view

| Note | Judgement |
|---|---|
| #556: empty-selection fallback accepts Close or the Factory tab | Player-visible result is Close focused (`rig-machine-empty-after`), which is the right control. The loose test does not matter to a player. No action. |
| #556: empty-selection test does not exercise `focusPending` | A branch only reachable when a rebuild and the emptying coincide in one frame; the player sees the same Close focus. Low risk, no action. |
| #557: idle line omitted below 1000 ms, also "less than 1 s" | Correct for the player: "could work for less than 1 s" would be noise. No action. |
| #557: four reasons covered by the table test only | The two visible reasons (active exists, capacity) are shown above; the other two (cooldown, bonus not available) use the same code path and the info icon. No action. |
| #557: stray blank line in `ContractsPanel.cs` | Cosmetic. Optional cleanup. |

## 5. New findings

| ID | Severity | Finding | Evidence | Proposed disposition |
|---|---|---|---|---|
| N-1 | minor | At 200 % text on the side-panel classes (768x1024, 1024x768, 1280x720, 1920x1080) the sheet title breaks inside a word ("Machin / e · Slot / 2", "Contra / cts") because the Overdrive control shares the header. The layout assertions do not catch a mid-word break. Not caused by T450-T452 (shell header), so outside FR-029 to FR-033. | `rig-machine-hint-1280x720-200`; the raw 768x1024, 1024x768 and 1920x1080 200 % captures show the same | Named F004/F005 layout polish task: let the title wrap at word boundaries or give the title priority over the Overdrive control. Not a blocker for US8. |
| N-2 | minor | At 640x480 the machine panel shows "Status: Paused: the machine cannot run now" at the top, but the reason ("Missing Iron Ore ...") and its fix button sit below Upgrade, Switch off and Remove, so a player must scroll to learn why. This is why FR-029 and FR-030 matter more at this size. | `rig-machine-hint-640x480-100` (reason absent from the first view) vs the `-scrolled` capture | Consider moving the guidance above the action buttons, or a one-line reason in the status line. Product/UX decision; not part of T450-T452. |
| N-3 | polish | Focus now lands on Upgrade, so repeated Enter after a fix buys further levels (it spends cash). This is what FR-030 specifies and it cannot toggle the machine; the cost is visible on the button. | `player-03` | Accept as specified. |
| N-4 | incidental | The development player log shows "Shader 'Hidden/Universal Render Pipeline/GaussianDepthOfField' is not supported or has been stripped" (22 `LoadShader` entries) at start, from the T411 URP switch. The UI rendered correctly in all captures. | `Player.log` of this run | Not T450-T452; hand to the T411/F004 owner for the stripping check. |

## 6. Not verifiable here

- 200 % text in a real player (no Windows text-scale input). Needed: Android device or emulator with system font scale 2.0, machine panel with a blocked Smelter.
- Screen classes other than 640x480 in the player (only the 390x844 boot screen was captured).
- Second-offer reason line ("Finish or abandon ...") and the warning-icon capacity case in the player: need Plate unlocked (factory level 2).
- Welcome-back idle line in the player: needs a real absence.
- Real-touch behavior (R-06 card jump, pan gesture after the `IgnoreEvent` change in the Research panel), screen reader, notch (F005 section 5 checks C-2, C-5, C-6, C-8, C-10, C-11 are unchanged by T450-T452 and remain device checks).

## 7. Evidence index (`specs/004-visual-identity/reviews/polish-review/`)

`player-01..06` real Windows player captures (640x480; 06 is 390x844 boot). `rig-*` captures are labelled "rig capture": off-screen `ShellRig` with `clearColor = true`, not a live player. The raw set lives in `artifacts/f004/t453/` and is not committed.

## Owner disposition (2026-10-28)

- N-1 and N-2: fixed in Feature 004 by the new layout polish task T458, with an independent UI/UX review recorded here.
- N-4: fixed in Feature 004 by the new render-foundation test-hardening task T457 (item f), so the console gate stays clean.

## N-1/N-2 re-review (T458, 2026-10-29)

Recorded by the parent from the reviewer's returned text. The reviewer's new observations were numbered N-3 and N-4; they are renumbered **N-5** and **N-6** here because N-3 and N-4 already exist in section 5. Verdicts, numbers and capture names are kept as given.

**Reviewer**: independent UI/UX Reviewer (Game Designer/Product Owner lens for player-visible meaning), separate session, model Sonnet 5.5, not the author of T458. **Reviewed**: Issue #594, PR #598, commit `5e1ff3304ba1e3d9c84d58c63095d5b2e3e4d972`.
**Authority**: N-1/N-2 above, spec.md US8, FR-029 to FR-033, SC-010, the owner's N-2 decision of 2026-10-28 (one-line reason in the status line; blockers and fix control stay below the actions), and the Feature 003 T249 stable-layout rule.

### Method and limits

- Same method as section 1: a temporary Play Mode capture test on the `ShellRig` off-screen panel with `clearColor = true` and an opaque colour set right after rig creation, run once through `scripts/test-unity.ps1 -TestFile ... -TimeoutSeconds 3000` (2/2). The pass was not counted as evidence by itself; the captures and the geometry log were read. The temporary test was deleted and incidental settings changes reverted.
- Sizes: the 8 `ShellScreens.All()` classes plus 640x480, each at 100 % and 200 % text.
- N-1: Contracts, Items, Market, Prestige and the machine sheet at each size, Overdrive at "Cool 4:59"; logged title text, title content width, widest word (`MeasureTextSize`) and the Overdrive rect against the header rect.
- N-2: the machine sheet at scroll 0 and at scroll bottom in states A (Smelter blocked, no Miner), B (running after a Miner is built), C (auto-sell selling the Ore), D (switched off), E (Miner storage-full attempt), F (slot emptied); logged status text and rect, viewport rect and the Upgrade, Switch off and Remove rects.
- Limits: rig captures only (the Windows player has no text-scale setting); no real touch input; accessible names and focus order judged from the diff, not captured; state E (storage full) not reached in 600 simulated seconds, so "Paused: Storage is full" is judged from code only; the status line is one fixed three-line reserve (T249).

### Verdicts

| Item | Verdict | Evidence |
|---|---|---|
| N-1: no mid-word break at 200 % on 768x1024, 1024x768, 1280x720, 1920x1080 | **Accept** | `n1-log.txt`: all 5 sheets at all 4 classes `midWordBreak=False`; "Contracts" 158 pt in a 347 pt title, "Machine · Slot 2" widest word 137 pt in 347 pt. `n1-768x1024-200-Contracts`, `n1-1280x720-200-machine` and the same-named sheets per class. |
| N-1: Overdrive reachable, inside the header, touch target unchanged | **Accept** | `odInHeader=True` in all 20 side-panel rows; Overdrive wraps to a second header row at 246 x 44 pt, its single-row size; title one line, Close unaffected. Cost: at 200 % the header is two rows (about 92 pt against 49 pt), about 43 pt less first view. Acceptable. |
| N-1: phones (single-row header) not regressed | **Accept-with-note** | Phone CSS equals the pre-T458 base; 360x640, 390x844 and 844x390 show no break at 100 % or 200 %; 640x480 breaks only between words (title 173 pt, widest word 158 pt). See N-6. |
| N-2: reason readable in the first view at 640x480, 100 % | **Accept** | `n2-640x480-100-A-blocked-top`: "Status: Paused: Missing Iron Ore" at y 126 to 158, viewport 98 to 423, Upgrade and Switch off also in view. |
| N-2: reason readable at 640x480, 200 % | **Accept** | `n2-640x480-200-A-blocked-top`: viewport ends at y 391; the status row (reserved 128 pt, y 212 to 340) ends 51 pt above it, the reason fills two lines at its top. The author's 2.5 pt overrun is not reproduced; even that reading would cut only the blank third reserved line. |
| N-2: wording clear and consistent with the blocker text below | **Accept** | "Paused: Missing Iron Ore"; auto-sell "Paused: Missing Iron Ore (auto-sell)" with its "Stop auto-selling" fix below; switched off "Status: Switched off". "Paused: Storage is full" code-reviewed only. |
| N-2: Upgrade, Switch off and Remove do not move when a blocker appears or clears (T249) | **Accept** | In all 18 size/percent combinations the three buttons' y is identical in A, B, C and D (640x480 @200 %: 749, 856, 931). Blockers sit below Remove again. |
| N-2: accessible names and focus order | **Accept** (judged from the diff) | UXML order restored to the T249 order; no label, name or focusable changed; the T450 focus tests cover FR-030. |
| Other classes and states (empty, disabled, error) | **Accept-with-note** | F (slot emptied) shows the empty-slot text at all sizes (`n2-640x480-100-F-empty`); C and D consistent, no overlap. N-5 and N-6 do not block this issue. |

### New observations

| ID | Severity | Observation | Evidence | Proposed disposition |
|---|---|---|---|---|
| N-5 | minor, not caused by T458 | At phone landscape 640x360 and 200 %, the machine sheet's status row begins at y 248 and the viewport ends at y 255, so the reason is below the fold (the first view shows the heading only). The same position applied before T458 to the old text. | `n2-640x360-200-A-blocked-top` | Outside the owner's N-2 acceptance (640x480). Candidate for a later layout pass for this class. |
| N-6 | minor, pre-existing, same class as N-1 | At phone landscape 640x360 and 200 %, "Contracts" breaks as "Contrac / ts": title 138 pt, word 158 pt, because Close shares the one-row header (Overdrive is not in the header on phones). The only break in the 5 phone classes. | `n1-640x360-200-Contracts`, `n1-log.txt` | Candidate follow-up: let the title wrap or shrink the Close label on this size. |

### Overall verdict

**Accept.** N-1: no mid-word break on the four side-panel classes at 200 %, Overdrive inside the header at an unchanged touch size. N-2: the pause reason is in the status line in the first view at 640x480 at 100 % and 200 %, consistent with the blocker text below. T249 stability holds in every size and state. No regression on the phone classes; N-5 and N-6 are small pre-existing items on phone landscape 640x360, not scoped by the issue, and do not block T458.

Captures (not committed): archived at `artifacts/f004/t458-review/` (copied from the worktree's `test-results/t458-review/`), 216 PNGs (`n1-<size>-<100|200>-<sheet>.png`, 90; `n2-<size>-<100|200>-<state>.png`, 126) plus `n1-log.txt`, `n2-log.txt`, `run.log`.

## N-5/N-6 review (T459, 2026-10-29)

Recorded by the parent from the reviewer's returned text, with the prose shortened; verdicts, numbers and capture names are kept as given.

**Reviewer**: independent UI/UX Reviewer (Game Designer/Product Owner lens), separate session, model Sonnet 5.5, not the author of T459. **Reviewed**: Issue #601, PR #604, commit `4b63cf760cb9a8d5aca581c1e592a09deef6b05b`.
**Authority**: N-5 and N-6 above, spec.md US8, FR-029 to FR-033, SC-010, the owner's N-2 decision of 2026-10-28, the Feature 003 T249 stable-layout rule, and the owner's N-5 decision of 2026-10-29: **relaxed rule**, the status row and the reason begin in the first view and at least 2 lines are visible, the third may scroll; no sheet-width, prefix or wording change.

### Method and limits

- Viewed the two new captures `blocked-machine-640x360-200.png` and `contracts-640x360-200.png`; the T458 before-captures were not viewed.
- Read the diff `fa2a19e..4b63cf7` (USS block, the `CloseButton.tooltip = "Close"` line, the two Play Mode tests). Other classes were judged from the selectors and code only: every new rule is scoped to `.run-text-scale--150` to `--200` with `.factory-hud--compact-landscape`, so 100 to 140 % and every other class are unchanged.
- No tests, Unity, real touch or screen reader. The author's measured numbers (status y 189, line 32 pt, viewport end 255, 66 pt visible, 3 lines) were not re-measured; the capture is consistent with them.

### Verdicts

| Criterion | Verdict | Evidence |
|---|---|---|
| N-5: status row and reason begin in the first view, at least 2 lines visible (owner rule, relaxed) | **Accept-with-note** | Heading "Smelter - level 1 of 100" at body size on 2 lines; "Status: Paused:" and "Missing Iron" visible at the bottom edge, the third line ("Ore") cut off. Status row y 248 → 189; 66 pt visible against 2 × 32 = 64 pt. The test asserts `status.yMin >= viewport.yMin` and `viewport.yMax - status.yMin >= 2 × measured line height`. Note: about 2 pt margin; the reason's key word needs a scroll at 200 %; the first view stays dense. |
| N-5: no sheet-width, prefix or wording change | **Accept** | CSS touches only the Close caption and margin, header and content padding, `.mpanel__name` size and margin, `.mpanel__text` margin. Sheet stays 40 %; status text unchanged ("Status: Paused: Missing Iron Ore"). |
| N-6: no mid-word break of "Contracts" | **Accept** | `contracts-640x360-200.png`: "Contracts" on one line, Close to its right. |
| N-6: Close icon-only, still 44×44 pt with an accessible name | **Accept-with-note** | Caption hidden at 150 to 200 % compact landscape only; size kept (author-measured 44×44). Tooltip as accessible name follows the project convention (F9; Contracts, Research, Cash, Store and Overdrive use `.tooltip` the same way). On touch there is no hover, so discoverability rests on the icon, a chevron-down in a box (reads as collapse/dismiss, not a conventional X), reinforced by the tab bar below. Whether UI Toolkit exposes `.tooltip` to a screen reader on Android is unverified, as for the other tooltip-named controls. |
| T249: actions do not move when a blocker appears or clears | **Accept** | The new test records Upgrade, Switch off and Remove y while blocked, builds a Miner, waits for Running, asserts each y unchanged within 0.5 pt, at every tested percent. The CSS does not touch actions, blockers or fix controls. |
| Consistency, 150 to 200 % phone landscape vs 100 to 140 % | **Accept-with-note** | Step change at a text-scale threshold in this one class (body-size name, tighter header, icon-only Close); other classes unchanged. See N-7. |
| Empty, disabled and error states | **Accept** (from code) | Only padding and margins change; empty-slot text, "Switched off" and auto-sell statuses use the same elements. The test covers the blocked-to-running transition; "Paused: Storage is full" remains code-reviewed only, as in T458. |

### New observations

| ID | Severity | Observation | Evidence | Proposed disposition |
|---|---|---|---|---|
| N-7 | minor | Phone landscape at 150 to 200 % looks different from 100 to 140 % (smaller machine name, icon-only Close); a player who raises the text scale loses the word "Close". | Selectors `--150` to `--200` with `compact-landscape`; capture shows the icon only. | Accept as is, as an intentional class-specific exception of the owner's 2026-10-29 decision. |
| N-8 | minor | At 200 % the sheet title "Machine · Slot 2" still wraps to 2 lines (between words), leaving the header about 75 pt tall, which is why the status row sits near the viewport end. | `blocked-machine-640x360-200.png` | Accept as is; shortening the title (for example "Slot 2") would be a wording change, which the owner ruled out for N-5. |

### Overall verdict

**Accept-with-notes**, no blocker for T459. N-5 is accepted only under the owner's relaxed rule of 2026-10-29 (status row y 248 → 189, 66 pt visible, 2 of 3 lines in the first view at 640x360 @200 %); the original "reason fully readable" intent is not met at 200 %. N-6: no mid-word break; Close icon-only at 44×44 pt with tooltip "Close" (chevron icon; tooltip as accessible name per the F9 convention).

Captures (not committed): the worktree's `test-results/t459/` (`blocked-machine-640x360-200.png`, `contracts-640x360-200.png`), archived by the parent at `artifacts/f004/t459-review/`.

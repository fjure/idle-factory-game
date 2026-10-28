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

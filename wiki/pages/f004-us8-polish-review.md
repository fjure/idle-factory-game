---
title: F004 US8 polish re-review
type: reference
sources: [S044, S047, S049]
updated: 2026-10-29
---

# F004 US8 polish re-review

T453 (#559, PR #585) is the independent UI/UX re-review of T450-T452. Verdict: accept T450-T452; no blocker and no regression of FR-029 to FR-033. (S044)

| ID | Severity | Finding |
|---|---|---|
| N-1 | minor | At 200 % text on the side-panel classes the sheet title breaks mid-word because the Overdrive control shares the header; the layout assertions do not catch it. Outside FR-029 to FR-033. |
| N-2 | minor | At 640x480 the pause reason and its fix button sit below the action buttons, so a player must scroll to learn why. Product/UX decision. |
| N-3 | polish | Focus lands on Upgrade, so repeated Enter buys levels; accepted as specified (FR-030). |
| N-4 | incidental | The dev Windows player log shows the URP GaussianDepthOfField "stripped" error 22 times; UI rendered correctly. Hand to the T411 owner. |

N-1 and N-2 await a named layout polish task. (S044)

## Method learnings

- The Windows player has no text-scale input, so 200 % evidence comes only from the rig or a device. (S044)
- A dev player can be driven with user32 `mouse_event` plus `CopyFromScreen`. (S044)

## Dispositions and re-review (batch 5)

- N-4 was fixed by T457 (#593, PR #597): see [F004 URP phase 0 results](./f004-urp-phase0-results.md). (S047)
- N-1 and N-2 were fixed by T458 (#594, PR #598). Owner N-2 decision: the reason sits in the status line ("Status: Paused: Missing Iron Ore"), blockers stay below the actions, so the Feature 003 T249 stable-layout rule holds. N-1: on side-panel classes the header wraps so the title keeps its width and never breaks mid-word at 200 % text. (S044, S047)
- Independent UI/UX re-review (not the author, rig captures only): **Accept**. New findings N-5 (640x360 at 200 %: status row below the fold) and N-6 (640x360 at 200 %: "Contracts" breaks mid-word because Close shares the header); both minor, pre-existing, not blocking. (S044)

## T459: N-5/N-6 at phone landscape 640x360 (batch 6, #601, PR #604)

- Owner decisions 2026-10-29: a new F004 task; N-5 criterion relaxed to "status row and reason begin in the first view, at least 2 lines visible". (S049)
- USS-only rules scoped to `.run-text-scale--150` to `--200` with `.factory-hud--compact-landscape`: Close icon-only (44 pt, tooltip "Close"), machine name at body size, tighter vertical spacing. Status row y 248 to 189; 66 pt visible against 64 needed, so the margin is tight (about 2 pt). (S044, S049)
- Independent UI/UX review: Accept-with-notes. N-7: that class looks different from 100-140 % text (accepted as an intentional exception); N-8: the 2-line title "Machine - Slot 2" leaves the header about 75 pt tall (shortening would be a wording change the owner ruled out). (S044)
- A full Play Mode run found a regression the targeted tests missed; see [batch 6 pitfalls](./f004-batch-6-workflow-pitfalls.md). OPS-4 and OPS-7 (T412 carry-overs) moved into T444 by owner decision. (S049)

Related: [run panel conventions](./run-panel-conventions.md), [F004 URP phase 0 results](./f004-urp-phase0-results.md).

---
title: F004 US8 polish re-review
type: reference
sources: [S044, S047]
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

Related: [run panel conventions](./run-panel-conventions.md), [F004 URP phase 0 results](./f004-urp-phase0-results.md).

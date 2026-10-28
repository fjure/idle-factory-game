---
title: F004 US8 polish re-review
type: reference
sources: [S044]
updated: 2026-10-28
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

Related: [run panel conventions](./run-panel-conventions.md), [F004 URP phase 0 results](./f004-urp-phase0-results.md).

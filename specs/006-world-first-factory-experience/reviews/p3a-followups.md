# F006 P3 batch A follow-ups (parent record)

**Batch**: P3 batch A (T532, T533, T534) and the gate fixes T552 and T553; the T543 batch-A gate. Also merged in this batch: T525, T526, T554.
**Recorded by**: the parent orchestrator, 2026-11-13, from the independent reviews, QA reports and the T543 gate reports ([playtest-agent-f006-A.md](playtest-agent-f006-A.md), [uiux-f006-A.md](uiux-f006-A.md), [uiux-f006-A-rerun.md](uiux-f006-A-rerun.md)).

## Owner decisions (2026-11-13)

| Decision | Record |
|---|---|
| P2-F9: a new "Office" glyph | Done as T554 (#752); see [p2-followups.md](p2-followups.md) P2-F22. |
| P2-F7(b): front-only wall as "Walled yard", conditional | P2-F23; proofs in T527. |
| T543 batch-A gate REJECT: fix first | T552 (#750, Android load hang) and T553 (#751, empty tap closes Details) added and merged; the touch checkpoint re-ran ACCEPT_WITH_NOTES. |
| Start batch B after the re-run | Given after reading the re-run summary. |
| R-7: no Android accessibility nodes (TalkBack announces nothing), pre-existing in the whole game | **Later feature**, not F006. Recorded as a known gap; propose a dedicated accessibility feature. |

## Carried items

| Id | Source | Follow-up | Carried by |
|---|---|---|---|
| A-1 | Re-run R-1 (major, 640×360 at 200 % only) | The card's `Details ›` is clipped to a sliver at the right edge when a cause line shows. | T535 |
| A-2 | U-A5 / A-F4 | `Details ›` moves with the cause line and the shortfall state. | T535 |
| A-3 | U-A8 / A-F6 | Details of a switched-off machine shows two "Switch on" buttons and a stale blocker. | T535 |
| A-4 | U-A6 / A-F3 | The shortfall shows twice (button and inline line). | T535 or T537 |
| A-5 | U-A4 / A-F2 (owner copy) | The card cause line is category-level ("Short on Iron Ore") and hides auto-sell; SC-003 wants the specific cause in one tap. | T537 (copy), T528 (model) |
| A-6 | Re-run R-2, R-3, R-4, R-5 | Details keeps the previous scroll offset; landscape Details covers the fourth column; bottom tab labels clip at 200 % and Mine overlays a column; a camera shift after a text-scale change with Details open. | T536, T537 |
| A-7 | Re-run R-11, A-F8 | "Show zoom buttons" does not hide the buttons on touch (FR-008a). | T537 |
| A-8 | A-F7 | A missing `display-v2.json` logs `preferences.display.invalid` on first launch. | T537 (T538 owner code) |
| A-9 | Re-run R-8 | The Android development APK is 82 MB with entries of 46 MB (dead zip data); `adb install` fails on the near-full emulator. Check the build script against the F004 APK budget (47.3 MB). | T541 (DevOps) |
| A-10 | T552 review | Budget 8 (SetPass) re-measure: the F004 `machines` atlas now has two pages. `ValidationRejectsAnOversizedPageOverride` asserts constants only. | T531, T544; test fix in any later Tech Art task |
| A-11 | T534 Architect | Move `IMachineEffects`/`MachineResponseKind` to Presentation when T529 wires `EffectPool`; ResetBaseline on card show; split `RunShellDocument.cs` (about 3,240 lines). | T529, T528, T535/T537 |
| A-12 | T534 UI/UX | No C2 leader/pointer from card to machine on landscape and tablet; Close icon has no accessible name beyond the tooltip (see R-7). | T535/T537; later feature |
| A-13 | T526 | Register the F006 catalog rows in the scene catalog and flip placed rows to required; remove the F004 floor atlas; C8 proof with the K4 tail over a trunk; luminance in `cue_over_lane_contrast`. | T527 |

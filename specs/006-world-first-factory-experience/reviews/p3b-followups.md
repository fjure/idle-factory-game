# F006 P3 batch B follow-ups (parent record)

**Batch**: P3 batch B remainder (T536, T537) after T535 and T538, plus the T543 batch-B gate.
**Recorded by**: the parent orchestrator, 2026-11-17, from the independent QA reports and the Software Architect, UI/UX, Product Owner and Art Director reviews of T536 (#708, PR #758) and T537 (#710, PR #759).

## Merged

| Task | Issue | PR | Commit | QA | Reviews |
|---|---|---|---|---|---|
| T536 | #708 | #758 | `d726309` | PASS: Edit Mode 2874/2874, Play Mode 3061/3061 | Architect APPROVE_WITH_NOTES; UI/UX and PO ACCEPT_WITH_NOTES; Art Director ACCEPT |
| T537 | #710 | #759 | `7042a7c` (fix round on `5fea213`) | PASS: Edit Mode 2877/2877, Play Mode 3149/3149 | Architect APPROVE_WITH_NOTES; UI/UX, PO and Art Director ACCEPT_WITH_NOTES |

T536 merged without a fix round. Its two review majors were not regressions and sat in files T537 rewrote, so they went into T537 as carried items 1 and 2, and both were closed there. The T537 fix round restored the OD-9b C1 assertion: the whole factory is in view at fit-all on every class. The first commit had relaxed that assertion for 640×360 at 200 %, and a parent cannot waive a binding owner condition. On compact landscape the zoom column now folds into the right action column, so the world share at 640×360 and 200 % rose from 27.7 % to 36.0 % (chrome 62.0 % to 55.8 %).

## Carried items

| Id | Source | Follow-up | Carried by |
|---|---|---|---|
| B-1 | T536 UI/UX F5, T537 item 5 | Audit the ~26 `SetSoftDisabled` call sites: every hosted disabled control needs a reason a touch user can reach (P2-F19). | T542 |
| B-2 | T535 Architect F6, T537 item 6 | First reframe before the entry layout. | T528 |
| B-3 | T534 Architect F8, T537 item 6 | Presenter-level integration tests. | T542 |
| B-4 | A-5 (copy), T537 item 7 | The card cause line is category-level ("Short on Iron Ore") and hides auto-sell. T528 supplies the specific cause in the model; the copy follows. | T528, then T547 |
| B-5 | A-6, T537 item 8 | Camera shift after a text-scale change with Details open; at portrait 200 %, overlays draw over the card edge. Needs a named acceptance check. | T543-B gate watch, then T528 or T542 |
| B-6 | A-12, T537 item 11 | No C2 leader from the card to its machine on landscape and tablet. The Close icon has no accessible name beyond its tooltip. | Later task (C2); T547 (name) |
| B-7 | T535 UI/UX U-2, U-4 to U-7; T534 UX F3 | Not checked in T537. | T542 or T547 |
| B-8 | T537 UI/UX 1, QA 4 | Phone landscape hides both persistent columns while a sheet or the machine card is open, so Cash, Store, attention, Office and Overdrive are not visible then. This is documented in contracts §1 "As built". | T543-B gate watch; owner |
| B-9 | T537 UI/UX 2, baseline.md | SC-010: persistent labels 13 to 10 (77 %, guidance 50 %). Phone-landscape chrome stays high (640×360 at 200 %: 55.8 %). | T543-B gate; owner |
| B-10 | T537 UI/UX 4 | Portrait: the ore-vein Mine overlay sits directly above Overdrive, a mis-tap risk. | T543-B gate watch |
| B-11 | T537 PO 8 | "Overdrive is not unlocked yet" and "Locked: not unlocked yet." give no cause. Name the condition, or show "Locked" alone. | T547 |
| B-12 | T536 UI/UX 2 | In-flow notices shift the body when a notice arrives or is dismissed. | T547 playthrough |
| B-13 | T536 Architect F5, T537 Architect 8 | USS cascade: `PanelScaffold.uss` is imported twice, and later FactoryRun rules can undercut the 48 pt floor. `ScaffoldScope` re-queries buttons on every geometry change. | T542 (performance and cleanup) |
| B-14 | T537 Architect 3 | `ShellTab`, `SelectTab` and `OpenTabAtSection` stay public as a shim, and the six-tab `TabNames` remain. Retire them when no caller is left. | T542 |
| B-15 | T537 QA (fix round) | `ScreenClassScalePlayTests.The1280x720PhoneAt320Dpi` no longer proves that the 1 % n_min tolerance is load-bearing (the replaced line duplicates line 195). The landscape zoom-hint assertion uses a loose `.Or`. | T542 |
| B-16 | T537 Architect 4 | Layout suites run with the zoom preference on, the worst case. The default touch player is covered only by the new landscape fit test. | T542 |
| B-17 | Office glyph | `f006-ui-office` is still not imported. The hub entry uses the contracts glyph behind `HubCards.EntryIconKey`. | T527 (with A-13) |

The open items from `p2-followups.md` and `p3a-followups.md` (A-9, A-10, A-11 remainder, A-13) remain carried as recorded there.

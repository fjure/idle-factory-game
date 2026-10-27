---
title: F003 UI/UX final review
type: decision
sources: [S018, S019]
updated: 2026-10-09
---

# F003 UI/UX final review

T128 (`reviews/ui-ux-review.md`, "Final review") on `872984a`: **pass with findings**, no blocker. F-1 to F-8 and F-10 of the shell gate are resolved; F-9 is still open. The reviewer re-ran nine Play Mode files green (HUD layout 178, text scale 150, MachinePanel 89, and others) but not Edit Mode, other panels or the player suite. Evidence is emulator or synthetic window sizes only, with no physical device. (S018)

## Findings worth remembering

- **N-1, N-3**: the camera stays off-centre after a sheet closes on a portrait phone, and the Research graph hint says "sideways" although a plain wheel scrolls vertically. (S018)
- **N-2**: a lone second warning icon under "Not enough Research points." (S018)
- **N-10**: a short lifecycle return with an expired effect or contract has no end-to-end test, only the pure `ShouldOpenSummary` check. (S018)
- **N-11**: tab labels are about 8 px in small desktop windows; a touch phone gets the 7 mm raise instead. (S018)
- **Verified**: T248 drag-pan and wheel (Shift+wheel only by tests), and T250 minimizing for 40 s kept the run live with no summary. A swipe near the Android back gesture is taken by the system; no exclusion region warranted. (S018)

## Open owner decisions (not decided)

| ID | Question |
|---|---|
| F-9 | World slots fall below 44 pt at fit-all (29.8 pt) and zoom minimum (32 pt). FR-032 and SC-007 need a written exemption or a higher zoom minimum; Product leaned to the exemption because the panel repeats state in 44 pt controls |
| N-5 | A cold start after a short unclean exit (18 to 56 s) opens the modal "Welcome back" summary, because the 60 s threshold covers lifecycle returns only; record "restarts always show it" or change it |
| N-8, N-9 | Reserved blank rows and an always-visible scroll track (judged acceptable), and the blocker section below the fold at 200 % text on small screens; a "Waiting: see below" pointer is recommended |

N-6 (about 6 pt pin glyph) and N-7 are cosmetic. Most items defer to Feature 004. (S018)

Related: [run panels](./run-panels.md), [run panel conventions](./run-panel-conventions.md), [text scale and device profile](./run-shell-device-profile-and-text-scale.md), [feature review](./f003-feature-review.md).

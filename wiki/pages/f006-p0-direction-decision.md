---
title: F006 P0 direction decision (OD-9a, OD-9b)
type: decision
sources: [S057]
updated: 2026-11-11
---

# F006 P0 direction decision

## World Bible freeze (OD-9a, T504)

`F006-WB v0.1` (`docs/world/world-bible-v0.1.md`, #679 at 72f0ecf) was frozen by the owner on 2026-11-11 with the copy inventory `reviews/world-copy.md`. Player text says "location"; "plate" and "bay" were dropped as names for the place a machine stands (the item "plates" stays). Hub label candidates were H-01 Works, H-02 Office and H-03 Operations; "Systems" and a bare "Menu" are forbidden. Site name "Meridian Works"; stages Open deck, Walled yard, Gantry line. (S057)

## Direction (OD-9b, T509)

Two briefs and scripted concept sprites (#722) were composed into mockup sheets (#724) and reviewed independently by UI/UX, Art Director and World Designer (#725). All three recommended **A**, the open platform deck with a railing edge. Rejected: **B**, the walled hall. The Art Director found its wall block behind the selected machine and the Faulted badge (blocking), `steel_*` tones outside the C5 backdrop set, a wall already in stage 1 that left stage 2 nothing to add, and a teal drum prop in the hue family of the running pip. (S057)

Owner decision (2026-11-11):

- **Direction A** with B's wall as the **stage-2 candidate**, placed only on the outer margin and accepted only after a C8 ring proof. The gantry is new P2 art. B's teal drum and closed bunker depot are not adopted. (S057)
- **Hub label H-02 "Office"** (the UI/UX pick). The World Designer preferred Works and noted the people/corporate reading; the owner accepted it because the word is allowed by WB v0.1 section 4. No World Bible version change. (S057)
- **Binding conditions** for P1 and P2: UI/UX C1 camera rule, C2 compact card, C3 hub exclusive with the card, C4 touch targets of at least 48 dp, C5 reduced-motion fallbacks, C6 header; Art Director conditions 1-9; World Designer: trunks end at the depot, the vein is legible or recorded as out of view, stage-2 and stage-3 art exist before progression is accepted. (S057)

## Layout findings that feed P1

- The 640x360 class at 200 % text cannot hold a docked card beside the machine; it needs a compact pattern (bottom sheet or narrow card, one-row header). There the header and strip take about 35 % of the height and the selected machine is only 63 % visible. (S057)
- Phone-portrait and tablet cameras leave a dead band (about 350 px above the lattice), and the tablet clips the first machine column behind the zoom stack. The camera rule must centre the world in the area the chrome leaves free and never clip a machine. (S057)

See [plan overview](./f006-plan-overview.md), [world presentation design](./f006-world-presentation-design.md), [baseline](./f006-p0-baseline.md), [P0 mockup tooling](./f006-p0-art-tooling.md).

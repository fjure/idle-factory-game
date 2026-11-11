---
title: F005 batch C UI and DevTools fixes
type: component
sources: [S022, S033]
updated: 2026-11-06
---

# F005 batch C UI and DevTools fixes

Defects the batch-B Play-Tester found, fixed in T365 (#489) and T366 (#490); the batch-C report confirms both for the cases it saw. (S022, S033)

## Presenter signature sentinel (T365)
- Cause: the machine panel skipped re-rendering when its signature matched the last one, and the empty string stood for "no blockers", so a blocker-free machine collided with the sentinel and kept the previous slot's guidance and fix buttons. Fix: an explicit "never rendered" sentinel distinct from an empty signature; empty slots clear guidance; running machines show none. 48 Play Mode cases, written red first. (S022)
- Lesson: never let a valid value (the empty string) double as the "unset" marker of a change-detection cache. (S022)
- Residual (C-F4, T352): a correct blocked reason can sit beside a generic hint such as "Build a Miner to make Iron Ore" with four Miners standing. (S033)

## Pointer input across UIDocuments (T366)
- A hit test written for one panel cannot see another UIDocument, so the DevTools overlay let clicks reach the slot beneath. Fix: a generic `RuntimePanelHitTest` that checks all active screen UIDocuments with no DevTools reference; the overlay marker and rows consume presses and hold placement while hovered. Verified with real input in a Windows development player, which batch mode cannot give. (S022)
- Session-log `header` records are written only when the selected time-scale step changes; the effective factor jitters in Max (308 of 706 lines in batch B). (S022)
- F-B5 stays a recipe note: after acceleration the timeline runs ahead of the device clock, so test offline return on a never-accelerated save. (S022)

Related: [batch B UI](./f005-batch-b-ui-and-android.md), [session log writer](./f005-session-log-writer.md), [presentation boundary](./unity-presentation-boundary.md).

---
title: F006 batch 5 shell, build card, stages and camera framing
type: component
sources: [S061]
updated: 2026-11-19
---

# F006 batch 5: persistent layer, build card, stages and framing (T536, T537, T555, T539, T540, T558)

Merged PRs #758, #759, #761, #769, #770, #773 (QA PASS on each; Edit Mode 2874 to 3127, Play Mode 3061 to 3439 over the batch). Contracts as built: `contracts/ui-surfaces-f006.md`. (S061)

## Persistent layer (T536, T537, T555)

- Panel scaffold consistency (T536) and the persistent layer (T537) removed the tab bar, the RP cell and tab badges. Phone portrait: strip on top plus a bottom action row (Office with attention dot, Overdrive). Phone landscape: strip column left, action column right. Tablet and desktop: strip above the world. `ShellTab` stays an internal shim of `SelectTab`. (S061)
- T537's first commit relaxed the OD-9b C1 assertion (whole factory in view at fit-all) for 640x360 at 200 %; a parent cannot waive a binding owner condition, so the fix round restored it by folding the zoom column into the action column (world share 27.7 % to 36.0 %). (S061)
- **Owner decision 2026-11-17 (U-B1)**: keep Cash and Overdrive visible in landscape. T555 turns the strip into a one-row status band at the top of the world column while a card, Details or sheet is open; the camera rectangle starts below it. (S061)

## Build card (T539)

Docked like the machine card; one card frame, so opening one dismisses the other. Rows and buttons are at least 48 px; selection uses a border, never colour alone. Confirm passes the shown price, so a price change rejects without charging. Success plays `BuildDust` and hands over to the machine card. (S061)

> The first T539 commit left the card inert in the shipped scene: the `buildCardTree` and styles SerializeFields were unwired and the worker had been told not to touch scenes. See [batch 5 pitfalls](./f006-batch-5-workflow-pitfalls.md). (S061)

## Development stages and milestones (T540)

- **Owner decision 2026-11-18, stage offset u0**: `stage = t <= u0 ? Early : clamp(floor(3*(u-u0)/(t-u0)), 0, 2)`, u0 being the locations at factory level 1 (research R15). The original `floor(3u/t)` made Early unreachable on Iron (4 of 12 open at level 1). Iron levels 1-5 now give Early, Early, Partial, Established, Established. Prestige resets the stage. (S061)
- Dressing: the margin ring is lit by stage, props go only on lit ring cells and empty or locked cells, adjacent stages differ by at least 30 points of lit deck. `MilestoneWatcher` diffs snapshots and is re-baselined on cold start, restore and non-prestige replace; offline milestones appear only in the offline summary. (S061)
- The milestone banner is an absolute overlay that never reframes the camera (a banner inside the unobstructed rect did, and covered the top machine row). At compact landscape or 150 % text it shows its main line only (T558: 20.0 % of height at 640x360 and 200 %). (S061)

## Camera framing (T558; owner decisions 2026-11-18 and 11-19)

Gate C found the closed-state camera clipping the depot at level 2 in portrait and tablet (U-C1) and a stale view after rotation under a card (C-F2). `FitBounds` now includes the depot column; `RunViewportDriver` re-fits when a card closes on another screen or after a fit ran while it was open. Where the depot cannot fit at `n_min` it is reached by panning (AR-4); phone portrait 1080x2400 fit-all sits at `n_min` 3 (AR-3). Both are recorded in Art Bible v1.1.1 sections 4.1 and 4.2. See [pixel camera scale](./pixel-camera-scale.md). (S061)

Related: [world and effects](./f006-batch-5-world-and-effects.md), [gates and owner decisions](./f006-batch-5-gate-and-owner-decisions.md), [batch 4 card and hub](./f006-batch-p3a-card-hub-and-gate-fixes.md).

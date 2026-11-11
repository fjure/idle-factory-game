---
title: F006 P0 baseline of the current shell
type: component
sources: [S057]
updated: 2026-11-11
---

# F006 P0 baseline (T502, merged #726 at acd78a2)

The "before" numbers every later F006 comparison uses, measured on origin/main c568d24 by Play Mode rigs (not on a device). The thresholds SC-001, SC-002, SC-003 and SC-010 are guidance (OD-6), so these figures steer design and are not gates. (S057)

## Probes and matrix

- `ShellGeometryProbe` reads `worldBound` of the persistent chrome elements and the unobstructed world rect, clipped to the safe area. `InteractionCountJourney` dispatches `NavigationSubmitEvent` on the real controls (device pointer events are dropped in the batch editor). `BaselineRig` composes the real shell, panels, slot grid and camera framing. `BaselineMatrixTests` runs 10 screen classes, 2 text scales, 5 states and 9 journeys and writes `reviews/baseline/<class>.json` plus 100 PNGs. Two full runs were byte-identical. (S057)
- Re-run with `IDLEFACTORY_F006_BASELINE_WRITE=1` and `scripts/test-unity.ps1 -TestFile` on `BaselineMatrixTests.cs`. The PNGs clear the panel to transparent first, per the [clearColor pitfall](./f006-batch-p0-workflow-pitfalls.md). (S057)

## Findings

- **SC-001 (factory dominance: world at least 60 %, chrome at most 15 %)**: met at both text scales only on desktop 1920x1080 (70.6 % world, 5.3 % chrome). The emulator portrait class meets it at 100 % text only. Phone landscape 640x360 shows 51.6 % world and 35.7 % chrome at 100 %, and 27.7 % and 62.0 % at 200 %. On tablet the always-present side panel takes 18.8 % to 46.9 % of the screen. (S057)
- **SC-002 (machine stays visible)**: the selected machine stays 100 % visible in every class, but the unobscured world share with a phone sheet open is 44-55 % at 100 % text, below the 50 % and 70 % targets in most phone classes. (S057)
- **SC-010 (persistent text)**: 13 labels on every class (Cash, 49.9M, Store, 2%, Overdrive, Ready, Mine, Factory, Market, Contracts, Items, Research, Prestige). The target is at most 6. (S057)
- **SC-003 (interactions)**: upgrade a machine and see the factory again 3, learn why one machine stopped 1, each system reached and left 2, build 3. (S057)
- **Device**: the emulator /data partition was full, so the app was reinstalled fresh for the device capture. (S057)

Related: [run shell and panels](./run-shell-and-panels.md), [plan overview](./f006-plan-overview.md), [direction decision](./f006-p0-direction-decision.md).

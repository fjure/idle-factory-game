---
title: F005 batch C content revision 3
type: decision
sources: [S022, S032, S033]
updated: 2026-10-22
---

# F005 batch C content revision 3

T345 (#410) tuned `contentRevision` 3 in both shipped copies of the content. It is a proposal: the owner approves the revision at T354 after T346 and a named human playtest (FR-027). (S032)

## Tuning order and why
1. **Dominance first.** Output per slot-second must rise along the chain. Raising downstream prices left Ore-only competitive and changed contract and market surfaces; slowing the Miner left the static rule failing. The winning lever multiplies the three downstream cycles by 0.4 (Smelter 0.8 s, Press 1.6 s, Assembler 2 s), so the terminal Gear beats Ore by 46 % against the 10 % margin, with no price or recipe amount change. (S032)
2. **Pace.** Speed growth 1.06 and upgrade cost growth 1.18 for every machine; the balanced 12-slot mix becomes 6 Miners, 2 Smelters, 2 Presses, 2 Assemblers. (S032)
3. **Ratios.** Standing offers 1.5, bonus 1.8, production objective 0.4; cooldowns 60 s (Gear, timed Bar), 120 s (Plate), 300 s (objective). (S032)
4. **Industry, threshold, ladder.** Threshold growth 1.10, `maxIndex` 30 (growth barely moves run time, it is an IP lever); Industry max levels 8/5/3/10/2 with decaying gains. A `maxLevel` cannot shrink because the load path rejects a level above it. (S032)
5. **Bands and sensitivity.** `contracts-off` 300 to 350 per mille; `returns` re-derived. (S032)

Not tuned: resource prices (Ore 1, Bar 3, Plate 8, Gear 24), recipes, `startingCash` 37, storage, research, Overdrive, offline policy, IP curve. (S032)

## Results (policy r2, 5 offsets)
- Chain medians 7,148 / 4,588 / 3,808 / 3,608 / 3,408 s; all three ECO-2 orientation bands inside; Ore-only 1.8x to 2.4x slower than the chain; all 16 gates assert and pass; IP ceiling 213 against highest 20 (10.7x). (S032, S033)
- The ratio-bounds gate is split: the engine half (configured 1.0 to 2.0, effective within one unit) always asserts, `ratio-content` asserts from revision 3. New validator rules: timed duration cap 7 days, bonus ratio above 1.5, mining share cap 100 %. (S032)
- A report-only variant (contracts-only plus one Ore sale) shows the real contracts-only time. (S032)
- Tuning order matters: dominance, then ratios, then bands, re-measuring the whole matrix after each step. (S032)

## Regression risk: W005-L
Regenerating the fixture (4,785 bytes) raised the load. On a loaded host (batch editor alive) the full ColdStart return measured 548.5 ms against the 500 ms budget and the algorithm 449.4 ms; revision 2 measured 233.8 / 274.1 ms on a quiet host. Indicative only: T349 re-measures on a quiet host and T351 on the emulator. See [measurements](./f005-batch-b-performance-and-measurements.md). (S032, S029)

Related: [policy r2 and gates](./f005-batch-c-policy-r2-and-gates.md).

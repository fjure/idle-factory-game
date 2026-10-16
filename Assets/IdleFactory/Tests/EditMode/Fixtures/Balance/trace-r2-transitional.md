# trace-r2.csv is a transitional golden (T313, issue #375; regenerated again by T320, issue #387)

`trace-r2.csv` was regenerated once by T313 (`IDLEFACTORY_REGENERATE_BALANCE_TRACE=1`) as a **transitional golden**, and a second time by T320
(see "T320 regeneration" below). It is not the F003 trace any more, and it is not the F005 trace: T341 (policy r2 and the asserted FR-024
gates in `BalanceTraceTests`) restores the asserted pacing bands and must delete this note.

## Why it changed (T313)

The shipped content (`Assets/IdleFactory/Content/Iron/iron-factory-content.json`) is schema version 3 with the **revision 2 values**. Schema v3 has no per-offer Cash curve; the contract Cash reward comes from `rewardRatioPpm`. T313 synthesized the F003 reward lists through a transitional adapter (`RunRewardAdapter`):

```
cash[i] = ceil(quantity[i] x basePrice x rewardRatioPpm / 10^6)
```

The ratios are the F003 index-0 reward over the market value of the index-0 quantity: Plate 1,400 / (100 x 8) = 1,750,000 ppm, Gear 4,000 / (100 x 24) = 1,666,666 ppm, timed Bar 510 / (100 x 3) = 1,700,000 ppm. Items came from the ladder with the F003 completion indices, owned by the offer of the matching tier. RP is the unchanged `rpReward` curve. The T313 golden moved only the Cash reward from completion index 2 onward (253 of 873 lines; run-1 eligibility 6,540 s).

## T320 regeneration

T320 removed the adapter and `ContractRewardDef.FromTable` and moved delivery onto `ContractRewards` (`Assets/IdleFactory/Core/Run/Domain/ContractRewards.cs`). Three things change the trace:

1. **Floor instead of ceil.** The Cash reward is `MulDivFloor(SalePricing value of the required units, rewardRatioPpm, 10^6)`. At the base price, index 0 to 5:

   | Offer | T313 (ceil) | T320 (floor) |
   |---|---|---|
   | Plate | 1400, 1414, 1442, 1470, 1498, 1526 | 1400, 1414, 1442, 1470, 1498, 1526 |
   | Gear | 4000, 4040, 4120, 4200, 4280, 4360 | 3999, 4039, 4119, 4199, 4279, 4359 |
   | timed Bar | 510, 516, 526, 536, 546, 556 | 510, 515, 525, 535, 545, 555 |

2. **The sale-price modifier is part of the reward (FR-009).** The basis is `SalePricing` at the modifier read at the delivery instant, so Research (Bargaining, Market Analysis) raises the Cash of a delivery. This is the largest change: it moves the Cash column and everything that follows from it. `BalanceTraceRunner` therefore measures the reward ratio against the sale value at the same modifier (`contract_market_value`, `contract_ratio_ppm_sum`), which keeps the ECO-5 bound meaningful.
3. **The Item ladder is run-wide.** An Item is granted where the run-wide completion index (all offers) equals the ladder index, at most `perRunCap` (10) per run, instead of per owning offer.

Measured before (T313) and after (T320), run 1: at minute 10 Cash 11 -> 198, Factory Value 4,718 -> 5,205, contracts completed 2 -> 2; at minute 60 Cash 270,572 -> 421,297, Factory Value 1,306,306 -> 1,486,808, contracts completed 69 -> 76; at minute 120 Factory Value 14,183,860 -> 14,602,152, contracts completed 213 -> 220. The floor (cause 1) moves single rewards by at most 1 Cash (Gear -1, Bar -1 from index 1); the modifier (cause 2) is the bulk of the shift once Research raises the sale price; the run-wide ladder (cause 3) moves which delivery grants an Item, and with it the Item timing.

What changed in the golden: 531 lines added and 528 removed of 877 (Cash and Factory Value columns of almost every row, and the counters). `var.contracts-off` is byte-identical (no delivery, no Item). Baseline run 1 reaches **first Gear at 680 s (was 720 s)** and **eligibility at 6,520 s (was 6,540 s; the ECO-2 band is 6,600 to 7,800 s)**; run 2 reaches eligibility at 4,660 s (the band is 4,200 to 4,800 s). No resource, Cash, RP, IP or Item is created or lost: the conservation violations list stays empty and the SC-001 chain test passes unchanged.

Sensitivity: the three recorded OWNER DECISION deviations (`var.contracts-off`, `var.price-80`, `var.returns`) are unchanged and asserted. `var.contracts-slow` newly misses its band under this golden (OWNER DECISION OPEN (#387); T341 retunes); it is listed in `TransitionalSensitivityDeviations` of `BalanceTraceTests` and only reported (a `REPORTED` line) until T341.

## What is asserted and what is reported

Asserted (unchanged): a loose guard that run-1 eligibility lies within 10 % of the F003 band (5,940 to 8,580 s; today 6,520 s), so accidental breakage fails before T341; the byte-for-byte golden, conservation (zero violations), the SC-001 chain, the ECO-3 monotonic and floor checks, the ECO-5 ratio bound (now against the modifier-adjusted sale value) and the ECO-6 Item bound (now `min(perRunCap, ladder entries)`), and the sensitivity set (`var.contracts-off`, `var.price-80`, `var.returns` stay the only asserted deviations).

Reported, not asserted, until T341 (each writes a `REPORTED` line to the test output):

- `Eco2BandsAndTheEco3RunTimesAreReportedForTheTransitionalRevision2`: the five ECO-2 and ECO-3 bands and the SC-003 first Gear, SC-003 first prestige and SC-004 second-run ranges. First-run eligibility (6,520 s) is the only figure outside its band.
- `ContentRecordR2QuotesAreReportedForTheTransitionalTrace`: the partial-batch statistics that `content-record-r2.md` quotes for run 1.
- `ReviewRecordQuotesAreReportedForTheTransitionalTrace`: the measured times that `balance-trace-r2.md` quotes (eligibility).
- the `var.contracts-slow` sensitivity band (see above).

`content-record-r2.md` and `balance-trace-r2.md` describe the F003 trace and are not edited by T313 or T320.

## T363 content slice (issue #466)

T363 adds the minimal revision 2 content slice the batch-A Play-Tester asked for (`playtest-agent-batch-a.md` F2 and F4: no cooldown, no bonus offer and no production objective were visible). Both shipped copies (`Assets/IdleFactory/Content/Iron/iron-factory-content.json` and `Assets/IdleFactory/Tests/PlayMode/Fixtures/SaveV1/Resources/RunPlayerFixtures/iron-factory-content.json`, byte-identical) changed; the frozen Save V2 catalog `iron-factory-content-r2.json` did not. `contentRevision` stays 2. No ratio, price, quantity or recipe of an existing offer moved (F3 and dominance stay for T345).

| Entry | Values | When a player meets it |
|---|---|---|
| `contract.plate` (cooldown) | `cooldownMs` 0 -> 60,000 | The first Plate delivery (factory level 2, run 1 around minute 6 to 10): the offer shows "ready in 1:00" after it ends, and the player can take Gear or the timed Bar meanwhile. |
| `contract.bonus-plate` (bonus, deliver) | Plate, 100 per delivery (flat), ratio 1,500,000 ppm (1.5x), RP 12, trigger `factoryLevelAtLeast` 2, window 900,000 ms | Opens when the factory reaches level 2 (the moment Plate unlocks); a 15 minute window covers the first Plates. Once per run. The trigger is wired by T327. |
| `contract.produce-plate` (production objective) | Plate, 20 units at index 0 (+1 % per completion, like the other offers; 5 % overflows 10^18 before index 1,023), ratio 250,000 ppm, RP 2, `cooldownMs` 300,000 | Available as soon as the first Plate is produced; 20 Plates is a minute or two of one Press. Completion counting is T327. |

Reasoning: both new rewards stay within the 1.5x cap (FR-004, D-D) although the three F003 offers do not (F3, transitional by owner decision). The production objective pays 0.25x the market value because ECO-1 requires R(produce) plus the highest standing delivery ratio of the same resource (Plate, 1.75 today) to stay at or below 2.0, which is exactly met. It is a Plate offer, not Ore or Bar, for two reasons: that stack rule, and the legacy `Kind` label of `RunCatalogBuilder.LabelKinds`, which names the cheapest untimed resource "plate" and every other "gear", so a cheaper untimed resource would relabel the F003 Plate and Gear offers in the Contracts panel.

Validator rules passed (read by inspection and against the schema; Unity run in Phase 2): `contract.objective.unknown`, `contract.class.unknown`, `contract.bonus.fields` (the bonus member exactly on the bonus offer), `contract.reward.ratio` (deliver floor 1.0 and ceiling `maxRewardRatioPpm`; no floor for a production objective), `contract.rp.growth`, `contract.quantity.cap`, `contract.produce.min` (20 >= 10), `unlock.unreachable` for the bonus trigger, and the revision 3 rules `contract.ratio.bonus`, `contract.ratio.standing` (new offers only) and `contract.ratio.produce`, so T345 inherits valid entries.

Policy and test changes that follow the content (no Core or validator code): `ReferencePolicy` and `RunJourneyPolicy` take only delivery offers that are out of cooldown and, for a bonus offer, inside its window. Production objectives are not modelled until T341, and the bonus trigger is not wired until T327, so the golden moves only through the Plate cooldown. `RunContentCanonicalTests` expects the five offers, the one cooldown, the one bonus and the one production objective, and its market-value floor applies to the deliver objective only.

Golden: `trace-r2.csv` is regenerated once with `IDLEFACTORY_REGENERATE_BALANCE_TRACE=1` and relabelled here as AR2-2 and T313 prescribe: the transitional status stays until T341, the asserted guards stay (conservation, SC-001 chain, ECO-3, ECO-5, ECO-6, the loose eligibility guard of 5,940 to 8,580 s, the three OWNER DECISION sensitivity deviations), and T341 still deletes this note. The measured change (lines changed, run 1 and run 2 eligibility, first Gear) is recorded in the section "T363 regeneration" below once Unity has produced it.

## T363 regeneration

`trace-r2.csv` was regenerated once more (`IDLEFACTORY_REGENERATE_BALANCE_TRACE=1`) on the merged `origin/main` (0ba64a7, with T326 and T333) plus the T363 content. Before the content slice, on that same main, the golden was byte-identical to the previous one, so the whole diff below is the Plate cooldown.

- Lines: 359 added and 357 removed of 879.
- Run 1: first Gear 740 s (was 680 s); eligibility 6,360 s (was 6,520 s; inside the asserted 5,940 to 8,580 s guard and nearer the ECO-2 band of 6,600 to 7,800 s from below). Run 2: eligibility 4,620 s (was 4,660 s; the band is 4,200 to 4,800 s).
- `var.contracts-slow` rows are byte-identical (still reported, not asserted, via `TransitionalSensitivityDeviations`); `var.contracts-off` is unaffected by the cooldown but the other variant rows shift with the Plate schedule.
- Conservation violations list stays empty and the SC-001 chain test passes unchanged.

Two content values were changed after the first commit, both forced by a test or the validator: the cooldown was lowered from 120,000 ms to 60,000 ms because 120,000 ms moved run-1 eligibility to 11,160 s, outside the asserted guard (30,000 ms and 60,000 ms both stayed inside; 60,000 ms keeps the cooldown visible); and the production objective growth went from 1,050,000 to 1,010,000 ppm because 20 x 1.05^index exceeds 10^18 at index 780 (`curve.overflow`), which failed 52 tests that build the shipped catalog.

## T327 regeneration (issue #392)

`trace-r2.csv` was regenerated once more (`IDLEFACTORY_REGENERATE_BALANCE_TRACE=1`) on the merged tree of T327 (production counting, bonus trigger, outcomes) and `origin/main` d64270a (T363). No guard, band or policy changed.

Cause: before T327 the bonus trigger did not exist in the unlock pass, so `contract.bonus-plate` never opened and the reference policy never saw it as available. With the trigger wired, the shipped bonus offer (Plate, 100 units, ratio 1.5, no cooldown, trigger `factoryLevelAtLeast` 2, window 900,000 ms) now opens when the factory reaches level 2 (before minute 10 of run 1), and the policy takes it inside its window (`IsBonusAvailable`). The production objective `contract.produce-plate` now counts, but the policy still skips every production objective (until T341), so it does not move the trace.

Evidence: `var.contracts-off` (31 of 31 rows) and `probe.ore-only` (23 of 23 rows) are byte-identical, so the change comes through contracts only. All `BalanceTraceTests` gates and bands hold against the new golden (20 of 20 pass): conservation violations stay empty, the SC-001 chain test, the asserted eligibility guard and the sensitivity set are unchanged.

What changed: 559 lines added and 541 removed of 897.

- Run 1: first Gear 740 s -> 680 s; eligibility 6,360 s -> 6,500 s (inside the 5,940 to 8,580 s guard). Run 2 and run 3: eligibility 4,620 s -> 4,520 s. Run 4 and run 5: first Gear and eligibility unchanged (160 s, 4,400 s), most rows differ.
- Variant first Gear: `var.price-80` 1,060 s -> 1,000 s; `var.overdrive-off` 780 s -> 920 s; `var.absence-30m` and `var.absence-60m` 740 s -> 680 s; `var.returns` 2,120 s -> 1,220 s; `var.price-120` and `var.contracts-slow` unchanged (rows differ). `scaled.run1` first Gear 740 s -> 680 s.

Known limitation: bonus state is not persisted until Save V3 (T334), so a reload re-triggers the bonus and restarts its window at the first settle after the load. The trace does not reload, so it is unaffected; the shipped game is until T334.

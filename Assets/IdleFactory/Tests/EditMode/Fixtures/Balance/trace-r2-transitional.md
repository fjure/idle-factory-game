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

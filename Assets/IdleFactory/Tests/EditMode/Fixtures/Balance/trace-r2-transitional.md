# trace-r2.csv is a transitional golden (T313, issue #375)

`trace-r2.csv` was regenerated once by T313 (`IDLEFACTORY_REGENERATE_BALANCE_TRACE=1`) as a **transitional golden**. It is not the F003 trace any more, and it is not the F005 trace: T339 (balance retune) regenerates it again for the final content and must delete this note.

## Why it changed

The shipped content (`Assets/IdleFactory/Content/Iron/iron-factory-content.json`) is schema version 3 with the **revision 2 values**. Schema v3 has no per-offer Cash curve; the contract Cash reward comes from `rewardRatioPpm`. Until T320 replaces the reward formula, `RunCatalogBuilder` synthesizes the F003 reward lists through the transitional adapter (`RunRewardAdapter`):

```
cash[i] = ceil(quantity[i] x basePrice x rewardRatioPpm / 10^6)
```

The ratios are the F003 index-0 reward over the market value of the index-0 quantity: Plate 1,400 / (100 x 8) = 1,750,000 ppm, Gear 4,000 / (100 x 24) = 1,666,666 ppm, timed Bar 510 / (100 x 3) = 1,700,000 ppm. Items come from the ladder with the F003 completion indices, owned by the offer of the matching tier, so the Item grants are identical to F003. RP is the unchanged `rpReward` curve.

## What changed

Only the Cash reward from completion index 2 onward. Indices 0 and 1 are identical. The quantity curve is `ceil`-iterated from a base of 100 (100, 101, 103, 105, 107, ...), which grows by about 2 per completion after the second, while the F003 Cash curve was its own `ceil`-iterated curve from a larger base (1% per step). A reward proportional to the quantity therefore pulls ahead:

| Offer | Cash at completion index 0, 1, 2, 3, 4, 5 (F003 -> adapter) |
|---|---|
| Plate | 1400 -> 1400, 1414 -> 1414, 1429 -> 1442, 1444 -> 1470, 1459 -> 1498, 1474 -> 1526 |
| Gear | 4000 -> 4000, 4040 -> 4040, 4081 -> 4120, 4122 -> 4200, 4164 -> 4280, 4206 -> 4360 |
| timed Bar | 510 -> 510, 516 -> 516, 522 -> 526, 528 -> 536, 534 -> 546, 540 -> 556 |

Effects on the golden (253 of 873 lines changed):

- Columns that change: `cash`, `fv`, `stock`, `machines`, `storageLevel`, `contractsCompleted`, `ip_preview`, `minute` (the eligibility row label) and `eligibleAtSec`. The first Gear time and every `itemsHeld`, `researchLevels` and `firstGearAtSec` column are unchanged.
- Baseline run 1 reaches **eligibility at 6,540 s instead of 6,940 s** (400 s faster; the ECO-2 band is 6,600 to 7,800 s). Runs 2 to 5 keep 4,640, 4,640, 4,320 and 4,320 s.
- The `var.contracts-off` and `var.contracts-slow` blocks do not deliver a contract (or deliver on the unchanged indices), so they are byte-identical. The variants that deliver contracts differ in the Cash and Factory Value columns only.
- No resource, Cash, RP, IP or Item is created or lost: the conservation violations list stays empty and the SC-001 chain test passes unchanged.

## What is asserted and what is reported

Asserted (unchanged): the byte-for-byte golden, conservation (zero violations), the SC-001 chain, the ECO-3 monotonic and floor checks, the ECO-5 ratio and ECO-6 Item bounds, and the sensitivity set (`var.contracts-off`, `var.price-80`, `var.returns` stay the only deviations).

Reported, not asserted, until T339 (each writes a `REPORTED` line to the test output):

- `Eco2BandsAndTheEco3RunTimesAreReportedForTheTransitionalRevision2`: the five ECO-2 and ECO-3 bands and the SC-003 first Gear, SC-003 first prestige and SC-004 second-run ranges. First-run eligibility (6,540 s) is the only figure outside its band.
- `ContentRecordR2QuotesAreReportedForTheTransitionalTrace`: the partial-batch statistics that `content-record-r2.md` quotes for run 1.
- `ReviewRecordQuotesAreReportedForTheTransitionalTrace`: the measured times that `balance-trace-r2.md` quotes (eligibility).

`content-record-r2.md` and `balance-trace-r2.md` describe the F003 trace and are not edited by T313.

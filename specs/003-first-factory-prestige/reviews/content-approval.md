# Content approval: revision 2

**Task**: T127 (owner gate, parent-only). **Owner**: Flo. **Date**: 2026-10-09.
**Approved**: `Assets/IdleFactory/Content/Iron/iron-factory-content.json`, `contentRevision` 2 (blob `66f41fb`, last changed in `9e96cf9`), as it stands on `origin/main` at `fced3ea`. This is the exact revision the owner played in T126 (`playtest-flo-20261009.md`).
**Inputs presented**: `balance-trace-r2.md` (verdicts, sensitivity and the seven OWNER DECISION items of section 6), `content-record-r2.md`, the T126 playtest record, and the parent's recommended decision package.

## Decision

**Approved, with the decision package below.** No content change is requested, so T125 is not reopened.

The minute bands of SC-003 and SC-004 are orientation, not acceptance thresholds (spec › Session 2026-10-09). The owner judged the feel of revision 2 good in the playtest, and that judgment is the acceptance basis.

| Item (`balance-trace-r2.md` §6) | Decision |
|---|---|
| 1. `var.price-80` misses its bound (+699 per mille against ±250) because integer rounding removes the smelting margin | Accepted as a test artifact. A price modifier that does not round replaces the variant in Feature 005's balance work. The gate keeps pinning the failure. |
| 2. `var.contracts-off` (+274 per mille) and `var.returns` (×2.12 wall time) miss their bounds | Accepted. Contracts and the value of offline returns are redesigned in Feature 005, which also re-tunes these bounds. |
| 3. Later-progression bands (runs 3 to 5) not met at the ECO-3 caps; the second prestige feels flat because of the Industry front-loading | Deferred to Feature 005 (Industry caps, IP per run, cost scaling). |
| 4. Ore-only dominance (13 % earlier than the chain) and the first prestige 4 minutes before "near 120" | Accepted. Selling and contracts are reworked in Feature 005; minute bands are orientation only. |
| 5. Contract reward ratios at 1.67 and 1.75 leave no headroom | Accepted. Covered by the Feature 005 contract redesign. |
| 6. Trace noise as wide as the bands | Noted. |
| 7. Revision 2 is a fit to reference policy r1 | Option (a): approve revision 2 as a fit to policy r1. A corrected policy and a re-tuned revision are Feature 005 work. |

## Consequences

- SC-003 and SC-004 are satisfied by the owner's feel judgment on the approved revision. The trace numbers stay recorded and remain targets for later features, not claims.
- T132 and T133 check that the shipped content equals this approved revision (blob `66f41fb`).
- The Feature 005 items above are tracked in `docs/product/ideas.md` until Feature 005 is specified.

# JSON Content Migration Review (T080)

**Date:** 2026-09-29 · **Reviewed commit:** `c1aed9ffa2ad455610cb77f0dc92a22babfb360a` (`origin/main` after PR #159, T079) · **Mode:** independent, read-only reviewers in two parallel waves (max_parallel 3). No reviewer implemented the reviewed code. · **Covers:** FR-021, SC-005

## Verdicts

| Role | Tier | Verdict | Summary |
|---|---|---|---|
| Game Designer/Product Owner | Medium | **PASS-WITH-FINDINGS** | US1–US4 pass. FR-020 holds: `development-catalog.json` equals `AcceptedFoundationCatalogFixture` field for field, with no added entries, mechanics or balance values. FR-019 approval is recorded. SC-005 walkthrough: PASS (below). |
| Software Architect | Medium | **PASS-WITH-FINDINGS** | Core, packages and asmdefs are unchanged over `51603f2..c1aed9f`. `GameplayContentLoader` is the only catalog source, and the bootstrap and `MachineInspectorPresenter` both use `bootstrap.Definitions`. The stages (version peek, structural, semantic, domain construction) are separated as designed. `Initialize()` fails closed before any checkpoint I/O. `SaveV1Mapper` is untouched. |
| Game Systems/Economy | Medium | **PASS-WITH-FINDINGS** | All 14 accepted values match (2→1, 50,000,000 ticks, 2×2, orientation [0], capacities 4/1, revision 1 at 4/5 with an 8 h cap, flags, labels, keys). Integer-only reading and the revision-1 lock hold. The Save V1 assertions (offline 288e9/288e9/230.4e9, D001 two cycles, W001 bytes) are exact and meaningful for the economy. |
| Test/QA | Medium | **PASS-WITH-FINDINGS** | Traceability runs from FR-001–FR-021, SC-001–SC-008 and the edge cases to named tests. All 31 diagnostic codes have a rejecting test. The #47 and #52 QA-loop fixes have regression tests. |
| Security/Privacy | Medium | **PASS-WITH-FINDINGS** | The size cap applies before decoding. The decoder is strict UTF-8 without BOM, depth is capped at 16, numbers are range-checked, and there is no regex, reflection or type-name deserialization. Diagnostics carry no paths or player data. Loading fails closed. The editor seam is unreachable in players. The Save V1 boundary is unchanged, and the test-only environment variables are not read at runtime. |

**Omitted roles:**
- **UI/UX:** the only visible change, the content-failure error state, was independently reviewed in T075 (#50), PASS-WITH-FINDINGS with two P3 notes.
- **Technical Art:** no sprite, prefab, import setting or `F001SpriteCatalog` changed. T079 deleted gameplay `.asset` definitions only, and `JsonContentAuthorityTests` proves every presentation key still resolves.
- **Performance:** SC-008 is measure-only. The Windows player median is 0.0612 ms against a 0.0320 ms baseline and a 100 ms guard, so the guard was not reached.
- **DevOps:** no build or pipeline change. The lease-script fix (#156) is outside this feature and was reviewed in its own PR.

## SC-005 walkthrough (Product Owner)

The reviewer had not seen the implementation and used only `docs/content/json-content-pipeline.md`.

- **Setup:** in a scratch copy outside the repository, the reviewer added resource `hypo.gear` and recipe `hypo.recipe.gear` (3 × `dev.input` → 1 × `hypo.gear`, 100,000,000 ticks), with collections sorted.
- **Attempt 1 passed:** `IDLEFACTORY_CONTENT_CHECK_PATH=<abs>` with `scripts/test-unity.ps1 -TestFile Assets/IdleFactory/Tests/EditMode/GameplayContentAuthoringCheckTests.cs` returned `UNITY_TEST_PASS … passed=1`, with no diagnostics.
- **Negative control:** a copy with an unresolved output (`hypo.gearx`) failed with `ref.resource.unknown /recipes/1/outputs/0/resourceId: Unknown resource 'hypo.gearx'.`
- **FR-005:** the guide states that such an addition needs its own approved specification.
- **Hygiene:** nothing was committed, the lease was released, and the checkout is clean.

**Verdict: PASS**, in 1 attempt against a target of at most 2.

## Findings (none blocking)

| ID | Sev | Source | Finding | Disposition |
|---|---|---|---|---|
| R-GS1 | P2 | Game Systems | The guide does not explain the offline formula (`floor(min(interval, cap) × num / den)`). It does not say that the 4/5 ratio and the 8 h cap are locked economy invariants, so an author could read them as tunable. | Guide fix, assigned to convergence |
| R-GS2 | P2 | Game Systems | The guide does not warn that changing `durationTicks`, `amount` or `capacity` on an existing ID silently changes existing players' progress. Only the offline policy is locked at runtime. | Guide fix, assigned to convergence |
| R-QA1 | P2→resolved | Test/QA | Three Play Mode tests use `Assert.Ignore` under `#if !UNITY_EDITOR`. | Not a risk: the player run includes only the `StandalonePlayer` category. Player runs reported 6/6 with 0 skipped. |
| R-QA2 | P3 | Test/QA | There is no explicit test that IDs differing only by case are distinct, and no dedicated tick-overflow test (`OutOfRangeInt64IsRejected` likely covers the latter). | Convergence |
| R-PO1 | P3 | Product Owner | The authoring check's pass looks the same as its no-op when the environment variable is unset, and its progress lines are not visible in the runner output. | Convergence (guide note or visible output) |
| R-PO2 | P3 | Product Owner | The authoring check does not verify `presentationKey` against `F001SpriteCatalog`; that happens at start-up. The guide should say so. | Convergence (guide) |
| R-GS3 | P3 | Game Systems | The JSON-path offline test uses exactly 8 h, so neither clipping at the cap nor floor rounding is exercised there. Core tests cover the formula. | Convergence |
| R-GS4 | P3 | Game Systems | The guide gives no seconds-to-ticks rule (1 s = 10,000,000 ticks) and does not say that a capacity of 0 blocks production. | Convergence (guide) |
| R-AR1 | P3 | Architect | Reader, validator, builder and DTO types are public, although only the loader should be used. | Accepted: tests need access. `JsonContentAuthorityTests` guards single authority. |
| R-AR2 / R-SEC3 | P3 | Architect, Security | The general bootstrap `catch` shows `exception.Message` (it covers store and session construction), which is inconsistent with the type-name-only rule. | Convergence |
| R-AR3 | P3 | Architect | The private `catalog` field is not reset in `DisableWithError`. | Convergence (cosmetic) |
| R-SEC1 | P3 | Security | An over-long ID is echoed in full into `EntryId` and the messages. | Convergence (truncate echoes) |
| R-SEC2 | P3 | Security | `GameplayContentLoader.Load` runs outside a `try`, so an unexpected exception would escape `Initialize`. The outcome is still fail-closed. | Convergence (map to `content.internal`) |
| R-SEC4 | P3 | Security | The `content.internal` message includes `exception.Message`. | Convergence |

**Parent verdict for T080:** every mandatory role's verdict on the exact merged commit is recorded (FR-021), and SC-005 passes. None of the findings blocks T081/T082. The P2 and P3 items feed `$speckit-converge` after acceptance.

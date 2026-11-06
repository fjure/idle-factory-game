---
title: Gameplay content validation
type: component
sources: [S011, S012, S013, S014, S008, S018]
updated: 2026-11-06
---

# Gameplay content validation

`GameplayContentValidator` reports semantic violations in document order. It retains up to 100 diagnostics and appends a truncation note to the last one when more violations remain. (S011)

`JsonDefinitionCatalogBuilder` creates domain definitions only after validation succeeds. It maps a constructor exception to `content.internal` and sorts recipe amounts and buffer capacities by ordinal resource ID, keeping nested collection order stable. (S012)

`GameplayContentOrderTests` permutes top-level definitions and nested recipe and buffer entries, then compares the resulting catalogs. `GameplayContentSchemaConsistencyTests` checks the author schema against reader limits and the author guide. (S013, S014)

Diagnostics never echo unbounded or sensitive text. Identifiers repeated in `EntryId` or in messages are clipped to 128 UTF-16 code units without splitting a surrogate pair. `content.internal`, the bootstrap's general failure and an unexpected loader exception report only the exception type name, because exception messages can carry file-system paths (T084). (S008)

`JsonContentEdgeCaseTests` pins three things on the JSON path: IDs that differ only in letter case stay distinct, `durationTicks` above Int64 is rejected with `value.out-of-range`, and an offline return longer than the cap clips to 8 h with floor rounding of the 4/5 ratio (T085). (S008)

Related: [JSON content authoring](./json-content-authoring.md).

## Version 2 (T096)

Feature 003 adds a separate strict pipeline in `Unity/Content/`: `RunContentReader` (a hand-written `JsonTextReader` token walk with the v1 envelope, limits and diagnostics; no reflection deserialization), `RunContentDtos` and `RunContentValidator`. The v1 loader and Save V1 stay unchanged. Building the domain `RunCatalog` from the DTOs is T136. See [gameplay content v2](./gameplay-content-v2.md). (S018)

- Every code in the contract's rule-code table has exactly one test case, and `EveryContractRuleCodeHasATestCase` fails if a code lacks one. `RunContentSchemaConsistencyTests` binds the reader to the real schema, the author guide and the contract's code table in both directions. (S018)
- Diagnostics are ordered by canonical family order and list order, not by physical JSON member order, because the DTOs carry no source positions. (S018)
- `speed.level.dead` and `batch.overflow` compose the worst case with the Core `SpeedComposer`, so validation and runtime use the same arithmetic. The effect ranges and curve iteration are re-implemented in the validator, however. Until T136 adds a parity test, "validates" does not yet prove "constructs". (S018)
- The worst-case speed check has no CPU budget. Hostile content could need about 10^9 fold steps; content ships with the build, so this is a known follow-up. (S018)
- Presentation keys are checked only when an `IPresentationKeyResolver` is injected; the start-up composition must pass one. (S018)
- Test-only `Fixtures/MiniJson.cs` parses the schema and guide, because the Edit Mode test assembly does not reference Newtonsoft. (S018)

T136 adds the catalog builder and the version dispatcher on top of this pipeline; see [run content dispatch](./run-content-dispatch.md). (S018)

- T097 adds two codes. `id.reserved` rejects any resource, machine, offer, research or Item with the ID `overdrive`, which `RunState` reserves for Overdrive. `unlock.unreachable` runs a static fixpoint from `runStart.initialUnlocks` plus always-true conditions and reports unlock chains that can never fire (never-produced resources, cycles, unreachable research). The check is deliberately optimistic: factory levels count as attainable and affordability is ignored. (S018)

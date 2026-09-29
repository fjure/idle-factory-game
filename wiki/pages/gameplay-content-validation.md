---
title: Gameplay content validation
type: component
sources: [S011, S012, S013, S014, S008]
updated: 2026-09-29
---

# Gameplay content validation

`GameplayContentValidator` reports semantic violations in document order. It retains up to 100 diagnostics and appends a truncation note to the last one when more violations remain. (S011)

`JsonDefinitionCatalogBuilder` creates domain definitions only after validation succeeds. It maps a constructor exception to `content.internal` and sorts recipe amounts and buffer capacities by ordinal resource ID, keeping nested collection order stable. (S012)

`GameplayContentOrderTests` permutes top-level definitions and nested recipe and buffer entries, then compares the resulting catalogs. `GameplayContentSchemaConsistencyTests` checks the author schema against reader limits and the author guide. (S013, S014)

Diagnostics never echo unbounded or sensitive text. Identifiers repeated in `EntryId` or in messages are clipped to 128 UTF-16 code units without splitting a surrogate pair. `content.internal`, the bootstrap's general failure and an unexpected loader exception report only the exception type name, because exception messages can carry file-system paths (T084). (S008)

`JsonContentEdgeCaseTests` pins three things on the JSON path: IDs that differ only in letter case stay distinct, `durationTicks` above Int64 is rejected with `value.out-of-range`, and an offline return longer than the cap clips to 8 h with floor rounding of the 4/5 ratio (T085). (S008)

Related: [JSON content authoring](./json-content-authoring.md).

---
title: Gameplay content validation
type: component
sources: [S011, S012, S013, S014]
updated: 2026-09-29
---

# Gameplay content validation

`GameplayContentValidator` reports semantic violations in document order. It retains up to 100 diagnostics and appends a truncation note to the last one when more violations remain. (S011)

`JsonDefinitionCatalogBuilder` creates domain definitions only after validation succeeds. It maps a constructor exception to `content.internal` and sorts recipe amounts and buffer capacities by ordinal resource ID, keeping nested collection order stable. (S012)

`GameplayContentOrderTests` permutes top-level definitions and nested recipe and buffer entries, then compares the resulting catalogs. `GameplayContentSchemaConsistencyTests` checks the author schema against reader limits and the author guide. (S013, S014)

Related: [JSON content authoring](./json-content-authoring.md).

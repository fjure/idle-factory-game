---
title: Gameplay content v2
type: decision
sources: [S018]
updated: 2026-09-29
---

# Gameplay content v2

Feature 003 plans a second packaged document, `Content/Iron/iron-factory-content.json`, with `contentSchemaVersion: 2` and an integer `contentRevision` for balance versions. The loader peeks the version. Version 2 goes to a new reader, validator and builder. Version 1 goes to the unchanged Feature 002 loader. That loader stays only so that `development-catalog.json` can validate Save V1 during import. (S018)

The envelope, limits, diagnostics format, all-or-nothing loading and canonical review gate carry over unchanged from [JSON content authoring](./json-content-authoring.md). (S018)

## Why new domain types

The run gets its own immutable definitions, and the Feature 001 `RecipeDefinition` is left alone. Two things rule out reusing it: its constructor and the Feature 002 rule `recipe.inputs.empty` forbid the input-free Miner, and relaxing them would change what content v1 means. (S018)

## Closed vocabularies

A new kind in any of these vocabularies needs code and a new schema version, 3. Retuning numbers and adding IDs within version 2 stays save-compatible. (S018)

| Vocabulary | Allowed kinds |
|---|---|
| Effects | `machineSpeed` (ppm), `machineOutput` (integer factor), `salePrice`, `upgradeCost`, `startingCash`, `startingStorageLevel` |
| Unlocks | `always`, `researchPurchased`, `factoryLevelAtLeast`, `resourceProduced` |
| Contracts | `plate`, `gear`, `timedBar` |
| Curves | `{base, growthPpm, rounding}` |

Curves are evaluated **iteratively per level**, and the catalog precomputes them into tables. Any value above 10^18 is rejected with `curve.overflow`. (S018)

## Guards

The validator also rejects (S018):

- a recipe that would cycle faster than 0.1 s at the maximum combined speed;
- an incomplete contract or Item-tier set;
- a missing affordable first Industry upgrade;
- Research prerequisite cycles;
- more than 12 slots.

Presentation keys for resources, machines and Items must resolve in the sprite catalog at start-up. (S018)

Related: [run economy and prestige](./run-economy-and-prestige.md), [gameplay content validation](./gameplay-content-validation.md).

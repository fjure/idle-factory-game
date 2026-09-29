---
title: Gameplay content v2
type: decision
sources: [S018]
updated: 2026-09-30
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

- a level, milestone or effect that the speed bound would make dead, or a worst-case batch amount above 10^18 (OD-6; an oversized batch completes partially, OD-9);
- an incomplete contract or Item-tier set;
- a missing affordable first Industry upgrade;
- Research prerequisite cycles;
- more than 12 slots.

Presentation keys for resources, machines and Items must resolve in the sprite catalog at start-up. (S018)

## Implemented content model (T094)

The immutable types live in `Assets/IdleFactory/Core/Run/Content/`, one file per type, with `RunCatalog` as the root. The Feature 001 definitions are unchanged. (S018)

- **Effect matrix is enforced in two places.** `EffectDef` checks the kind→target matrix: `machineSpeed` → `allMachines`/`machine`; `machineOutput` → `machineType`/`machine`; `salePrice` → `allResources`/`resource`; `upgradeCost` → `allMachines`/`machine`/`expansion`; starting effects → `none`. Each owner then checks the source rule. Items allow only speed and output, with output on `machineType`. Research rejects the starting effects, with output on `machine`. Industry rejects `machineOutput`. The first implementation accepted `factory` and `machineType` for speed and failed QA. (S018)
- **The output factor is bounded at catalog level.** `EffectDef` accepts factors 2 to 10^6. `RunCatalog` then rejects any factor above `RunStart.MaxCombinedOutputFactor`. A constructor default of 4 was rejected: it would silently bind content whose run start allows a different cap. (S018)
- **Limits:** 256 per nested list (milestones, rewards, completion indices, prerequisites, ID lists, recipe sides) and 1,024 per top-level family and level table. An earlier shared guard allowed 1,024 everywhere. Boundary tests cover 256 accepted and 257 rejected. (S018)
- **Owner decisions in T096:** an `upgradeCost` effect on `expansion` has no `id` and lowers both expansion tracks, storage and factory. Each contract kind uses one resource across all its offers, and no resource is shared by two kinds. The example documents round Cash rewards with `ceil`, because `floor` breaks `contract.reward.ratio` at high completion indices. (S018)
- **Test fixture:** `Tests/EditMode/Fixtures/Run/RunTestCatalogs.IronLoop()` builds the literal FR-002 Iron loop in code, so later suites do not depend on the JSON sheet. Optional parameters set the output cap and an Item factor. (S018)

Related: [run economy and prestige](./run-economy-and-prestige.md), [gameplay content validation](./gameplay-content-validation.md).

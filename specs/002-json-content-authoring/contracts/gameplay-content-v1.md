# Contract: Gameplay Content Document v1

**Consumers**: `GameplayContentReader` → `JsonDefinitionCatalogBuilder` (Unity assembly), human and AI content authors, review gates.
**Normative for**: FR-001–FR-010, FR-015, FR-017. The implementation copy of this contract for authors is `docs/content/json-content-pipeline.md` (task T071); where the two differ, this file wins until T071 is accepted, and the two must then agree.

## Encoding and envelope

- UTF-8 without BOM, strict decoding, at most 1 MiB, JSON depth at most 16, exactly one root object, and only whitespace after it.
- Numbers are JSON integers written without a fraction or exponent. Every field has an explicit range. Strings follow JSON escaping rules. Comments are not allowed.
- Every object member is required unless it is marked optional. Unknown and duplicate members are rejected at every level.

## Document shape (v1)

```json
{
  "contentSchemaVersion": 1,
  "resources": [
    { "id": "dev.input", "developmentLabel": "Input", "presentationKey": "F001-RESOURCE-DEV-INPUT", "isDevelopmentOnly": true }
  ],
  "recipes": [
    {
      "id": "dev.recipe.basic",
      "inputs":  [ { "resourceId": "dev.input",  "amount": 2 } ],
      "outputs": [ { "resourceId": "dev.output", "amount": 1 } ],
      "durationTicks": 50000000,
      "isDevelopmentOnly": true
    }
  ],
  "buildings": [
    {
      "id": "dev.machine.basic",
      "footprintWidth": 2,
      "footprintHeight": 2,
      "allowedOrientations": [0],
      "recipeId": "dev.recipe.basic",
      "inputBuffer":  [ { "resourceId": "dev.input",  "capacity": 4 } ],
      "outputBuffer": [ { "resourceId": "dev.output", "capacity": 1 } ],
      "presentationKey": "F001-MACHINE-D001-NORMAL",
      "isDevelopmentOnly": true
    }
  ],
  "offlinePolicies": [
    { "revision": 1, "efficiencyNumerator": 4, "efficiencyDenominator": 5, "maxCreditedTicks": 288000000000 }
  ]
}
```

The example is illustrative and abbreviated: `dev.output` is omitted from `resources`, and the example therefore fails `ref.resource.unknown`. The complete migrated document is `Assets/IdleFactory/Content/Development/development-catalog.json` (task T074). The "documented example" that FR-002 requires to validate is the complete example in the pipeline guide, not this abbreviated illustration. Its values are fixed by spec FR-011 and the parity fixture.

## Fields

| Path | Type / range | Required | Meaning |
|---|---|---|---|
| `contentSchemaVersion` | int, must be `1` | yes | Content contract version. It is read first and is independent of Save V1 `schemaVersion`. |
| `resources[]` | 1–1,024 entries | yes | Resource definitions |
| `resources[].id` | ID | yes | Stable resource ID (save identity) |
| `resources[].developmentLabel` | non-blank string ≤ 128 | yes | Development-only display label, not an ID |
| `resources[].presentationKey` | non-blank string ≤ 128 or omitted | optional | Key into `F001SpriteCatalog`. Optional, because `ResourceDefinition` uses `OptionalText`. |
| `resources[].isDevelopmentOnly` | bool | yes | Development content flag |
| `recipes[]` | 1–1,024 | yes | Recipe definitions |
| `recipes[].inputs[]`, `.outputs[]` | 1–256 entries each, `resourceId` unique per side | yes | Aggregate whole-unit exchange |
| `…amount` | int64, 1…9,223,372,036,854,775,807 | yes | Whole resource units |
| `recipes[].durationTicks` | int64 ≥ 1 | yes | Duration in 100 ns ticks (50,000,000 = 5 s) |
| `buildings[]` | 1–1,024 | yes | Building definitions |
| `buildings[].footprintWidth`, `footprintHeight` | int32 ≥ 1 | yes | Cells |
| `buildings[].allowedOrientations` | 1–4 unique ints in 0–3 | yes | Quarter turns |
| `buildings[].recipeId` | ID or omitted | optional | A producing building requires both buffers |
| `buildings[].inputBuffer`, `outputBuffer` | 1–256 `{resourceId, capacity}` or omitted | optional | Omitted means no buffer. Capacity is int64 ≥ 0. Every recipe resource must be declared on its side. |
| `buildings[].presentationKey` | non-blank string ≤ 128 | yes | Key into `F001SpriteCatalog`. Required, because `BuildingDefinition` uses `RequiredText`. |
| `offlinePolicies[]` | 1–1,024 | yes | Keyed by `revision` |
| `offlinePolicies[].revision` | int32 ≥ 1 | yes | Offline policy revision referenced by Save V1 |
| `…efficiencyNumerator`, `efficiencyDenominator` | int64 ≥ 1, numerator < denominator | yes | Offline efficiency ratio |
| `…maxCreditedTicks` | int64 ≥ 1 | yes | Credited offline cap (288,000,000,000 = 8 h) |

**ID**: a JSON string of 1–128 UTF-16 code units whose value is unchanged by trim + Unicode NFC. IDs are compared by case-sensitive ordinal equality and are unique per family. IDs are never derived from labels, file names or asset paths.

**Currently accepted policy set**: exactly revision 1 with 4/5 and 288,000,000,000 ticks (`DefinitionCatalog`). A different set is rejected with `policy.revision-1.required`.

## Canonical form (review gate, not a runtime rule)

- Two-space indentation, LF line endings and a final newline. Members appear in the order listed in the Fields table.
- `resources`, `recipes` and `buildings` are sorted by `id` (ordinal). `offlinePolicies` are sorted by `revision`. Nested amounts and capacities are sorted by `resourceId`, and `allowedOrientations` ascending.
- Optional members are omitted rather than written as `null`. `null` is always rejected.

## Diagnostics

Each diagnostic has these fields:

- `code`: from `data-model.md`;
- `path`: a JSON Pointer into the document, for example `/buildings/0/inputBuffer/1/capacity`;
- `entryId`: when known;
- `message`: one line of text.

Structural failures report the first violation together with its line and column. Semantic failures report every violation in document order, up to 100. The output never includes absolute file-system paths or player data. The bootstrap logs the diagnostic list once, through `DisableWithError`.

## Load outcome

`ContentLoadResult` is either `Loaded(DefinitionCatalog)` or `Rejected(IReadOnlyList<ContentDiagnostic>)`. It never returns a partial catalog. On `Rejected`, the bootstrap:

- creates no `FactorySession`;
- does not open the checkpoint store;
- does not fall back to another source;
- shows the existing initialization error state.

## Evolution and compatibility

- A new content family (costs, research, items, prestige, contracts), a new required field, or a change in meaning requires a separate approved specification and `contentSchemaVersion` 2. Reader support for each version is explicit, so version 1 documents keep their exact meaning.
- Save V1 stores IDs and policy revisions. Removing or renaming a referenced ID, or changing its meaning, is a save-compatibility change. It needs an owner decision and a Save migration plan (FR-017). Adding new IDs is compatible.
- `F001SpriteCatalog` owns presentation assets. Every `presentationKey` must resolve there, otherwise start-up fails with `presentation.key.unresolved`.

# Gameplay Content Authoring Guide (JSON, v1)

This is the author guide for human and AI editors of the packaged gameplay content document. It is written to agree
exactly with the normative contract, `specs/002-json-content-authoring/contracts/gameplay-content-v1.md`. If the two
ever disagree, the contract wins and this file must be corrected to match — do not resolve a disagreement by editing
only one of them.

The domain code is authoritative over both documents. Every rule below is a direct reading of
`Assets/IdleFactory/Core/Domain/Definitions.cs`, `Assets/IdleFactory/Core/Domain/DefinitionCatalog.cs` and
`Assets/IdleFactory/Core/Domain/Identifiers.cs`.

This guide describes content version 1, which is now the legacy import catalog. The gameplay content document is
version 2: see [Gameplay Content Authoring Guide (JSON, v2)](#gameplay-content-authoring-guide-json-v2) below.

## What this document is for

The game reads one packaged JSON document at start-up and builds an immutable `DefinitionCatalog` from it. This guide
explains the document's shape, its rules, its diagnostics, and how to check a document before committing it. It does
not explain runtime simulation, save files, or anything outside the content contract.

## Encoding and envelope

- UTF-8, **without** a byte order mark, strict decoding.
- At most 1 MiB (1,048,576 bytes).
- JSON nesting depth at most 16 (content itself only nests to depth 5; the limit is a generous safety guard, not a
  target).
- Exactly one root JSON object. Only whitespace is allowed after it — no trailing content, no second document.
- Numbers are JSON integers written without a fraction or exponent (`50000000`, not `5.0e7` or `50000000.0`).
- Comments are not allowed. JSON has no comment syntax, and none is added.
- Every object member is required unless this guide marks it optional. **Unknown members and duplicate members are
  rejected at every level** — do not add extra fields "for documentation" or "for future use".
- `null` is never valid for any field, required or optional. An optional field is *omitted*, never written as `null`.

## Units

- Resource amounts and buffer capacities are **whole resource units** — always non-fractional integers.
- Time (`durationTicks`, `maxCreditedTicks`) is expressed in **100 ns ticks**.
  - `50,000,000` ticks = 5 seconds.
  - `288,000,000,000` ticks = 8 hours.
  - 1 second = 10,000,000 ticks.
- An output `capacity` of `0`, or any capacity below the recipe's output `amount`, blocks production: a recipe
  cannot start while the building's free output capacity is less than the output amount
  (`ProductionBlockerKind.InsufficientOutputCapacity`, `FactoryState.cs`, `ProductionSimulator.cs`). The definition
  is still valid; the building simply never produces.
- **Balance warning.** Saves store only IDs and the offline-policy revision, not the numbers behind them. Changing
  `durationTicks`, an `amount`, or a `capacity` on an *existing* ID therefore silently changes existing players'
  progress on their next load. Such an edit needs its own balance decision or specification; it is a separate
  concern from the FR-017 rule about ID and revision meaning (see "Evolution and Save V1 compatibility").

## Document shape (v1)

The top-level object has exactly five members, in this order:

```json
{
  "contentSchemaVersion": 1,
  "resources": [ ],
  "recipes": [ ],
  "buildings": [ ],
  "offlinePolicies": [ ]
}
```

## IDs

An **ID** is a JSON string that:

- is 1–128 UTF-16 code units long;
- is unchanged by trimming leading/trailing whitespace and applying Unicode Normalization Form C (NFC) — in other
  words, it must already be trimmed and already NFC-normalized as written in the document;
- is compared to every other ID in its family by case-sensitive ordinal equality (`"Dev.Input"` and `"dev.input"` are
  different IDs; there is no case folding and no locale-aware comparison);
- is unique within its family (`resources[].id`, `recipes[].id`, `buildings[].id` are each their own family);
- is never derived from a label, a file name, or an asset path — it is always written out explicitly.

`resourceId` references inside recipes and buffers, and `recipeId` references inside buildings, are the same ID type
and follow the same rules; they must match an `id` declared elsewhere in the document (or, for `recipeId`, be
omitted).

## Fields

### Top level

| Field | Type / range | Required | Meaning |
|---|---|---|---|
| `contentSchemaVersion` | integer, must be `1` | yes | Content contract version. Read first, independent of the Save V1 checkpoint `schemaVersion`. |
| `resources` | array, 1–1,024 entries | yes | Resource definitions. |
| `recipes` | array, 1–1,024 entries | yes | Recipe definitions. |
| `buildings` | array, 1–1,024 entries | yes | Building definitions. |
| `offlinePolicies` | array, 1–1,024 entries | yes | Offline policy definitions, keyed by `revision`. |

### `resources[]`

| Field | Type / range | Required | Meaning |
|---|---|---|---|
| `id` | ID | yes | Stable resource ID. This is the save identity referenced by Save V1. |
| `developmentLabel` | non-blank string ≤ 128 UTF-16 code units | yes | Development-only display label. It is **not** an ID — no uniqueness or normalization rule applies to it beyond being non-blank. |
| `presentationKey` | non-blank string ≤ 128 or omitted | optional | Key into `F001SpriteCatalog`. Optional because `ResourceDefinition`'s constructor accepts `null` (`DefinitionGuard.OptionalText`). |
| `isDevelopmentOnly` | boolean | yes | Development content flag. |

### `recipes[]`

| Field | Type / range | Required | Meaning |
|---|---|---|---|
| `id` | ID | yes | Stable recipe ID. |
| `inputs` | array, 1–256 entries, each `{ "resourceId": ID, "amount": int }`; `resourceId` unique within `inputs` | yes | Whole-unit resources consumed per craft. |
| `outputs` | array, 1–256 entries, same shape; `resourceId` unique within `outputs` | yes | Whole-unit resources produced per craft. |
| `inputs[].amount`, `outputs[].amount` | int64, 1 … 9,223,372,036,854,775,807 | yes | Whole resource units. Must be positive. |
| `durationTicks` | int64 ≥ 1 | yes | Craft duration in 100 ns ticks (50,000,000 = 5 s). |
| `isDevelopmentOnly` | boolean | yes | Development content flag. |

`inputs` and `outputs` are each their own uniqueness scope — the same `resourceId` may appear once in `inputs` and
once in `outputs` (e.g. a recipe that both consumes and produces the same resource is not itself forbidden by this
rule), but it may not repeat within one side.

### `buildings[]`

| Field | Type / range | Required | Meaning |
|---|---|---|---|
| `id` | ID | yes | Stable building ID. |
| `footprintWidth`, `footprintHeight` | int32 ≥ 1 | yes | Footprint in grid cells. |
| `allowedOrientations` | array, 1–4 unique integers, each in 0–3 | yes | Quarter turns the building may be placed at. |
| `recipeId` | ID, must match a declared `recipes[].id`, or omitted | optional | The recipe this building produces. A building with a `recipeId` is a *producing* building and requires both `inputBuffer` and `outputBuffer`. |
| `inputBuffer`, `outputBuffer` | array of 1–256 `{ "resourceId": ID, "capacity": int64 }`, or omitted | optional | Omitted means "no buffer" (mirrors the ScriptableObject-era `hasInputBuffer: false`). When present, `capacity` is int64 ≥ 0 and `resourceId` is unique within the buffer. Every resource on the recipe's matching side (inputs → `inputBuffer`, outputs → `outputBuffer`) must be declared in the buffer. |
| `presentationKey` | **non-blank** string ≤ 128 | **yes** | Key into `F001SpriteCatalog`. Required because `BuildingDefinition`'s constructor calls `DefinitionGuard.RequiredText` — unlike resources, there is no optional overload. Omitting it produces `member.missing`. |
| `isDevelopmentOnly` | boolean | yes | Development content flag. |

`buildings[].presentationKey` being required (while `resources[].presentationKey` is optional) is intentional and
code-derived, not a typo — see `Definitions.cs`: `ResourceDefinition` uses `DefinitionGuard.OptionalText`,
`BuildingDefinition` uses `DefinitionGuard.RequiredText`.

### `offlinePolicies[]`

| Field | Type / range | Required | Meaning |
|---|---|---|---|
| `revision` | int32 ≥ 1 | yes | Offline policy revision, referenced by Save V1. Unique across `offlinePolicies`. |
| `efficiencyNumerator` | int64 ≥ 1 | yes | Offline efficiency ratio numerator. Must be strictly less than `efficiencyDenominator`. |
| `efficiencyDenominator` | int64 ≥ 1 | yes | Offline efficiency ratio denominator. |
| `maxCreditedTicks` | int64 ≥ 1 | yes | Credited offline cap in 100 ns ticks (288,000,000,000 = 8 h). |

**Currently accepted policy set**: the catalog accepts *exactly* one policy, revision 1 with `efficiencyNumerator: 4`,
`efficiencyDenominator: 5`, `maxCreditedTicks: 288000000000` (`DefinitionCatalog.ValidateSupportedPolicy`). Any other
set — a second revision, different numbers on revision 1, or a missing revision 1 — is rejected with
`policy.revision-1.required`. This is a runtime rule, not a formatting rule; it is enforced regardless of canonical
form.

**Offline economics.** `OfflineReconciler` (`Assets/IdleFactory/Core/Application/OfflineReconciler.cs`) credits
`effective credited time = floor(min(interval, maxCreditedTicks) × efficiencyNumerator / efficiencyDenominator)`,
where `interval` is the measured absence in ticks (a negative interval credits 0). The numerator/denominator pair is
the fraction of the *capped* absence time that is credited. Revision 1 (4/5, 288,000,000,000 ticks = 8 h) is a
**locked economy invariant**: the catalog accepts exactly these values, and a different revision needs its own
specification.

## Canonical form (review gate, not a runtime rule)

The runtime does not reject non-canonical formatting — whitespace has no semantic meaning to the loader. A separate
Edit Mode review-gate test does fail when the packaged document differs from its canonical rendering, so authors
should still follow it:

- Two-space indentation, LF line endings, a final newline.
- Object members appear in the order listed in the Fields tables above (`contentSchemaVersion`, `resources`,
  `recipes`, `buildings`, `offlinePolicies` at top level; each nested object in the order shown in its table).
- `resources`, `recipes`, and `buildings` are sorted by `id` (ordinal). `offlinePolicies` are sorted by `revision`.
- Nested amount/capacity entries (`inputs`, `outputs`, `inputBuffer`, `outputBuffer`) are sorted by `resourceId`
  (ordinal). `allowedOrientations` is sorted ascending.
- Optional members are omitted rather than written as `null`. `null` is always rejected, canonical or not.

ID text itself is not a formatting concern: a non-canonical ID (one that trim + NFC would change) is a **runtime**
rejection (`id.not-canonical`), not just a review-gate failure.

## Diagnostics

Every diagnostic reported by the loader has:

- `code` — one of the codes below;
- `path` — a JSON Pointer into the document, e.g. `/buildings/0/inputBuffer/1/capacity`;
- `entryId` — the entry's `id` when it is known at the point of failure;
- `message` — one line of human-readable text.

Structural failures (malformed JSON, wrong types, unknown members, etc.) stop at the **first** violation, reported
with its line and column, because the rest of the stream cannot be trusted after a lexical error. Semantic failures
(everything past a clean structural parse) are collected in full — every violation, in document order — up to 100,
with a truncation note beyond that. No diagnostic ever includes an absolute file-system path or player data.

| Code | Meaning |
|---|---|
| `content.too-large` | Document exceeds 1 MiB. |
| `content.empty` | Document is empty. |
| `content.encoding` | Bytes are not strict UTF-8, or a byte order mark is present. |
| `content.version.missing` | `contentSchemaVersion` is absent. |
| `content.version.unsupported` | `contentSchemaVersion` is present but not `1`. |
| `json.malformed` | The bytes are not well-formed JSON. |
| `json.trailing-content` | Non-whitespace content follows the root value. |
| `json.depth` | Nesting exceeds depth 16. |
| `member.unknown` | An object has a member not defined for its type. |
| `member.duplicate` | The same member name appears twice in one object. |
| `member.missing` | A required member is absent (e.g. an omitted `buildings[].presentationKey`). |
| `value.type` | A value's JSON type does not match the expected type (e.g. a string where an integer is required). |
| `value.not-integer` | A number has a fraction or exponent where an integer is required. |
| `value.out-of-range` | An integer is outside its documented range. |
| `value.null` | A `null` appears where any value (required or optional) is expected. |
| `collection.empty` | A required non-empty array (or one whose "present but empty" state is disallowed) has zero entries. |
| `collection.too-many` | An array exceeds its maximum entry count (1,024 top-level, 256 nested). |
| `id.empty` | An ID is an empty string. |
| `id.too-long` | An ID exceeds 128 UTF-16 code units. |
| `id.not-canonical` | An ID's value changes under trim + Unicode NFC (it was not written already-normalized). |
| `id.duplicate` | Two entries in the same family declare the same ID after normalization. |
| `ref.resource.unknown` | A `resourceId` does not match any declared `resources[].id`. |
| `ref.recipe.unknown` | A `recipeId` does not match any declared `recipes[].id`. |
| `recipe.amount.non-positive` | An input or output `amount` is ≤ 0. |
| `recipe.duration.non-positive` | `durationTicks` is ≤ 0. |
| `recipe.resource.duplicate` | The same `resourceId` appears twice within one recipe's `inputs` or within its `outputs`. |
| `recipe.inputs.empty` | `inputs` is present but empty. |
| `recipe.outputs.empty` | `outputs` is present but empty. |
| `footprint.non-positive` | `footprintWidth` or `footprintHeight` is ≤ 0. |
| `orientation.invalid` | An `allowedOrientations` entry is outside 0–3. |
| `orientation.duplicate` | The same orientation value appears twice. |
| `orientation.empty` | `allowedOrientations` is empty. |
| `buffer.capacity.negative` | A buffer `capacity` is < 0. |
| `buffer.resource.duplicate` | The same `resourceId` appears twice within one buffer. |
| `buffer.empty` | A present `inputBuffer`/`outputBuffer` has zero entries. |
| `buffer.missing-recipe-resource` | The recipe references a resource on a side whose buffer does not declare it. |
| `building.producing-requires-buffers` | A building with a `recipeId` is missing `inputBuffer` and/or `outputBuffer`. |
| `policy.revision.non-positive` | `revision` is ≤ 0. |
| `policy.revision.duplicate` | Two offline policies declare the same `revision`. |
| `policy.ratio.invalid` | `efficiencyNumerator`/`efficiencyDenominator` is non-positive, or numerator ≥ denominator. |
| `policy.cap.non-positive` | `maxCreditedTicks` is ≤ 0. |
| `policy.revision-1.required` | The offline policy set is not exactly revision 1 with 4/5 and 288,000,000,000 ticks. |
| `content.internal` | A domain constructor rejected a graph the semantic validator accepted — a validator gap, not an authoring mistake. |
| `presentation.key.unresolved` | A `presentationKey` does not resolve in `F001SpriteCatalog` (checked by the Unity composition after the catalog is built). |

Every semantic code above corresponds to an existing throw in `Definitions.cs` or `DefinitionCatalog.cs`
(constructors, `ValidateGraph`, `ValidateSupportedPolicy`). The JSON validator adds no new domain rule beyond the
authoring-only guards `id.too-long`, `id.not-canonical`, the size/collection limits, and the structural codes.

## Load outcome

Loading a document produces exactly one of:

- `Loaded(DefinitionCatalog)` — every rule passed; the catalog is built and published.
- `Rejected(diagnostics)` — one or more diagnostics as above.

There is never a partial catalog. On `Rejected`, the bootstrap:

- creates no `FactorySession`;
- does not open the checkpoint store;
- does not fall back to another content source;
- shows the existing initialization error state, having logged the diagnostic list once.

## A complete, valid, annotated example

The example below is the full accepted Feature 001 development content (FR-011), reproduced with every resource the
recipe and building reference actually declared, so it validates on its own. (The abbreviated illustration in the
normative contract intentionally omits `dev.output` from `resources` to demonstrate `ref.resource.unknown` — this
version does not.) These are the same values the migrated packaged document
(`Assets/IdleFactory/Content/Development/development-catalog.json`, task T074) carries, and the same values the
parity fixture checks. Annotations are given as prose below the block, since JSON itself has no comment syntax.

```json
{
  "contentSchemaVersion": 1,
  "resources": [
    {
      "id": "dev.input",
      "developmentLabel": "Input",
      "presentationKey": "F001-RESOURCE-DEV-INPUT",
      "isDevelopmentOnly": true
    },
    {
      "id": "dev.output",
      "developmentLabel": "Output",
      "presentationKey": "F001-RESOURCE-DEV-OUTPUT",
      "isDevelopmentOnly": true
    }
  ],
  "recipes": [
    {
      "id": "dev.recipe.basic",
      "inputs": [
        { "resourceId": "dev.input", "amount": 2 }
      ],
      "outputs": [
        { "resourceId": "dev.output", "amount": 1 }
      ],
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
      "inputBuffer": [
        { "resourceId": "dev.input", "capacity": 4 }
      ],
      "outputBuffer": [
        { "resourceId": "dev.output", "capacity": 1 }
      ],
      "presentationKey": "F001-MACHINE-D001-NORMAL",
      "isDevelopmentOnly": true
    }
  ],
  "offlinePolicies": [
    {
      "revision": 1,
      "efficiencyNumerator": 4,
      "efficiencyDenominator": 5,
      "maxCreditedTicks": 288000000000
    }
  ]
}
```

Walking through it:

- `contentSchemaVersion: 1` — the only version this reader accepts today.
- `resources`: two entries, `dev.input` and `dev.output`, sorted by ID (`dev.input` < `dev.output` ordinally). Both
  carry a `presentationKey` here, but that field is optional for resources — it could be omitted for a resource with
  no sprite yet.
- `recipes`: one entry, `dev.recipe.basic`. It consumes 2 units of `dev.input` and produces 1 unit of `dev.output`
  every 50,000,000 ticks (5 seconds). Both `resourceId`s used here are declared in `resources`, so `ref.resource.unknown`
  does not fire.
- `buildings`: one entry, `dev.machine.basic`. It is a 2×2 footprint building allowed only at orientation `0`
  (no rotation). Its `recipeId` points at `dev.recipe.basic`, so it is a *producing* building and must (and does)
  carry both `inputBuffer` and `outputBuffer`, each covering the recipe's resource on the matching side
  (`dev.input` in, `dev.output` out) with capacity ≥ the recipe's per-craft amount (not itself a validated
  relationship, but good authoring practice). `presentationKey` is required here — buildings never omit it.
- `offlinePolicies`: exactly the one currently accepted set — revision 1, 4/5 efficiency, an 8-hour cap
  (288,000,000,000 ticks). A document with any other policy set is rejected with `policy.revision-1.required`.

## Walkthrough: adding a resource and a recipe

This is a worked example of the *editing motion* only. The specific IDs, labels, and presentation keys below are
**hypothetical placeholders** — do not commit content shaped like this without its own approved specification (a new
resource/recipe family, or new instances of the existing development family, both need a feature spec and, per
FR-005, in the case of a genuinely new *family* also a `contentSchemaVersion` bump). This walkthrough only shows the
mechanics of extending the existing `resources`/`recipes` families with a same-shape entry.

1. **Choose a stable ID.** Pick something that will never need to change meaning later (see Evolution below). For
   example `dev.gear` — lowercase, dotted, already trimmed and NFC-normal as written, well under 128 code units.
2. **Add the resource**, keeping `resources` sorted by `id`:

   ```json
   {
     "id": "dev.gear",
     "developmentLabel": "Gear",
     "presentationKey": "F001-RESOURCE-DEV-GEAR",
     "isDevelopmentOnly": true
   }
   ```

   `presentationKey` here must already resolve in `F001SpriteCatalog`, or start-up fails with
   `presentation.key.unresolved` — adding the JSON entry and adding the presentation asset are two coordinated
   changes, not one.
3. **Add a recipe that references it**, keeping `recipes` sorted by `id`:

   ```json
   {
     "id": "dev.recipe.gear",
     "inputs": [
       { "resourceId": "dev.input", "amount": 3 }
     ],
     "outputs": [
       { "resourceId": "dev.gear", "amount": 1 }
     ],
     "durationTicks": 100000000,
     "isDevelopmentOnly": true
   }
   ```

   `dev.recipe.gear` consumes 3 `dev.input` and produces 1 `dev.gear` every 100,000,000 ticks (10 seconds). Every
   `resourceId` used (`dev.input`, `dev.gear`) must already be declared in `resources` — order the addition so the
   resource entry lands in the same change as (or before) the recipe that references it.
4. **If a building should produce it**, update that building's `inputBuffer`/`outputBuffer` to cover the new recipe's
   resources on each side, and re-sort the buffer entries by `resourceId`.
5. **Run the authoring check** (below) before committing.
6. **Re-run canonical formatting** (two-space indent, LF, sorted arrays) so the review-gate test stays green.

## Authoring check

Before committing a change to the packaged content document, validate it with the Edit Mode authoring-check test,
pointed at the document you edited via an environment variable:

```
IDLEFACTORY_CONTENT_CHECK_PATH=<absolute path to the document> scripts/test-unity.ps1 -TestFile Assets/IdleFactory/Tests/EditMode/GameplayContentAuthoringCheckTests.cs
```

On Windows PowerShell:

```powershell
$env:IDLEFACTORY_CONTENT_CHECK_PATH = "C:\path\to\your\document.json"
scripts/test-unity.ps1 -TestFile Assets/IdleFactory/Tests/EditMode/GameplayContentAuthoringCheckTests.cs
```

`IDLEFACTORY_CONTENT_CHECK_PATH` must be an absolute path. The test reads exactly this environment variable name —
it is not configurable per invocation any other way.

Caveats:

- A pass is only meaningful when `IDLEFACTORY_CONTENT_CHECK_PATH` is set. Confirm the variable is set in the shell
  that runs the test, and that the runner output refers to the document you edited.
- The check does not verify `presentationKey` values. They are resolved against `F001SpriteCatalog` at start-up, so
  an unresolved key passes the check and still fails start-up with `presentation.key.unresolved`.

## Future content families

This contract (v1) defines exactly four families: `resources`, `recipes`, `buildings`, `offlinePolicies`. It adds
**no** fields for costs, research, items, prestige, or contracts — not even placeholders. Per FR-005, each of those
families is added only by:

1. its own approved feature specification, defining its shape, rules, and diagnostics the same way this document
   does for v1; and
2. a new `contentSchemaVersion` (2 or later), with explicit reader support for each version so that existing v1
   documents keep their exact current meaning.

Do not anticipate these families by adding unused fields, comments-as-placeholders, or speculative schema entries —
`member.unknown` would reject them today, and doing so anyway (via a reader change) would violate FR-005.

## Evolution and Save V1 compatibility

- A new content family, a new required field, or any change in an existing field's meaning requires a new
  `contentSchemaVersion` and its own approved specification, as above.
- **Save V1 stores IDs and offline-policy revisions.** Removing or renaming an ID that an existing checkpoint
  references, or changing what that ID or a policy revision means, is a **save-compatibility change** (FR-017). It
  requires an explicit owner decision and a Save migration plan before it can ship — it is not something a content
  edit alone may do. Adding new IDs is always compatible.
- `F001SpriteCatalog` owns presentation assets and is authored outside this document. Every non-omitted
  `presentationKey` (resources' optional key, or a building's required key) must resolve there; an unresolved key
  fails start-up with `presentation.key.unresolved`.
- The currently accepted offline policy set is exactly **revision 1, 4/5 efficiency, 288,000,000,000 ticks (8
  hours)**. Adding a second policy revision, or changing revision 1's numbers, is rejected by
  `policy.revision-1.required` until the domain code (`DefinitionCatalog.ValidateSupportedPolicy`) is itself changed
  under its own reviewed change — this document does not gate that decision, it only describes the current rule.

---

# Gameplay Content Authoring Guide (JSON, v3)

This section is the author copy of `specs/005-economy-contracts/contracts/gameplay-content-v3.md` (Feature 005), which
evolves the Feature 003 contract `specs/003-first-factory-prestige/contracts/gameplay-content-v2.md` in place: everything the
v3 contract does not change keeps the v2 shape and rules. The two must agree; until this section is accepted the contract wins,
and a disagreement is resolved by correcting this section, never by editing only one file. The documentation-only schema is
`Assets/IdleFactory/Content/Schemas/gameplay-content-v3.schema.json` (JSON Schema draft 2020-12). It describes the
structure and closed vocabularies; the C# reader and validator remain the only runtime authority, and
`RunContentSchemaConsistencyTests` checks this section, the schema and the reader's constants against each other.

The packaged v3 document is `Assets/IdleFactory/Content/Iron/iron-factory-content.json`. It drives the whole Iron
factory run: resources, recipes, machines, expansion tables, contracts, Items, Research, Overdrive, Prestige,
Industry upgrades and the offline policy. It is schema version 3 with **revision 2 values**: the numbers are the F003
balance, expressed in the v3 members (see "v3: what changed from v2").

## v3: what changed from v2

`contentSchemaVersion` is `3`. The reader evolved in place; a version 2 document is rejected with
`content.version.unsupported`. Members that v3 adds are required unless marked optional. The members that v3 removes
(`kind` on a contract, `rewards` on a contract, `thresholdFactoryValue` on `prestige`) are unknown members now and fail
with `member.unknown`.

| Where | v3 change |
|---|---|
| `runStart` | `sellSteps` (integers, optionally ending with `"all"`), `liveGapThresholdMs`, `manualMining`, `minRunDurationMs`, `uptimeMaxPpm` |
| `machines[]` | `buildPrice` (`base`, `ownedGrowthPpm`, `prestigeGrowthPpm`, `cap`); `cost` keeps meaning the upgrade cost |
| `contracts[]` | `kind` removed; `objective`, `class`, `rewardRatioPpm`, `cooldownMs`, `rpReward` added; `bonus` optional; `rewards` removed |
| `contractItemLadder` | new top-level object (`entries`, `perRunCap`); Items come from the ladder instead of per-offer rewards |
| `prestige` | `thresholdFactoryValue` replaced by `threshold` (`base`, `growthPpm`, `maxIndex`) |
| `milestones` | new top-level list of `fractionPpm` entries |
| `industryUpgrades[]` | `decayPpm`, `practicalCeilingLevel`, `minGainPpm` added |
| top level | `chainDominanceMarginPpm` added |

**Transitional reward adapter.** Until task T320 replaces the reward formula, `RunCatalogBuilder` synthesizes the F003
reward definitions from the v3 members so the delivery path keeps working: the Cash reward at completion index `i` is
`ceil(quantity[i] × basePrice × rewardRatioPpm / 10^6)` in checked integer math (an overflow is `contract.reward.ratio`),
the RP reward is the `rpReward` curve, and each `contractItemLadder` entry becomes a completion index of an Item reward on
the standing delivery offer that owns the Item's tier (standing delivery offers ordered by the price of their resource,
then by `id`, take tiers 1, 2 and 3 in turn). `perRunCap` is not applied by the adapter. T320 removes the adapter.

**Structural rules (T314).** The validator implements `sell.steps`, `live-gap.range`, `contract.bonus.fields`,
`contract.ratio.standing`, `contract.ratio.bonus`, `contract.quantity.cap`, `contract.produce.min`, `milestone.order`,
`build.base.bound` and `cost.floor`, plus `build.price.cap` (T323), and extends `speed.level.dead`; "v3: structural rules" below gives each predicate. The
story rules `contract.ratio.produce`, `contract.quantity.capacity`, `ladder.uptime`, `start.reachable` and
`manual.yield.bound` are not implemented yet (`chain.dominance` landed with T316, `prestige.threshold.bound` and `industry.ceiling.level` with T331);
the v3 contract lists them. The semantic F005 rules apply only from `contentRevision` 3, and the shipped content stays at
revision 2 values. The `manualYield` effect kind belongs to the manual-mining task and is rejected as
`effect.kind.unknown` until then.

## v3: version dispatch

`contentSchemaVersion` is read first.

| Value | Route | Role |
|---|---|---|
| `1` | The unchanged Feature 002 loader (sections above) | **Legacy import catalog** only. It validates Save V1 during import and is never the gameplay catalog. |
| `3` | `RunContentReader` → `RunContentValidator` → `RunCatalogBuilder` | **Gameplay content** (this section). |
| anything else (including `2`) | rejected | `content.version.unsupported` |

## v3: carried over from v1 unchanged

Everything in "Encoding and envelope", the ID rules, "Diagnostics" format and "Load outcome" applies to v3 exactly:

- Strict UTF-8 without a BOM, at most 1 MiB, JSON depth at most 16, integers only (no fraction or exponent), no `null`,
  no comments. Unknown, duplicate and missing members are rejected at every level.
- At most 1,024 entries per top-level family and 256 per nested list.
- IDs are 1–128 UTF-16 code units, unchanged by trim and Unicode NFC (`id.not-canonical` otherwise), compared by
  case-sensitive ordinal equality, unique within their family.
- Diagnostics carry `code`, a JSON Pointer path, `entryId` and a message. A structural failure reports the first
  violation with line and column; semantic failures report every violation, up to 100, in document order.
- Loading is all-or-nothing.

## v3: numbers, ppm and rounding (R06)

- Resource units, Cash, RP, IP, Item counts and every amount in this document are integers in `0 … 10^18`
  (`ExactBound`). Any value that would leave this range rejects the document. A table value above `10^18` is
  `curve.overflow`.
- Multipliers are integer **parts per million (ppm)**: `1,000,000` means ×1. A speed, sale price or growth value is
  written as an integer ppm (`1080000` is ×1.08).
- Cash is whole dollars. Time inside recipes and the minimum cycle is in 100 ns ticks (`20000000` = 2 s); contract,
  Item and Overdrive times are in milliseconds (`durationMs`).
- **Curves** `{ "base", "growthPpm", "rounding": "ceil" | "floor" }` are evaluated iteratively per index:
  `v₀ = base`, `vₙ₊₁ = round(vₙ × growthPpm / 10^6)`, where `round` is the declared `rounding`. The index is the level
  for costs and the completion count for contracts. The catalog precomputes a table up to `maxLevel` (costs) or 1,024
  entries (contracts). A flat curve is `growthPpm = 1000000`; a growing curve that does not strictly increase at
  every step is rejected as `curve.stalled` (for example `floor` with a small base and a small growth).
- Sale income is `MulDivFloor(units × basePrice, salePricePpm, 10^6)` per sale. Each sale floors separately.

## v3: revision and balance-hypothesis rules

- `contentRevision` is an integer ≥ 1 that versions the balance of the document inside `contentSchemaVersion` 3. The first
  F005 tuning is revision 3; the shipped schema-3 document keeps the revision 2 values, and the semantic F005 rules are gated
  on revision ≥ 3.
- Adding IDs and retuning numbers is a new `contentRevision`. It is save-compatible because Save V2 rescales progress
  on load. Removing or renaming an ID that a Save V2 references needs a save migration decision (FR-017).
- A new effect kind, unlock kind, contract objective or class, or curve kind, or a new required member, needs code and a new
  `contentSchemaVersion`. Ordinary tuning is content-only; a new mechanic is not.
- **Every number in the document is a balance hypothesis** until the balance approval (R13). The FR-002, FR-007,
  FR-012 and FR-021 targets are authoritative; all other values come from the versioned initial content sheet.
  Do not treat an unapproved number as a promise, and record every retune in a new `contentRevision` with its reason.

## v3: document shape

The top level has exactly these members, in this order:

```json
{
  "contentSchemaVersion": 3,
  "contentRevision": 1,
  "runStart": { },
  "resources": [ ],
  "recipes": [ ],
  "machines": [ ],
  "storageLevels": [ ],
  "factoryLevels": [ ],
  "contracts": [ ],
  "contractItemLadder": { },
  "items": [ ],
  "research": [ ],
  "overdrive": { },
  "prestige": { },
  "milestones": [ ],
  "industryUpgrades": [ ],
  "chainDominanceMarginPpm": 0,
  "offlinePolicies": [ ]
}
```

All members are required. `research`, `industryUpgrades` and `milestones` (at most 64 entries) may be empty arrays; every other
array needs at least one entry, except `contractItemLadder.entries` (which may be empty, at most 1,024 entries).
`industry.first-affordable.missing` applies when Industry upgrades exist.

## v3: fields

Every member of an object is required unless the table says otherwise (the only optional members in v3 are a contract's
`durationMs` and `bonus`). `id`,
`label` and `presentationKey` follow the v1 rules (non-blank, at most 128 UTF-16 code units, the v1 limit carried over); a `label` is non-blank display text. Amount-like integers are in
`0 … 10^18`.

### Shared shapes

**Curve** (`cost`, `quantity`, `rpReward`, `rpCost`, `ipCost`):

| Field | Type / range | Meaning |
|---|---|---|
| `base` | int 0 … 10^18 | `v₀` |
| `growthPpm` | int ≥ 1,000,000 | Growth in ppm; `1000000` is flat (`curve.growth.invalid`, `curve.stalled`) |
| `rounding` | `"ceil"` or `"floor"` | Rounding of each step |

**Unlock** (closed vocabulary, evaluated at event and command timestamps, monotonic within a run):

| `kind` | Extra members |
|---|---|
| `always` | none |
| `researchPurchased` | `id` (a Research ID) |
| `factoryLevelAtLeast` | `level` (int ≥ 1) |
| `resourceProduced` | `id` (a resource ID) |

Any other `kind` is rejected. Resource entries, machines, contracts and Overdrive each carry an `unlock`.

**Amount entry** (recipe `inputs` and `outputs`): `{ "resourceId": ID, "amount": int 1 … 10^18 }`, sorted by
`resourceId`, no resource twice in one list (`recipe.resource.duplicate`, `recipe.amount.non-positive`).

### `runStart`

| Field | Type / range | Meaning |
|---|---|---|
| `startingCash` | int 0 … 10^18 | Cash at the start of every run before Industry bonuses |
| `initialUnlocks` | array of IDs, sorted | Unlocked at every run start (`runstart.unlock.unknown`) |
| `removalRefundPpm` | int 0 … 999,999 | Refund share on machine removal (`runstart.refund.out-of-range`) |
| `minCycleTicks` | int ≥ 1,000,000 | Minimum effective cycle in ticks (`runstart.min-cycle.invalid`) |
| `maxCombinedSpeedPpm` | int 10^6 … 10^12 | Overflow bound for the composed speed (`runstart.speed-bound.invalid`) |
| `maxCombinedOutputFactor` | int ≥ 2 | Upper bound of the combined output factor (`runstart.output-bound.invalid`) |
| `maxRewardRatioPpm` | int ≥ 1,000,000 | Maximum Cash reward to market ratio (`runstart.reward-ratio.invalid`) |
| `sellSteps` | array of integers, optionally ending with the string `"all"` | The sell step sizes. The reader accepts at most 64 integers and `"all"` only as the last entry; the validator requires 1 … 8 distinct, strictly ascending integers, each 1 … 10^18 (`sell.steps`) |
| `liveGapThresholdMs` | int 1,000 … 28,800,000 | Wall-clock gap above which a live frame counts as a suspension (`live-gap.range`; 28,800,000 ms is 8 hours) |
| `manualMining` | object | Hand-mining parameters, below |
| `minRunDurationMs` | int 0 … 10^18 | Minimum run duration for the later rule `ladder.uptime` |
| `uptimeMaxPpm` | int 0 … 10^18 | Maximum Item uptime for the later rule `ladder.uptime` |

`manualMining` is `{ "resourceId": ID, "baseYieldMilli": int ≥ 1, "maxActivationsPerSecond": int 1 … 20, "startWindowMs": int ≥ 0,
"referenceMinerLevel": int ≥ 1, "maxShareOfMinerPpm": int 0 … 3,000,000, "miningAdvantageMaxPpm": int ≥ 0 }`. The resource must
exist (`ref.resource.unknown`); a number outside its range is `value.out-of-range`. The rules `start.reachable` and
`manual.yield.bound` come with the manual-mining task.

### `resources[]`

| Field | Type / range | Meaning |
|---|---|---|
| `id` | ID | Stable ID |
| `label` | string | Display text |
| `presentationKey` | string | Sprite catalog key (`presentation.key.unresolved` at start-up) |
| `basePrice` | int 0 … 10^18 | Cash per unit (`resource.price.negative`) |
| `unlock` | Unlock | When the resource becomes visible |

### `recipes[]`

| Field | Type / range | Meaning |
|---|---|---|
| `id` | ID | Stable ID |
| `inputs` | array of amount entries | **May be empty** (the input-free Miner) |
| `outputs` | array of amount entries | At least one (`recipe.outputs.empty`) |
| `durationTicks` | int 1 … 10^12 | Recipe duration in 100 ns ticks (`recipe.duration.non-positive`, `recipe.duration.out-of-range`) |

Every `resourceId` must exist (`ref.resource.unknown`).

### `machines[]`

| Field | Type / range | Meaning |
|---|---|---|
| `id`, `label`, `presentationKey` | as above | |
| `recipeId` | ID | The recipe the machine runs (`ref.recipe.unknown`) |
| `maxLevel` | int ≥ 1 | Highest level (`machine.max-level.invalid`) |
| `cost` | Curve | Upgrade cost of level `L`, evaluated by level |
| `buildPrice` | object | `{ "base": int 0 … 10^12, "ownedGrowthPpm": int ≥ 1,000,000, "prestigeGrowthPpm": int ≥ 1,000,000, "cap": int 0 … 10^12 }`: the price of building one more machine, growing per owned machine and per prestige (`base` or `cap` above 10^12 is `build.base.bound`, a negative one `value.out-of-range`; a `cap` below `base` is `build.price.cap`) |
| `speedGrowthPpm` | int > 1,000,000 | Per-level speed growth |
| `milestones` | array | Sorted by `level`; each is `{ "level": int 2 … maxLevel, "speedPpm": int > 10^6 }`; levels are unique (`machine.milestone.level-invalid`, `machine.milestone.duplicate`) |
| `unlock` | Unlock | |

`SpeedTable[L]` is the speed growth applied `L−1` times (floor) multiplied by every milestone whose level is `≤ L`, using
`MulDivFloor` in ascending level order. It must strictly increase up to `maxLevel` (`speed.level.dead`).

### `storageLevels[]` and `factoryLevels[]`

| Field | Type / range | Meaning |
|---|---|---|
| `level` | int ≥ 1 | Sorted by `level`, starting at 1, contiguous (`level.sequence.invalid`) |
| `capacity` (storage) | int 1 … 10^18 | Total stock capacity; strictly increasing (`storage.capacity.non-increasing`) |
| `slots` (factory) | int 1 … 12 | Machine slots; strictly increasing (`factory.slots.non-increasing`, `factory.slots.too-many`) |
| `cost` | int 0 … 10^18 | Cash cost; level 1 costs 0 (`level.first-cost.non-zero`) |

### `contracts[]`

| Field | Type / range | Meaning |
|---|---|---|
| `id` | ID | Stable ID |
| `objective` | `"deliver"` or `"produce"` | Closed vocabulary (`contract.objective.unknown`) |
| `class` | `"standing"` or `"bonus"` | Closed vocabulary (`contract.class.unknown`) |
| `resourceId` | ID | The required resource (`ref.resource.unknown`) |
| `quantity` | Curve | Required units by completion count |
| `durationMs` | int, **optional** | A present value makes the offer timed: 1 … 604,800,000 ms = 7 days (`contract.duration.out-of-range`) |
| `rewardRatioPpm` | int 0 … 10^18 | Cash reward ratio in ppm. A delivery offer pays between 1.0 and `maxRewardRatioPpm` times the market value of the required units (`contract.reward.ratio`); the `contract.ratio.*` ranges come with task T314 |
| `cooldownMs` | int 0 … 10^18 | Cooldown after the offer ends |
| `rpReward` | Curve | RP reward by completion count; it may not grow faster than `quantity` (`contract.rp.growth`) |
| `bonus` | object, **optional** | `{ "trigger": Unlock, "windowMs": int ≥ 1 }`. The shape is checked here; that it is present exactly for `class = bonus` is the later rule `contract.bonus.fields` |
| `unlock` | Unlock | |

The Cash reward is not written in the offer any more. Until task T320 the transitional adapter derives it as
`ceil(quantity[i] × basePrice × rewardRatioPpm / 10^6)` for completion index `i` (checked; a table value above 10^18 is
`contract.reward.ratio`). The Items an offer grants come from `contractItemLadder`. The v2 members `kind` and `rewards` are
unknown members. The `objective` and `class` vocabularies are checked by the validator before any catalog object is built.

### `contractItemLadder`

`{ "entries": [ { "index": int 0 … 1023, "itemId": ID }, … ], "perRunCap": int 0 … 10^18 }`. `entries` is sorted by
`index` and then by `itemId`; the same Item may appear at several indices and several Items at one index. An unknown Item is
`contract.reward.item.unknown`, an index outside 0 … 1023, a repeated `(index, itemId)` pair or more than 256 entries for one
Item is `value.out-of-range`. Every Item tier needs an Item that appears in the ladder (`item.tiers.incomplete`). The later rule
`ladder.uptime` bounds the ladder against the Item durations. For the shipped revision 2 document the indices are the F003
per-offer completion indices, which the transitional adapter applies per offer.

### `items[]`

| Field | Type / range | Meaning |
|---|---|---|
| `id`, `label`, `presentationKey` | as above | |
| `tier` | int 1 … 3 | `item.tier.invalid`; every tier needs an Item obtainable from the contract Item ladder (`item.tiers.incomplete`) |
| `durationMs` | int 1 … 604,800,000 (7 days) | Effect duration (`item.duration.non-positive`, `item.duration.out-of-range`) |
| `target` | `{ "kind": "factory" }` or `{ "kind": "machineType", "allowedMachineIds": [ID, …] }` | Non-empty `allowedMachineIds` (`item.target.invalid`); applies to every machine of the chosen type |
| `effect` | `{ "kind": "machineSpeed", "speedPpm": int > 10^6 }` or `{ "kind": "machineOutput", "factor": int 2 … maxCombinedOutputFactor }` | Only these two kinds are allowed for Items |

The conflict key of an active Item effect is `(effect.kind, targetKey)`; at most one effect per key is active.

### `research[]`

| Field | Type / range | Meaning |
|---|---|---|
| `id`, `label` | as above | |
| `maxLevel` | int ≥ 1 | `upgrade.max-level.invalid` |
| `rpCost` | Curve | RP cost by level |
| `prerequisites` | array, sorted by `id` | `{ "id": research ID, "level": int ≥ 1 }` (the JSON member is `id`; the C# definition field in data-model.md is `researchId`, so the reader maps it explicitly) (`research.prerequisite.unknown`, `research.prerequisite.cycle`, `research.prerequisite.level-invalid`) |
| `effect` | Effect | See the effect table |

### `industryUpgrades[]`

Same shape as Research, with `ipCost` in place of `rpCost` and no `prerequisites`, plus `decayPpm` (int 1 … 10^6 from `contentRevision` 3, the per-level decay
multiplier; `1000000` is the explicit "no decay"; 0 is accepted only below revision 3, where it is the legacy "no decay"), `practicalCeilingLevel` (int ≥ 0) and `minGainPpm` (int ≥ 0), written between `ipCost` and
`effect`; a number outside its range is `value.out-of-range`. From revision 3 a `decayPpm` of 0 is rejected as `value.out-of-range` at `/industryUpgrades/<i>/decayPpm`
(owner decision 2026-10-13, #396); below revision 3 it still means no decay, like `1000000`.
Below `1000000` each level's effect decays with iterated floors (T331: multiplicative increment, discount decrement and additive amount per
level; see `industry.ceiling.level` below). At least one level-1
`ipCost` must be affordable with the IP earned at the prestige threshold base (`industry.first-affordable.missing`).

### Effects

Closed vocabulary of six kinds. An unknown kind is `effect.kind.unknown`; a wrong target is `effect.target.invalid`; a
value outside its range is `effect.value.out-of-range`.

| `kind` | Value member | Allowed `target.kind` | Sources |
|---|---|---|---|
| `machineSpeed` | `speedPpm` (Items) / `valuePerLevelPpm` (> 10^6) | `allMachines`, `machine` | Research, Industry, Item |
| `machineOutput` | `factor` (integer 2 … `maxCombinedOutputFactor`) | `machineType` (Items), `machine` | Item, Research |
| `salePrice` | `valuePerLevelPpm` (> 10^6) | `allResources`, `resource` | Research, Industry |
| `upgradeCost` | `valuePerLevelPpm` (10^5 … < 10^6) | `allMachines`, `machine`, `expansion` | Research, Industry |
| `startingCash` | `valuePerLevel` ($) | none | Industry |
| `startingStorageLevel` | `valuePerLevel` (levels) | none | Industry |

A target is written `{ "kind": "allMachines" }`, `{ "kind": "machine", "id": "…" }`, `{ "kind": "allResources" }` or
`{ "kind": "resource", "id": "…" }` or `{ "kind": "expansion" }`. The `expansion` target has no `id`: an `upgradeCost` effect with target `expansion` lowers the upgrade cost of both expansion tracks (storage and factory), because the domain `EffectDef` has no track discriminator. Effects whose kind has no target omit `target` (Item effects carry their target on
the Item itself).

### `overdrive`

`{ "durationMs": int ≥ 1, "cooldownMs": int ≥ 0, "speedPpm": int > 10^6, "unlock": Unlock }` (`overdrive.value.invalid`); `durationMs` and `cooldownMs` are at most 604,800,000 (7 days, `overdrive.duration.out-of-range`, `overdrive.cooldown.out-of-range`).

### `prestige`

`{ "threshold": { "base": int 1 … 10^18, "growthPpm": int ≥ 1,000,000, "maxIndex": int 1 … 10,000 }, "ipCurve": { "kind": "sqrt", "coefficient": int ≥ 1, "divisor": int ≥ 1 } }`
(`prestige.threshold.invalid` for the base, `value.out-of-range` for the other threshold numbers, `prestige.curve.unsupported`). The Factory Value needed at
prestige index 0 is `threshold.base`; later indices grow by `growthPpm` and clamp at `maxIndex` (`maxIndex` above 10,000 is
`value.out-of-range` at `/prestige/threshold/maxIndex`, which bounds the threshold walk to 10,000 steps; owner decision 2026-10-13, #396; the rule
`prestige.threshold.bound` checks the largest threshold). Only `sqrt` is supported: `IP = ISqrt(MulDivFloor(coefficient², FV, divisor))`,
which equals `floor(coefficient × sqrt(FV / divisor))`.

### `milestones[]`

`{ "fractionPpm": int 1 … 999,999 }`, at most 64 entries: prestige milestones as fractions of the threshold. `milestone.order`
requires every fraction inside (0, 10^6) and the list to ascend strictly; an empty list is valid.

### `chainDominanceMarginPpm`

An integer 1 … 10^18 (`value.out-of-range`; it must be greater than 0): the margin of the rule `chain.dominance` (T316).

### `offlinePolicies[]`

As in v1: `revision`, `efficiencyNumerator`, `efficiencyDenominator`, `maxCreditedTicks`. Exactly one policy is
accepted: **revision 1, `4`/`5`, `288000000000` ticks (eight hours)**; anything else is `policy.revision-1.required`. The contract fixes this for all v2 and v3 content, so the schema pins these values (`const`) and allows exactly one entry; a new policy revision needs its own specification and a schema change.

The offline formula is unchanged from Feature 001: credited time is
`E(x) = floor(min(max(x − d, 0), maxCreditedTicks) × 4 / 5)` for an absence from departure `d` to instant `x`. Real
boundaries (Item and Overdrive expiry, contract deadlines) inside the absence are mapped through the same `E`, so
segmenting at boundaries credits exactly what the unsegmented formula credits. Revision 1 is a locked economy invariant;
a sensitivity result never silently changes it.

## v3: speed, batching and cycle rules

- The effective speed composition order is `SpeedTable` → Research → Industry → Items (in `itemId` order) → Overdrive.
  `maxCombinedSpeedPpm` is an overflow bound that valid content never reaches (`speed.level.dead`): the worst-case
  composed speed with every source at its maximum must stay at or below it.
- No cycle is shorter than `minCycleTicks`. With `D₀ = durationTicks`, the batch factor is
  `k = MulDivCeil(speedPpm, minCycleTicks, D₀ × 10^6)` (at least 1) and
  `cycleDuration = MulDivCeil(k × D₀, 10^6, speedPpm)`. A completion exchanges `k′ ≤ k` times the recipe's inputs and
  outputs (outputs also times the output factor) atomically; `k′ = 0` waits and the remainder is never carried over.
  A worst-case batch amount above `10^18` is `batch.overflow`; a batch larger than the store is valid.

## v3: reserved IDs and unlock reachability

Two content-authoring checks that are not runtime rules.

**Reserved ID (`id.reserved`).** The ID `overdrive` is the run's unlock key for the Overdrive control, so a resource, machine, contract offer, Research or Item may not use it. IDs are case-sensitive, so `Overdrive` is a different (legal) ID, but avoid it. Recipes and Industry upgrades are not unlockable and are not checked.

```json
{ "id": "overdrive", "label": "Overdrive", "presentationKey": "F003-RES-IRON-ORE", "basePrice": 1, "unlock": { "kind": "always" } }
```

The resource above is rejected with `id.reserved` at `/resources/<index>/id`; rename it (for example `iron.ore`).

**Unlock reachability (`unlock.unreachable`).** Every resource, machine, contract offer and Overdrive must be unlockable by a player who follows the unlock conditions from `runStart.initialUnlocks`. The validator repeats until nothing changes: an entry joins the reachable set when its unlock can hold.

| Unlock | Holds when |
|---|---|
| `always`, `factoryLevelAtLeast` | always (factory levels are bought) |
| `resourceProduced` | a reachable machine runs a recipe that outputs the resource, every input and output resource of that recipe is reachable, and every input resource is itself producible |
| `researchPurchased` | the Research is purchasable: every prerequisite is purchasable and a reachable contract offer for a producible resource has an `rpReward` that pays RP (a curve base above 0) |

An ID in `initialUnlocks` is reachable from the start. A violation is reported at the entry's `unlock` pointer (`/overdrive/unlock` for Overdrive) and names the entry ID. Typical mistakes and fixes:

- A **cycle**: the Smelter unlocks with `{ "kind": "resourceProduced", "id": "iron.bar" }`, and only the Smelter makes Bars. Everything that waits for Bars (and for what Bars feed) is reported. Fix: unlock the Smelter by Ore (`iron.ore`), a factory level, or list it in `initialUnlocks`.
- A **never-produced resource**: a contract unlocks with `resourceProduced` on a resource no recipe outputs. Fix: add the producing recipe and machine, or choose another condition.
- A `researchPurchased` unlock on **unreachable Research** (a prerequisite cycle, or no reachable contract pays RP). Fix: break the cycle or give a reachable contract an `rpReward` with a base above 0.

References to unknown IDs keep their own code (`ref.research.unknown`, `ref.resource.unknown`) and are not reported twice. The complete example above and the packaged sheet satisfy both checks.

## v3: canonical form (review gate, not a runtime rule)

As in v1, plus:

- Two-space indentation, LF line endings, a final newline; object members in the order of the tables above.
- Every array of objects is sorted by `id` (ordinal); `storageLevels` and `factoryLevels` by `level`; a machine's
  `milestones` by `level`; the prestige `milestones` by `fractionPpm`; the ladder `entries` by `index` and then `itemId`;
  `offlinePolicies` by `revision`; nested amount entries by `resourceId`; `prerequisites` by `id`; `initialUnlocks` and
  `allowedMachineIds` ascending. `sellSteps` is written in its own order: the integers ascending, then `"all"`.
- A contract is written in the order `id`, `objective`, `class`, `resourceId`, `quantity`, `durationMs` (when present),
  `rewardRatioPpm`, `cooldownMs`, `rpReward`, `bonus` (when present), `unlock`.
- An optional member that is absent is left out, never written as `null`; `null` is always rejected.

## v3: semantic rule codes

In addition to the v1 identity and structure codes. The v2 codes that checked a contract's kind, its reward list and its
required or forbidden duration are retired with the members they checked.

| Area | Codes |
|---|---|
| Version | `content.version.unsupported` |
| Run start | `runstart.unlock.unknown`, `runstart.refund.out-of-range`, `runstart.min-cycle.invalid`, `runstart.speed-bound.invalid`, `runstart.output-bound.invalid`, `runstart.reward-ratio.invalid`, `sell.steps`, `live-gap.range` |
| Unlocks | `unlock.kind.unknown`, `unlock.level.invalid` (1 … number of factory levels), `unlock.unreachable` (an unlock that can never be satisfied), `ref.research.unknown` (a `researchPurchased` unlock naming no Research) |
| Resources/recipes | `resource.price.negative`, `recipe.outputs.empty`, `recipe.amount.non-positive`, `recipe.duration.non-positive`, `recipe.duration.out-of-range`, `recipe.resource.duplicate`, `ref.resource.unknown` |
| Machines | `ref.recipe.unknown`, `machine.max-level.invalid`, `machine.milestone.level-invalid`, `machine.milestone.duplicate`, `curve.growth.invalid`, `curve.overflow`, `curve.stalled`, `speed.level.dead`, `batch.overflow`, `build.base.bound`, `build.price.cap`, `cost.floor` |
| Expansion | `level.sequence.invalid`, `level.first-cost.non-zero`, `storage.capacity.non-increasing`, `factory.slots.non-increasing`, `factory.slots.too-many` |
| Identity and structure | `id.empty`, `id.too-long`, `id.not-canonical`, `id.duplicate`, `id.reserved` (the reserved ID `overdrive`), `value.null`, `value.out-of-range` (a number or vocabulary value outside its range where no dedicated code exists) |
| Contracts | `contract.objective.unknown`, `contract.class.unknown`, `contract.duration.out-of-range`, `contract.reward.item.unknown` (an unknown Item in the ladder), `contract.reward.ratio`, `contract.rp.growth`, `contract.bonus.fields`, `contract.ratio.standing`, `contract.ratio.bonus`, `contract.quantity.cap`, `contract.produce.min` |
| Prestige | `milestone.order`, `prestige.threshold.bound` |
| Chain | `chain.dominance` |
| Items | `item.tier.invalid`, `item.tiers.incomplete`, `item.target.invalid`, `item.duration.non-positive`, `item.duration.out-of-range` |
| Research/Industry | `research.prerequisite.unknown`, `research.prerequisite.cycle`, `research.prerequisite.level-invalid`, `upgrade.max-level.invalid`, `industry.first-affordable.missing`, `industry.ceiling.level` |
| Effects | `effect.kind.unknown`, `effect.target.invalid`, `effect.value.out-of-range` |
| Overdrive/Prestige | `overdrive.value.invalid`, `overdrive.duration.out-of-range`, `overdrive.cooldown.out-of-range`, `prestige.threshold.invalid` (the threshold base), `prestige.curve.unsupported` |
| Offline policy | `policy.revision-1.required` |
| Presentation (Unity composition) | `presentation.key.unresolved` (resources, machines and Items) |

## v3: structural rules

The structural rules (task T314) are checked by `RunContentValidator`. All arithmetic is checked integer math. A rule never repeats
a violation another code already reported for the same member.

| Code | Predicate | Revision | Notes |
|---|---|---|---|
| `sell.steps` | `runStart.sellSteps` holds 1 … 8 integers, each 1 … 10^18, strictly ascending (so distinct) | all | An entry out of range or out of order is reported at its own index; the count at the list. The optional `"all"` is not counted. |
| `live-gap.range` | `runStart.liveGapThresholdMs` is 1,000 … 28,800,000 | all | A programmatic catalog may leave the Core value unset; a document always carries it. |
| `contract.bonus.fields` | `bonus` is present exactly when `class` is `bonus` | all | Checked only for a known class (an unknown one is `contract.class.unknown`). A present `bonus` takes the full unlock validation on its trigger (shape, then reachability as `unlock.unreachable`) and `windowMs` ≥ 1. |
| `contract.ratio.standing` | A standing delivery offer has `rewardRatioPpm` ≤ 1,500,000 (its floor of 1,000,000 is the v2 check below) | ≥ 3 | `produce` objectives belong to the later rule contract.ratio.produce (T320). |
| `contract.ratio.bonus` | A bonus offer has `rewardRatioPpm` ≤ 2,000,000 | ≥ 3 | Its floor for delivery is the v2 check below. |
| `contract.reward.ratio` (v2, kept) | Delivery: 1,000,000 ≤ R ≤ `maxRewardRatioPpm`; the transitional Cash table fits 10^18 | all | Stays ungated, so revision 2 content (ratios 1.67 to 1.75) validates. The `contract.ratio.*` rules layer on top and run only for an offer this check did not report. |
| `contract.quantity.cap` | A delivery offer's smallest requirement (quantity curve index 0) is at most the top storage capacity | all | The catalog has no cap member (see below). |
| `contract.produce.min` | A production offer's smallest requirement (index 0) is at least 10 | all | Content constant `ProduceMinQuantity` = 10 in the validator. |
| `milestone.order` | Each prestige milestone `fractionPpm` is inside (0, 10^6) and strictly greater than the previous valid one | all | An out-of-range fraction is reported once, without an ordering comparison. |
| `build.base.bound` | `buildPrice.base` ≤ 10^12 and `buildPrice.cap` ≤ 10^12 | all | A negative number stays `value.out-of-range`. |
| `build.price.cap` | `buildPrice.cap` ≥ `buildPrice.base` (T323; reported at `/cap` once both are in range) | all | The price is `min(curve, cap)`, so a cap below the base would price the first build under its own base. |
| `cost.floor` | For each machine, and for the expansion track, the product of every Research and Industry `upgradeCost` factor (targeting `allMachines`, that machine, or `expansion`) at its maximum level is at least 0.5 (500,000 ppm) | all | Fixed point at 10^18, floored per level. A decaying Industry upgrade folds its decayed per-level factors (T331). Content constant `CostFloorPpm` = 500,000 in the validator. The catalog has no build-price discount, so only upgrade costs carry a multiplier. |
| `chain.dominance` | Output per occupied slot-second, evaluated exactly: for each resource P, `E(P) = basePrice(P) / S(P)` with `S(P) = (cycle(R) + Σ amount × S(input)) / outputAmount(P)` over the cheapest producing recipe `R` and `cycle(R) = durationTicks × 10^6 / speedPpm`. (1) `E(output) ≥ E(input)` for every input and output of every recipe with inputs (efficiency never falls along the chain); (2) every terminal resource (produced, consumed by no recipe) has `E(terminal) × 10^6 > E(Ore) × (10^6 + chainDominanceMarginPpm)`, where Ore is the best resource of an input-free recipe | ≥ 3 | Evaluated at level 1 and at every machine milestone level, every machine at `min(level, maxLevel)` with the SpeedTable of `speed.level.dead`. A violation is reported once per recipe or terminal resource, at the recipe (monotone) or at `/chainDominanceMarginPpm` (terminal). Inputs another rule rejects are skipped; a recipe cycle has no slot time and is skipped. Base prices, not sale-price modifiers. |
| `speed.level.dead` (extended) | Besides the v2 meaning, an Industry `startingStorageLevel` upgrade whose last level adds nothing is dead: the start level `1 +` the decayed amounts of every level below its last, `+` every other `startingStorageLevel` upgrade at its maximum, must stay below the number of `storageLevels` (T331: decay and the sum across upgrades). A decaying Industry upgrade (`0 < decayPpm < 10^6`) with a level whose raw value is zero (an additive amount of 0, or a factor that decays to the neutral 10^6) is dead too | all | The next-run start storage level is clamped to the table. |
| `prestige.threshold.bound` | `T(maxIndex)` is at most 10^18, with the runtime recurrence: `acc = base × 10^6`, per index `acc = floor(acc × growthPpm / 10^6)`, then `ceil(acc / 10^6)` (ECO-7) | all | Checked only when `base`, `growthPpm` and `maxIndex` are each in range. Reported at `/prestige/threshold/maxIndex`. The index is clamped at `maxIndex` (D-K), so this is the largest threshold. |
| `industry.ceiling.level` | `practicalCeilingLevel` is the last level whose marginal value is still at least `minGainPpm`, or `maxLevel` when every level qualifies (owner decision 2026-10-13, #396); when not even level 1 qualifies the content is rejected at `/industryUpgrades/<i>/minGainPpm` ("minGainPpm is above the gain of level 1", owner decision 2026-10-13-b, #396) | ≥ 3, or when `practicalCeilingLevel` or `minGainPpm` is non-zero | The unit of `minGainPpm` differs per kind: for `machineSpeed`, `salePrice` and `upgradeCost` it is ppm of the base, the decayed increment or decrement `abs(factor − 10^6)` of level L; for `startingCash` and `startingStorageLevel` it is ppm of the level-1 amount, `amount(L) × 10^6 / amount(1)`. Reported at `/industryUpgrades/<i>/practicalCeilingLevel`. |

Other T314 check without a new code: `chainDominanceMarginPpm` must be greater than 0 (`value.out-of-range`). The ladder's ascending
`index` order is not validated: ladder entries are a permutable family, so the order is a property of the canonical form (sorted by `index`,
then `itemId`), and the validator accepts any document order (its duplicate check does not depend on it). `RunCatalogBuilder` keeps the document order of the entries today; sorting by `index`, then `itemId`, is left to T320.

**Content constants.** `contract.quantity.cap`, `contract.produce.min` and `cost.floor` have no catalog member. The bounds are named
constants of the validator (`ProduceMinQuantity`, `CostFloorPpm`, and the top storage capacity, which is the last `storageLevels` capacity).
Changing one is a validator change with a recorded decision, not a content edit. `contract.quantity.cap` checks the one value the catalog
carries, index 0 of the quantity curve; whether every reachable index fits is the later rule contract.quantity.capacity (T320).

## v3: a complete, valid, annotated example

The block below is a complete v3 document in canonical form. JSON has no comments, so the annotations follow the block.
It validates against `gameplay-content-v3.schema.json`. Its numbers illustrate shape and satisfy the rules above; they
are **not** an approved balance (see the balance-hypothesis rules).

```json
{
  "contentSchemaVersion": 3,
  "contentRevision": 3,
  "runStart": {
    "startingCash": 25,
    "initialUnlocks": [
      "iron.ore",
      "machine.miner"
    ],
    "removalRefundPpm": 500000,
    "minCycleTicks": 1000000,
    "maxCombinedSpeedPpm": 100000000,
    "maxCombinedOutputFactor": 4,
    "maxRewardRatioPpm": 2000000,
    "sellSteps": [
      1,
      10,
      100,
      "all"
    ],
    "liveGapThresholdMs": 60000,
    "manualMining": {
      "resourceId": "iron.ore",
      "baseYieldMilli": 100,
      "maxActivationsPerSecond": 10,
      "startWindowMs": 60000,
      "referenceMinerLevel": 10,
      "maxShareOfMinerPpm": 250000,
      "miningAdvantageMaxPpm": 100000
    },
    "minRunDurationMs": 1800000,
    "uptimeMaxPpm": 500000
  },
  "resources": [
    {
      "id": "iron.bar",
      "label": "Iron Bar",
      "presentationKey": "F003-RES-IRON-BAR",
      "basePrice": 3,
      "unlock": {
        "kind": "resourceProduced",
        "id": "iron.ore"
      }
    },
    {
      "id": "iron.gear",
      "label": "Iron Gear",
      "presentationKey": "F003-RES-IRON-GEAR",
      "basePrice": 12,
      "unlock": {
        "kind": "factoryLevelAtLeast",
        "level": 3
      }
    },
    {
      "id": "iron.ore",
      "label": "Iron Ore",
      "presentationKey": "F003-RES-IRON-ORE",
      "basePrice": 1,
      "unlock": {
        "kind": "always"
      }
    },
    {
      "id": "iron.plate",
      "label": "Iron Plate",
      "presentationKey": "F003-RES-IRON-PLATE",
      "basePrice": 8,
      "unlock": {
        "kind": "factoryLevelAtLeast",
        "level": 2
      }
    }
  ],
  "recipes": [
    {
      "id": "recipe.forge-iron-gear",
      "inputs": [
        {
          "resourceId": "iron.bar",
          "amount": 3
        }
      ],
      "outputs": [
        {
          "resourceId": "iron.gear",
          "amount": 1
        }
      ],
      "durationTicks": 50000000
    },
    {
      "id": "recipe.mine-iron-ore",
      "inputs": [],
      "outputs": [
        {
          "resourceId": "iron.ore",
          "amount": 2
        }
      ],
      "durationTicks": 200000000
    },
    {
      "id": "recipe.press-iron-plate",
      "inputs": [
        {
          "resourceId": "iron.bar",
          "amount": 2
        }
      ],
      "outputs": [
        {
          "resourceId": "iron.plate",
          "amount": 1
        }
      ],
      "durationTicks": 40000000
    },
    {
      "id": "recipe.smelt-iron-bar",
      "inputs": [
        {
          "resourceId": "iron.ore",
          "amount": 2
        }
      ],
      "outputs": [
        {
          "resourceId": "iron.bar",
          "amount": 1
        }
      ],
      "durationTicks": 30000000
    }
  ],
  "machines": [
    {
      "id": "machine.forge",
      "label": "Forge",
      "presentationKey": "F003-MACHINE-FORGE",
      "recipeId": "recipe.forge-iron-gear",
      "maxLevel": 20,
      "cost": {
        "base": 200,
        "growthPpm": 1150000,
        "rounding": "ceil"
      },
      "buildPrice": {
        "base": 200,
        "ownedGrowthPpm": 1100000,
        "prestigeGrowthPpm": 1050000,
        "cap": 20000
      },
      "speedGrowthPpm": 1050000,
      "milestones": [
        {
          "level": 10,
          "speedPpm": 2000000
        },
        {
          "level": 20,
          "speedPpm": 2000000
        }
      ],
      "unlock": {
        "kind": "factoryLevelAtLeast",
        "level": 3
      }
    },
    {
      "id": "machine.miner",
      "label": "Miner",
      "presentationKey": "F003-MACHINE-MINER",
      "recipeId": "recipe.mine-iron-ore",
      "maxLevel": 25,
      "cost": {
        "base": 10,
        "growthPpm": 1150000,
        "rounding": "ceil"
      },
      "buildPrice": {
        "base": 10,
        "ownedGrowthPpm": 1100000,
        "prestigeGrowthPpm": 1050000,
        "cap": 1000
      },
      "speedGrowthPpm": 1080000,
      "milestones": [
        {
          "level": 10,
          "speedPpm": 2000000
        },
        {
          "level": 25,
          "speedPpm": 2000000
        }
      ],
      "unlock": {
        "kind": "always"
      }
    },
    {
      "id": "machine.press",
      "label": "Press",
      "presentationKey": "F003-MACHINE-PRESS",
      "recipeId": "recipe.press-iron-plate",
      "maxLevel": 20,
      "cost": {
        "base": 120,
        "growthPpm": 1150000,
        "rounding": "ceil"
      },
      "buildPrice": {
        "base": 120,
        "ownedGrowthPpm": 1100000,
        "prestigeGrowthPpm": 1050000,
        "cap": 12000
      },
      "speedGrowthPpm": 1050000,
      "milestones": [
        {
          "level": 10,
          "speedPpm": 2000000
        },
        {
          "level": 20,
          "speedPpm": 2000000
        }
      ],
      "unlock": {
        "kind": "factoryLevelAtLeast",
        "level": 2
      }
    },
    {
      "id": "machine.smelter",
      "label": "Smelter",
      "presentationKey": "F003-MACHINE-SMELTER",
      "recipeId": "recipe.smelt-iron-bar",
      "maxLevel": 20,
      "cost": {
        "base": 40,
        "growthPpm": 1150000,
        "rounding": "ceil"
      },
      "buildPrice": {
        "base": 40,
        "ownedGrowthPpm": 1100000,
        "prestigeGrowthPpm": 1050000,
        "cap": 4000
      },
      "speedGrowthPpm": 1050000,
      "milestones": [
        {
          "level": 10,
          "speedPpm": 2000000
        },
        {
          "level": 20,
          "speedPpm": 2000000
        }
      ],
      "unlock": {
        "kind": "resourceProduced",
        "id": "iron.ore"
      }
    }
  ],
  "storageLevels": [
    {
      "level": 1,
      "capacity": 1000,
      "cost": 0
    },
    {
      "level": 2,
      "capacity": 2000,
      "cost": 2500
    },
    {
      "level": 3,
      "capacity": 4000,
      "cost": 12000
    }
  ],
  "factoryLevels": [
    {
      "level": 1,
      "slots": 4,
      "cost": 0
    },
    {
      "level": 2,
      "slots": 6,
      "cost": 5000
    },
    {
      "level": 3,
      "slots": 9,
      "cost": 25000
    },
    {
      "level": 4,
      "slots": 12,
      "cost": 100000
    }
  ],
  "contracts": [
    {
      "id": "contract.gear",
      "objective": "deliver",
      "class": "standing",
      "resourceId": "iron.gear",
      "quantity": {
        "base": 100,
        "growthPpm": 1010000,
        "rounding": "ceil"
      },
      "rewardRatioPpm": 1500000,
      "cooldownMs": 0,
      "rpReward": {
        "base": 10,
        "growthPpm": 1000000,
        "rounding": "floor"
      },
      "unlock": {
        "kind": "resourceProduced",
        "id": "iron.gear"
      }
    },
    {
      "id": "contract.plate",
      "objective": "deliver",
      "class": "standing",
      "resourceId": "iron.plate",
      "quantity": {
        "base": 100,
        "growthPpm": 1010000,
        "rounding": "ceil"
      },
      "rewardRatioPpm": 1500000,
      "cooldownMs": 0,
      "rpReward": {
        "base": 10,
        "growthPpm": 1000000,
        "rounding": "floor"
      },
      "unlock": {
        "kind": "resourceProduced",
        "id": "iron.plate"
      }
    },
    {
      "id": "contract.timed-bar",
      "objective": "deliver",
      "class": "standing",
      "resourceId": "iron.bar",
      "quantity": {
        "base": 40,
        "growthPpm": 1010000,
        "rounding": "ceil"
      },
      "durationMs": 180000,
      "rewardRatioPpm": 1500000,
      "cooldownMs": 30000,
      "rpReward": {
        "base": 2,
        "growthPpm": 1000000,
        "rounding": "floor"
      },
      "unlock": {
        "kind": "resourceProduced",
        "id": "iron.bar"
      }
    },
    {
      "id": "contract.urgent-gear",
      "objective": "deliver",
      "class": "bonus",
      "resourceId": "iron.gear",
      "quantity": {
        "base": 10,
        "growthPpm": 1010000,
        "rounding": "ceil"
      },
      "durationMs": 90000,
      "rewardRatioPpm": 1800000,
      "cooldownMs": 60000,
      "rpReward": {
        "base": 5,
        "growthPpm": 1000000,
        "rounding": "floor"
      },
      "bonus": {
        "trigger": {
          "kind": "factoryLevelAtLeast",
          "level": 3
        },
        "windowMs": 120000
      },
      "unlock": {
        "kind": "resourceProduced",
        "id": "iron.gear"
      }
    }
  ],
  "contractItemLadder": {
    "entries": [
      {
        "index": 0,
        "itemId": "item.diamond-drill-head"
      },
      {
        "index": 4,
        "itemId": "item.tuned-gearbox"
      },
      {
        "index": 9,
        "itemId": "item.golden-blueprint"
      }
    ],
    "perRunCap": 3
  },
  "items": [
    {
      "id": "item.diamond-drill-head",
      "tier": 1,
      "label": "Diamond Drill Head",
      "presentationKey": "F003-ITEM-DIAMOND-DRILL-HEAD",
      "durationMs": 60000,
      "target": {
        "kind": "machineType",
        "allowedMachineIds": [
          "machine.miner"
        ]
      },
      "effect": {
        "kind": "machineOutput",
        "factor": 2
      }
    },
    {
      "id": "item.golden-blueprint",
      "tier": 3,
      "label": "Golden Blueprint",
      "presentationKey": "F003-ITEM-GOLDEN-BLUEPRINT",
      "durationMs": 60000,
      "target": {
        "kind": "factory"
      },
      "effect": {
        "kind": "machineSpeed",
        "speedPpm": 1200000
      }
    },
    {
      "id": "item.tuned-gearbox",
      "tier": 2,
      "label": "Tuned Gearbox",
      "presentationKey": "F003-ITEM-TUNED-GEARBOX",
      "durationMs": 60000,
      "target": {
        "kind": "machineType",
        "allowedMachineIds": [
          "machine.press",
          "machine.smelter"
        ]
      },
      "effect": {
        "kind": "machineSpeed",
        "speedPpm": 1500000
      }
    }
  ],
  "research": [
    {
      "id": "research.market-insight",
      "label": "Market Insight",
      "maxLevel": 5,
      "rpCost": {
        "base": 5,
        "growthPpm": 1500000,
        "rounding": "ceil"
      },
      "prerequisites": [],
      "effect": {
        "kind": "salePrice",
        "target": {
          "kind": "allResources"
        },
        "valuePerLevelPpm": 1050000
      }
    },
    {
      "id": "research.smelter-speed",
      "label": "Smelter Tuning",
      "maxLevel": 5,
      "rpCost": {
        "base": 5,
        "growthPpm": 1500000,
        "rounding": "ceil"
      },
      "prerequisites": [],
      "effect": {
        "kind": "machineSpeed",
        "target": {
          "kind": "machine",
          "id": "machine.smelter"
        },
        "valuePerLevelPpm": 1100000
      }
    },
    {
      "id": "research.tool-discount",
      "label": "Tool Discount",
      "maxLevel": 3,
      "rpCost": {
        "base": 8,
        "growthPpm": 1500000,
        "rounding": "ceil"
      },
      "prerequisites": [
        {
          "id": "research.smelter-speed",
          "level": 1
        }
      ],
      "effect": {
        "kind": "upgradeCost",
        "target": {
          "kind": "allMachines"
        },
        "valuePerLevelPpm": 950000
      }
    }
  ],
  "overdrive": {
    "durationMs": 30000,
    "cooldownMs": 300000,
    "speedPpm": 2000000,
    "unlock": {
      "kind": "always"
    }
  },
  "prestige": {
    "threshold": {
      "base": 10000000,
      "growthPpm": 1050000,
      "maxIndex": 10
    },
    "ipCurve": {
      "kind": "sqrt",
      "coefficient": 10,
      "divisor": 10000000
    }
  },
  "milestones": [
    {
      "fractionPpm": 250000
    },
    {
      "fractionPpm": 500000
    },
    {
      "fractionPpm": 750000
    }
  ],
  "industryUpgrades": [
    {
      "id": "industry.seed-capital",
      "label": "Seed Capital",
      "maxLevel": 10,
      "ipCost": {
        "base": 5,
        "growthPpm": 1500000,
        "rounding": "ceil"
      },
      "decayPpm": 950000,
      "practicalCeilingLevel": 10,
      "minGainPpm": 5000,
      "effect": {
        "kind": "startingCash",
        "valuePerLevel": 500
      }
    },
    {
      "id": "industry.warehouse",
      "label": "Bigger Warehouse",
      "maxLevel": 2,
      "ipCost": {
        "base": 20,
        "growthPpm": 2000000,
        "rounding": "ceil"
      },
      "decayPpm": 1000000,
      "practicalCeilingLevel": 2,
      "minGainPpm": 5000,
      "effect": {
        "kind": "startingStorageLevel",
        "valuePerLevel": 1
      }
    }
  ],
  "chainDominanceMarginPpm": 100000,
  "offlinePolicies": [
    {
      "revision": 1,
      "efficiencyNumerator": 4,
      "efficiencyDenominator": 5,
      "maxCreditedTicks": 288000000000
    }
  ]
}
```

Annotations:

- `contentSchemaVersion: 3` selects the v3 reader; `contentRevision: 3` is the first F005 balance revision, the revision from which the semantic F005 rules apply.
- `runStart`: `initialUnlocks` lists the ore resource and the Miner. `maxCombinedSpeedPpm` (×100) stays above the worst
  composed speed: the Miner at level 25 is about ×25.4, ×2 with Overdrive and ×1.2 with the factory-wide Item gives
  about ×61, and the worst batch is 1 recipe unit (the 20 s Miner cycle is far above the minimum cycle), 8 ore with the output factor 4, far below 10^18.
- `resources`: four Iron resources sorted by `id`. Only `iron.ore` is `always`; the rest unlock through
  `resourceProduced` or `factoryLevelAtLeast`. Plate and Gear are priced above the value of their inputs.
- `recipes`: `recipe.mine-iron-ore` has `"inputs": []`, the input-free Miner. Durations are ticks (`20000000` = 2 s; the Miner recipe takes 20 s so that the example passes `chain.dominance`).
- `machines`: each machine links a `recipeId`, has a `cost` curve iterated per level (`ceil`), a `speedGrowthPpm`
  of ×1.05 to ×1.08 and two ×2 milestones.
- `storageLevels` and `factoryLevels`: contiguous from level 1, level 1 costs `0`, capacity and slots strictly increase,
  slots end at 12.
- `contracts`: three standing delivery offers and one bonus offer. Only the timed offers have `durationMs`. `rewardRatioPpm` is
  1.5 for the three standing offers (the Cash reward is the market value of the required units times the ratio, rounded up) and 1.8 for the
  bonus offer, inside `maxRewardRatioPpm`; the RP curves are flat (`growthPpm: 1000000`), so they do not outgrow the quantity curve.
  `contract.urgent-gear` is a `bonus` offer, so it carries `bonus`: it is triggered by factory level 3 and stays open for `windowMs`.
  Quantities of 100 with `ceil` and growth ×1.01 strictly increase.
- `contractItemLadder`: three entries, one Item of each tier, so tiers 1–3 are all obtainable. The transitional adapter gives each Item to the
  standing delivery offer that owns its tier (the cheapest resource owns tier 1: the timed Bar offer, then the Plate and the Gear offers).
- `milestones`, `chainDominanceMarginPpm` and the `buildPrice`, `decayPpm`, `practicalCeilingLevel` and `minGainPpm` members illustrate the
  v3 shape. `industry.seed-capital` decays (`decayPpm` 950000: +500, +475, +451, ... Cash per level, so every level is far above the
  `minGainPpm` of 5000 and the ceiling is the last level, 10); `industry.warehouse` does not (`decayPpm` 1000000), because +1 decayed by 0.95 floors to 0 and
  such a level would be dead (`speed.level.dead`). The remaining rules come with later tasks.
- `items`: a machine-type Item (`machineOutput`, factor 2), a machine-type Item with two allowed machines (`machineSpeed`)
  and a factory-wide Item (`machineSpeed`).
- `research`: three effects on three different kinds and targets (`salePrice`/`allResources`,
  `machineSpeed`/`machine`, `upgradeCost`/`allMachines`); one prerequisite chain.
- `overdrive` and `prestige`: at the `10000000` threshold base `IP = floor(10 × sqrt(1)) = 10`, which affords the level-1
  Industry upgrade (`ipCost.base` 5). The threshold grows ×1.05 per prestige index and stops growing at index 10.
- `industryUpgrades`: `startingCash` and `startingStorageLevel` (at most two extra levels, so storage level 3 is the
  ceiling).
- `offlinePolicies`: the locked revision 1 policy.

## v3: authoring check

Validate a document with the Edit Mode authoring check, pointed at the document through an environment variable:

```
IDLEFACTORY_CONTENT_CHECK_PATH=<absolute path to the document> scripts/test-unity.ps1 -TestFile Assets/IdleFactory/Tests/EditMode/RunContentAuthoringCheckTests.cs
```

On Windows PowerShell:

```powershell
$env:IDLEFACTORY_CONTENT_CHECK_PATH = "C:\path\to\your\document.json"
scripts/test-unity.ps1 -TestFile Assets/IdleFactory/Tests/EditMode/RunContentAuthoringCheckTests.cs
```

- `IDLEFACTORY_CONTENT_CHECK_PATH` must be an absolute path. The test reads exactly this variable name.
- When the variable is unset the test prints an explicit "not configured" line; that is not a pass for your document.
- The check validates the document through the dispatcher, so a `contentSchemaVersion: 1` document is validated by the
  legacy loader and a `3` document by the v3 reader and validator.
- Like v1, the check does not resolve `presentationKey` values; an unresolved key still fails start-up with
  `presentation.key.unresolved`.
- Unity runs need the Unity (and, with an Android active target, emulator) leases described in `AGENTS.md`.
- The test class is created by task T136; until it exists, the schema plus this section is the only author aid.

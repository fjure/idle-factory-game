# Gameplay Content Authoring Guide (JSON, v1)

This is the author guide for human and AI editors of the packaged gameplay content document. It is written to agree
exactly with the normative contract, `specs/002-json-content-authoring/contracts/gameplay-content-v1.md`. If the two
ever disagree, the contract wins and this file must be corrected to match — do not resolve a disagreement by editing
only one of them.

The domain code is authoritative over both documents. Every rule below is a direct reading of
`Assets/IdleFactory/Core/Domain/Definitions.cs`, `Assets/IdleFactory/Core/Domain/DefinitionCatalog.cs` and
`Assets/IdleFactory/Core/Domain/Identifiers.cs`.

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

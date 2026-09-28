# Data Model: JSON Content Authoring Migration

**Spec**: [spec.md](spec.md) · **Contract**: [contracts/gameplay-content-v1.md](contracts/gameplay-content-v1.md)

Runtime domain entities are unchanged. For their meaning and invariants, see `specs/001-core-game-foundation/data-model.md`. This feature adds only authoring-side, transient types.

## Ownership and lifetime

| Type | Assembly | Owner / writer | Lifetime | Persisted |
|---|---|---|---|---|
| Content document bytes | Packaged `TextAsset` | Content authors (VCS) | Build input | No (ships in the build) |
| `GameplayContentDocumentDto` and nested DTOs | `IdleFactory.Unity` (`Unity/Content`) | Created only by `GameplayContentReader` | Local to one load; unreachable after mapping | No |
| `ContentDiagnostic` list | `IdleFactory.Unity` | Reader and semantic validator | Returned in the load result, logged once | No |
| `DefinitionCatalog` | `IdleFactory.Core` | Built once by `JsonDefinitionCatalogBuilder` | Session lifetime, immutable | IDs referenced by Save V1 |
| Presentation mapping (`F001SpriteCatalog`) | `IdleFactory.Unity` | Authored Unity asset | Scene lifetime | No |

## DTOs (fresh per load, mutable only inside the reader)

- **GameplayContentDocumentDto**: `ContentSchemaVersion` (int), `Resources`, `Recipes`, `Buildings`, `OfflinePolicies` (lists).
- **ResourceDto**: `Id`, `DevelopmentLabel`, `PresentationKey?`, `IsDevelopmentOnly`.
- **RecipeDto**: `Id`, `Inputs[]`, `Outputs[]` (each a `ResourceAmountDto`: `ResourceId`, `Amount`), `DurationTicks`, `IsDevelopmentOnly`.
- **BuildingDto**: `Id`, `FootprintWidth`, `FootprintHeight`, `AllowedOrientations[]`, `RecipeId?`, `InputBuffer?`, `OutputBuffer?` (each a list of `ResourceCapacityDto`: `ResourceId`, `Capacity`), `PresentationKey` (required), `IsDevelopmentOnly`.
- **OfflinePolicyDto**: `Revision`, `EfficiencyNumerator`, `EfficiencyDenominator`, `MaxCreditedTicks`.

The field set exactly mirrors the current ScriptableObject fields and domain constructor arguments. A buffer that is absent (`null`) means "no buffer", which corresponds to the SO's `hasInputBuffer: false`. An empty buffer list is rejected (FR-006).

## Validation rules

| Stage | Rule (code) | Source |
|---|---|---|
| Size | `content.too-large` (> 1 MiB), `content.empty` | FR-006 |
| Encoding | `content.encoding` (invalid UTF-8, BOM) | FR-006 |
| Version | `content.version.missing`, `content.version.unsupported` (≠ 1) | FR-003 |
| Structure | `json.malformed`, `json.trailing-content`, `json.depth`, `member.unknown`, `member.duplicate`, `member.missing`, `value.type`, `value.not-integer`, `value.out-of-range`, `value.null`, `collection.empty`, `collection.too-many` | FR-006 |
| Identity | `id.empty`, `id.too-long` (> 128), `id.not-canonical` (trim + NFC changes it), `id.duplicate` (per family) | FR-004, FR-007 |
| References | `ref.resource.unknown`, `ref.recipe.unknown` | FR-007 |
| Recipes | `recipe.amount.non-positive`, `recipe.duration.non-positive`, `recipe.resource.duplicate`, `recipe.inputs.empty`, `recipe.outputs.empty` | FR-007 |
| Buildings | `footprint.non-positive`, `orientation.invalid` (outside 0–3), `orientation.duplicate`, `orientation.empty`, `buffer.capacity.negative`, `buffer.resource.duplicate`, `buffer.empty`, `buffer.missing-recipe-resource`, `building.producing-requires-buffers` | FR-007 |
| Offline policy | `policy.revision.non-positive`, `policy.revision.duplicate`, `policy.ratio.invalid` (non-positive, or numerator ≥ denominator), `policy.cap.non-positive`, `policy.revision-1.required` (the catalog currently accepts exactly revision 1 = 4/5, 8 h; any other set is rejected) | FR-007, FR-017 |
| Mapping | `content.internal` (a domain constructor rejected a graph the validator accepted) | R03 |
| Presentation (Unity composition) | `presentation.key.unresolved` | FR-015 |

Every semantic code corresponds to an existing throw in `Core/Domain/Definitions.cs` or `DefinitionCatalog.cs` (the constructors, `ValidateGraph`, and the revision-1 guard `ValidateSupportedPolicy`, `DefinitionCatalog.cs:123–140`). The JSON validator introduces no new domain rule. Its only additions are the authoring guards `id.too-long`, `id.not-canonical`, the limits and the structural codes. A parity test proves that each semantic code rejects exactly the graphs that the domain constructors reject.

## State transitions (load)

```text
Unloaded ──bytes──▶ VersionChecked ──▶ Structured(DTO) ──▶ SemanticallyValid ──▶ CatalogBuilt ──▶ PresentationResolved ──▶ Published(session)
    └────────────── any failure ─────────────▶ Rejected(diagnostics) ──▶ bootstrap DisableWithError (no session, no checkpoint I/O)
```

`Published` happens exactly once per start-up and only from `PresentationResolved`. Nothing downstream of the bootstrap observes intermediate states.

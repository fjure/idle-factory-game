# Implementation Plan: JSON Content Authoring Migration

**Branch**: `feat/002-json-content-authoring` | **Date**: 2026-09-28 | **Spec**: [spec.md](spec.md)

**Input**: The clarified specification (Clarifications, session 2026-09-28), the Feature 001 artifacts it succeeds (`research.md` "Post-foundation successor to R03", `data-model.md`), the constitution, and Phase 0 research in [research.md](research.md).

## Summary

Replace the gameplay ScriptableObject authoring with one packaged, versioned UTF-8 JSON document. A new strict content reader in the Unity assembly performs five steps:

1. it reads the document synchronously from a serialized `TextAsset`;
2. it gates on `contentSchemaVersion`;
3. it walks the tokens into fresh DTOs;
4. it collects every semantic violation;
5. only then does it build the existing immutable `DefinitionCatalog` through the unchanged domain constructors.

The bootstrap validates presentation keys against `F001SpriteCatalog` and routes every failure through the existing `DisableWithError` path, so no session and no checkpoint I/O ever happen after a content failure.

The migration is proven in stages: an independent parity fixture, Save V1 and simulation regression tests, a player-build failure case, and an SC-008 measurement. The gameplay ScriptableObjects are removed only after the owner approves that evidence. Core, simulation and Save V1 remain byte-for-byte unchanged.

## Technical Context

**Language/Version**: C# supported by Unity `6000.6.0f1`; no unsafe code

**Primary Dependencies**: Existing `com.unity.nuget.newtonsoft-json` `3.2.2` (only its `JsonTextReader` token API, R02); Unity Test Framework `1.8.0`. No new package.

**Storage**: One read-only packaged JSON `TextAsset` (`Assets/IdleFactory/Content/Development/development-catalog.json`). Save V1 storage is unchanged.

**Testing**: Edit Mode NUnit for the reader, semantic validator, schema and documentation consistency, canonical form, parity, order independence and compatibility. Play Mode for bootstrap success and failure and for the scene journey. A `[Category("StandalonePlayer")]` Windows player test covers packaged content load, the SC-004 failure case and the SC-008 timing. Android runs as an emulator journey.

**Target Platform**: The Windows editor and the Windows Standalone player (Mono) are required evidence. The Android ARM64 IL2CPP Development Player on the API 36 x86_64 AVD is emulator-only evidence. iOS stays deferred under Issue #39.

**Project Type**: Single Unity 2D game repository

**Performance Goals**: SC-008 requires measuring content load time and recording it against a 100 ms guard. The guard does not block acceptance (R10).

**Constraints**:
- Core stays engine-free and JSON-free.
- Save V1 code, format and limits are unchanged.
- No reflection-based deserialization (IL2CPP).
- No silent fallback, no partial publication.
- The content limits in R04 apply.
- The canonical form is enforced as a review gate (R06).

**Scale/Scope**: 2 resources, 1 recipe, 1 building and 1 offline policy today. The limits allow 1,024 entries per family.

## Constitution Check

*GATE: Passed before Phase 0 and rechecked after Phase 1.*

| Principle | Evidence | Result |
|---|---|---|
| I. Correct observable behavior | Parity fixture (SC-001), Save V1 and simulation regression (SC-002), failure matrix (SC-004), and no change to any value (FR-020) | PASS |
| II. Domain architecture, explicit state | Core untouched. DTOs are transient, owned by the reader and unreachable after mapping. The catalog remains the single runtime authority (data-model ownership table). | PASS |
| III. Deterministic, data-driven | Integer-only fields and a strict number policy. The catalog is keyed by ID and independent of order (R05). Diagnostics are ordered deterministically. | PASS |
| IV. Tests protect behavior | Every edge-case class has a rejection test. Player-level evidence covers packaged content and checkpoint safety (the lesson recorded in the `player-level-test-gaps` wiki page). | PASS |
| V. Simplicity | One reader, one validator and one builder, with no schema-validation package and no shared-validator refactor (R02, R07). The JSON Schema file serves documentation only, and a test keeps it consistent. | PASS |
| VI. Measured performance | SC-008 has a named method and baseline (R10). No optimization is planned. | PASS |
| VII. Human authority | Three owner clarifications are recorded. Retirement is gated on recorded owner approval (FR-019). ID and revision meaning changes are excluded (FR-017). | PASS |
| VIII. Traceability | The FR/SC → contract → quickstart mapping is below, and tasks and analyze follow. The Phase 8 seeds are superseded by explicit mapping (R11). | PASS |
| IX. Independent review | Spec reviews are recorded in `reviews/spec-reviews.md`. A plan review wave by the Architect, Test/QA and Gameplay Developer follows this document. FR-021 lists the final verdicts. | PASS |
| X. Safe delivery, durable knowledge | Explicit coded failures, compatibility policy (contract § Evolution), Git rollback for retirement, post-plan wiki ingest | PASS |

Post-design recheck: there are no violations and no Complexity Tracking entries.

## Design

### Components (all in `IdleFactory.Unity`, namespace `IdleFactory.Unity.Content`)

| Component | Responsibility | Requirements |
|---|---|---|
| `GameplayContentDtos` | Plain mutable DTO classes, internal to the assembly | FR-001 |
| `GameplayContentLimits` | Named constants from R04 | FR-006 |
| `GameplayContentReader` | Bytes → strict UTF-8 → version peek → token walk → DTOs, or first structural diagnostic with JSON Pointer, line and column | FR-003, FR-006, FR-008 |
| `GameplayContentValidator` | DTO graph → complete ordered semantic diagnostics (ID canonical form and length, duplicates, references, recipe, building, buffer and policy rules that mirror domain throws) | FR-004, FR-007, FR-008 |
| `JsonDefinitionCatalogBuilder` | Zero-diagnostic DTOs → domain definitions → `DefinitionCatalog`. A constructor throw here becomes `content.internal`. | FR-009 |
| `GameplayContentLoader` | Orchestrates the steps above and returns `ContentLoadResult` (`Loaded` or `Rejected`) | FR-009, FR-014 |
| `CoreGameFoundationBootstrap` (changed) | A `[SerializeField] TextAsset gameplayContent` replaces `DefinitionCatalogAsset developmentCatalog`, and a read-only `Definitions` property replaces `DevelopmentCatalog`. `Initialize()` is **reordered**. Today `spriteCatalog.Validate()` runs after `CreateCheckpointStore` and `new FactorySession`, at lines 137–150. The new order is: (1) content load, (2) `spriteCatalog.Validate()`, (3) presentation-key resolution, and only then (4) checkpoint store and session creation. Any rejection in steps 1–3 calls `DisableWithError` before any checkpoint I/O. | FR-013, FR-014, FR-015 |
| `MachineInspectorPresenter` (changed) | Today it builds a **second** catalog at `MachineInspectorPresenter.cs:250` (`DefinitionCatalogBuilder.Build(bootstrap.DevelopmentCatalog)`). It must instead consume `bootstrap.Definitions`, the single catalog instance. This change ships with the bootstrap switch, because removing `DevelopmentCatalog` would otherwise break the build. | FR-013, FR-019 |

### Test seams

- **Editor Play Mode**: the existing `#if UNITY_EDITOR` composition seam (`OverrideCompositionDependenciesForTests`) gains a static `Func<byte[]>` content override next to its clock and store overrides. Play Mode tests use it to inject a missing, empty or invalid document or unresolved keys without editing the scene. Using a delegate avoids having to construct a `TextAsset` in tests.
- **Built player (SC-004)**: that seam does not exist in players. Following the `PlayerCheckpointRoundTripTests` pattern (T052), the `StandalonePlayer` test does three things. It calls `GameplayContentLoader` directly on the packaged `TextAsset` bytes, which proves they are packaged. It calls the loader on corrupted copies of those bytes. And it opens a real `JsonCheckpointStore` on a temporary root holding a captured checkpoint, proving that a rejected load never reaches the store and leaves its bytes identical. The bootstrap ordering itself is proven in editor Play Mode.
- The authoring-check test reads a document path from an environment variable, so authors can validate scratch files (SC-005).
- **Line and column in diagnostics** come from `IJsonLineInfo` on the `JsonTextReader`. `FloatParseHandling.Decimal`, combined with requiring `JsonToken.Integer`, rejects `1.0` and `1e3`.
- **Scene reference swap**: made only through Unity tooling (Unity MCP or the editor) under the `unity` lease. The YAML is never edited by hand. The serialized-change review confirms that only the bootstrap field changed.

### Data flow

`TextAsset.bytes` → `GameplayContentReader` → DTOs → `GameplayContentValidator` → `JsonDefinitionCatalogBuilder` → `DefinitionCatalog` → presentation-key check → `FactorySession` and the existing checkpoint load. Any `Rejected` stops before the session.

### Retirement scope (after owner approval)

Delete these:

- `ResourceDefinitionAsset`, `RecipeDefinitionAsset`, `BuildingDefinitionAsset`, `OfflinePolicyAsset`, `DefinitionCatalogAsset` and `DefinitionCatalogBuilder`, with their `.meta` files;
- `Assets/IdleFactory/Content/Development/{Resources,Recipes,Buildings,OfflinePolicies}/*.asset` and `DevelopmentCatalog.asset`.

Rewrite the affected tests to use JSON authority or the parity fixture:

- `DefinitionCatalogBuilderTests`;
- `DevelopmentContentTests`;
- the presentation-key test in `F001AssetContractTests`;
- the SO-dependent parts of `CheckpointMappingTests`, `CheckpointStoreTests` and `PersistenceApplicationTests`;
- any remaining use of `DevelopmentCatalog` (the `MachineInspectorPresenter` change already lands in item 6);
- the scene reference.

`F001SpriteCatalog` and every sprite remain.

### Requirement traceability

| Requirement | Design / evidence |
|---|---|
| FR-001–FR-005, FR-017 | Contract, pipeline doc, schema file, consistency and documentation completeness tests |
| FR-006, FR-003 | Reader, structural rejection suite |
| FR-004, FR-007, FR-008 | Validator, semantic rejection suite, domain-parity test for each rule code |
| FR-009, SC-007 | Builder; order-permutation test |
| FR-010, SC-005 | Edit Mode authoring check; GD/PO walkthrough |
| FR-011, FR-012, SC-001 | Migrated document; independent parity fixture (R09) |
| FR-013, FR-014, FR-015, SC-004 | Bootstrap switch; Play Mode failure matrix; Windows player failure case |
| FR-016, FR-018, SC-002 | Pre-migration checkpoint fixtures; D001/W001 regression |
| FR-019, FR-020, SC-006 | Owner approval record; retirement; authority test |
| FR-021 | Review record |
| SC-008 | Player timing test with a pre-migration baseline (R10) |

### Proposed work breakdown (input to `$speckit-tasks`)

The existing Issues #46–#54 are reused where their scope matches (R11). ∥ marks work that can run in parallel on disjoint paths.

1. Contract doc, schema file, and schema and documentation consistency tests (#46).
2. ∥ **Pre-migration capture**, run while the SO path is still live. It produces three things:
   - a hand-written `AcceptedFoundationCatalogFixture.cs`, plus a one-time assertion that it equals `DefinitionCatalogBuilder.Build(DevelopmentCatalog.asset)`;
   - Save V1 checkpoint files written by the unchanged `SaveV1Mapper` from the SO catalog into `Assets/IdleFactory/Tests/EditMode/Fixtures/SaveV1/`, with a `README.md` recording the capture commit and scenario. The scenarios are: an empty factory; an idle placed D001; D001 mid-cycle with partial progress; D001 blocked with its output full; and a checkpoint with a departure time for offline return. A W001-scale checkpoint is generated by the same capture test;
   - the SC-008 baseline timing.

   This work touches only test and fixture paths and runs parallel to items 1 and 3. It needs the `unity` lease.
3. Reader, limits and structural suite (#47). Its Edit Mode tests can start as soon as the contract is fixed in this plan, in parallel with 1, because they share only the contract.
4. Validator, builder, semantic suite and order test (#48), after 3.
5. Migrated document with parity and canonical-form tests (#49), after 2 and 4.
6. Bootstrap switch, presentation-key check, Play Mode failure matrix and Windows player case (#50), after 5.
7. Save V1 and simulation compatibility (#51), after 6.
8. Owner approval gate (parent task, no code).
9. Retirement and authority test (#52), after 7 and 8.
10. Independent reviews (#53).
11. Final acceptance (#54), split so that each gate can fail and be triaged on its own:
   - 11a: full Edit Mode and Play Mode suites and a console check;
   - 11b: Windows player suite and packaged-content evidence;
   - 11c: Android ARM64 emulator journey (emulator-only);
   - 11d: SC-008 measurement against the baseline, and the acceptance record.

   Items 11a, 11b and 11d share the `unity` lease; 11c also needs `emulator`.

## Project Structure

### Documentation (this feature)

```text
specs/002-json-content-authoring/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/gameplay-content-v1.md
├── checklists/requirements.md
├── reviews/ (spec-reviews.md, plan-reviews.md, json-content-migration-review.md, json-content-final-acceptance.md)
└── tasks.md            # $speckit-tasks
```

### Source code (repository root)

```text
docs/content/json-content-pipeline.md                        # new (author doc)
Assets/IdleFactory/Content/Schemas/gameplay-content-v1.schema.json   # new (doc-only schema)
Assets/IdleFactory/Content/Development/development-catalog.json      # new (sole gameplay source)
Assets/IdleFactory/Unity/Content/                            # new reader/validator/builder/loader; SO types removed at retirement
Assets/IdleFactory/Unity/Bootstrap/CoreGameFoundationBootstrap.cs    # changed
Assets/IdleFactory/Scenes/CoreGameFoundation.unity           # reference swap only (Unity tooling)
Assets/IdleFactory/Tests/EditMode/GameplayContent*Tests.cs, JsonDevelopmentContentTests.cs, JsonContentCompatibilityTests.cs, JsonContentAuthorityTests.cs, Fixtures/
Assets/IdleFactory/Tests/PlayMode/JsonContentBootstrapTests.cs, JsonContentPersistenceTests.cs, JsonContentPlayerTests.cs
```

**Structure Decision**: Use the existing four-assembly layout. No new assembly: the content reader belongs to the Unity adapter layer, which already owns Newtonsoft and content authoring.

## Complexity Tracking

None.

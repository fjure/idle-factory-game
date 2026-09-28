# Research: JSON Content Authoring Migration

**Date**: 2026-09-28 · **Spec**: [spec.md](spec.md)

Three read-only research assignments ran in parallel: packaged content access; strict parsing and reuse of the Save V1 validator; and presentation mapping plus affected tests. The parent verified every decision below against repository files.

## R01 — Packaged content access

- **Decision**: The bootstrap holds one serialized `TextAsset` reference to `Assets/IdleFactory/Content/Development/development-catalog.json`. It reads the asset synchronously through `TextAsset.bytes` and decodes those bytes with strict UTF-8 (`new UTF8Encoding(false, true)`). A byte order mark is rejected.
- **Rationale**: `CoreGameFoundationBootstrap.Initialize()` is synchronous. It builds the catalog before any session exists and routes every failure to `DisableWithError`, which gives FR-014 its no-partial-publication guarantee. A serialized reference is packaged in Windows (Mono, the Standalone default) and Android (IL2CPP, `ProjectSettings.asset` `scriptingBackend: Android: 1`) players without any access code specific to either platform. Reading `.bytes` keeps the bytes exact, which `.text` does not.
- **Alternatives rejected**: StreamingAssets with `UnityWebRequest` is asynchronous on Android, where content sits inside the APK. It would need new start-up state machinery and create the partial-initialization risk that FR-014 forbids. `Resources.Load` relies on an implicit path lookup, gives weaker authoring ergonomics, and adds no benefit over a direct reference.
- **Test seam**: Editor Play Mode tests inject bytes through a `Func<byte[]>` added to the `#if UNITY_EDITOR` composition seam. That seam does not exist in built players, so the player test calls the loader directly on the packaged bytes and on corrupted copies (the T052 pattern; see `plan.md` › Test seams). The authored scene is never edited.

## R02 — Strict parsing without touching Save V1

- **Decision**: Build a new content reader in `IdleFactory.Unity` on the proven pattern of a hand-written `JsonTextReader` token walk. It reuses the following Save V1 settings, but not Save V1 code:
  - a strict UTF-8 `StreamReader` that rejects a byte order mark;
  - `MaxDepth`;
  - `DateParseHandling.None`;
  - `FloatParseHandling.Decimal`, so that fractional or exponent numbers are rejected before any conversion;
  - no multiple content;
  - after the root value, the only allowed content is whitespace.

  Unknown members, duplicate members, wrong types and non-integer numbers are rejected in the token walk, using the same `seen`-set technique as `SaveV1Mapper.ReadObjectMember`.
- **Rationale**: `StrictJsonLexicalValidator` (`SaveV1Mapper.cs:843`) is a private scanner that hard-codes the Save V1 identifier member names. Parameterizing it would amount to rewriting it. Four suites cover Save V1 behavior only indirectly: `CheckpointMappingTests`, `CheckpointStoreTests`, `PersistenceApplicationTests` and `PlayerCheckpointRoundTripTests`. Keeping Save V1 byte-for-byte unchanged is safer (FR-016). The token-walk style never uses reflection-driven deserialization, so IL2CPP stripping is not a risk. T052 showed that the style runs on the Android ARM64 IL2CPP player without a `link.xml`.
- **Alternatives rejected**: Extracting a shared validator changes the Save V1 regression surface. `JsonConvert.DeserializeObject` with attribute-driven DTOs cannot detect duplicate members and depends on reflection that IL2CPP may strip.

## R03 — Validation stages and complete diagnostics

- **Decision**: The pipeline runs in this order:
  1. size check;
  2. strict UTF-8 decode;
  3. **version peek**: read only `contentSchemaVersion` and reject unsupported values (FR-003);
  4. **structural token walk** into fresh DTOs (FR-006);
  5. **semantic pass** over the DTO graph, which collects *all* violations (FR-007, FR-008);
  6. only when there are zero violations, domain constructors and the `DefinitionCatalog` constructor.

  Structural failures stop at the first violation. After a lexical error the rest of the stream cannot be interpreted reliably, so one located diagnostic is reported. Semantic failures are reported in full and in deterministic order: document order, then rule order.
- **Rationale**: The domain constructors in `Definitions.cs` and `DefinitionCatalog.cs` fail fast by design, and Save V1 relies on that. Collecting diagnostics before construction keeps the constructors' guarantees and gives authors every semantic error in one run. If a domain constructor still throws after a clean semantic pass, that is a validator gap. It is reported as an internal content-validation failure and fails the start-up.

## R04 — Limits

The limits below are safety guards, not gameplay limits. Content is trusted build input, but authors make mistakes.

| Limit | Value | Rationale |
|---|---:|---|
| Document size | 1 MiB | About 1,000× the development document. It bounds read time well below the SC-008 guard. Save V1's 16 MiB covers 100,000 buildings of player state and does not fit a hand-authored definition file. |
| JSON depth | 16 | Content nests to at most depth 5. Same value as Save V1. |
| Entries per top-level collection | 1,024 | Bounded diagnostics and validation cost with wide headroom. |
| Entries per nested list (recipe inputs and outputs, buffer capacities, orientations) | 256 | Matches the Save V1 per-buffer limit, so every authored capacity list is saveable. |
| ID length | 128 UTF-16 code units | FR-004. A new content guard modeled on Save V1. |
| Diagnostics reported per run | 100, then a truncation note | Keeps output readable. The count is still reported. |

## R05 — Order independence (SC-007)

`DefinitionCatalog` indexes definitions by ID in dictionaries, and `BuildingDefinition` sorts `AllowedOrientations`. Catalog behavior is therefore already independent of entry order, and the JSON path adds no sorting. Diagnostics still follow document order, for readability.

## R06 — Canonical form and review rules

- **Decision**: The canonical form uses:
  - two-space indentation, LF line endings and a final newline;
  - members in the documented order;
  - top-level collection entries sorted by ID (ordinal) and offline policies by revision;
  - nested amounts and capacities sorted by resource ID;
  - no redundant defaults.

  An Edit Mode **review-gate test** fails when the packaged document differs from its canonical rendering. The runtime does *not* reject non-canonical formatting, because formatting has no semantic meaning. Canonical ID text is different: a non-canonical ID is a runtime rejection (spec Assumptions).
- **Rationale**: Diffs stay minimal and deterministic for human and AI editors, and the game does not fail over whitespace.

## R07 — JSON Schema file

- **Decision**: Ship `Assets/IdleFactory/Content/Schemas/gameplay-content-v1.schema.json` (draft 2020-12) as **editor and documentation support only**. Edit Mode consistency tests parse it as plain JSON and assert that its property names, required lists, integer bounds and ID `maxLength` agree with the reader's constants. They also check that `docs/content/json-content-pipeline.md` documents every property (FR-002). No schema-validation package is added.
- **Rationale**: One enforcement authority (the C# reader), a precise reference for editors, and no new dependency (constitution X).

## R08 — Presentation mapping after retirement

- **Decision**: `F001SpriteCatalog` (`Unity/Presentation/F001SpriteCatalog.cs`) is already independent of the definition ScriptableObjects and remains the only presentation-key owner. The JSON carries `presentationKey` exactly as the domain does today: optional for resources (`OptionalText`) and required for buildings (`RequiredText`, `Definitions.cs:276`). After the catalog is built, the bootstrap checks that every non-null key resolves through `F001SpriteCatalog.TryGet`. An unresolved key calls `DisableWithError` with a diagnostic that names the key (FR-015, clarification 2026-09-28). This check runs in the Unity composition, because Core must not know sprites.
- **Note**: Resource presentation keys are carried and validated but not rendered today. This is unchanged Feature 001 behavior.

## R09 — Independent parity fixture

- **Decision**: A hand-written C# expected fixture (`Tests/EditMode/Fixtures/AcceptedFoundationCatalogFixture.cs`) records every accepted value from the current `.asset` files. The values are captured while the assets still exist, and the capture is verified against `DefinitionCatalogBuilder.Build(DevelopmentCatalog.asset)` in that same task. The fixture has no dependency on ScriptableObjects, so it survives retirement (FR-012). Capturing the fixture depends only on the current assets, so it can run **in parallel** with the contract and reader work.

## R10 — SC-008 measurement

- **Decision**: A `StandalonePlayer`-tagged Play Mode test times `Stopwatch` from the start of content load until the catalog is published, in the built Windows player. It records the median of 5 cold loads in the acceptance record. The pre-migration baseline is the same measurement around `DefinitionCatalogBuilder.Build`, captured before retirement. The result is compared with the 100 ms guard; exceeding it only triggers a Performance review (clarification 2026-09-28).

## R11 — Ownership of the Feature 001 Phase 8 seeds

- **Decision**: This feature's `tasks.md` replaces T042–T050 in `specs/001-core-game-foundation/tasks.md`. Those rows are annotated as superseded by `specs/002-json-content-authoring/tasks.md`. MAQA Issues #46–#54 are retitled and rescoped to the new task IDs, and missing tasks get new Issues. Records land under `specs/002-json-content-authoring/reviews/`.

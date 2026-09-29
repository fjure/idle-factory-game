# What I Learned: JSON Content Authoring Migration

**Feature**: Move gameplay definitions from ScriptableObjects to a versioned JSON document while preserving runtime and save behavior.
**Generated**: 2026-09-29
**Scope**: Full feature
**Implementation status**: 14/14 tasks completed (T069–T082).

---

## Key Decisions

### 1. Capture an independent pre-migration oracle

**What we did**: `AcceptedFoundationCatalogFixture` records the accepted definition values as Core types, and `PreMigrationCaptureTests` compares it field by field with the still-live ScriptableObject catalog. T070 also captures six Save V1 checkpoints, including the W001 workload, before that authoring path is retired.

**Why**: A migration needs an expected result independent of the new JSON reader. If both the input and expected catalog came from the same JSON document, a shared mistake could make the parity test pass.

**Alternatives considered**:

| Approach | Why it wasn't chosen |
|----------|---------------------|
| Use the JSON document as the expected fixture | The test would compare an implementation input with itself. |
| Keep the ScriptableObject asset as the long-term fixture | It would disappear with the old authoring path and keep tests coupled to Unity assets. |

**When you'd choose differently**: If the migration intentionally changes an ID or its meaning, first define an explicit save migration and owner decision; a parity fixture should not silently bless that change.

---

### 2. Parse with an explicit token walk into fresh DTOs

**What we did**: `GameplayContentReader` uses `JsonTextReader` to check the byte envelope and token structure, then builds a fresh DTO graph. It rejects duplicate and unknown members, non-integer number forms, invalid UTF-8 and other structural errors without reflection-based deserialization.

**Why**: The reader can enforce the exact contract at the point each token is seen and attach a JSON Pointer and source position to structural errors. It also avoids reflection behavior that could differ under Unity's IL2CPP player.

**Alternatives considered**:

| Approach | Why it wasn't chosen |
|----------|---------------------|
| Deserialize directly into DTOs | Ordinary deserialization can hide duplicate-member and coercion behavior the contract rejects. |
| Add a runtime JSON Schema validator | That would add a dependency and create a second enforcement authority. |

**When you'd choose differently**: Reflection-based deserialization is reasonable for trusted documents with permissive JSON semantics when linker behavior is verified on every target.

---

### 3. Keep the schema as author documentation

**What we did**: T071 adds the author guide and JSON Schema reference; T072 implements the C# reader as the runtime enforcement authority. T073 adds semantic validation and consistency tests, and T074 adds the packaged development catalog with parity and canonical-form checks. Bootstrap integration remains T075.

**Why**: Editors benefit from a familiar schema, while the game needs one precise source for duplicate handling, ordered diagnostics and limits. A consistency test can catch drift without making the schema engine a runtime dependency.

**Alternatives considered**:

| Approach | Why it wasn't chosen |
|----------|---------------------|
| Use the schema as the runtime validator | It does not by itself preserve the reader's diagnostic ordering and duplicate-member contract. |
| Maintain an unchecked guide and schema | Authors could receive rules that the game enforces differently. |

**When you'd choose differently**: A larger authoring ecosystem may justify schema-driven editor tooling, as long as the runtime's authoritative checks and consistency tests remain explicit.

---

### 4. Collect semantic errors before building domain objects

**What we did**: T073 reports semantic violations in document order, retains up to 100, and appends a truncation note to the last diagnostic when there are more. It builds a `DefinitionCatalog` only when validation is clean, and returns a constructor exception as `content.internal`.

**Why**: Authors can repair several cross-reference or policy mistakes in one edit cycle, while the catalog remains an all-or-nothing domain value. The constructor fallback protects the content boundary if a domain invariant changes without a matching validator update.

**Alternatives considered**:

| Approach | Why it wasn't chosen |
|----------|---------------------|
| Stop at the first semantic error | It would force authors to repeat the load-and-fix cycle for independent mistakes. |
| Construct partial definitions while reporting errors | A partially valid catalog could be mistaken for usable gameplay content. |

**When you'd choose differently**: Fail-fast validation can be useful when checks are expensive or later checks depend on earlier state; for small authored catalogs, a bounded collected report is easier to act on.

---

### 5. Make fixture generation read-only by default

**What we did**: `GenerateSaveV1FixturesTests` regenerates the six fixtures in memory during the normal Edit Mode run and compares them byte-for-byte with committed files. It writes fixtures and README only when `IDLEFACTORY_REGENERATE_SAVEV1_FIXTURES=1` is set, and normalizes README line endings to LF before comparison and writing.

**Why**: The test gives a repeatable regression check without modifying the checkout during ordinary validation. The Unity runner treats skipped tests as failures, so the generator must remain active in the normal suite instead of being marked explicit. Canonicalizing README line endings also avoids a Windows CRLF versus checked-in LF mismatch.

**Alternatives considered**:

| Approach | Why it wasn't chosen |
|----------|---------------------|
| Mark the generator `[Explicit]` | The normal suite reports it as skipped, which this repository's runner rejects. |
| Rewrite fixtures on every run | Test execution would mutate committed inputs and make review results harder to reproduce. |
| Compare platform-native README text | Windows and the checked-in LF Markdown can differ only by line endings, creating a false regression. |

**When you'd choose differently**: A generated artifact that is too large or costly to reproduce on every test run can use a dedicated opt-in check, but its default suite behavior must still match the runner's skip policy. Preserve native line endings only when they are part of the artifact's intended semantics.

---

### 6. Keep canonical formatting as a review gate

**What we did**: T074 checks that the authored development catalog matches its canonical rendering in Edit Mode, while the expected catalog remains the separately recorded `AcceptedFoundationCatalogFixture`. The canonical renderer stays in the test class; the runtime loader does not reject equivalent whitespace or member formatting.

**Why**: Stable member and collection order makes code review diffs predictable, and the independent fixture catches value drift. Separating formatting from runtime semantics avoids turning harmless whitespace changes into a startup failure.

**Alternatives considered**:

| Approach | Why it wasn't chosen |
|----------|---------------------|
| Derive the expected catalog from the JSON document | The parity test would compare the document with values produced from itself and could miss the same mistake on both sides. |
| Reject non-canonical whitespace in the runtime loader | Formatting has no gameplay meaning, so this would reject semantically equivalent content at startup. |

**When you'd choose differently**: A content-generation tool may need to write canonical files automatically, but the runtime can still accept equivalent formatting and tests should continue comparing against an independent fixture.

---

### 7. Make the error screen ready before the content can fail

**What we did**: T075 reordered `CoreGameFoundationBootstrap.Initialize()`. It now validates the non-content references, configures the HUD and applies the safe-area layout first. Then it loads content, validates sprites and resolves presentation keys, and only then creates the checkpoint store and session.

**Why**: A content failure has to be visible (the coded diagnostic in the existing error state) and harmless (no checkpoint I/O, no half-built session). The old order validated sprites after the store and session already existed, so a presentation failure could arrive after persistence was touched.

**Alternatives considered**:

| Approach | Why it wasn't chosen |
|----------|---------------------|
| Load content first, configure the HUD later | A rejection before HUD configuration has no screen to show its diagnostic on. |
| Build the session, then validate and dispose it on failure | Creating the checkpoint store already risks I/O, and "undo" paths are where partial publication bugs hide. |

**When you'd choose differently**: If content loading became asynchronous (for example StreamingAssets on Android), you would need an explicit loading state, and the error screen would still have to be ready before the first await.

---

### 8. Prove player behavior with the real packaged bytes, not the editor seam

**What we did**: T076's `StandalonePlayer` tests load the build scene through `SceneManager`, read the serialized `gameplayContent` bytes by reflection and run the real loader on them. The editor-only composition override is not available in players.

**Why**: The Feature 001 lesson was that green suites can miss player defects hidden behind test seams. Only the packaged `TextAsset` in a built player proves that the content ships and parses there.

**Alternatives considered**:

| Approach | Why it wasn't chosen |
|----------|---------------------|
| Extend the `#if UNITY_EDITOR` override for players | Production code would gain a test-only branch, and the test would still bypass the packaged asset. |
| Only test an embedded copy of the JSON | It would pass even if the packaged asset were missing or different. The embedded copy is kept only as a cross-check. |

**When you'd choose differently**: If a public read-only accessor for the packaged content existed for other reasons, use it instead of reflection. Reflection on a private field name is brittle under refactoring.

---

### 9. Prove save compatibility with committed bytes, not regenerated ones

**What we did**: T077 loads the six Save V1 files captured before the migration exactly as committed, against the JSON-built catalog. It asserts the README outcomes and re-serializes them byte for byte. The W001 scenario gets its own test JSON document, first proven equal to `FoundationFixtures.CreateW001`.

**Why**: Old saves on players' devices are the compatibility contract. Files produced by the new code could not detect a change in meaning. Byte-identical re-serialization also proves that IDs and policy revisions kept their meaning.

**Alternatives considered**:

| Approach | Why it wasn't chosen |
|----------|---------------------|
| Regenerate the checkpoints with the migrated build | They would agree with the new catalog by construction, even if meaning had drifted. |
| Skip W001 because its building is not in the development content | It would lose the largest determinism check. A dedicated JSON document keeps it on the JSON path. |

**When you'd choose differently**: Once a deliberate Save V1 migration exists, the fixtures become versioned inputs to that migration and are no longer frozen oracles.

---

### 10. Retire tests only with a case-by-case coverage map

**What we did**: T079 deleted `DefinitionCatalogBuilderTests` together with the builder. QA mapped each of the 20 deleted cases to a surviving test or marked it obsolete, where the case only exercised Unity-object handling. That found two gaps on the JSON path. The new `JsonDefinitionCatalogBuilderTests` closes them, and each test was shown to fail against a deliberately mutated builder.

**Why**: Deleting a test file together with the code it tested looks harmless, but some of its cases usually still protect behavior the replacement shares, such as flag pass-through and optional buffers. A green suite cannot show what it no longer checks.

**Alternatives considered**:

| Approach | Why it wasn't chosen |
|----------|---------------------|
| Port every builder test to the JSON path unchanged | Many cases were about null or destroyed Unity objects, which no longer exist. Porting them would add noise, not protection. |
| Trust the broad parity and compatibility tests | They use only `true` flags and a producing building, so a hard-coded `true` or a non-null empty buffer would have passed. |

**When you'd choose differently**: If the replacement reuses the old code path, as in a pure rename, a diff of test names is enough. The map is needed when the implementation behind the tests changes.

---

## Concepts to Know

### Independent golden master

**What it is**: A golden master is a separately recorded expected result used to detect later drift. Independence matters: if the code under test also creates the expected value, the test can repeat the same error on both sides.

**Where we used it**: `AcceptedFoundationCatalogFixture` and the six files under `Assets/IdleFactory/Tests/EditMode/Fixtures/SaveV1/` capture pre-migration values before retirement.

**Why it matters**: Later JSON, save and simulation tests can compare against accepted behavior without needing the old asset pipeline to remain alive.

### Structural versus semantic validation

**What it is**: Structural validation checks whether the document has valid JSON shape and types; semantic validation checks whether the values make sense together, such as whether a recipe references an existing resource.

**Where we used it**: T072 implements the structural `GameplayContentReader`; T073 adds the semantic validator and catalog builder, with tests for diagnostic coverage and schema/guide consistency.

**Why it matters**: Keeping stages separate gives syntax failures precise locations while allowing the semantic pass to report multiple authoring mistakes in one run.

### Stable IDs as save identity

**What it is**: A content ID is a compatibility key, not a display name. Save V1 stores IDs and offline-policy revisions, so changing their meaning can change an old checkpoint even when its JSON still parses.

**Where we used it**: The pre-migration fixture records `dev.input`, `dev.output`, `dev.recipe.basic` and `dev.machine.basic`; Feature 002 keeps Save V1 unchanged.

**Why it matters**: Removing or reinterpreting an ID requires an explicit compatibility decision and migration plan, not just an authoring edit.

### Canonicalize nested collection order

**What it is**: When a collection's order is not part of its meaning, sort it by a stable key before creating the domain representation. This gives equivalent authoring inputs the same internal order.

**Where we used it**: `JsonDefinitionCatalogBuilder` orders recipe amounts and buffer capacities by ordinal resource ID; `GameplayContentOrderTests` permutes outer and nested lists and compares the resulting catalogs.

**Why it matters**: Deterministic ordering makes parity checks and reviews reproducible without changing amounts, IDs, or gameplay rules.

### Content version versus save version

**What it is**: The content schema version describes the authoring document's shape; the Save V1 schema describes persisted player checkpoints. They evolve for different reasons.

**Where we used it**: `GameplayContentReader` reads `contentSchemaVersion` before interpreting definitions, while the captured fixtures keep the existing Save V1 format.

**Why it matters**: Versioning content must not accidentally imply that player checkpoints changed format or meaning.

### Canonical authoring form

**What it is**: Canonical formatting chooses one stable byte representation for semantically equivalent JSON, including member order, indentation, line endings and collection order. It is a review convention separate from runtime validation.

**Where we used it**: `development-catalog.json` is checked against its test-only rendering in `JsonDevelopmentContentTests`; the same tests compare loaded values with the independent fixture.

**Why it matters**: Stable diffs make authored changes easier to review while keeping runtime behavior independent of harmless formatting differences.

---

### Fail closed before side effects

**What it is**: Validate everything that can fail before the first operation that changes durable state, and publish results only after all checks pass.

**Where we used it**: The ordering of `CoreGameFoundationBootstrap.Initialize()`; `Definitions` is set only on the success path and cleared in `DisableWithError`.

**Why it matters**: Without it, a late failure leaves a half-initialized world. Here that could have been an opened checkpoint store with no valid catalog behind it.

---

### One shared lock needs one shared location

**What it is**: A cross-process lock only works if every participant resolves the same lock file. A relative path answered by a tool (here `git rev-parse --git-common-dir`) is relative to wherever the tool ran, not to wherever you join it.

**Where we used it**: `scripts/resource-lease.ps1` (fixed in PR #156). From the main checkout, Git answered `../.git` relative to `scripts/`, and the script joined it with the checkout root. That created a second, invisible lease store outside the repository.

**Why it matters**: The Unity and emulator leases protected nothing between the main checkout and worktrees. The symptom was contradictory `-Status` output, not an error, so ask tools for absolute paths when a location is shared.

---

## Architecture Overview

The accepted Core fixture provides a pre-migration reference; the unchanged `SaveV1Mapper` creates compatibility checkpoints from it. Separately, T071 documents the JSON contract, T072 parses JSON bytes into transient DTOs, and T073 validates semantics before building the domain catalog. T074 adds a packaged JSON document and checks its field-for-field parity and canonical form against the independent fixture. T075 made the packaged JSON the only runtime input of the bootstrap, and `MachineInspectorPresenter` now uses the single `bootstrap.Definitions` catalog. T076 and T077 prove the player behavior and Save V1 compatibility against the unchanged Core and persistence layers, T078 recorded the owner's approval, and T079 removed the ScriptableObject path; `JsonContentAuthorityTests` now guards the single JSON authority. T080–T082 closed the feature with independent reviews, Windows acceptance and an emulator-only Android run.

## Glossary

| Term | Meaning |
|------|---------|
| Save V1 | The existing serialized checkpoint format that records factory state and stable definition IDs. |
| `contentSchemaVersion` | The version of the gameplay authoring document, separate from Save V1. |
| DTO | A temporary data-transfer object populated while parsing and discarded after mapping. |
| JSON Pointer | A path such as `/buildings/0/recipeId` that locates a value in a JSON document. |
| IL2CPP | Unity's ahead-of-time C# compilation path used by player builds. |
| `StandalonePlayer` | NUnit category that `scripts/test-unity.ps1 -StandaloneWindowsPlayer` builds into and runs in a Windows player. |
| Lease | A file in `<git-common-dir>/resource-leases` that reserves the single Unity slot or the emulator for one owner. |

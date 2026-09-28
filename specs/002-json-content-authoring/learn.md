# What I Learned: JSON Content Authoring Migration

**Feature**: Move gameplay definitions from ScriptableObjects to a versioned JSON document while preserving runtime and save behavior.
**Generated**: 2026-09-29
**Scope**: Full feature
**Implementation status**: 4/14 tasks completed (T069 and T070–T072).

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

**What we did**: T071 adds the author guide and JSON Schema reference; T072 implements the C# reader as the planned enforcement authority. Runtime bootstrap integration is still pending, and T073 is assigned to add consistency checks between the schema, reader constants and guide.

**Why**: Editors benefit from a familiar schema, while the game needs one precise source for duplicate handling, ordered diagnostics and limits. A consistency test can catch drift without making the schema engine a runtime dependency.

**Alternatives considered**:

| Approach | Why it wasn't chosen |
|----------|---------------------|
| Use the schema as the runtime validator | It does not by itself preserve the reader's diagnostic ordering and duplicate-member contract. |
| Maintain an unchecked guide and schema | Authors could receive rules that the game enforces differently. |

**When you'd choose differently**: A larger authoring ecosystem may justify schema-driven editor tooling, as long as the runtime's authoritative checks and consistency tests remain explicit.

---

### 4. Make fixture generation read-only by default

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

## Concepts to Know

### Independent golden master

**What it is**: A golden master is a separately recorded expected result used to detect later drift. Independence matters: if the code under test also creates the expected value, the test can repeat the same error on both sides.

**Where we used it**: `AcceptedFoundationCatalogFixture` and the six files under `Assets/IdleFactory/Tests/EditMode/Fixtures/SaveV1/` capture pre-migration values before retirement.

**Why it matters**: Later JSON, save and simulation tests can compare against accepted behavior without needing the old asset pipeline to remain alive.

### Structural versus semantic validation

**What it is**: Structural validation checks whether the document has valid JSON shape and types; semantic validation checks whether the values make sense together, such as whether a recipe references an existing resource.

**Where we used it**: T072 implements the structural `GameplayContentReader`; T073 is assigned the semantic validator and catalog builder.

**Why it matters**: Keeping stages separate gives syntax failures precise locations while allowing the semantic pass to report multiple authoring mistakes in one run.

### Stable IDs as save identity

**What it is**: A content ID is a compatibility key, not a display name. Save V1 stores IDs and offline-policy revisions, so changing their meaning can change an old checkpoint even when its JSON still parses.

**Where we used it**: The pre-migration fixture records `dev.input`, `dev.output`, `dev.recipe.basic` and `dev.machine.basic`; Feature 002 keeps Save V1 unchanged.

**Why it matters**: Removing or reinterpreting an ID requires an explicit compatibility decision and migration plan, not just an authoring edit.

### Content version versus save version

**What it is**: The content schema version describes the authoring document's shape; the Save V1 schema describes persisted player checkpoints. They evolve for different reasons.

**Where we used it**: `GameplayContentReader` reads `contentSchemaVersion` before interpreting definitions, while the captured fixtures keep the existing Save V1 format.

**Why it matters**: Versioning content must not accidentally imply that player checkpoints changed format or meaning.

---

## Architecture Overview

The accepted Core fixture provides a pre-migration reference; the unchanged `SaveV1Mapper` creates compatibility checkpoints from it. Separately, T071 documents the JSON contract and T072 parses JSON bytes into transient DTOs. At this checkpoint, semantic validation, catalog construction, the packaged JSON document and bootstrap integration are still future tasks, so the game's runtime authority has not yet moved.

## Glossary

| Term | Meaning |
|------|---------|
| Save V1 | The existing serialized checkpoint format that records factory state and stable definition IDs. |
| `contentSchemaVersion` | The version of the gameplay authoring document, separate from Save V1. |
| DTO | A temporary data-transfer object populated while parsing and discarded after mapping. |
| JSON Pointer | A path such as `/buildings/0/recipeId` that locates a value in a JSON document. |
| IL2CPP | Unity's ahead-of-time C# compilation path used by player builds. |

# Feature Specification: JSON Content Authoring Migration

**Feature Branch**: `feat/002-json-content-authoring`

**Created**: 2026-09-28

**Status**: Draft

**Input**: User description: "Post-foundation JSON content authoring migration (roadmap seeds T042–T050 in specs/001-core-game-foundation/tasks.md Phase 8, existing MAQA Issues #46–#54). Replace the gameplay/balancing ScriptableObject authoring (resources, recipes, buildings, offline policy) with a versioned, documented, strictly validated UTF-8 JSON source packaged with the game, mapped through fresh DTOs into the existing immutable DefinitionCatalog. No new mechanics, content families, balance values or Save V1 format change; presentation-only references stay separate. Every D001 stable ID and value must be preserved, existing Save V1 checkpoints must keep loading with identical meaning, invalid/missing JSON must fail explicitly without partial state, and the old ScriptableObject gameplay authority is retired only after compatibility is proven."

## Context and authority

This feature carries out the post-foundation successor to research decision R03. It is recorded in `specs/001-core-game-foundation/research.md` ("Post-foundation successor to R03") and `specs/001-core-game-foundation/data-model.md` (`DefinitionCatalog`, `OfflinePolicyDefinition`). Gameplay definitions currently come from Unity ScriptableObject assets and are copied into the engine-independent, immutable definition catalog. This feature changes only the **authoring input**. The catalog, simulation, persistence format and player-visible behavior of Feature 001 stay unchanged.

This specification supersedes the roadmap seeds T042–T050 in Phase 8 of `specs/001-core-game-foundation/tasks.md`. That phase says those rows do not satisfy the Spec Kit lifecycle for this migration. The existing MAQA Issues #46–#54 are reconciled to this feature's approved tasks without duplication. This feature's review and acceptance records live under `specs/002-json-content-authoring/`.

Wiki knowledge carried forward (query 2026-09-28):

- IDs are normalized, case-sensitive ordinal strings that act as save identities, never labels or asset paths. Their meaning is immutable within Save V1. Source: [core architecture](../../wiki/pages/core-architecture.md) (S001).
- Save V1 parser safety limits: 16 MiB, 100,000 buildings, 256 entries per buffer, 128 UTF-16 code units per ID. The offline policy is revision 1: numerator 4, denominator 5, eight-hour credited cap. Source: [local checkpoints and offline return](../../wiki/pages/local-checkpoints-and-offline-return.md) (S001).
- The D001 fixture values do not establish final balance. Source: [deterministic production](../../wiki/pages/deterministic-production.md) (S001).
- Wiki coverage gaps that this specification resolves: limits for content documents, how packaged content is made available on each platform, and rules for evolving the content schema.

## User Scenarios & Testing *(mandatory)*

The actors are **content authors**, meaning human designers and AI agents who edit gameplay definitions, and **players** of the development build. Players see no behavior change. Their value from this feature is continuity: their factory and checkpoints behave exactly as before.

### User Story 1 - Author and review gameplay content as text (Priority: P1)

A content author opens one documented, human-readable text document, the gameplay content document. It describes every current resource, recipe, building and offline policy. The author changes a definition, reviews the change as a plain text diff, and runs the automated content validation. Validation either accepts the document or names every rejected entry and rule.

**Why this priority**: Readable, diffable, validated text authoring is the purpose of the migration. It lets humans and AI agents author and review content without Unity asset tooling, and it prevents invalid content from reaching the game.

**Independent Test**: Validate the migrated development document and a set of deliberately broken copies with the automated validation alone, without a scene or running game. The valid document is accepted. Each broken copy is rejected with a diagnostic that names the entry, the field and the violated rule.

**Acceptance Scenarios**:

1. **Given** the migrated development content document, **When** it is validated, **Then** it is accepted, and the resulting definitions equal the accepted Feature 001 definitions field for field.
2. **Given** a copy with a misspelled field name, **When** it is validated, **Then** it is rejected as an unknown field, the field and entry are named, and no definitions are produced.
3. **Given** a copy in which a recipe references a resource ID that is not defined, **When** it is validated, **Then** it is rejected as an unresolved reference that names the recipe and the missing ID.
4. **Given** a copy whose declared content schema version is unsupported, **When** it is validated, **Then** it is rejected before any definition is interpreted.
5. **Given** the content contract documentation, **When** an author who knows neither Unity nor the codebase adds a hypothetical second resource following its examples, **Then** the document validates on the first or second attempt. The accompanying documentation states that such an addition is a content change requiring its own specification.

---

### User Story 2 - Play from JSON-authored content with unchanged behavior (Priority: P1)

A player starts the development build. The game builds its definitions only from the packaged content document. Navigation, placement, production, the inspector, save, load and offline return behave exactly as in the accepted Feature 001 build.

**Why this priority**: The migration has value only if it is invisible to players. Any drift in stable IDs or values would change production, balance or save meaning.

**Independent Test**: Run the Feature 001 D001 journeys, meaning placement, a two-cycle production run with blocking, save, restore and offline return, on a build whose definitions come only from the content document. Compare the outcomes with the accepted pre-migration outcomes.

**Acceptance Scenarios**:

1. **Given** a fresh start, **When** the D001 machine is placed and runs two production cycles, **Then** the inputs consumed, the outputs produced, the timing and the blocked states match the accepted Feature 001 results exactly.
2. **Given** a Save V1 checkpoint written by the pre-migration build, **When** the migrated build loads it, **Then** it loads without modification, and every building, buffer, progress value and offline-policy revision has the same meaning.
3. **Given** a loaded pre-migration checkpoint with elapsed offline time, **When** the player returns, **Then** the offline credit and resulting production equal what the pre-migration build would produce for the same checkpoint and interval.
4. **Given** the migrated build, **When** it saves, **Then** the checkpoint is valid Save V1 and loads identically in both the pre-migration and migrated builds.

---

### User Story 3 - Fail explicitly and safely on missing or invalid content (Priority: P1)

The packaged content document can be missing, unreadable, truncated, oversized or invalid. It can also be incompatible with an existing checkpoint, for example when an ID the checkpoint references was removed. In each case the game reports a clear content failure and publishes no partial definitions, no factory state and no modified checkpoint.

**Why this priority**: A silent fallback or partial catalog would corrupt player state or hide authoring mistakes. The constitution requires explicit failure and validated input.

**Independent Test**: Start the game with each failure class substituted for the packaged document. Verify the error state, the diagnostic, the absence of a published catalog or live factory, and byte-identical checkpoint files.

**Acceptance Scenarios**:

1. **Given** the content document is missing, **When** the game starts, **Then** it enters the existing initialization error state, reports that gameplay content is unavailable, and neither loads nor writes a checkpoint.
2. **Given** an invalid document, **When** the game starts, **Then** the same error state appears, and a developer diagnostic names the first failing rule and its location.
3. **Given** a document that is valid on its own but lacks an ID referenced by the existing checkpoint, **When** the checkpoint is loaded, **Then** the checkpoint is rejected by the existing Save V1 incompatibility path, the files stay unchanged, and no reward or state is published.
4. **Given** any content failure, **When** the game is inspected, **Then** no earlier, cached or alternative gameplay definition source was used as a silent fallback.

---

### User Story 4 - A single gameplay authority after retirement (Priority: P2)

After compatibility is proven, the old gameplay ScriptableObject definitions and their builder are removed. The content document is the only gameplay and balancing source. Presentation-only assets such as sprites, prefabs and placeholder graphics remain and are joined to gameplay definitions by stable ID or presentation key.

**Why this priority**: Two gameplay sources would violate the constitution's single-source-of-truth rule. Retirement is ordered after proof, so it is P2 in sequence, not in importance.

**Independent Test**: An automated authority check finds no gameplay or balancing value outside the content document. It also finds that every presentation reference resolves to a defined ID or presentation key, and it runs the full suites green.

**Acceptance Scenarios**:

1. **Given** the retired state, **When** the project is searched for gameplay definition sources, **Then** exactly one exists, the packaged content document.
2. **Given** a presentation key in the content document, **When** the scene renders the building, **Then** the matching presentation asset is used. A key without a presentation asset stops start-up in the initialization error state, with a diagnostic naming the key, rather than silently drawing nothing.
3. **Given** the retired state, **When** parity, compatibility and full suites run, **Then** they pass without the retired assets.

### Edge Cases

- Empty file, byte order mark, invalid UTF-8, trailing content after the root value, or a truncated document: rejected as malformed.
- Duplicate object members in any object: rejected, never "last one wins".
- A document above the content size limit, or more entries than a collection limit allows: rejected before full interpretation.
- Non-integer, fractional, exponent-form, negative or out-of-range numbers where whole units are required: rejected.
- `null` or an empty collection where a non-empty collection is required: rejected.
- IDs that are empty or over 128 UTF-16 code units long: rejected. IDs not written in canonical form (surrounding whitespace, or not in Unicode NFC, the existing trim + NFC normalization): rejected, so the document text equals the stable identity. IDs that differ only by letter case are distinct.
- Two entries with the same normalized ID within a family: rejected.
- A recipe with zero or negative amounts or duration, or a duration that overflows the tick range: rejected.
- A building footprint with non-positive dimensions, an invalid or duplicate orientation, or a recipe resource missing from its buffer capacities: rejected.
- Two offline policies with the same revision, a non-positive revision, a non-positive numerator or denominator, a numerator greater than or equal to the denominator (the existing domain rule), or a negative cap: rejected.
- Entries listed in a different order but with identical content: produce an identical catalog.
- A content document that removes or changes the meaning of an ID or policy revision referenced by an existing checkpoint: the checkpoint is rejected through the existing incompatibility path. The content change itself requires an explicit compatibility decision (see FR-017).

## Requirements *(mandatory)*

### Functional Requirements

#### Content contract

- **FR-001**: The project MUST define a versioned gameplay content contract for UTF-8 JSON documents. It covers resources, recipes, buildings (footprint, allowed orientations, recipe, input and output buffer capacities), offline policies and development metadata (development-only flag, development label, presentation key). All fields are expressed in the existing units: whole resource units and 100 ns ticks.
- **FR-002**: The contract MUST be documented for human and AI editors. The documentation includes a complete annotated example, every field's meaning, unit, bounds and whether it is required, the ID rules, the canonical formatting and ordering rules for review, the limits, and the schema evolution policy. Two automated checks MUST prove completeness: every field of the schema is documented, and the documented example validates.
- **FR-003**: Every content document MUST declare its content schema version. That version is independent of the Save V1 checkpoint schema version. A document with an unsupported version MUST be rejected before any definition is interpreted.
- **FR-004**: Stable IDs MUST be explicit and non-empty, written in the canonical form produced by the existing domain normalization (trim + Unicode NFC), and compared by case-sensitive ordinal identity. The content validator MUST also enforce a maximum of 128 UTF-16 code units per ID. This is a new content-validator guard modeled on the Save V1 parser limit; the domain identifiers do not enforce it today. IDs MUST NOT be derived from labels, file names or asset paths.
- **FR-005**: The contract MUST describe how future content families gain definitions, including costs, research, items, prestige and contracts. Each is added only through its own approved specification and a new content schema version. This feature MUST NOT add fields, placeholders or values for those families.

#### Validation and mapping

- **FR-006**: Parsing MUST be strict and MUST reject malformed, truncated or oversized documents. It MUST also reject missing required fields, unknown fields, duplicate members, wrong types, non-integer or out-of-range numbers, and `null` or empty required collections.
- **FR-007**: Semantic validation MUST reject the entire document when any rule fails. The rules cover duplicate IDs after normalization, unresolved resource or recipe references, non-positive recipe amounts or durations, buffer capacities that do not cover the recipe's inputs and outputs, invalid footprints or orientations, and invalid or duplicate offline-policy revisions. Validation MUST publish no partial set of definitions.
- **FR-008**: Validation MUST report every failure deterministically, with the entry, field and rule. Diagnostics MUST NOT include file-system paths outside the project, or player data.
- **FR-009**: A valid document MUST map into the existing immutable definition catalog. No parsed intermediate object remains reachable or authoritative after mapping. The resulting catalog MUST be independent of the entry order in the document.
- **FR-010**: Validation MUST be runnable as an automated check without a scene or running game, so authors and review gates can validate content before it is merged.

#### Migration and runtime

- **FR-011**: The development content MUST be migrated into one packaged content document that preserves every accepted Feature 001 value. This includes the resources `dev.input` and `dev.output`, the recipe `dev.recipe.basic` (2 × `dev.input` → 1 × `dev.output`, 5 seconds), and the building `dev.machine.basic` (2×2 footprint, its allowed orientations, input capacity 4, output capacity 1, presentation key `F001-MACHINE-D001-NORMAL`). It also includes all development flags and labels, and offline policy revision 1 (4/5, eight-hour credited cap).
- **FR-012**: Parity MUST be proven against an independent expected fixture of the accepted pre-migration catalog values. The fixture MUST remain valid after the old assets are retired.
- **FR-013**: The game MUST build its gameplay definitions only from the packaged content document, in the editor and in built players. Evidence MUST come from Play Mode tests in the editor and from the tagged Windows Standalone player suite. Android evidence comes from the ARM64 Development Player on the API 36 x86_64 emulator, is labeled emulator-only, and is recorded as unresolved if that Player cannot run. Physical Android devices and iOS remain unverified.
- **FR-014**: A missing, unreadable or invalid content document MUST put the game into the existing initialization error state, with a content-specific developer diagnostic. The game MUST NOT fall back to another definition source, publish a partial catalog, create or change a live factory, or read, write or modify a checkpoint.
- **FR-015**: Presentation-only references, such as sprites, prefabs, placeholder graphics, audio, animation and VFX, MUST stay outside the content document. They MUST be joined by stable ID or presentation key. A presentation key without a matching presentation asset is a content integrity failure. It MUST be handled like invalid content under FR-014: initialization error state, a diagnostic naming the key, and no factory published.

#### Compatibility and retirement

- **FR-016**: The Save V1 format, its limits and its validation MUST remain unchanged. Checkpoints written before the migration MUST load with identical meaning, and checkpoints written after it MUST remain valid Save V1.
- **FR-017**: Removing an ID, renaming it, or changing the meaning of an ID or offline-policy revision referenced by Save V1 is a save-compatibility change. It MUST require an explicit human decision and a migration plan. The content contract documentation MUST state this rule. Such changes are not permitted in this feature.
- **FR-018**: Simulation, offline reconciliation and all Feature 001 functional requirements and success criteria MUST produce outcomes identical to the pre-migration build for the D001 and W001 fixtures.
- **FR-019**: The gameplay ScriptableObject definitions, their catalog root and their builder MUST be retired only after FR-012, FR-016 and FR-018 pass on committed evidence, and after the owner's explicit approval of that evidence is recorded in this feature's review record. Git history is the rollback source. Afterwards an automated check MUST prove that no second gameplay or balancing source and no silent fallback remain.
- **FR-020**: This feature MUST NOT add player-facing mechanics, content families, content entries or balance values, and it MUST NOT change any existing value.
- **FR-021**: Independent Game Designer/Product Owner, Software Architect, Game Systems/Economy, Test/QA and Security/Privacy verdicts on the exact committed migration MUST be recorded before final acceptance. UI/UX and Technical Art verdicts are required only if their boundaries changed, and omitted roles are recorded with their reasons.

### Key Entities *(include if feature involves data)*

- **Gameplay content document**: a packaged, versioned UTF-8 JSON text. It holds a content schema version and the resource, recipe, building and offline-policy collections. It is authored input only and is never runtime state.
- **Content schema version**: the integer version of the content contract. It is distinct from the Save V1 checkpoint schema version.
- **Resource / Recipe / Building / Offline policy definition**: the existing Feature 001 definitions (see `specs/001-core-game-foundation/data-model.md`), unchanged in meaning and now authored in the document.
- **Content validation diagnostic**: a deterministic report of rejected entries, naming the entry, the field and the rule.
- **Definition catalog**: the existing immutable runtime authority, unchanged.
- **Presentation mapping**: the Unity-side association from stable ID or presentation key to presentation assets. It carries no gameplay values.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the accepted Feature 001 definition values are reproduced exactly from the content document, with zero differences against the independent parity fixture.
- **SC-002**: 100% of the representative pre-migration Save V1 checkpoints load without modification and produce identical simulation and offline outcomes for the D001 and W001 fixtures.
- **SC-003**: Every edge-case class listed in this specification has at least one automated rejection check. Every rejection names the entry, field and rule, and none produces a partial set of definitions.
- **SC-004**: In 100% of the missing or invalid content start-ups tested, the game shows the initialization error state, publishes no factory state, and leaves checkpoint files byte-identical. At least one such case is proven in a built Windows player against the real checkpoint store.
- **SC-005**: In a recorded walkthrough, a reviewer who has not seen the implementation adds a hypothetical resource and recipe using only the documentation. The reviewer obtains a valid document within two validation attempts. The Game Designer/Product Owner review records the attempts, the diagnostics shown and the verdict.
- **SC-006**: After retirement, exactly one gameplay definition source exists, and the complete configured test suites pass with no relevant console errors.
- **SC-007**: Reordering the entries of the content document changes neither the validation result nor the resulting definitions.
- **SC-008**: The cold-start cost of content loading is measured in a built Windows player on the reference development machine and recorded against a 100 ms guard, compared with the pre-migration build. The measurement is required. Exceeding the guard does not block acceptance; it triggers a Performance Engineer review.

## Assumptions

- The migrated document is one development content document, as the Phase 8 seeds anticipated. Splitting content across several files is a later decision, made when content volume justifies it.
- The content is packaged read-only with the game. Players cannot change it, and runtime override, modding or downloadable content are out of scope. Content is therefore trusted build input, but it is still fully validated, because authors, including AI agents, make mistakes.
- IDs in the document must already be in canonical form. The domain's trim + NFC normalization is applied as a check, not as a silent rewrite. This keeps review diffs identical to save identities, and it is stricter than, but compatible with, the existing catalog rules.
- The 100 ms guard in SC-008 applies to a single small document and is not a measured product target (clarified 2026-09-28: measured and recorded, not blocking).
- Content document limits are safety guards, not gameplay limits. They follow the Save V1 parser style. The plan fixes their exact values (size and per-collection entry counts) with headroom well above the development content.
- Offline policies are a collection keyed by revision. The development document contains only revision 1. A later revision is added alongside revision 1 and never replaces it.
- The existing initialization error state and its player-facing presentation are reused unchanged. Content failures are development and build-integrity failures, not expected player outcomes.
- The reference Windows machine and cold-start measurement method match the Feature 001 performance evidence.
- iOS remains deferred under Issue #39. Android evidence remains emulator-only, and physical-device and release qualification remain unverified.

## Clarifications

### Session 2026-09-28

- Q: Must the owner approve explicitly before the gameplay ScriptableObjects are deleted, or do green automated checks suffice? → A: Explicit owner approval of the parity, Save V1 and simulation evidence, recorded in the review record before retirement; Git history is the rollback source (FR-019).
- Q: Does the +100 ms cold-start budget block acceptance? → A: No. It must be measured in a Windows player and recorded; exceeding it triggers a Performance review (SC-008).
- Q: What happens when a presentation key has no matching presentation asset? → A: Start-up error, the same as invalid content: initialization error state, a diagnostic naming the key, no factory (FR-015, US4-AS2).

## Scope boundaries

**In scope**: the content contract, its schema and documentation, strict parsing and semantic validation, mapping into the existing catalog, the migration of the development content, the runtime switch, the compatibility proof, the retirement of the gameplay ScriptableObjects, and the reviews and acceptance.

**Out of scope**: new mechanics, content families, content entries or balance values; Save V1 changes; runtime or modded content loading; remote or downloadable content; content editors or authoring UI; localization; costs, research, items, prestige and contracts; iOS validation.

## Dependencies

- The Feature 001 parent completion verdict is merged to `origin/main` ([PR #136](https://github.com/fjure/idle-factory-game/pull/136)). This feature's MAQA dispatch waits for that merge.
- The existing MAQA Issues #46–#54 are reconciled to this feature's tasks without duplicates.

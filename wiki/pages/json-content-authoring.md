---
title: JSON content authoring
type: decision
sources: [S008]
updated: 2026-09-28
---

# JSON content authoring

Feature 002 replaces the gameplay ScriptableObjects with one packaged, versioned UTF-8 JSON document, `development-catalog.json`. The runtime `DefinitionCatalog`, Core, the simulation and Save V1 stay unchanged. The migration adds no mechanic, entry or value. (S008)

## Planned decisions (2026-09-28)

- **Loading**: The bootstrap holds a serialized `TextAsset` and reads `.bytes` synchronously with strict UTF-8, rejecting a byte order mark. StreamingAssets was rejected because Android access is asynchronous inside the APK, and that would put the no-partial-publication guarantee at risk. (S008)
- **Parser**: A new content reader built on the proven `JsonTextReader` token walk. Save V1's private `StrictJsonLexicalValidator` is neither reused nor extracted: it hard-codes the Save V1 member names, and it is covered only indirectly by the persistence suites. Reflection-based deserialization is avoided because of IL2CPP. (S008)
- **Stages**: The pipeline runs in this order: `contentSchemaVersion` peek → structural walk (first error, with JSON Pointer, line and column) → semantic pass that collects *all* violations → fail-fast domain constructors, only when the semantic pass is clean. Every semantic rule code mirrors an existing throw in `Definitions.cs` or `DefinitionCatalog.cs`. A constructor throw after a clean pass becomes `content.internal`. (S008)
- **Limits**: 1 MiB, depth 16, 1,024 entries per family, 256 per nested list, and 128 UTF-16 code units per ID. The ID length limit is a new content guard; domain identifiers only trim and NFC-normalize. IDs must already be in canonical form in the document. (S008)
- **Policy**: The catalog accepts exactly offline revision 1 with 4/5 and eight hours. A new revision needs its own specification. (S008)
- **Presentation**: `F001SpriteCatalog` remains the only owner of presentation keys. An unresolved key is a start-up error, not a silent blank (owner clarification). (S008)
- **Schema file**: The JSON Schema file serves documentation only. Tests keep it consistent with the reader constants and the author guide, and no schema-validation package is used. (S008)
- **Retirement**: The ScriptableObjects are removed only after recorded owner approval of the parity, Save V1 and simulation evidence. Git history is the rollback source. The SC-008 load-time guard of 100 ms is measured, not blocking. (S008)

## Gotchas found in review

- `MachineInspectorPresenter` built a second catalog from `bootstrap.DevelopmentCatalog`. Single-authority work must search for every `Build(` call site, not just the bootstrap. (S008)
- `spriteCatalog.Validate()` ran after checkpoint store and session creation. A content failure must be detected before any checkpoint I/O, so `Initialize()` is reordered. (S008)
- The composition test seam is `#if UNITY_EDITOR`. Player evidence therefore calls the loader directly, as described in [player-level test gaps](./player-level-test-gaps.md). (S008)

Related: [core architecture](./core-architecture.md), [local checkpoints and offline return](./local-checkpoints-and-offline-return.md).

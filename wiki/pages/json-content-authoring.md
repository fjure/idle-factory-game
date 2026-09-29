---
title: JSON content authoring
type: decision
sources: [S008, S010]
updated: 2026-09-29
---

# JSON content authoring

Feature 002 replaces the gameplay ScriptableObjects with one packaged, versioned UTF-8 JSON document, `development-catalog.json`. The runtime `DefinitionCatalog`, Core, the simulation and Save V1 stay unchanged. The migration adds no mechanic, entry or value. (S008)

## Planned decisions (2026-09-28)

- **Loading**: The bootstrap holds a serialized `TextAsset` and reads `.bytes` synchronously with strict UTF-8, rejecting a byte order mark. StreamingAssets was rejected because Android access is asynchronous inside the APK, and that would put the no-partial-publication guarantee at risk. (S008)
- **Parser**: A new content reader built on the proven `JsonTextReader` token walk. Save V1's private `StrictJsonLexicalValidator` is neither reused nor extracted: it hard-codes the Save V1 member names, and it is covered only indirectly by the persistence suites. Reflection-based deserialization is avoided because of IL2CPP. (S008)
- **Stages**: The pipeline runs in this order: `contentSchemaVersion` peek → structural walk (first error, with JSON Pointer, line and column) → semantic pass that collects violations → domain catalog construction only when validation is clean. Every semantic rule code mirrors an existing throw in `Definitions.cs` or `DefinitionCatalog.cs`. (S008)
- **Limits**: 1 MiB, depth 16, 1,024 entries per family, 256 per nested list, and 128 UTF-16 code units per ID. The ID length limit is a new content guard; domain identifiers only trim and NFC-normalize. IDs must already be in canonical form in the document. (S008)
- **Policy**: The catalog accepts exactly offline revision 1 with 4/5 and eight hours. A new revision needs its own specification. (S008)
- **Presentation**: `F001SpriteCatalog` remains the only owner of presentation keys. An unresolved key is a start-up error, not a silent blank (owner clarification). (S008)
- **Schema file**: The JSON Schema file serves documentation only. Tests keep it consistent with the reader constants and the author guide, and no schema-validation package is used. (S008)
- **Retirement**: The ScriptableObjects are removed only after recorded owner approval of the parity, Save V1 and simulation evidence. Git history is the rollback source. The SC-008 load-time guard of 100 ms is measured, not blocking. (S008)

## Gotchas found in review

- `MachineInspectorPresenter` built a second catalog from `bootstrap.DevelopmentCatalog`. Single-authority work must search for every `Build(` call site, not just the bootstrap. (S008)
- `spriteCatalog.Validate()` ran after checkpoint store and session creation. A content failure must be detected before any checkpoint I/O, so `Initialize()` is reordered. (S008)
- The composition test seam is `#if UNITY_EDITOR`. Player evidence therefore calls the loader directly, as described in [player-level test gaps](./player-level-test-gaps.md). (S008)

## Pre-migration performance baseline

The accepted development catalog's `DefinitionCatalogBuilder.Build` baseline is a **0.0320 ms median** from five Windows standalone-player samples. The sorted samples were 0.0114, 0.0119, 0.0320, 0.0850 and 7.6382 ms; the first-call outlier suggests fixed/JIT warm-up cost dominates this tiny catalog. Compare the post-migration load on the same platform and report the median, not a single sample or mean. The player test loads the build-settings scene through `SceneManager` because `AssetDatabase` is unavailable in a player; no production code was changed for the measurement. (S008)

## Save V1 fixture regression check

Normal Edit Mode runs regenerate six captured checkpoints in memory and compare them byte-for-byte with committed files. Disk and README writes require `IDLEFACTORY_REGENERATE_SAVEV1_FIXTURES=1`; active assertions avoid skips, which the Unity runner treats as suite failures. (S009, S010)

README line endings are canonicalized to LF for comparison and opt-in writes, avoiding platform-specific mismatches. (S010)

Related: [JSON content migration evidence](./json-content-migration-evidence.md), [canonical JSON content form](./json-content-canonical-form.md), [gameplay content validation](./gameplay-content-validation.md), [core architecture](./core-architecture.md), [local checkpoints and offline return](./local-checkpoints-and-offline-return.md).

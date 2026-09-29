---
title: JSON content migration evidence
type: concept
sources: [S008]
updated: 2026-09-29
---

# JSON content migration evidence

How Feature 002 proved that switching gameplay authoring to JSON changed nothing observable, and which seams carry that proof. Design decisions are in [JSON content authoring](./json-content-authoring.md). (S008)

## Runtime authority (T075)

`CoreGameFoundationBootstrap.Initialize()` runs its steps in this order:

1. non-content references;
2. HUD configuration and safe-area layout;
3. content bytes;
4. `GameplayContentLoader`;
5. `spriteCatalog.Validate()`;
6. every `presentationKey`;
7. only then the checkpoint store and `FactorySession`.

Because the HUD is ready first, every rejection shows its coded diagnostic in the existing error state with Build and Confirm disabled. `Definitions` is published only on success. A null serialized `TextAsset` reaches the reader as `content.empty`, not as a generic missing reference. A throwing byte source becomes `content.unavailable` and reports only the exception type, because I/O messages can carry paths. An unresolved presentation key names the entry and the key (FR-015). (S008)

## Built player (T076)

The editor composition seam is unavailable in players. The `StandalonePlayer` tests therefore load the build-index-0 scene through `SceneManager`, read the packaged `gameplayContent` bytes by reflection, and load them with `GameplayContentLoader`. The Play Mode test assembly also compiles for players and cannot reference the editor-only Edit Mode fixtures, so it duplicates the accepted values. QA diffed the duplicated values byte for byte against `development-catalog.json` and the T070 fixture. (S008)

Measured SC-008 result in the Windows player: a median `GameplayContentLoader.Load` of **0.0612 ms**, against a pre-migration `DefinitionCatalogBuilder.Build` baseline of 0.0320 ms. The 100 ms guard is measure-only. (S008)

## Save V1 compatibility (T077)

All six pre-migration Save V1 fixtures load unmodified against the JSON catalog and re-serialize byte-identically. Scenario 06 uses the `test.machine.w001` building, which the development document does not contain, so the test authors a W001 JSON document and first proves it equal to `FoundationFixtures.CreateW001`. A valid document that renames a referenced building ID produces `InvalidCheckpoint` through `SaveV1Mapper`'s catalog resolution, not through content initialization. The checkpoint files and live state stay unchanged. (S008)

## Retirement gate (T078)

The owner approved the retirement of the ScriptableObjects on 2026-09-29 against evidence commit `a4ddbc7`. Git history is the rollback source. (S008)

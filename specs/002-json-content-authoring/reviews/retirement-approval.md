# Retirement Approval (T078) — JSON Content Authoring Migration

**Date:** 2026-09-29 · **Decision:** **APPROVED** by the project owner (fjure), via explicit answer in the parent session · **Evidence commit:** `a4ddbc7` (`origin/main`, merge of PR #157)

The owner approves T079: the gameplay ScriptableObject definitions, their catalog root and `DefinitionCatalogBuilder` may be retired (FR-019, spec clarification 2026-09-28).

## Evidence presented

| Evidence | Task / PR | Result |
|---|---|---|
| Catalog parity | T074 (#49, PR #151): `JsonDevelopmentContentTests` against the independent `AcceptedFoundationCatalogFixture` (T070, PR #145) | Every accepted Feature 001 value is reproduced exactly (FR-011, FR-012, SC-001) |
| Save V1 and simulation compatibility | T077 (#51, PR #157): `JsonContentCompatibilityTests`, `JsonContentPersistenceTests` | All 6 pre-migration Save V1 fixtures load unmodified. The offline return (288e9 / 288e9 / 230.4e9 ticks), the D001 two-cycle run and W001 determinism are identical. Re-serialization is byte-identical. A missing referenced ID is rejected through the Save V1 path with no file or state change (FR-016, FR-018, SC-002) |
| Runtime authority and failure safety | T075 (#50, PR #154): `JsonContentBootstrapTests` | Start-up reads only the packaged JSON. Every failure class shows a coded error before checkpoint access, and checkpoint files stay byte-identical (FR-013–FR-015, SC-004) |
| Built player | T076 (#139, PR #155): `JsonContentPlayerTests` | The packaged JSON loads in the Windows player. Corrupted bytes are rejected and a real store stays untouched. Median load time is 0.0612 ms against a 0.0320 ms baseline, with a 100 ms guard (SC-008) |
| Suites | QA runs at `9869415` and `ac66cfd` | Edit Mode 677/677, Play Mode 156/156, Windows player 6/6 |

## Rollback

Git history is the rollback source. Every retired type and `.asset` file exists unchanged on `main` up to and including `a4ddbc7`. Restoring them means reverting the T079 merge; Save V1 is unaffected because IDs and meanings are unchanged.

## Scope of T079

T079 retires the ScriptableObject types `ResourceDefinitionAsset`, `RecipeDefinitionAsset`, `BuildingDefinitionAsset`, `OfflinePolicyAsset` and `DefinitionCatalogAsset`, together with `DefinitionCatalogBuilder`, the development `.asset` files and `PreMigrationCaptureTests`. It rewrites the dependent tests against the JSON document or the fixture, and adds `JsonContentAuthorityTests`. `F001SpriteCatalog` and all sprites stay.

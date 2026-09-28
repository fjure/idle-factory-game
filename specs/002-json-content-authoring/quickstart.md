# Quickstart Validation: JSON Content Authoring Migration

**Prerequisites**: a clean checkout of the accepted commit, Unity `6000.6.0f1`, and the repository runner (`docs/testing/unity-test-harness.md`). Before any Unity run, acquire the lease; for the reason, see `AGENTS.md` → Shared workstation resources:

```powershell
scripts/resource-lease.ps1 -Acquire -Resource unity,emulator -Owner <owner> -Purpose "002 validation" -TtlMinutes 90
```

## 1. Validate content without a scene (FR-010, SC-003, SC-007)

```powershell
scripts/test-unity.ps1 -TestFile Assets/IdleFactory/Tests/EditMode/GameplayContentReaderTests.cs
scripts/test-unity.ps1 -Suite EditMode
```

Run the first command for each content test file named in `tasks.md`. Expected result: the reader, semantic validator, schema-consistency, documentation-completeness, canonical-form, parity and order-independence suites pass. Every edge-case class in the spec has a named rejection test.

## 2. Author a change (US1, SC-005)

1. Copy `Assets/IdleFactory/Content/Development/development-catalog.json` to a scratch location.
2. Using only `docs/content/json-content-pipeline.md`, add one hypothetical resource and one recipe that uses it.
3. Validate it with the Edit Mode authoring-check test pointed at the scratch file (the command and environment variable are documented in the pipeline doc, task T003).
4. Record the attempts and the diagnostics shown in the Game Designer/Product Owner review. The scratch file is never committed.

## 3. Run the game from JSON (US2, FR-013, SC-001, SC-002)

```powershell
scripts/test-unity.ps1                              # full Edit Mode + Play Mode
scripts/test-unity.ps1 -StandaloneWindowsPlayer     # tagged Windows player suite, incl. content load + SC-004 case
```

Expected result: all suites pass. In the editor, the D001 journey matches Feature 001's `quickstart.md`: placement, two cycles with a blocked interval, save, load and offline return. Pre-migration checkpoint fixtures load unchanged.

## 4. Fail safely (US3, FR-014, SC-004)

Play Mode and player tests cover these cases through injected content:

- a missing asset;
- an empty, BOM, truncated or oversized document;
- an unknown member;
- an unresolved reference;
- an unsupported version;
- an unresolved presentation key;
- a document that lacks a checkpoint-referenced ID.

Expected result: the initialization error state, a coded diagnostic, no session, and checkpoint files that are byte-identical before and after.

## 5. Single authority (US4, FR-019, SC-006)

After the owner has approved retirement (T010) and T011 has merged:

- `JsonContentAuthorityTests` passes;
- a search for the retired type names outside `specs/`, `docs/` and `wiki/` returns nothing;
- the full suites and the player suite pass.

## 6. Cold start measurement (SC-008)

The `StandalonePlayer` measurement test prints the median of 5 content loads. Record it with the pre-migration baseline in `reviews/json-content-final-acceptance.md`. A value above 100 ms triggers a Performance review and does not block acceptance.

## 7. Android (FR-013, emulator only)

Follow `scripts/android-emulator-journey.ps1` with the ARM64 Development build on the API 36 x86_64 AVD. Label the result emulator-only. If the Player cannot launch, record the blocker and leave this evidence unresolved.

Release the leases afterwards:

```powershell
scripts/resource-lease.ps1 -Release -Resource unity,emulator -Owner <owner>
```

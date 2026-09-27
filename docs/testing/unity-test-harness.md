# Unity Test Harness and MAQA Bootstrap Evidence

**Gate**: External Greenfield Bootstrap Prerequisite for Feature 001
**Editor**: Unity `6000.6.0f1` (`f7f8ed4d1e24`)
**Project**: Idle Factory Game
**Evidence date**: 2026-09-15, Europe/Berlin
**Status**: PASS; all four independent reviews and the committed-candidate run succeeded, and the reviewed baseline is integrated on `main`

## Installed and configured baseline

| Item | Verified state |
|---|---|
| Input System | Direct `com.unity.inputsystem` pin `1.20.0`, lock depth 0 |
| Newtonsoft JSON | Direct `com.unity.nuget.newtonsoft-json` pin `3.2.2`, lock depth 0 |
| Unity Test Framework | Direct `com.unity.test-framework` pin `1.8.0`, lock depth 0 |
| Input backend | New Input System only: `activeInputHandler: 1` |
| Production assemblies | `IdleFactory.Core` with `noEngineReferences: true`; `IdleFactory.Unity` references Core and `Unity.InputSystem` |
| Test assemblies | Separate Edit Mode and Play Mode assemblies with `TestAssemblies` and `UNITY_INCLUDE_TESTS` |
| MAQA full command | `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-unity.ps1` |
| MAQA file command | `powershell -NoProfile -ExecutionPolicy Bypass --% -File scripts/test-unity.ps1 -TestFile "{file}"` |
| TDD flag | `false`; workers still prove red→green inside each final-green Issue where practical |
| Worktree base | Repository-local ignored `.maqa-worktrees` |

The installed CoplayDev Unity MCP package remains in `Packages/manifest.json`. No Unity MCP tool was exposed to the active agent runtime during this bootstrap, so editor import and test validation used the repository-owned Unity CLI runner. MCP availability must be checked again in each agent session; its absence does not remove the CLI gate.

## Runner contract

`scripts/test-unity.ps1`:

- binds execution to the Unity version pinned in `ProjectSettings/ProjectVersion.txt`;
- accepts `-Suite EditMode|PlayMode` and an optional repository-relative `-TestFile`;
- infers a focused file's suite from its repository path, extracts its test class filter, and rejects a requested-suite mismatch;
- runs Edit Mode and Play Mode sequentially when neither option is supplied;
- omits `-quit` and waits for the Test Framework-controlled Unity process exit;
- creates unique ignored XML/log paths under `test-results/unity/<UTC-run-id>/`;
- applies a configurable per-suite timeout (default `900` seconds), attempts to terminate the spawned
  Unity process tree on expiry, falls back to terminating the spawned process, retains the checkout
  lock if termination cannot be confirmed, and fails with the log path;
- rejects missing or stale XML, zero discovered or zero passing tests, every skipped/inconclusive/non-passing case, a non-passing root result, nonzero Unity exit, focused results from another class, compiler errors, and fatal batch failures;
- supports one namespace and one non-partial, non-generic top-level test class per focused file, verifies the owning `TestAssemblies` asmdef, and rejects ambiguous files instead of silently omitting tests;
- rejects a focused file or owning asmdef reached through a symbolic link, junction, or other
  reparse point between the file and repository root;
- serializes each checkout with `.unity-test-runner.lock` and rejects a live Unity process owning the same checkout.

An interactive Editor must never own the same checkout while this runner executes. An unreadable or malformed `Library/EditorInstance.json` fails closed; a well-formed file whose PID is no longer active is treated as stale. Only the invocation that successfully acquired the lock may remove it. Different worktrees have different project state, but the parent must still respect `max_parallel: 3`, machine capacity, and any shared device/build target.

The batch argument line quotes every Unity argument so project and result paths containing spaces remain one native argument. The runner excludes only the exact non-fatal licensing update line observed with a valid cached Unity license; compiler failures, Unity error records, common runtime exception records, assertion failures, and `Debug.LogError`/`LogError` records remain fatal.

The MAQA focused command uses PowerShell's `--%` stop-parsing token before the child command
arguments. This keeps the substituted repository-relative `.cs` path literal until the runner applies
its own path, extension, assembly, identifier, and reparse-point validation.

## Windows Codex sandbox note

On the local Windows Codex host, normal sandboxed Unity CLI starts have repeatedly stopped before
test discovery because Unity could not open the local Package Manager database and the UPM IPC
channel timed out. These attempts produced no NUnit result XML. Moving `UPM_CACHE_ROOT` into the
worktree did not resolve the sandboxed startup failure. The same runner succeeded when invoked with
narrow, per-command sandbox escalation and local IPC access; see the [US2 production acceptance
record](../../specs/001-core-game-foundation/reviews/us2-production-acceptance.md) and [US3 persistence
acceptance record](../../specs/001-core-game-foundation/reviews/us3-persistence-offline-acceptance.md).

For Unity CLI work on this host, perform checkout/editor ownership checks first, then request
command-level escalation for the initial runner/build invocation when local UPM access is required.
Do not first spend a run reproducing the known sandbox failure, and do not broaden the sandbox or
change package caches globally. Command-level escalation is separate from the Windows sandbox's
`elevated`/`unelevated` implementation modes. If escalation is unavailable, use Unity MCP only when
it is verified against the exact target checkout, or record the blocker. The retained Unity logs
establish the database/IPC failure and the successful local-access rerun, but do not identify the
specific denied Windows object or access code.

## Verification results

| Proof | Command | Result |
|---|---|---|
| Full suites | `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-unity.ps1` | PASS; Edit Mode 1/1 and Play Mode 1/1 |
| Focused Edit Mode | `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-unity.ps1 -TestFile "Assets/IdleFactory/Tests/EditMode/HarnessSmokeTests.cs"` | PASS; exactly 1 Edit Mode test |
| Focused Play Mode | `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-unity.ps1 -TestFile "Assets/IdleFactory/Tests/PlayMode/HarnessSmokeTests.cs"` | PASS; exactly 1 Play Mode test |
| Suite/file mismatch | Add `-Suite PlayMode` to the focused Edit Mode command | Expected rejection before Unity starts |
| Deliberate red | Temporarily expected Unity version `deliberate-red-proof`, then ran the focused Edit Mode command | Expected FAIL; Unity exit 2, 1 failed test, wrapper exit 1 |
| Restored green | Restored expected version `6000.6.0f1`, then reran the focused Edit Mode command | PASS; exactly 1 Edit Mode test |
| Entirely skipped focus | Temporarily ran one `[Ignore]` test as the only focused case | Expected FAIL; root `Skipped:Ignored`, 0 passing/1 skipped, wrapper exit 1 |
| Ambiguous focus source | Temporarily supplied a file containing two namespaces and two fixtures | Expected rejection before Unity starts; wrapper exit 1 |
| MAQA command substitution | Supplied a literal focused path containing a PowerShell `$()` expression through the configured stop-parsing command | Expected rejection with the literal, unexecuted path preserved |
| Reparse-point focus | Supplied an apparent in-repository test path through a junction to an external directory | Expected rejection at the junction before Unity starts; wrapper exit 1 |
| Suite timeout | Ran an isolated fake Unity executable with `-TimeoutSeconds 1` | Expected FAIL; wrapper exit 1, spawned process stopped, owned checkout lock removed |
| Concurrent lock | Held `.unity-test-runner.lock` open and invoked a second runner | Expected rejection; wrapper exit 1 and the owning lock remained present |
| Malformed editor ownership | Temporarily supplied invalid `Library/EditorInstance.json` | Expected fail-closed rejection; wrapper exit 1; original/absent state restored |
| Final hardened full suites | `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-unity.ps1` | PASS; Edit Mode 1/1 and Play Mode 1/1, no skipped cases |
| Committed candidate | Same full-suite command in isolated worktree at commit `7bbec00f55c3ce66d5cf5d938fba94a57bfcb054` | PASS; Edit Mode 1/1 and Play Mode 1/1, no skipped cases, wrapper exit 0 |

Ignored local evidence paths:

- Full pass: `test-results/unity/20260914T185445473Z/`
- Focused Edit pass: `test-results/unity/20260914T185527942Z/`
- Focused Play pass: `test-results/unity/20260914T185544501Z/`
- Deliberate red: `test-results/unity/20260914T185611325Z/`
- Restored green: `test-results/unity/20260914T185635182Z/`
- Entirely skipped rejection: `test-results/unity/20260914T190643620Z/`
- Hardened deliberate red: `test-results/unity/20260914T190738997Z/`
- Hardened restored green: `test-results/unity/20260914T190815187Z/`
- Final hardened full pass: `test-results/unity/20260914T190830342Z/`
- Committed-candidate full pass: `test-results/unity/20260915T105956082Z-commit-7bbec00/`

## Dependency correction discovered by the gate

The planned Input System `1.17.0` pin failed compilation under Unity `6000.6.0f1` because package editor code referenced APIs removed by this editor, including `Object.GetInstanceID()` and `ProjectWindowUtil.CreateAssetWithContent(... Action<int>)`. The failing import was recorded locally in `test-results/unity/20260914T185243495Z/EditMode.log`.

The repository now pins Input System `1.20.0`. Unity's official package registry identifies it as a Unity 6000 package, Unity resolved it at depth 0, and both test suites compile and pass. No package-cache source was modified. The corrected version is reflected in `plan.md`, `research.md`, `quickstart.md`, `tasks.md`, and the relevant wiki pages.

## Worktree proof

The parent resolved and verified the test target inside the workspace, added a detached worktree at `.maqa-worktrees/bootstrap-proof`, and removed it cleanly with Git:

- source commit: `70c426c8dd5353b6d51c538e05b360ba594ee128`
- worktree commit: `70c426c8dd5353b6d51c538e05b360ba594ee128`
- commit resolved after removal: `70c426c8dd5353b6d51c538e05b360ba594ee128`
- remaining registered worktrees: only the primary checkout

This proves the configured base is writable, ignored, removable through `git worktree remove`, and does not lose the referenced commit.

## Independent review record

| Role | Verdict | Evidence |
|---|---|---|
| Software Architect | PASS | Independent review: package/assembly boundaries, runner contract, MCP fallback, and MAQA base are coherent; final commit/run/integration remain |
| Test and QA Engineer | PASS | Independent review of stored full/focused/red/skipped results plus live editor-ownership rejection found no blocking defect |
| Security and Privacy Reviewer | PASS | Re-review confirmed literal MAQA path handling, repository-relative and reparse-point rejection, signing-secret ignores, loopback MCP guidance, and external-data controls |
| DevOps and Operations Engineer | PASS | Re-review confirmed the bounded Unity timeout with process-tree termination and corrected workflow/bootstrap status |

All four independent reviews and the committed-candidate full-suite run pass. The reviewed baseline is integrated on `main`; the parent must verify the final Main commit with the same full-suite command before pushing it to `origin/main`.

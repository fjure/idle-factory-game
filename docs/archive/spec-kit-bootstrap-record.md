# Spec Kit Platform Bootstrap Record

Archived from [`docs/spec-kit-greenfield-workflow.md`](../spec-kit-greenfield-workflow.md) on 2026-10-08. The one-time bootstrap is complete; this record is kept verbatim for reference and for setting up a new integration or clone from scratch. The active extension table and the per-clone Git hook step remain in the workflow document.

## Audit result (2026-09-13)

The Spec Kit installation is structurally sound: Spec Kit 1.0.6 recognizes eight enabled extensions, every installed AI integration (currently Codex in `.agents/skills/` and Claude Code in `.claude/skills/`, with Codex as the default) has matching skills for all 33 core and extension commands, every manifest command points to an installed file, registry and manifest versions agree, lifecycle hooks are present, and all relevant YAML files parse successfully. Atlas' bundled manifest suite passes with six tests.

The one-time Spec Kit platform bootstrap is complete and the repository is ready for product
specification and planning. Unity `6000.6.0f1` is the selected application stack. The Unity test
harness and real MAQA Edit Mode/Play Mode commands are now configured and proof-tested. The
independently reviewed, final-green baseline is integrated on `main`; after the final Main test and
push, Feature 001 may proceed through MAQA synchronization. The three installed-extension
compatibility workarounds remain documented below.

Current GitHub Projects mapping:

| MAQA state | GitHub Project option |
|---|---|
| Todo | `Todo` (`f75ad846`) |
| In Progress | `In Progress` (`47fc9ee4`) |
| In Review | `In Review` (`95678686`) |
| Done | `Done` (`98236657`) |

Bootstrap verification record:

- GitFlow branch configuration validated with no non-compliant feature branches or spec folders.
- Wiki initialized for product knowledge, architecture decisions, external dependencies, and reusable
  Idle Factory Game knowledge.
- Initial baseline commit `42d3871` pushed to `origin/main`; GitHub reports `main` as the default branch.
- MAQA installation and skill registration verified for each installed AI integration; required GitHub labels created.
- Additional agents join through `specify integration install <key> --script ps`. Extension commands are
  registered for a non-default integration by temporarily selecting it with `specify integration use <key>`
  and then restoring the default; verify that no other integration's skill files changed.
- GitHub Project #4 `Idle-Factory-Game` and all Status IDs reverified.
- Unity `6000.6.0f1` is pinned and the CoplayDev Unity MCP package is declared in `Packages/manifest.json`.
  An active MCP connection must still be verified per agent session.
- Unity Edit Mode and Play Mode MAQA commands now use the repository-owned runner documented in
  `docs/testing/unity-test-harness.md`. All four independent reviews and the committed-candidate
  full-suite run pass, and the reviewed baseline is integrated on `main`. Accessibility,
  responsive/safe-area, and empty/error-state QA switches are enabled; the supported device/input
  matrix is defined by Feature 001.
- Local MCP readiness is recorded in `docs/integrations/mcp-readiness.md`. Unity's local server,
  ComfyUI authentication/server installation, and Blender's separate client-side server must be
  verified in a fresh session of the agent that will receive the assignment before assignments that
  require those tools.

## One-time project bootstrap

Complete this gate on `main` before creating the first application feature.

1. Define the project constitution:

   ```text
   $speckit-constitution
   ```

   Replace every placeholder. Include at least product principles, architecture constraints, testing policy, accessibility expectations, security rules, observability, dependency policy, and the definition of done.

2. Configure one branch convention:

   ```text
   $speckit-branch-convention-configure gitflow
   $speckit-branch-convention-validate all
   ```

   `gitflow` is a reasonable default for human-created feature branches. MAQA's generated `maqa/issue-<number>-<slug>` branches are workflow branches and should be treated as an explicit convention exception.

3. Initialize persistent project knowledge:

   ```text
   $speckit-wiki-init Produktwissen, Architekturentscheidungen, externe Abhängigkeiten und wiederverwendbare Erkenntnisse für Idle Factory Game.
   $speckit-wiki-status
   ```

4. Create the initial repository baseline:

   ```text
   $speckit-checkpoint-commit
   git push -u origin main
   ```

   GitHub must report `main` as its default branch before MAQA can create issue worktrees from `origin/main`.

5. Finish MAQA bootstrap:

   ```text
   $speckit-maqa-setup
   ```

   Confirm that `maqa`, `maqa:in-progress`, `maqa:in-review`, and `maqa:blocked` exist.

6. Keep the verified GitHub Projects setup:

   ```text
   owner: fjure
   project: #4 Idle-Factory-Game
   ```

   Do not rerun setup unless the Project or Status field changes.

7. Configure the selected Unity stack before implementation:

   - Pin the supported Unity Editor version in `ProjectSettings/ProjectVersion.txt`.
   - Add Unity Test Framework assemblies and a repository-owned batch-mode test wrapper.
   - Set `test_command` to a command that runs the required Edit Mode and Play Mode suites and fails
     on test, compilation, or relevant console errors.
   - Set `test_file_command` to a reliable focused test command using `{file}`.
   - Enable `tdd` only when the focused command is reliable.
   - Prefer `auto_push: true` for MAQA so worktree commits also exist remotely.
   - For Unity UI work, enable `accessibility`, `responsive`, and `empty_states` QA checks and
     define supported screen classes, orientation, and input methods.
   - Keep `board_mirror: "none"` until an actual MAQA↔Projects status-mirroring command is installed.

8. Verify the Unity production baseline:

   - Commit `Assets/` with all `.meta` files, `Packages/manifest.json`,
     `Packages/packages-lock.json`, and `ProjectSettings/`.
   - Keep `Library/`, `Temp/`, `Logs/`, `Obj/`, `Build/`, `Builds/`, and
     `UserSettings/` ignored.
   - Verify the Unity MCP connection before assigning Unity MCP work. The installed package alone does
     not make the tool callable in every agent runtime.
   - Use the pinned Unity executable and repository-owned scripts as the fallback when Unity MCP is
     unavailable.
   - Verify ComfyUI and Blender MCP with a real tool call before dispatching an asset assignment that
     names either workflow. Follow `docs/integrations/mcp-readiness.md`; do not treat the installed
     desktop application or editor add-on as a complete client connection.
   - Record target platforms, device classes, orientation, input methods, and initial performance
     budgets in the first product/architecture feature.

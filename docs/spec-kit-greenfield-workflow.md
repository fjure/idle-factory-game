# Spec Kit Greenfield Workflow

Stand: 2026-09-13

## Audit result

The Spec Kit installation is structurally sound: Spec Kit 1.0.6 recognizes eight enabled extensions, all 23 registered Codex commands have matching skills, every manifest command points to an installed file, registry and manifest versions agree, lifecycle hooks are present, and all relevant YAML files parse successfully. Atlas' bundled manifest suite passes with six tests.

The one-time platform bootstrap is complete and the repository is ready for product
specification and planning. Feature implementation remains gated on selecting the application
technology stack and replacing the empty MAQA test commands with real project commands. The three
installed-extension compatibility workarounds remain documented below.

| Extension | Installed state | Readiness and intended use |
|---|---|---|
| Orchestration Task Context Management 0.0.0 | 2/2 commands registered; hooks present | Use after stable task generation for direct implementation. On Windows, always pass `specs/<feature>/tasks.md` explicitly because the no-argument fallback references a missing Bash script. |
| Atlas 0.1.0 | 2/2 commands registered; `uv` present; 6 tests pass | Use Storybook at milestones for this repository. Use Map only for a real multi-repository workspace. The operational scripts are under `skill/scripts/`. |
| Branch Convention 1.0.0 | 3/3 commands registered; hook present | GitFlow is configured as `{type}/{seq}-{kebab}` for branches and `{seq}-{kebab}` for spec folders. Validation passes. The hook validates names; it does not create a feature branch. |
| Checkpoint 1.0.0 | 1/1 command registered | Use after reviewed Spec Kit phases and coherent direct-implementation units. It stages all changes, so run it only in a clean, correctly scoped checkout. Do not use it inside MAQA workers. |
| LLM Wiki 1.0.0 | 5/5 commands registered; config valid; hooks present | `wiki/` is initialized with the Idle Factory Game scope. It currently has no ingested pages or sources; query before new specs and ingest after plan/implementation. |
| Learning 1.1.0 | 3/3 commands registered; hook present | Local installation is correct. `specify extension info learn` shows stale catalog metadata for 1.0.0 with two commands; the installed manifest, registry, and three Codex skills consistently use 1.1.0. |
| MAQA 0.3.1 | 5/5 commands registered; config valid | GitHub identity, default branch `main`, and all four MAQA labels are configured; `auto_push` is enabled. Test commands remain intentionally empty until the technology stack is selected. The rendered sync skill points to a missing extension-local script; use the installed core PowerShell script documented in `AGENTS.md`. |
| MAQA GitHub Projects 0.1.0 | 2/2 commands registered; Project #4 and all four Status IDs verified | Setup is valid. Populate creates one draft card per feature, while MAQA uses one GitHub Issue per task. No installed hook keeps those drafts synchronized, so use the board as an informational feature view only. |

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
- MAQA installation and Codex skill registration verified; required GitHub labels created.
- GitHub Project #4 `Idle-Factory-Game` and all Status IDs reverified.
- Technology-specific MAQA test commands and applicable UI QA switches remain deferred until the stack
  is selected.

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

7. Once the technology stack is selected, update `maqa-config.yml` deliberately:

   - Set `test_command` to the full project test command.
   - Set `test_file_command` to a focused test command using `{file}`.
   - Enable `tdd` only when the focused command is reliable.
   - Prefer `auto_push: true` for MAQA so worktree commits also exist remotely.
   - For web UI work, enable `accessibility`, `responsive`, and `empty_states` QA checks.
   - Keep `board_mirror: "none"` until an actual MAQA↔Projects status-mirroring command is installed.

## Per-feature design workflow

### 1. Recover context and define the outcome

Start each feature by reading accumulated project knowledge:

```text
$speckit-wiki-status
$speckit-wiki-query <domain question for this feature>
```

Create and switch to a branch that follows `.specify/branch-convention.yml`, then validate it. The Branch Convention extension is a validator and renamer; it does not create the branch.

Run a discovery review wave in parallel:

- Product Owner: user value, stories, acceptance, priority, exclusions, measurable outcomes.
- Game Systems/Economy: core loop, progression, production formulas, upgrades, unlocks, offline progress, balance and abuse cases.
- UI/UX Reviewer: user journey, interaction states, accessibility and responsive needs when the feature has a UI.

Run a technical-feasibility review when the idea crosses system boundaries:

- Architect: boundaries, constraints, risks, dependencies, and data ownership.
- Security/Privacy: trust boundaries, player data, abuse paths, validation and authorization.
- DevOps/Operations: runtime dependencies, deployment, observability, migrations and recovery.

The parent agent reconciles their findings into the feature description before continuing.

### 2. Specify and clarify

```text
$speckit-specify <complete feature description>
$speckit-learn-clarify
$speckit-checklist requirements
$speckit-checkpoint-commit
```

Use `$speckit-clarify` instead of the learning variant when compact questions are preferred. Do not run both unless unresolved ambiguity remains.

Exit gate:

- User stories are independently valuable and prioritized.
- Acceptance scenarios are observable.
- Edge cases, failure paths, exclusions, and success criteria are explicit.
- No implementation detail has leaked into product requirements without a genuine constraint.
- Product Owner findings are resolved.

### 3. Plan architecture

```text
$speckit-plan
$speckit-wiki-ingest <active feature directory>
$speckit-learn-diagrams --all
$speckit-checkpoint-commit
```

Run a plan review wave in parallel:

- Architect checks structure, data, contracts, security, reliability, observability, and evolution cost.
- Test/QA derives test boundaries and a risk matrix from acceptance criteria.
- Backend and Frontend specialists review the parts they will consume or implement.
- UI/UX Reviewer checks that planned UI states cover loading, success, empty, error, recovery, keyboard, focus, and responsive behavior.
- Game Systems/Economy checks that formulas, tuning parameters, progression invariants, offline calculations, and balancing telemetry are explicit.
- Security/Privacy checks every affected trust boundary and persistence path.
- DevOps/Operations checks build, CI, deployment, configuration, observability, migration, backup, and rollback obligations.

The parent resolves findings in `plan.md`, `research.md`, data models, and contracts before task generation.

### 4. Generate tasks and pass the consistency gate

```text
$speckit-tasks
$speckit-analyze
```

Do not sync issues immediately after the first task generation. First resolve all critical/high analysis findings and regenerate tasks when the spec or plan changed.

When tasks are stable:

```text
$speckit-orchestration-task-context-management-prepare specs/<feature>/tasks.md
$speckit-orchestration-task-context-management-validate specs/<feature>/tasks.md
$speckit-checkpoint-commit
```

Always include the explicit task path on Windows. Refresh the ledger after every task change.

Exit gate:

- Every requirement and acceptance criterion has task coverage.
- Dependencies and safe parallel work are explicit.
- Backend, Frontend, UI, test, migration, documentation, and operational tasks exist where relevant.
- Economy and progression tasks include deterministic formulas, boundary cases, tuning ownership, and tests where relevant.
- Analysis has no unresolved critical finding.
- The orchestration ledger covers every task exactly once.

## Choose one implementation lane

### Lane A: direct Spec Kit implementation

Use for small features, bootstrap work, or changes whose files overlap too much for issue-level parallelism.

```text
$speckit-implement
$speckit-converge
```

The parent agent must execute the mandatory orchestration validation with the explicit active `tasks.md` path. Delegate independent work units using the role and assignment contract in `AGENTS.md`. Use `$speckit-checkpoint-commit` after coherent reviewed units.

If convergence appends tasks:

```text
$speckit-orchestration-task-context-management-prepare specs/<feature>/tasks.md refresh
$speckit-orchestration-task-context-management-validate specs/<feature>/tasks.md
$speckit-implement
$speckit-converge
```

Repeat until converged.

### Lane B: MAQA multi-agent implementation

Use by default when at least two tasks are independent and can be isolated in worktrees.

```text
$speckit-maqa-sync
$speckit-maqa-coordinator
```

Compatibility rule: if MAQA Sync asks for `.specify/extensions/maqa/scripts/powershell/check-prerequisites.ps1`, use `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` instead.

The parent consumes the coordinator's `SPAWN` records and dispatches at most three agents concurrently:

- Backend lens for domain, API, persistence, validation, auth, migrations, and backend tests.
- Frontend lens for state, API consumption, rendering, routing, interaction, and frontend tests.
- UI implementation lens for styling, design system, responsive behavior, accessibility, and interaction states when paths do not overlap Frontend ownership.

For economy or progression issues, use a Game Systems lens with Backend as the implementation partner. For infrastructure issues, use DevOps as primary and require Architecture and Security review. Secondary reviewers never edit the same worktree concurrently.

Each worker uses `$speckit-maqa-feature`, works only in its assigned worktree, and commits exactly once with the required assignment trailer. Do not use `$speckit-checkpoint-commit` inside that worktree.

For every completed worker result:

1. Return the result to `$speckit-maqa-coordinator results ...`.
2. Dispatch `$speckit-maqa-qa` for the exact commit.
3. Have Product Owner verify acceptance and Architect verify boundaries for cross-cutting changes.
4. Have UI/UX review user-visible changes.
5. On QA failure, dispatch only the reported fixes through `SPAWN_FIX`.
6. Merge only a QA-approved commit.
7. Return `$speckit-maqa-coordinator merged #<issue>` so it verifies the merge, closes the issue, removes the clean worktree, and reassesses readiness.

After the batch is merged:

```text
$speckit-converge
```

If convergence appends tasks, refresh the orchestration ledger, rerun `$speckit-maqa-sync`, and continue through the coordinator.

Do not use `specify workflow run speckit` in this lane. Its bundled graph proceeds from tasks directly to core implementation and bypasses the MAQA issue/worktree flow.

## Role-specific completion evidence

| Role | Required evidence |
|---|---|
| Product Owner | Acceptance matrix with each criterion marked pass/fail and uncovered product questions listed. |
| Architect | Boundary/dependency review, contract changes, risks, and constitution alignment. |
| Backend | Changed paths, API/data behavior, validation/auth behavior, migrations, targeted and full tests. |
| Frontend | Changed paths, state and interaction behavior, contract usage, loading/error/empty handling, tests. |
| UI/UX | Reviewed screens/states, keyboard/focus behavior, responsive checks, accessibility findings, concrete acceptance impact. |
| Test/QA | Risk-based scenarios, commands/results, regression evidence, exact failing location and violated criterion. |
| Game Systems/Economy | Formula and invariant review, progression simulation cases, balance assumptions, exploit risks, telemetry gaps. |
| Security/Privacy | Threat scenarios, trust boundaries, validation/auth evidence, data exposure and dependency findings. |
| DevOps/Operations | CI/deploy evidence, configuration and secret handling, observability, migration/rollback and recovery checks. |

## Knowledge, board, and architecture closeout

After implementation converges:

```text
$speckit-wiki-ingest <active feature directory>
$speckit-wiki-lint
$speckit-wiki-status
$speckit-learn-review
```

Use the GitHub Project only as a high-level feature view:

```text
$speckit-maqa-github-projects-populate
```

This creates a feature-level draft card. It does not replace or automatically track MAQA's task-level GitHub Issues in the installed version.

At a milestone or release candidate:

```text
$speckit-atlas-storybook
```

Use `$speckit-atlas-map` only after the product spans multiple repositories and an atlas workspace can describe their relationships.

Final feature gate:

- All acceptance criteria pass.
- Configured tests pass.
- Independent QA passes on committed code.
- Product Owner, Architect, and applicable UI/UX checks pass.
- `$speckit-converge` reports no remaining work.
- Relevant knowledge has been ingested and linted.
- The final branch and repository state have been verified by the parent agent.

## Fast invocation prompt

Use this when starting the application design:

```text
Follow AGENTS.md and docs/spec-kit-greenfield-workflow.md. Run the Greenfield bootstrap gate first. Then design the application through constitution, specification, role-based review, clarification, planning, diagrams, tasks, analysis, and orchestration readiness. Stop at the implementation-lane decision and present the evidence for choosing Direct or MAQA.
```

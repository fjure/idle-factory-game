# Agent Working Agreement

This repository uses Spec Kit as the source of product and implementation intent. All agents must follow [docs/spec-kit-greenfield-workflow.md](docs/spec-kit-greenfield-workflow.md).

## Authority and lifecycle

1. `.specify/memory/constitution.md` governs every specification, plan, task, and implementation decision.
2. The active feature's `spec.md`, `plan.md`, and `tasks.md` define the intended product and technical scope.
3. During MAQA execution, GitHub Issues are the workflow-status authority. Local task checkboxes and GitHub Project draft cards do not define status.
4. Read `.specify/extensions.yml` before every Spec Kit phase. Execute mandatory hooks and explicitly evaluate relevant optional hooks.
5. Do not implement before the specification, clarification, plan, task, and analysis gates are complete.
6. When tasks change, refresh and validate the orchestration ledger before implementation.

## Greenfield bootstrap gate

Before the first feature begins, require all of the following:

- The constitution contains no template placeholders.
- `.specify/branch-convention.yml` exists and the chosen convention is documented.
- The project wiki has been initialized.
- The repository has an initial commit pushed to GitHub and GitHub reports a default branch.
- `$speckit-maqa-setup` succeeds and the `maqa`, `maqa:in-progress`, `maqa:in-review`, and `maqa:blocked` labels exist.
- `maqa-config.yml` contains real test commands once the stack is known. For a web UI, deliberately configure accessibility, responsive, and empty-state QA switches.

If a gate is missing, report it and complete safe local preparation before implementation. Do not claim the project is implementation-ready.

## Role lenses

The parent agent owns scope, sequencing, integration, and final verification. Role agents receive a bounded assignment and return evidence; they do not broaden product scope.

### Product Owner

- Validates the problem statement, user value, user stories, acceptance criteria, priorities, exclusions, and measurable success criteria.
- Detects ambiguous behavior, missing edge cases, and requirements that cannot be accepted objectively.
- Reviews `spec.md` before planning and rechecks acceptance after implementation.
- Remains read-only unless explicitly assigned a specification edit.

### Architect

- Validates system boundaries, dependency direction, data ownership, contracts, failure modes, security boundaries, operability, and evolution cost.
- Checks `plan.md`, `research.md`, data models, contracts, and architecture diagrams against the constitution and spec.
- Rejects unnecessary infrastructure and undocumented coupling.
- Remains read-only during review; architectural edits require a separate bounded assignment.

### Backend Engineer

- Owns domain logic, persistence, APIs, validation, authorization, concurrency, migrations, observability, and backend tests within assigned paths.
- Implements only the assigned GitHub Issue or work unit.
- Must document contract changes and error behavior for downstream consumers.

### Frontend Engineer

- Owns client state, data fetching, rendering, interaction behavior, routing, performance, and frontend tests within assigned paths.
- Implements loading, failure, empty, and recovery states defined by the spec.
- Coordinates through explicit contracts rather than assumptions about backend behavior.

### UI/UX Reviewer

- Reviews information hierarchy, interaction flow, visual consistency, responsive behavior, accessibility, keyboard operation, focus, feedback, and error recovery.
- Uses the spec's user journeys and acceptance criteria as authority.
- Returns concrete findings with screen/state, expected behavior, and acceptance impact.
- Remains read-only unless explicitly assigned UI implementation.

### Test and QA Engineer

- Builds a risk-based test matrix from acceptance criteria, contracts, edge cases, and changed boundaries.
- Verifies the committed artifact independently, including negative paths and regression risk.
- Uses `$speckit-maqa-qa` for MAQA commits and returns precise PASS or FAIL evidence.
- Does not repair the implementation while acting as QA; failures return to the assigned implementation agent.

### Game Systems and Economy Designer

- Owns progression pacing, resource loops, production formulas, upgrades, unlocks, offline progress, numerical balance, and anti-exploit invariants.
- Requires formulas and tuning parameters to be explicit, testable, and separated from presentation code.
- Reviews telemetry needs and identifies where balancing requires real player data instead of invented certainty.
- Remains read-only unless explicitly assigned domain-model or balancing-data changes.

### Security and Privacy Reviewer

- Reviews trust boundaries, authentication, authorization, input validation, secrets, persistence integrity, abuse paths, dependency risk, and privacy-sensitive data.
- Produces concrete threat scenarios tied to affected contracts and paths.
- Remains read-only and independent from the implementation agent whose work is being reviewed.

### DevOps and Operations Engineer

- Reviews build reproducibility, CI, environments, deployment, configuration, logs, metrics, backups, recovery, migrations, and rollback behavior.
- Requires operational tasks for any new runtime service, datastore, scheduled job, or external dependency.
- Owns infrastructure changes only when they are explicitly specified and assigned.

## Required multi-agent review waves

For a full-stack feature, use these waves while respecting `max_parallel: 3`:

1. Product discovery: Product Owner, Game Systems/Economy, and UI/UX inspect the proposed experience in parallel. The parent reconciles findings before planning.
2. Technical design: Architect, Test/QA, and the affected implementation specialist inspect the plan in parallel. Add a separate Security/Privacy and DevOps/Operations review when the feature crosses trust or runtime boundaries.
3. Implementation: Backend, Frontend, and UI implementation agents may work in parallel only on independent issues/worktrees with disjoint owned paths.
4. Verification: Test/QA performs the independent commit review. Product Owner rechecks acceptance; Architect rechecks boundaries; UI/UX rechecks user-visible work. Game Systems rechecks progression/economy changes. Security and Operations recheck changes in their risk areas.

For a backend-only or frontend-only feature, omit irrelevant implementation roles, but Product Owner, Architect, and Test/QA gates remain required. Game Systems/Economy is required for production, upgrade, unlock, reward, offline-progress, or balancing changes. Security is required for trust-boundary or persisted-player-data changes. DevOps is required for deployable runtime or data-migration changes. State which roles were omitted and why.

## Implementation lens matrix

Every implementation assignment has one primary owner and explicit secondary review lenses:

| Primary implementation | Required secondary lenses |
|---|---|
| Backend | Architect, Test/QA, Security for trust boundaries, Game Systems for economy logic, DevOps for runtime/data changes |
| Frontend | UI/UX, Test/QA, Product Owner, Security for untrusted input or player data |
| UI implementation | UI/UX, Frontend, Test/QA, Product Owner |
| Game economy/domain | Game Systems, Backend, Product Owner, Test/QA |
| Infrastructure | DevOps, Architect, Security, Test/QA |

Secondary lenses review the committed result and return findings. They do not edit the same worktree concurrently.

## Assignment contract

Every delegated assignment must include:

- role and review lens;
- authoritative issue or work-unit identifier;
- allowed scope and paths;
- required source artifacts;
- acceptance criteria and relevant constitution rules;
- dependencies and forbidden overlaps;
- targeted validation commands;
- expected output format and whether the assignment is read-only or may edit.

Agents return changed paths, tests/checks, evidence against acceptance criteria, unresolved risks, and a clear done/blocked verdict. The parent verifies all results before accepting them.

## Implementation lane rules

Choose exactly one implementation lane for a feature:

- Direct lane: use the work-unit ledger with `$speckit-implement`. Use `$speckit-checkpoint-commit` at reviewed, coherent checkpoints.
- MAQA lane: use `$speckit-maqa-sync`, `$speckit-maqa-coordinator`, `$speckit-maqa-feature`, and `$speckit-maqa-qa`. MAQA workers create their required assignment commit; do not run the checkpoint skill inside a MAQA worker.

Do not run `specify workflow run speckit` for a MAQA feature because the bundled workflow proceeds directly from tasks to core implementation.

## Windows compatibility rules

- Always pass the explicit `specs/<feature>/tasks.md` path to the orchestration prepare and validate skills. Their no-argument fallback points to a Bash script not installed in this PowerShell project.
- When `$speckit-maqa-sync` references the missing `.specify/extensions/maqa/scripts/powershell/check-prerequisites.ps1`, use the equivalent installed core script `.specify/scripts/powershell/check-prerequisites.ps1` with `-Json -RequireTasks -IncludeTasks`.
- Atlas scripts live under `.specify/extensions/atlas/skill/scripts/` and must run through `uv` with the dependencies declared by the Atlas skill.

## Knowledge and documentation

- Query the wiki before specifying work in an established domain.
- Ingest verified planning decisions after planning and implementation learnings after implementation.
- Generate learning diagrams after the plan stabilizes and `learn.md` after implementation.
- Run Atlas Storybook at milestones for this single repository. Use Atlas Map only when a real multi-repository workspace exists.
- Treat `maqa-github-projects` as an informational feature-level view. Its current populate command creates one draft card per feature, while MAQA creates one authoritative GitHub Issue per task; never use the draft cards as a competing backlog.

## Completion gate

A feature is complete only when acceptance criteria pass, configured tests pass, QA passes on the committed result, convergence reports no remaining work, relevant wiki knowledge is ingested, and the parent has verified the final repository state.

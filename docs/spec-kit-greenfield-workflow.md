# Spec Kit Greenfield Workflow

Stand: 2026-09-13

## Audit result

The Spec Kit installation is structurally sound: Spec Kit 1.0.6 recognizes eight enabled extensions, every installed AI integration (currently Codex in `.agents/skills/` and Claude Code in `.claude/skills/`, with Codex as the default) has matching skills for all 33 core and extension commands, every manifest command points to an installed file, registry and manifest versions agree, lifecycle hooks are present, and all relevant YAML files parse successfully. Atlas' bundled manifest suite passes with six tests.

The one-time Spec Kit platform bootstrap is complete and the repository is ready for product
specification and planning. Unity `6000.6.0f1` is the selected application stack. The Unity test
harness and real MAQA Edit Mode/Play Mode commands are now configured and proof-tested. The
independently reviewed, final-green baseline is integrated on `main`; after the final Main test and
push, Feature 001 may proceed through MAQA synchronization. The three installed-extension
compatibility workarounds remain documented below.

| Extension | Installed state | Readiness and intended use |
|---|---|---|
| Orchestration Task Context Management 0.0.0 | 2/2 commands registered; hooks present | Use after stable task generation for direct implementation. On Windows, always pass `specs/<feature>/tasks.md` explicitly because the no-argument fallback references a missing Bash script. |
| Atlas 0.1.0 | 2/2 commands registered; `uv` present; 6 tests pass | Use Storybook at milestones for this repository. Use Map only for a real multi-repository workspace. The operational scripts are under `skill/scripts/`. |
| Branch Convention 1.0.0 | 3/3 commands registered; hook present | GitFlow is configured as `{type}/{seq}-{kebab}` for branches and `{seq}-{kebab}` for spec folders. Validation passes. The hook validates names; it does not create a feature branch. |
| Checkpoint 1.0.0 | 1/1 command registered | Use after reviewed Spec Kit phases and coherent direct-implementation units. It stages all changes, so run it only in a clean, correctly scoped checkout. Do not use it inside MAQA workers. |
| LLM Wiki 1.0.0 | 5/5 commands registered; config valid; hooks present | `wiki/` is initialized with the Idle Factory Game scope. Source `S001` and five indexed pages contain the verified Feature 001 planning decisions; query before new specs and ingest after plan/implementation. |
| Learning 1.1.0 | 3/3 commands registered; hook present | Local installation is correct. `specify extension info learn` shows stale catalog metadata for 1.0.0 with two commands; the installed manifest, registry, and the three skills of each installed integration consistently use 1.1.0. |
| MAQA 0.3.1 | 5/5 commands registered; config valid | GitHub identity, default branch `main`, and all four MAQA labels are configured; `auto_push` and the Unity UI accessibility/responsive/empty-state checks are enabled. Full and focused Unity commands use the proof-tested repository runner. The rendered sync skill points to a missing extension-local script; use the installed core PowerShell script documented in `AGENTS.md`. |
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

## Per-feature design workflow

### 1. Recover context and define the outcome

Start each feature by reading accumulated project knowledge:

```text
$speckit-wiki-status
$speckit-wiki-query <domain question for this feature>
```

Create and switch to a branch that follows `.specify/branch-convention.yml`, then validate it. The Branch Convention extension is a validator and renamer; it does not create the branch.

Run a discovery review wave in parallel:

- Game Designer/Product Owner: player value, core-loop impact, mechanics, controls, stories, acceptance, priority, exclusions, and measurable outcomes.
- Game Systems/Economy: core loop, progression, production formulas, upgrades, unlocks, offline progress, balance and abuse cases.
- UI/UX: player journey, touch/pointer behavior, HUD/build flow, feedback states, accessibility, and supported screen classes.
- Art Director: visual language, silhouette/state readability, grid constraints, and Art Bible impact when the feature introduces or changes visuals.

Run a technical-feasibility review when the idea crosses system boundaries:

- Software Architect: boundaries, constraints, risks, dependencies, and data ownership.
- Technical Artist: asset/import, prefab, animation, atlas, memory, and production-pipeline feasibility.
- Performance Engineer: simulation/render scale, device workload, budgets, and measurement approach.
- Security/Privacy: trust boundaries, player data, abuse paths, validation and authorization.
- DevOps/Operations: runtime dependencies, deployment, observability, migrations and recovery.

Activate only the applicable specialist reviews, state omissions and reasons, and never exceed three concurrent roles. The parent agent reconciles their findings into the feature description before continuing.

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
- Game Designer/Product Owner findings are resolved.
- New visual or audio work has a named production owner, review owner, and required brief.

### 3. Plan architecture

```text
$speckit-plan
$speckit-wiki-ingest <active feature directory>
$speckit-learn-diagrams --all
$speckit-checkpoint-commit
```

Run a plan review wave in parallel:

- Software Architect checks plain-C# domain/simulation boundaries, Unity-facing adapters, state ownership, data, contracts, persistence, reliability, and evolution cost.
- Test/QA derives Edit Mode, Play Mode, build/device, simulation, negative-path, and regression coverage from acceptance criteria.
- Gameplay Developer reviews C#, grid, factory, logistics, input, save, and application contracts it will implement.
- UI/UX Developer and an independent UI/UX Reviewer check UI state ownership, touch/pointer behavior, feedback, loading, disabled, empty, error, recovery, navigation/focus, and screen-size behavior.
- Game Systems/Economy checks that formulas, tuning parameters, progression invariants, offline calculations, and balancing telemetry are explicit.
- Art Director checks the Art Bible, perspective, palette, silhouettes, state readability, scale, and briefs for new visual work.
- Asset Artist or 3D/Render Artist reviews source-production feasibility; Technical Artist reviews import, sprite/atlas, animation, prefab, compression, and memory requirements.
- Performance Engineer defines representative factory/device workloads, metrics, baselines, budgets, and profiling points for scale-sensitive work.
- Sound Designer reviews cues, loops, transitions, variation, loudness, licensing, import, and mixing requirements when audio changes.
- Online Services reviews client/service contracts only when the feature introduces cloud save, accounts, analytics, live operations, or another remote boundary.
- Security/Privacy checks every affected trust boundary and persistence path.
- DevOps/Operations checks Unity batch commands, build reproducibility, CI, target-platform configuration, logs, artifacts, deployment, migration, backup, and rollback obligations.

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
- Gameplay, Unity UI, Edit Mode, Play Mode, asset production, Technical Art, audio, profiling, build, migration, documentation, and operational tasks exist where relevant.
- Economy and progression tasks include deterministic formulas, boundary cases, tuning ownership, and tests where relevant.
- Asset tasks preserve the sequence brief → production → Art Director review → Technical Art integration → Unity validation.
- Performance tasks name the workload, device class, metric, baseline, target, profiler evidence, and regression check.
- Parallel work units have disjoint Unity paths; shared scenes, prefabs, settings, and package files have one writer at a time.
- Analysis has no unresolved critical finding.
- The orchestration ledger covers every task exactly once.

## Choose one implementation lane

### Token and tool budget

Apply the model-routing table in `AGENTS.md` to every worker and reviewer. The Low tier handles deterministic, tightly specified work; the Medium tier is the implementation and QA default; the High tier is reserved for recorded high-risk ambiguity, cross-boundary architecture/security/persistence, or remediation still unresolved after Medium at `high` reasoning. Each agent runtime uses its own model mapping for these tiers. Escalate reasoning before model class, never use `max` by default, and route bounded follow-up work back down after the risky decision is resolved.

The parent prepares one compact Issue evidence packet and reuses it across the wave. Include exact requirements, changed paths, relevant contract sections, validation commands, prior findings, and omitted-role rationale; reference stable repository sections instead of copying whole artifacts. Resume the same worker for fixes, make reviews diff-first, combine only compatible optional read-only lenses, and retain independent Product, Architecture, and Test/QA verdicts.

For Unity-facing work, verify a real Unity MCP call in the target session and use focused MCP inspection for scenes, objects, importers, console state, and targeted test status. Keep the repository CLI runner as the reproducible full-suite gate and fallback. Do not consume tokens parsing broad Unity logs or serialized files when an equivalent structured MCP query is available.

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

On the Windows agent host, use the process-scoped direct-access procedure in [`docs/integrations/github-cli.md`](integrations/github-cli.md) for GitHub CLI requests and Git fetch/push operations when proxy variables point to the refusing `127.0.0.1:9` endpoint. Verify `gh auth status` and the exact `origin` repository before coordinating; do not refresh a valid keyring token just because the proxied request failed.

The parent consumes the coordinator's `SPAWN` records and dispatches at most three agents concurrently:

- Gameplay Developer for assigned domain, simulation, factory/logistics, application, persistence, input, and C# test paths.
- UI/UX Developer for assigned Unity UI, presentation, interaction, layout, accessibility, and UI test paths.
- Technical Artist for approved asset imports, sprites/atlases, animation, materials, prefabs, and definition wiring.
- Asset, 3D/Render, or Audio production agents for source content in paths that do not overlap Unity integration.
- Online Services or DevOps for explicitly specified remote-service, CI, build, or release paths.

For economy or progression Issues, use Game Systems as the design lens and Gameplay Developer as the implementation partner. For visual production, preserve the dependency chain Art Direction → source asset → Art review → Technical Art; do not parallelize dependent stages. For infrastructure Issues, use DevOps as primary and require Architecture and Security review. Secondary reviewers never edit the same worktree concurrently.

Each worker uses `$speckit-maqa-feature`, works only in its assigned worktree, and commits exactly once with the required assignment trailer. Do not use `$speckit-checkpoint-commit` inside that worktree.

For every completed worker result:

1. Return the result to `$speckit-maqa-coordinator results ...`.
2. Dispatch `$speckit-maqa-qa` for the exact commit.
3. Have Game Designer/Product Owner verify acceptance and Software Architect verify boundaries.
4. Have UI/UX review user-visible changes.
5. Have Art Direction, Technical Art, Audio, Game Systems, Performance, Security, and Operations review commits in their activated domains.
6. On QA or specialist-review failure, dispatch only the reported fixes through `SPAWN_FIX`.
7. Merge only a QA-approved commit with every applicable specialist gate satisfied.
8. Return `$speckit-maqa-coordinator merged #<issue>` so it verifies the merge, closes the issue, removes the clean worktree, deletes the exact merged MAQA branch locally and from `origin`, verifies both refs are absent, and reassesses readiness. Branch deletion occurs only after the Issue is complete and the QA-approved commit is confirmed on the default branch; cleanup never uses force deletion.

After the batch is merged:

```text
$speckit-converge
```

If convergence appends tasks, refresh the orchestration ledger, rerun `$speckit-maqa-sync`, and continue through the coordinator.

Do not use `specify workflow run speckit` in this lane. Its bundled graph proceeds from tasks directly to core implementation and bypasses the MAQA issue/worktree flow.

## Unity production pipelines

These pipelines sit inside the selected Direct or MAQA implementation lane. They do not replace Spec Kit gates or create a third lane.

### Gameplay and simulation

1. Game Designer/Product Owner defines observable mechanics and player outcomes.
2. Game Systems/Economy makes formulas, units, caps, rounding, tuning ownership, and exploit cases explicit.
3. Software Architect defines plain-C# domain/simulation boundaries and Unity adapters.
4. Gameplay Developer implements the assigned paths with deterministic Edit Mode tests.
5. Test/QA verifies boundary, persistence, offline-progress, regression, and Play Mode scenarios.
6. Performance Engineer runs scale tests when the feature affects tick, graph, rendering, allocation, or large-factory behavior.

### Visual assets and Unity integration

The Art Director owns an approved, versioned Art Bible and approves every asset-family brief before production. Each brief must include at least:

```yaml
asset:
  id: smelter_mk1
  type: production_building
grid:
  width: 2
  height: 2
perspective: top-down-pseudo-isometric
ports:
  input: left
  output: right
states: [idle, working, no_power]
targets:
  source_size: <pixels>
  unity_pixels_per_unit: <value>
art_bible: <approved-version>
source_and_license: <provenance>
```

The production chain is mandatory:

1. Asset Artist creates source candidates with the approved local or external workflow.
2. Art Director checks style, silhouette, scale, perspective, ports, states, and visual noise.
3. Technical Artist imports only approved candidates and configures sprite mode, pivot, compression, sorting, atlases, animation, prefab, grid footprint, and definition references.
4. UI/UX or Game Designer checks player readability; Test/QA validates the prefab and required states in Unity.
5. Performance reviews atlas, texture, batching, animation, memory, and render cost when material.

ComfyUI is the preferred optional local exploration/2D production tool once an approved workflow exists. Blender is a later option for assets that benefit from a fixed-camera, fixed-lighting 3D-to-2D render pipeline. Neither tool is required for version 0.1, and generated assets never bypass Art Director and Technical Artist review.

### Audio

Start audio after representative factory scenes exist. Sound Design defines machine loops, belts, construction, upgrades, UI feedback, ambience, loudness, variation, looping, licenses, and triggers. Technical Art or the assigned Unity integrator configures import and mixer settings; Gameplay/UI code owns explicit trigger contracts. Composition begins after the core loop and regional progression are stable. Prefer local tools and licensed libraries; hosted generation remains an explicitly selected fallback.

### QA and performance simulation

QA derives acceptance tests, runs Edit Mode and Play Mode suites, inspects compilation and console output, validates builds, and uses deterministic factory scenarios such as 1 hour, 24 hours, and 7 days of simulated progress. Performance work uses declared workloads such as machine and belt counts on a named device class, records simulation time, frame time, GC allocations, and memory, and returns a decision brief before any readability-reducing optimization.

## Role-specific completion evidence

| Role | Required evidence |
|---|---|
| Game Designer/Product Owner | Acceptance matrix, player-value outcome, mechanic/control decisions, exclusions, and uncovered product questions. |
| Software Architect | Plain-C# and Unity boundary review, state ownership, dependencies, contract changes, risks, and constitution alignment. |
| Gameplay Developer | Changed C# paths, gameplay/state/save behavior, deterministic evidence, Edit Mode/Play Mode commands, and console result. |
| UI/UX Developer | Changed UI paths, state/interaction contracts, touch/navigation behavior, loading/disabled/empty/error states, and tests. |
| UI/UX Reviewer | Reviewed screens and states, touch targets, navigation/focus, screen-class checks, accessibility findings, and acceptance impact. |
| Test/QA | Risk-based Edit/Play/build/simulation scenarios, commands/results, clean relevant console output, regression evidence, and exact failures. |
| Game Systems/Economy | Formula and invariant review, progression simulation cases, balance assumptions, exploit risks, telemetry gaps. |
| Art Director | Art Bible version, brief approval, style/silhouette/perspective/state-readability review, and rejected deviations. |
| Asset or 3D/Render Artist | Source paths, brief/version, workflow/model settings, provenance/license, produced states, and Art review result. |
| Technical Artist | Import settings, pivots/sorting/atlases/animation, prefab and grid validation, memory/render checks, and reproducibility. |
| Sound Designer | Source and import paths, cue/loop/variation checks, loudness/mix evidence, license/provenance, and trigger coverage. |
| Composer | Source paths, theme/layer/transition map, loop and progression checks, license/provenance, and review result. |
| Performance Engineer | Named workload/device, baseline, metric and target, profiler evidence, bottleneck analysis, options, and recommendation. |
| Online Services | Contract, validation/auth, failure/retry behavior, migrations, observability, and service/client tests. |
| Security/Privacy | Threat scenarios, trust boundaries, validation/auth evidence, data exposure and dependency findings. |
| DevOps/Operations | Unity batch/build/CI evidence, target configuration, artifacts, secrets, observability, deployment, rollback, and recovery checks. |

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
- Configured Unity Edit Mode, Play Mode, and applicable build/device tests pass.
- Compilation succeeds and the Unity console contains no relevant errors or unexplained warnings.
- Independent QA passes on committed code.
- Game Designer/Product Owner, Software Architect, and applicable UI/UX checks pass.
- New visual/audio assets have approved briefs, provenance/licenses, specialist review, valid Unity imports, required states, and no missing `.meta` files.
- Performance-sensitive work meets its declared workload and budget with profiler or simulation evidence.
- `$speckit-converge` reports no remaining work.
- Relevant knowledge has been ingested and linted.
- The final branch and repository state have been verified by the parent agent.

## Fast invocation prompt

Use this when starting the application design:

```text
Follow AGENTS.md and docs/spec-kit-greenfield-workflow.md. Verify the Greenfield and Unity test/tool gates first. Then design the feature through wiki recovery, Game Designer/Product Owner discovery, applicable Unity production-role reviews, specification, clarification, planning, diagrams, tasks, analysis, and orchestration readiness. Stop at the implementation-lane decision and present the evidence for choosing Direct or MAQA, including activated and omitted roles.
```

# Agent Working Agreement

This repository uses Spec Kit as the source of product and implementation intent. All agents must follow [docs/spec-kit-greenfield-workflow.md](docs/spec-kit-greenfield-workflow.md).

## Authority and lifecycle

1. `.specify/memory/constitution.md` governs every specification, plan, task, and implementation decision.
2. The active feature's `spec.md`, `plan.md`, and `tasks.md` define the intended product and technical scope.
3. During MAQA execution, GitHub Issues are the workflow-status authority. Local task checkboxes and GitHub Project draft cards do not define status.
4. Read `.specify/extensions.yml` before every Spec Kit phase. Execute mandatory hooks and explicitly evaluate relevant optional hooks.
5. Do not implement before the specification, clarification, plan, task, and analysis gates are complete.
6. When tasks change, refresh and validate the orchestration ledger before implementation.
7. The `after_implement` hooks `speckit.wiki.ingest` and `speckit.learn.review` are mandatory (`optional: false`) in both implementation lanes. Do not report an implementation run, MAQA batch, or feature as complete until both have run on the result, or until a recorded blocker explains why one could not run. After any `specify extension add` or `update`, confirm that both entries still have `optional: false`, and that the `before_specify` `speckit.wiki.query` entry still exists with `optional: false`, because re-registration can restore the extension default. The wiki extension does not register that `before_specify` hook itself.
8. Spec Kit skills are installed for every configured AI integration (Codex in `.agents/skills/`, Claude Code in `.claude/skills/`; Codex is the default). This document writes skill invocations in Codex form, such as `$speckit-maqa-qa`; Claude Code invokes the same skill as `/speckit-maqa-qa`. Hook command IDs such as `speckit.maqa.qa` map to either form by replacing dots with hyphens.

## Greenfield bootstrap gate

Before the first feature begins, require all of the following:

- The constitution contains no template placeholders.
- `.specify/branch-convention.yml` exists and the chosen convention is documented.
- The project wiki has been initialized.
- The repository has an initial commit pushed to GitHub and GitHub reports a default branch.
- `$speckit-maqa-setup` succeeds and the `maqa`, `maqa:in-progress`, `maqa:in-review`, and `maqa:blocked` labels exist.
- `maqa-config.yml` contains real Unity Edit Mode and Play Mode test commands before implementation starts. UI features must explicitly cover accessibility, supported screen classes, and empty/error states in their acceptance and QA evidence.

If a gate is missing, report it and complete safe local preparation before implementation. Do not claim the project is implementation-ready.

## Roles and review gates

The parent agent owns scope, sequencing, integration, and final verification. Role agents receive a bounded assignment and return evidence; they do not broaden product scope.

Role lenses, role activation by project phase, the review waves, the implementation lens matrix, and per-role tool boundaries are in [docs/agents/roles-and-reviews.md](docs/agents/roles-and-reviews.md), which is part of this agreement. The parent reads it before writing any assignment, dispatching any reviewer, or planning review waves, and copies the assigned role's section and required secondary lenses into the assignment. Workers and reviewers read the whole file only when their assignment requires it.

Independence rules that bind every agent:

- Reviewers, including Test/QA, Software Architect, UI/UX Reviewer, Security/Privacy, Performance, and Art Director in review, remain read-only. QA does not repair the implementation; failures return to the assigned implementation agent.
- No agent approves its own work: the agent that wrote UI cannot give its independent UI/UX approval, and an architect does not approve its own implementation.
- Secondary lenses review the committed result and return findings. They do not edit the same worktree concurrently.
- Review waves respect `max_parallel: 3`.

Game Designer/Product Owner, Software Architect, and Test/QA gates remain mandatory for every non-trivial feature. Game Systems/Economy is mandatory for production, upgrade, unlock, reward, offline-progress, or balancing changes. UI/UX is mandatory for player-visible interaction. Art Direction is mandatory for new or changed visual assets; Technical Art is mandatory when assets, prefabs, animation, import settings, or atlases change. Performance is mandatory for hot paths or scale targets. Security is mandatory for trust boundaries or persisted player data. DevOps is mandatory for builds, automation, online runtime, or migrations. State which optional roles were omitted and why.

## Token-efficient agent and model routing

Use the least expensive model and reasoning level that safely covers the assignment. Model choice never removes a required review, test, or independence gate.

| Tier | Default reasoning | Codex model | Claude Code model | Appropriate work |
|---|---|---|---|---|
| Low | `low` | `GPT-5.6 Luna` | `haiku` (Haiku 4.5) | Deterministic extraction, issue/worktree reconciliation, narrow documentation changes, checklist validation, test-log summarization, and read-only reviews with explicit criteria and small diffs |
| Medium | `medium` | `GPT-5.6 Sol` | `sonnet` (Sonnet 5) | Default implementation, focused debugging, ordinary Unity/domain/UI work, Test/QA, and bounded specialist reviews |
| High | `high` | `GPT-6 Astra` | `opus` (Opus 5.5) | Cross-boundary architecture, persistence/security/concurrency, ambiguous systemic failures, migrations, or remediation that remains unresolved after Medium at `high` reasoning |

The parent agent uses the model column of its own runtime. Where a runtime cannot set reasoning per delegated agent, the tier's model choice applies and reasoning escalation is recorded in the assignment instead.

The parent orchestrator runs on the High-tier model: it owns scope, sequencing, integration, and final verification. Delegated agents run on their assignment's tier. A Claude Code subagent inherits the parent's model when none is given, so every spawn passes the tier's model explicitly (`model: haiku|sonnet|opus`), and the assignment names the tier.

- Start at the listed default. Raise Medium to `high` reasoning before moving to the High tier; use High at `xhigh` only when the parent records a concrete unresolved risk. Do not use `max` by default.
- Route by the actual Issue, not by role prestige. A mechanical architecture checklist may use Low; a subtle deterministic-simulation implementation may require Medium at `high`; the High tier remains exception-driven.
- Resume the same worker for bounded QA fixes so it reuses established context. Do not start a fresh high-cost agent unless the prior context is unreliable or independence is required.
- The parent reads shared artifacts once and sends a compact evidence packet: exact task text, relevant requirement/contract sections, changed paths, commands, and known risks. Do not paste whole specs or plans when path-and-section references or short excerpts suffice.
- Review the exact commit and changed contracts first. Expand to surrounding code only when a dependency, public boundary, or finding requires it. Do not ask multiple agents to rediscover the same repository facts.
- Keep Game Designer/Product Owner, Software Architect, and Test/QA verdicts independent. Compatible optional lenses may share one read-only review when independence is preserved, for example Product plus Game Systems on a small economy invariant.
- Use deterministic tools before model reasoning: repository searches, compiler/test output, schema validation, Git ancestry, and MCP inspection. Return concise structured evidence and omit narrative already present in the handoff.
- Escalate model or reasoning only after recording the concrete ambiguity, failed attempt, or risk that the lower tier could not resolve. After the high-risk decision is settled, route bounded implementation and verification back to the Medium or Low tier where appropriate.

## Tool and permission boundaries

Grant each assignment the smallest useful tool and write scope. Installing a Unity package does not prove that its MCP server is connected to the active agent runtime; verify tool availability before dispatch and record the fallback command when it is unavailable.

Local Unity, ComfyUI, and Blender readiness is tracked in `docs/integrations/mcp-readiness.md`. Before assigning work that names one of these MCPs, verify a real tool call in the target agent's current session. When Unity MCP is available, prefer its focused scene, hierarchy, serialized-object, importer, test-status, and console inspection tools over broad file/log ingestion; this is both more authoritative and more token-efficient. Use the repository-owned CLI for complete reproducible suites and as the explicit fallback when the current session lacks Unity tool bindings. ComfyUI and Blender are required gates for assignments whose approved asset brief selects those workflows; do not silently replace them with another generator. Their absence does not block unrelated gameplay/domain work.

Per-role normal tools and prohibitions are listed in the roles file above.

ComfyUI, Blender, hosted audio generation, and other external providers are optional capabilities, not project prerequisites. Use them only after the relevant workflow, model/provider, license/provenance rule, cost boundary, and output path are approved. Never expose repository secrets or grant every agent every available MCP.

### Unity plugin skills (Claude Code only)

The Unity plugin adds optional `unity:*` skills for Claude Code agents. They are domain knowledge, not workflow steps or gates; Codex does not have them, so no assignment may depend on them. Before a Claude Code agent uses one, or when the parent names applicable skills in an assignment, read [docs/agents/unity-plugin-skills.md](docs/agents/unity-plugin-skills.md), which is part of this agreement. Its guardrails bind: tests and builds still run through `scripts/test-unity.ps1` under the resource leases, package changes need a plan decision, and the render pipeline changes only through F004.

### Shared workstation resources

The workstation has one Unity editor slot and one Android emulator (`emulator-5554`). Sessions and MAQA workers that used them in parallel corrupted each other's evidence. Unity CLI runs and builds whose active build target is Android also end with "Killing ADB server", which takes the emulator offline. Both resources are therefore leased through `scripts/resource-lease.ps1`. A lease is an atomically created file in the Git common directory, so every worktree and session of this clone sees it.

- Before any Unity editor session, Unity CLI test run or build, or emulator/`adb` work, acquire the resource: `-Acquire -Resource unity|emulator -Owner <issue-n or session name> -Purpose <text> -TtlMinutes <n>`. When a Unity run may have Android as its active target, which is true for any worktree whose `Library/` came from an Android build, acquire both resources in one call: `-Resource unity,emulator`.
- Exit code 2 means another owner holds the resource. Wait, or ask that holder; never delete a live lease or take it over. An expired lease counts as free, and the takeover is reported.
- Release as soon as the work ends: `-Release -Resource ... -Owner ...`. Acquiring again as the same owner renews the lease. Use `-Status` to see who holds what.
- In the MAQA lane, the parent acquires the leases a dispatched Issue needs, with owner `issue-<n>`, before the `SPAWN`. The parent releases them after QA and merge, or when the Issue is blocked. GitHub labels `resource:unity` and `resource:emulator` mark which Issues need which lease. Issues without these labels, such as plain-C# domain work or documentation, may run in parallel up to `max_parallel`.
- `scripts/android-emulator-journey.ps1` acquires the emulator lease for its run with `-LeaseOwner`. It renews a lease its owner already holds and stops before any `adb` call when someone else holds it.

## Assignment contract

Every delegated assignment must include:

- role and review lens;
- complexity tier, selected model/reasoning level, and any escalation rationale;
- authoritative issue or work-unit identifier;
- allowed scope and paths;
- allowed tools/MCPs and explicit forbidden tools or mutations, plus applicable Unity plugin skills for Claude Code agents;
- required source artifacts;
- relevant wiki pages as `wiki/pages/<page>.md` paths chosen from `wiki/INDEX.md` for the assignment's domain, or an explicit "none relevant"; pass paths, not page content;
- acceptance criteria and relevant constitution rules;
- dependencies and forbidden overlaps;
- Unity version, target platform/device class, and Edit Mode/Play Mode validation required;
- Art Bible version and structured asset brief for visual work;
- workload, baseline, metric, and target for performance work;
- targeted validation commands;
- expected output format and whether the assignment is read-only or may edit.

Agents return changed paths, tests/checks, evidence against acceptance criteria, unresolved risks, and a clear done/blocked verdict. The parent verifies all results before accepting them.

## Unity architecture and asset rules

- Unity `6000.6.0f1` is pinned in `ProjectSettings/ProjectVersion.txt`. A version change requires an explicit plan and compatibility verification.
- Prefer `Assets/Scripts/Domain/`, `Simulation/`, `Application/`, `Presentation/`, and `Persistence/` as responsibility boundaries when the feature plan confirms they fit. Domain and simulation assemblies must remain independently testable without scenes.
- MonoBehaviours, views, input handlers, animation events, and editor tooling must delegate gameplay decisions to application/domain operations.
- ScriptableObjects may hold authored definitions and tuning data; mutable runtime state must have an explicit owner and must not silently mutate source assets.
- Commit `Assets/` with every required `.meta` file, `Packages/manifest.json`, `Packages/packages-lock.json`, and `ProjectSettings/`. Never edit or commit generated `Library/`, `Temp/`, `Logs/`, `Obj/`, or `UserSettings/`.
- Scene, prefab, material, animation, and importer changes must be made through Unity tooling where practical and reviewed for unintended serialized changes.
- Unity builds and test runs re-serialize known noise into `ProjectSettings/`: empty iOS icon slots, an explicit Standalone managed code variant, an automatic Graphics API entry, a leaked GLES3 override, and Enter Play Mode options. `scripts/check-settings-noise.ps1` detects each signature a change introduces. The `.githooks/pre-commit` hook runs it on staged changes; enable the hook once per clone with `scripts/install-git-hooks.ps1`. MAQA QA also runs it with `-Commit <sha>` on the exact assignment commit, because a worker may commit without the hook. Commit a deliberate change to one of these settings only with `IDLEFACTORY_ALLOW_SETTINGS_NOISE=1` and a recorded decision.
- The batch-end Windows test-player command is `scripts/test-unity.ps1 -StandaloneWindowsPlayer`. It explicitly passes `-buildTarget win64` and runs only Play Mode tests tagged `[Category("StandalonePlayer")]`. A run on this host logged `Killing ADB server` at shutdown despite the Windows target, so until proven otherwise acquire both `unity,emulator` leases; the test does not launch or use an emulator.
- Gameplay/economy rules require Edit Mode tests. Scene, prefab, UI, input, save/load, and runtime integration require Play Mode tests when the behavior cannot be proven below the engine boundary.
- On this Windows Codex host, Unity CLI starts in the normal sandbox have repeatedly failed before test discovery with a UPM database-open error and local Package Manager IPC timeout; the same runner passed with local IPC access after a narrowly approved, per-command sandbox escalation. A process-scoped `UPM_CACHE_ROOT` did not resolve the restricted-sandbox failure. For Unity CLI tests/builds that need UPM, complete the usual checkout/editor ownership preflight and request the narrow command-level escalation on the first invocation instead of retrying once in the restricted sandbox. Keep the general sandbox enabled; do not reset or rewrite UPM caches as a workaround. This execution escalation is distinct from the Windows sandbox implementation setting `elevated`/`unelevated`. If escalation is unavailable, use Unity MCP only after verifying it is connected to the exact target checkout, or report the blocker; do not describe a pre-discovery startup failure as a test failure.
- Generated visual or audio files must record source/provenance, license, generator/workflow version, transformation steps, and the approved brief. They are not production-ready until specialist review and Unity integration checks pass.

## Implementation lane rules

Choose exactly one implementation lane for a feature:

- Direct lane: use the work-unit ledger with `$speckit-implement`. Use `$speckit-checkpoint-commit` at reviewed, coherent checkpoints. `$speckit-implement` dispatches the mandatory `after_implement` hooks itself.
- MAQA lane: use `$speckit-maqa-sync`, `$speckit-maqa-coordinator`, `$speckit-maqa-feature`, and `$speckit-maqa-qa`. MAQA workers create their required assignment commit; do not run the checkpoint skill inside a MAQA worker. MAQA never passes through `$speckit-implement`, so the parent runs the mandatory `after_implement` hooks itself after each merged batch and before `$speckit-converge`: `$speckit-wiki-ingest specs/<feature>` and then `$speckit-learn-review`. Workers and QA agents never run these hooks. The parent may dispatch both hooks, in that order, to one dedicated Medium-tier documentation agent, and the wiki lint to a Low-tier agent. That agent receives the batch's merged Issues and commits and edits only `wiki/` and `specs/<feature>/learn.md`. The parent stays accountable: it reviews the resulting diff against the merged batch before committing, and `scripts/check-after-implement-hooks.ps1` gates the result as before.
- MAQA workers do not tick `tasks.md` checkboxes. Before the hooks run, the parent mirrors Issue state into them with `scripts/maqa-sync-task-checkboxes.ps1 -TasksFile specs/<feature>/tasks.md`. A closed Issue is a verified merge, which satisfies the ledger's rule that checkboxes are ticked only after parent verification. The mirrored checkboxes are derived data. GitHub Issues stay the status authority, and neither the coordinator nor `$speckit-maqa-sync` reads checkbox state.
- After merges, the parent runs `scripts/maqa-stale-worktrees.ps1`. The report is read-only and lists, for each linked worktree, whether it is merged, dirty, and what state its MAQA Issue is in. For worktrees that are merged, clean, and whose Issue is closed, it prints the non-forcing removal commands (`git worktree remove`, `git branch -d`). The parent reviews these and hands them to the user when auto mode forbids deletion. Worktrees of other tools (`.codex`, `.kilo`) and branches that were re-committed or rebased stay REVIEW.
- Before `$speckit-converge`, the parent runs `scripts/check-after-implement-hooks.ps1 -FeatureDir specs/<feature>`. The gate checks four things: the hooks are still mandatory, `learn.md` and the feature's wiki ingest are newer than the last implementation commit, the wiki was linted after that ingest, and the checkboxes match the Issues. A failed gate blocks convergence and feature completion.

A MAQA batch ends when its merges, the `after_implement` hooks, the hook gate, and the stale-worktree report are done. The parent then stops and proposes a fresh session to the user instead of starting the next batch, unless the user has said to continue. The handoff names the feature directory, the last merged commit, the next ready Issues, and any leases still held. GitHub Issues and the ledger carry the authoritative state, so the new session loses nothing.

Do not run `specify workflow run speckit` for a MAQA feature because the bundled workflow proceeds directly from tasks to core implementation.

## Windows compatibility rules

- GitHub CLI and GitHub Git operations must use direct access when the agent shell inherits the failing `http://127.0.0.1:9` proxy. Follow [`docs/integrations/github-cli.md`](docs/integrations/github-cli.md); clear proxy variables only for the command process and verify `origin` before MAQA operations.
- Always pass the explicit `specs/<feature>/tasks.md` path to the orchestration prepare and validate skills. Their no-argument fallback points to a Bash script not installed in this PowerShell project.
- When `$speckit-maqa-sync` references the missing `.specify/extensions/maqa/scripts/powershell/check-prerequisites.ps1`, use the equivalent installed core script `.specify/scripts/powershell/check-prerequisites.ps1` with `-Json -RequireTasks -IncludeTasks`.
- Atlas scripts live under `.specify/extensions/atlas/skill/scripts/` and must run through `uv` with the dependencies declared by the Atlas skill.

## Knowledge and documentation

- Query the wiki before specifying work. The mandatory `before_specify` hook `speckit.wiki.query` enforces this; pass the feature's domain question and carry cited pages and reported coverage gaps into the spec.
- The wiki is read during execution too: every delegated assignment names its relevant wiki pages (see the assignment contract), so workers and reviewers start from recorded pitfalls instead of rediscovering them.
- Ingest verified planning decisions after planning and implementation learnings after implementation.
- Generate learning diagrams after the plan stabilizes and `learn.md` after implementation.
- Keep the approved Art Bible, asset briefs, generation provenance, audio rules, performance budgets, profiling reports, and Unity import conventions in versioned project artifacts and ingest durable decisions into the wiki.
- Freeze versioned Art Bible rules before producing an asset family. A style change requires a new approved version and an explicit migration decision for existing assets.
- Run Atlas Storybook at milestones for this single repository. Use Atlas Map only when a real multi-repository workspace exists.
- GitHub Project #4 mirrors one item per MAQA Issue through `scripts/maqa-board-mirror.ps1`, which implements the coordinator's `board_mirror` step. The parent calls `-Issue <n> -Status InProgress|InReview|Done` only after a verified coordinator transition, and `-Backfill` rebuilds the whole board from Issue state. Mirror failures are warnings and never change MAQA state. Never read the board as workflow status. Do not run `$speckit-maqa-github-projects-populate`: its per-feature draft card would duplicate the per-Issue items.

## Completion gate

A feature is complete only when acceptance criteria pass, configured Unity Edit Mode and Play Mode tests pass, the tagged Windows test-player suite passes once for each implementation batch, the Unity console contains no relevant errors, QA passes on the committed result, convergence reports no remaining work, and the parent verifies the final repository state. Player-visible work also requires independent UI/UX acceptance; visual/audio work requires the applicable Art, Audio, and Technical Art evidence; performance-sensitive work requires measured results against its declared workload and target. Relevant knowledge and production rules must be ingested into the wiki, and the mandatory `after_implement` hooks (`$speckit-wiki-ingest`, `$speckit-learn-review`) must have run on the final implementation, with `specs/<feature>/learn.md` current.

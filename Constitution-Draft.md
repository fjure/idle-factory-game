# Idle Factory Game Constitution

## Core Principles

### I. Correctness and Observable Behavior

Every implementation MUST satisfy the approved specification and its observable acceptance
criteria before convenience, brevity, cleverness, or delivery speed are considered. Agents MUST
NOT silently approximate, reinterpret, or simplify specified gameplay, state transitions,
calculations, persistence, progression, resource consumption, rewards, timing, input behavior,
failure cases, or edge cases.

Material ambiguity MUST be resolved through the Human Decision process before implementation.
Expected behavior MUST cover the happy path, boundaries, invalid input, failure behavior, state
interaction, player feedback, and compatibility impact. Invalid states MUST be prevented by design
where practical and MUST otherwise be detected explicitly.

Rationale: an idle game is defined by accumulated state and numerical rules; a small silent error
can invalidate progression, saves, balance, or player trust.

### II. Domain Architecture and Explicit State

Architecture MUST reflect game concepts rather than framework accidents. Gameplay and economy
rules MUST NOT be distributed across UI components, input handlers, animations, rendering, or
unrelated infrastructure. Dependencies MUST point toward stable domain concepts; presentation and
infrastructure MAY depend on the domain, while the domain MUST remain independently testable.

Every significant mutable state value MUST have an identifiable owner, permitted writers, explicit
transitions, observer mechanism, persistence rule, and determinism requirement. State changes MUST
occur through explicit operations. Derived values MUST be calculated rather than redundantly stored
unless a measured constraint justifies caching. Global mutable state, circular dependencies, hidden
temporal coupling, and undocumented call-order requirements are prohibited.

Architecture MUST remain proportional to current requirements. Public APIs MUST expose the smallest
useful contract, and internal details MUST NOT become public for convenience.

### III. Deterministic and Data-Driven Simulation

Gameplay, production, upgrade, unlock, reward, offline-progress, and economy calculations MUST be
deterministic whenever the mechanic permits. Randomness, time, frame delta, network responses,
filesystem state, platform APIs, and external services MUST be explicit dependencies when they
affect outcomes. Randomness MUST be seedable and time MUST be controllable in tests where practical.

Values expected to change through balancing MUST be separated from implementation logic and given
domain names. Formulas, units, rounding, limits, overflow behavior, offline-time caps, and numerical
invariants MUST be documented and tested. Configuration MUST be introduced only for real variation
or tuning needs; moving every value to configuration is not a design goal.

Balance assumptions MUST be distinguishable from verified product decisions. When live telemetry
is required to tune a value, the plan MUST state the hypothesis, required signal, and safe adjustment
mechanism instead of presenting an invented value as final.

### IV. Tests Protect Behavior

Testing is part of implementation. Critical domain rules, state transitions, economy formulas,
persistence behavior, and public contracts MUST have automated tests where technically possible.
Tests MUST focus on observable behavior and contracts rather than private implementation details.

Use the smallest effective level:

- unit tests for deterministic domain and calculation logic;
- integration tests for persistence, subsystem interaction, and contracts;
- scenario or play-mode tests for engine/runtime behavior;
- manual evaluation only for experiential qualities that automation cannot judge.

Relevant boundary conditions, invalid inputs, failure paths, exceptional conditions, save/load
round trips, and regression risks MUST be covered. Every reproducible bug fix MUST add a regression
test. A feature MUST NOT be considered complete because only its happy path works. Test code MUST
remain readable and maintainable.

### V. Simplicity and Maintainable Code

Production code MUST communicate domain intent and remain safe to change. High cohesion, low
coupling, encapsulation, separation of concerns, dependency inversion, composition, fail-fast
behavior, and the principle of least surprise MUST be applied when they solve a current problem.
SOLID, DRY, KISS, and YAGNI are decision tools rather than quotas.

Every abstraction, interface, service, manager, factory, repository, event bus, framework, or layer
MUST solve an identified problem. Agents MUST prefer a clear concrete implementation until meaningful
variation exists. Duplication MAY remain temporarily when the shared concept is not understood, but
multiple sources of truth for the same domain rule MUST be consolidated.

Names MUST express game-domain intent. Generic names such as `Manager`, `Helper`, `Utils`, `Data`,
or `Thing` require a documented reason when a precise domain name exists. Comments MUST explain
rationale, constraints, or invariants rather than restating code. Touched code MUST leave its local
area in an equal or better structural state, while unrelated large refactors MUST become separate
specified work.

### VI. Performance Is Measured

Performance-sensitive work MUST define a credible workload or measurable target before optimization.
Frame time, update frequency, allocations, garbage collection, CPU, GPU, loading, asset use, storage,
and repeated calculations MUST be considered where relevant. Code executed every frame, tick, or over
large collections requires explicit performance review.

Premature optimization is prohibited. Caching, pooling, batching, spatial partitioning, specialized
data structures, and readability-reducing micro-optimizations require profiling, benchmark evidence,
or a documented hard constraint. Any non-obvious optimization MUST document why it exists and how its
benefit is verified.

### VII. Human Authority and Explicit Decisions

Agents MAY make routine, reversible implementation decisions that do not materially change an
external contract or observable game behavior. A human decision is REQUIRED for material changes to
gameplay mechanics, progression, economy, balance, difficulty, rewards, penalties, player agency,
game feel, controls, UX, accessibility, save compatibility, persistent state, procedural behavior,
narrative, content policy, monetization, analytics, privacy, security, destructive migrations, or
other externally observable behavior.

Agents MUST NOT invent missing domain rules to unblock work. They MUST first investigate available
evidence and then present a compact decision brief containing context, constraints, two to four
realistic options, tradeoffs, a reasoned recommendation, explicit assumptions, and the smallest set
of questions needed. Facts, assumptions, and recommendations MUST be labeled distinctly.

Human attention is scarce. Agents MUST batch related high-value questions and MUST NOT repeatedly ask
about routine details covered by project conventions or authoritative artifacts.

### VIII. Specification, Traceability, and Quality Gates

Every non-trivial change MUST pass through the Spec Kit lifecycle defined in
`docs/spec-kit-greenfield-workflow.md`. The constitution governs all lower-level artifacts. The
feature `spec.md` defines observable intent, `plan.md` defines the justified technical approach, and
`tasks.md` defines independently verifiable implementation work.

Requirements and acceptance criteria MUST be traceable through the plan, tasks, implementation, and
verification evidence. Plans MUST explain responsibility boundaries, state ownership, dependencies,
data flow, persistence, compatibility, performance, security, observability, and test strategy where
applicable. Tasks MUST describe verifiable outcomes, include validation, preserve dependency order,
and split unrelated responsibilities.

`$speckit-analyze` MUST run after stable task generation and before implementation. Critical findings
MUST be resolved. The orchestration ledger MUST cover every task exactly once and MUST be refreshed
after task changes. `$speckit-converge` MUST run after implementation; appended work MUST be
implemented and convergence rerun until no specified work remains.

### IX. Role-Based Independent Review

Non-trivial features MUST be evaluated through the role lenses defined in `AGENTS.md`. Product Owner,
Architect, and Test/QA review gates are mandatory. Game Systems/Economy is mandatory for progression,
production, upgrades, rewards, offline progress, or balance. UI/UX is mandatory for user-visible
behavior. Security/Privacy is mandatory for trust boundaries or persisted player data. DevOps and
Operations is mandatory for runtime services, deployment, configuration, or migrations.

Implementing agents MUST receive bounded ownership, authoritative acceptance criteria, allowed paths,
dependencies, and validation commands. Parallel writers MUST use independent worktrees and disjoint
paths. Review agents MUST inspect committed evidence independently and MUST NOT repair the artifact
while acting as QA. The parent agent owns reconciliation, integration, and final verification.

### X. Safe Delivery and Durable Knowledge

Failures MUST be explicit through validation, typed results, assertions, structured logging, or
recoverable behavior appropriate to the context. Exceptions MUST NOT be swallowed. Expected gameplay
outcomes MUST NOT be modeled as technical errors. Diagnostic information MUST be sufficient to locate
a failure without exposing secrets or unnecessary player data.

Save formats, persisted state, APIs, and externally consumed contracts MUST have an explicit
compatibility and migration policy. Destructive or irreversible migrations require human approval,
backup and rollback planning, and verification evidence. Dependencies MUST be intentional, minimal,
pinned or constrained according to the selected ecosystem, and reviewed for maintenance and security
risk.

Meaningful decisions MUST become durable project knowledge in the appropriate spec, plan, research,
ADR, contract, wiki page, game-design document, or code documentation. Conversation history alone is
not a project artifact. Previously approved decisions MUST NOT be reopened without new evidence.

## Engineering and Product Constraints

- The game domain MUST remain independent from rendering and delivery technology wherever practical.
- Player-visible calculations MUST define units, rounding, bounds, and feedback behavior.
- Persistence MUST validate loaded data, handle corruption safely, and preserve documented save
  compatibility. Schema changes MUST include migration and rollback or an explicit compatibility break.
- Offline progress MUST define its time source, maximum elapsed interval, clock-manipulation behavior,
  numerical limits, and reconciliation with persisted state.
- UI flows MUST cover loading, success, empty, error, disabled, and recovery states where applicable.
  Keyboard use, focus, readable contrast, scalable text, and responsive layouts are acceptance concerns,
  not optional polish.
- Security boundaries MUST define trusted and untrusted input, authorization, secret handling, abuse
  controls, and privacy-sensitive data. Client-provided values MUST NOT be trusted for authoritative
  economy or progression state.
- Runtime services and storage MUST define configuration, structured logs, health signals, backups,
  recovery, migration, and rollback before production use.
- Performance claims MUST have a target and measurement method. Experiential claims MUST have a named
  human evaluation method when automation is insufficient.
- Public contracts, configuration, non-obvious invariants, and operational procedures MUST be documented.
- No technology, engine, framework, database, or deployment platform is mandated until selected through
  a documented plan decision grounded in product constraints.

## Development Workflow and Quality Gates

### Project bootstrap

Before the first feature is implemented:

1. This constitution MUST be ratified and free of placeholders.
2. The branch convention MUST be configured and validated.
3. The project wiki MUST be initialized.
4. The initial repository state MUST be committed and pushed so GitHub has a default branch.
5. `$speckit-maqa-setup` MUST complete and create all required workflow labels before MAQA is used.
6. `maqa-config.yml` MUST contain real test commands as soon as the technology stack is known.
7. GitHub Projects MAY provide a feature-level view, but its draft cards MUST NOT compete with the
   task-level GitHub Issues used by MAQA.

### Feature design

Every non-trivial feature MUST follow this order:

1. Query existing wiki knowledge and inspect authoritative project context.
2. Run the Product Owner, Game Systems/Economy, and UI/UX discovery lenses as applicable.
3. Create a compliant feature branch and run `$speckit-specify`.
4. Resolve material ambiguity with `$speckit-learn-clarify` or `$speckit-clarify`.
5. Run requirements checklists where risk warrants them.
6. Run `$speckit-plan`, record research and decisions, and ingest durable planning knowledge.
7. Generate relevant architecture diagrams and complete role-based plan reviews.
8. Run `$speckit-tasks`, then `$speckit-analyze` and resolve blocking findings.
9. Prepare and validate the work-unit ledger using the explicit active `tasks.md` path.
10. Commit the reviewed design artifacts at a coherent checkpoint.

Configured mandatory hooks MUST execute. Relevant optional hooks MUST be evaluated explicitly rather
than silently ignored. Windows compatibility fallbacks in `AGENTS.md` are binding when an extension
references a script unavailable in this PowerShell installation.

### Implementation

The parent agent MUST choose exactly one implementation lane per feature:

- Direct lane: `$speckit-implement` executes validated work units. Checkpoint commits are allowed at
  coherent, reviewed boundaries.
- MAQA lane: `$speckit-maqa-sync` creates the authoritative task Issues, the coordinator dispatches
  bounded worktrees, `$speckit-maqa-feature` implements one assignment, and `$speckit-maqa-qa`
  independently verifies the committed result. MAQA workers MUST NOT create checkpoint commits.

The bundled `specify workflow run speckit` MUST NOT be used for a MAQA feature because it proceeds
directly from tasks to core implementation. No feature may use both implementation lanes.

### Definition of done

A feature is complete only when all applicable conditions hold:

- acceptance criteria, edge cases, error behavior, and approved domain decisions are implemented;
- no unresolved behavior-changing assumption remains;
- relevant automated tests exist and all configured tests pass;
- performance-sensitive behavior meets its measured target;
- persistence, migration, security, accessibility, responsive, and operational obligations pass;
- independent Test/QA passes on the committed artifact;
- Product Owner and Architect approve the result, with other required role lenses completed;
- `$speckit-converge` reports no remaining specified work;
- meaningful implementation knowledge is ingested into the wiki and the wiki is linted;
- public contracts and non-obvious constraints are documented;
- dead code, debug artifacts, and unrelated scope changes are absent;
- the parent agent verifies the final diff, test evidence, branch, and repository state.

## Governance

This constitution is the highest project-local authority for specifications, plans, tasks, code,
reviews, and delivery. When it conflicts with a lower-level artifact, the lower-level artifact MUST
change. `AGENTS.md` and `docs/spec-kit-greenfield-workflow.md` operationalize this constitution and
MUST remain consistent with it.

Amendments require explicit human approval and MUST record the changed rule, rationale, consequences,
and migration impact. Versioning follows semantic governance rules:

- MAJOR for removal or backward-incompatible redefinition of a principle;
- MINOR for a new principle or materially expanded mandatory guidance;
- PATCH for clarification without a semantic policy change.

Every amendment MUST update the version and `Last Amended` date. `Ratified` records the original
adoption date and MUST NOT change. Compliance MUST be reviewed during specification analysis,
architecture review, implementation QA, convergence, and final parent verification. A violation MUST
be resolved by changing the specification, plan, tasks, or implementation, obtaining a human domain
decision, or explicitly amending this constitution; it MUST NOT be waived merely to continue work.

**Version**: 1.0.0 | **Ratified**: 2026-09-13 | **Last Amended**: 2026-09-13

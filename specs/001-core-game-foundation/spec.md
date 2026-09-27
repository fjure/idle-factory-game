# Feature Specification: Core Game Foundation

**Feature Branch**: `feat/001-core-game-foundation`

**Created**: 2026-09-13

**Status**: Clarified — ready for planning

**Input**: [Specify-Draft.md](../../Specify-Draft.md), supplied by the project owner as the complete feature description. Production-cycle clarification confirmed by the owner on 2026-09-13 is recorded below.

## User Scenarios & Testing *(mandatory)*

This feature provides a small playable **development validation scenario**, not the final early-game experience. The users are a player testing the foundation and a developer/QA reviewer inspecting it. Their goal is to place a building, observe controlled resource transformation and blocking, preserve the factory, and reproduce its behavior.

Priorities express delivery order; all four stories are required for this foundation. Preconfigured development factories may be used to test a story independently. The non-canonical fixture D001 is defined under Assumptions.

### User Story 1 - Navigate a factory and place test buildings (Priority: P1)

As a player testing the foundation, I can explore a factory area, identify available space, and place development buildings so their locations and occupied space are understandable.

**Why this priority**: A navigable world with valid placement is the smallest useful interaction slice and makes later production understandable.

**Independent Test**: In a development factory without production or persistence enabled, use touch and desktop pointer input to pan, zoom, preview a building footprint, and accept or reject placement.

**Acceptance Scenarios**:

1. **Given** a factory area extending beyond the visible view, **When** the player pans and zooms, **Then** relevant areas and building footprints can be brought into view and inspected with both touch and desktop pointer input.
2. **Given** available cells, **When** the player previews a development building, **Then** its complete required footprint and placement validity are understandable before confirmation, using a cue beyond color alone.
3. **Given** a valid candidate location, **When** placement is confirmed, **Then** exactly one identifiable building appears at the requested logical location and its entire footprint becomes occupied.
4. **Given** a candidate whose footprint partly overlaps an existing building or extends outside the development area, **When** placement is attempted, **Then** the request is rejected with an understandable reason and no existing identity, occupancy, or resource state changes.
5. **Given** two non-overlapping placements of the same building type, **When** their state is inspected, **Then** they have different persistent instance identities and independently owned operational/resource state.
6. **Given** no selected object, **When** the player selects a placed building and then clears selection, **Then** selection and empty-selection feedback are understandable and neither action changes simulation state.
7. **Given** a separately declared development variant with a different area size and building footprint, **When** its valid, overlapping and out-of-area placements are evaluated, **Then** occupancy and rejection follow that variant's complete footprint and bounds using the same world concepts; D001 dimensions are not permanent limits.

### User Story 2 - Run and inspect constrained production (Priority: P1)

As a player or developer testing the foundation, I can supply a test machine, observe its process, understand why it stops, and make it resume by resolving the actual blocking condition.

**Why this priority**: Correct resource accounting and block/resume behavior establish the factory's core behavior without requiring logistics or final balancing.

**Independent Test**: Use one preconfigured machine and explicit development controls for adding inputs, removing outputs, and toggling its active state. Placement and final player inventory behavior are not prerequisites.

**Acceptance Scenarios**:

1. **Given** D001 with four test inputs, no output, and an active machine, **When** 2.5 simulation seconds pass, **Then** there are still four inputs, no output, and 2.5 seconds of cycle progress.
2. **Given** that partial cycle, **When** another 2.5 simulation seconds pass, **Then** two inputs are consumed and one output appears in one complete operation; progress returns to zero and full output blocks the next cycle.
3. **Given** too little input or insufficient space for the whole output of a cycle, **When** simulation time advances, **Then** no cycle progress, input consumption, or output creation occurs, and inspection identifies the unmet condition.
4. **Given** a partly completed cycle, **When** input is removed or output capacity is filled through a development control, **Then** the process pauses at its current progress; after the condition is resolved, only the remaining productive time is required.
5. **Given** a full output with adequate input, **When** output is removed and productive time advances, **Then** production resumes without credit for the preceding blocked interval.
6. **Given** a partly completed active cycle, **When** the machine is deactivated and later reactivated, **Then** its progress and resources are retained, no production occurs while inactive, and it resumes only if input and output conditions are satisfied.
7. **Given** resource storage at its declared capacity or with insufficient stock, **When** an addition or removal would violate its limits, **Then** the operation is rejected without partial transfer, negative quantities, or capacity overflow.
8. **Given** a selected machine, **When** production, supply, blocking, active state, or loading changes its state, **Then** inspection shows its process, active state, blocking conditions, available/required inputs, stored output/free capacity, and cycle progress consistently.
9. **Given** separate test machines, **When** one receives or produces material, **Then** that action does not automatically supply another machine.

### User Story 3 - Save, restore and apply reduced offline production (Priority: P1)

As a player testing the foundation, I can explicitly save my factory and restore it after closing or resetting the running scenario. When I return after being offline, eligible production is recalculated from the elapsed absence at a reduced rate so that resources reflect constrained offline operation.

**Why this priority**: Persistent identity, resources, partial progress and elapsed absence must survive a session boundary before later progression systems can depend on them.

**Independent Test**: Start from predefined valid factories with ready, partially completed, blocked or inactive production; save and close them for controlled absence durations, restore them, compare state and reduced offline results, then continue with equivalent inputs and elapsed time.

**Acceptance Scenarios**:

1. **Given** a valid factory, **When** an explicit save succeeds and is restored immediately with zero measured offline duration, **Then** building identities/types/positions, occupancy, resource quantities, process references, active state, and partial progress are preserved.
2. **Given** a save made halfway through D001, **When** it is restored with zero measured offline duration and advanced for the remaining 2.5 productive seconds, **Then** exactly one cycle completes with the same resource balances as the uninterrupted reference.
3. **Given** a factory saved while blocked or inactive, **When** it is restored after an offline interval, **Then** input, output and capacity constraints are respected throughout recalculation; blocked or inactive time creates no production credit, and the prior progress is preserved.
4. **Given** a corrupt, incomplete, unsupported-version, or invariant-violating save, **When** loading is attempted, **Then** it fails explicitly and leaves the current valid factory unchanged without partially loading, clamping, or silently discarding progress.
5. **Given** a valid running factory and an earlier successful save, **When** a new save fails, **Then** failure is reported and both the running factory and the previous successful save remain usable.
6. **Given** no save exists, **When** loading is attempted, **Then** the absence is reported without destroying the current factory and the player can continue the development scenario.
7. **Given** an active, unblocked D001 machine with four inputs and empty output, **When** the player next returns after one valid offline second, **Then** it has 0.8 seconds of cycle progress with unchanged resources; after 6.25 valid offline seconds, exactly one five-second cycle has completed. Both cases use the same atomic resource accounting, capacity blocking and residual-progress rules as active simulation.
8. **Given** the same valid saved factory and the same measured offline interval, **When** offline recalculation is repeated from that starting state, **Then** it produces the same resources, blocking state and residual progress and does not count the interval a second time after successful restoration.
9. **Given** a valid running factory, **When** the app reports suspension/background entry or a regular shutdown, **Then** it attempts to persist one coherent lifecycle checkpoint containing the current foundational state and local UTC departure time. If the attempt fails, the previous successful checkpoint remains usable and the failure does not partly overwrite it.

### User Story 4 - Reproduce simulation independently of the view (Priority: P2)

As a developer or QA reviewer, I can advance a known factory by explicit elapsed time and compare its resulting state without relying on animation or rendering.

**Why this priority**: Reproducible behavior enables trustworthy regression tests, persistence checks, and later offline/logistics work.

**Independent Test**: Advance preconfigured factories with identical definitions, state and time-stamped development actions, both without a view and with different view refresh schedules.

**Acceptance Scenarios**:

1. **Given** equivalent starting factories and identical ordered actions at identical simulation times, **When** both advance by the same elapsed time, **Then** their resource quantities, occupancy, active/blocking state and partial progress are equivalent.
2. **Given** unchanged external conditions, **When** one D001 factory advances by five seconds and another by five one-second intervals, **Then** their complete resulting states match; subdividing time does not create or lose resources or progress.
3. **Given** a factory at a known partial-cycle state, **When** its visual representation is removed and recreated, **Then** logical state is unchanged and the recreated view represents it correctly.
4. **Given** finite output already full and adequate input, **When** the factory is advanced by one hour, 24 hours or seven days of explicit simulation time, **Then** it remains within capacity with unchanged inputs and progress; clearing the blockage does not release a backlog of blocked-time production.
5. **Given** the D001 multi-cycle variant with output capacity two, four inputs and empty output, **When** ten seconds are advanced in one interval or ten one-second intervals, **Then** both factories contain zero inputs, two outputs and zero cycle progress. Advancing a further hour while blocked changes none of those values.

### Edge Cases

- An anchor cell fits but the rest of a multi-cell footprint does not: reject the entire placement (US1.4).
- A resource request is negative, invalid, or greater than available quantity/capacity: reject without partial state changes (US2.7).
- Input equals the exact cycle requirement or output capacity equals the exact cycle yield: the cycle can proceed; a shortfall cannot (US2.2–3).
- A condition changes halfway through a cycle: pause and preserve progress; never consume a partial recipe or produce a partial yield (US2.4).
- More than one blocking condition applies: inspection exposes the unmet conditions without inventing a new gameplay priority rule (US2.8).
- Elapsed time is zero: no production change; invalid, negative, or unrepresentable elapsed time is rejected explicitly without state corruption (US4).
- An elapsed interval spans multiple cycles and then reaches a resource limit: process only productive time and retain valid residual progress, never accumulate blocked time (US4.2–4).
- A save contains duplicate instance identities, unknown definition references, overlapping/out-of-area footprints, invalid quantities, or impossible progress: reject it as a whole (US3.4).
- Save/load occurs exactly at a cycle boundary: restore a complete pre- or post-completion state, never a mixed resource transaction (US3).
- Offline recalculation reaches missing input or full output before the effective interval ends: stop at that condition and discard no resources; unused blocked time creates no backlog (US3.3/7).
- The measured local UTC return time precedes the stored departure time or cannot be determined reliably: grant no offline production and report the invalid interval without corrupting or partially replacing the valid saved state (US3.4/7).
- Placeholder visuals are regenerated or a machine leaves the visible view: the underlying production remains authoritative (US4.3).

## Requirements *(mandatory)*

### Functional Requirements

#### World, identity and interaction

- **FR-001**: The system MUST represent a logical two-dimensional factory area with explicit cell availability and occupancy, sufficient to validate complete building footprints.
- **FR-002**: Building types MUST be distinct from placed instances; each instance MUST retain a stable unique identity, type, logical position, footprint, active state, process/progress where applicable, and orientation if the development definition uses it.
- **FR-003**: Placement MUST validate every footprint cell and either create the entire instance or reject the request without altering the existing factory. At least one multi-cell development footprint MUST be demonstrated.
- **FR-004**: Players MUST be able to pan, zoom, preview and confirm placement, select a building and inspect it through touch and desktop pointer interaction, without requiring keyboard shortcuts, right-click, or pixel-perfect targeting.
- **FR-005**: The development view MUST distinguish the factory area, buildings, selection and valid/invalid placement; rejection and unavailable actions MUST provide understandable feedback beyond color alone.
- **FR-006**: Recreating or refreshing the visual representation MUST NOT change authoritative occupancy, resources, machine state or production progress.

#### Resources and production

- **FR-007**: Resources MUST have stable identities distinct from their stored quantities. Test names, values and recipes MUST be explicitly identified as non-canonical development content.
- **FR-008**: Storage MUST expose quantity and finite capacity constraints for its declared contents, accept valid additions/removals, and reject invalid operations atomically without negative quantities or overflow. The reusable capability MUST NOT depend on a final inventory/stacking design.
- **FR-009**: A production definition MUST identify its input requirements, cycle duration and complete output quantities separately from any placed machine. Multiple instances MUST be able to use the same definition without sharing mutable machine state.
- **FR-010**: The foundation MUST demonstrate at least one test process with an input, positive cycle duration and output, plus development operations for supplying input and removing output. These operations MUST be visibly/documentably development controls.
- **FR-011**: A machine MUST progress only while active, with all required input available and enough free capacity for the complete cycle output.
- **FR-012**: Inputs MUST remain in storage during progress. At cycle completion, the required inputs and full output MUST be booked together exactly once; no input is consumed without its corresponding output and no output is created without its input.
- **FR-013**: Missing input, insufficient output capacity or deactivation MUST pause the current cycle without resetting its partial progress or changing its resource quantities. Once resolved, only the remaining productive duration is needed. Stopped time MUST NOT grant future progress or output.
- **FR-014**: Inspection MUST distinguish active/producing, missing-input, insufficient-output-capacity and inactive conditions, and show the process, progress, required/available input and output quantity/free capacity. Multiple unmet conditions MUST remain discoverable.
- **FR-015**: Adding/removing test resources MUST target explicit storage. The foundation MUST NOT automatically transfer material between arbitrary machines, implement conveyors, or imply a globally shared factory inventory.
- **FR-016**: The foundation MUST separate resource definitions, storage, processes and placed instances sufficiently that future recipes, buffers and transport can build on these concepts without introducing those future systems now.

#### Time and state integrity

- **FR-017**: Simulation MUST accept a known elapsed duration without requiring visual rendering or an actively viewed factory. Render frequency MUST NOT determine production yields.
- **FR-018**: Equivalent initial state, definitions and ordered external actions at the same simulation times MUST produce equivalent resulting state.
- **FR-019**: Under identical external conditions, advancing by a duration in one interval or equivalent subintervals MUST preserve the same resource outcome, active/blocking state and partial progress. Invalid elapsed inputs MUST fail without corrupting state.
- **FR-020**: An interval crossing multiple cycle completions MUST respect input/output limits at each completion. Once blocked, later time in that interval MUST NOT create extra cycles or future production credit.
- **FR-021**: Each significant state value MUST have an identifiable owner and controlled changes; selection, inspection and presentation MUST NOT become alternative sources of gameplay truth.
- **FR-022**: On the next valid return after suspension or closure, the feature MUST determine the elapsed offline interval, credit at most eight hours of that interval, and apply an initial configurable efficiency of 80% to derive effective production time. The maximum effective production time per return is therefore 6.4 hours. Changing the efficiency or cap requires an explicit balancing decision.

#### Persistence and development validation

- **FR-023**: Users MUST be able to explicitly save foundational state and restore it in a later session. A successful save MUST represent a coherent state rather than a partly applied production or placement operation.
- **FR-024**: Restoration MUST preserve enough data for equivalent play and continuation: factory bounds/occupancy, identities, type/process/resource references, positions, applicable orientation, active state, storage quantities/capacities and cycle progress, whether encoded directly or reconstructed from unchanged definitions.
- **FR-025**: Saved data MUST identify its format version. This feature MUST provide the initial format and explicitly reject unsupported versions; implementing older-format migrations is outside this feature.
- **FR-026**: Loading MUST validate supported version, complete required state, identities, references, footprints, resource limits and process progress before replacing the current valid factory. Invalid input MUST NOT be silently repaired or partly applied.
- **FR-027**: Save/load outcomes, missing saves and failures MUST be reported explicitly. Failed save/load MUST preserve the current valid factory; a failed save MUST preserve the previous successful save.
- **FR-028**: The playable development scenario MUST demonstrate placement, supply, production, capacity blocking, output removal, resumption, inspection, saving, closing/resetting the scenario and equivalent restoration.
- **FR-029**: Foundation behavior MUST be verifiable without final visuals. Validation MUST cover resource accounting, complete-footprint placement, partial-cycle interruptions, time partitioning, independent instances, failure paths and save/load continuation.
- **FR-030**: Development definitions and scenario instructions MUST identify all temporary dimensions, footprints, quantities, capacities, durations and controls as replaceable validation choices, never final balancing or game-design decisions.
- **FR-031**: The foundation MUST allow a different development area size and multi-cell footprint without redefining the underlying world concepts. It MUST NOT establish a maximum future factory size or require speculative large-factory infrastructure.
- **FR-032**: Offline recalculation MUST advance eligible production by the effective production time using the same cycle-completion, input-consumption, output-capacity, blocking and residual-progress rules as controlled active simulation.
- **FR-033**: Inactive machines MUST receive no offline production. Machines that become blocked during recalculation MUST receive no credit for the remainder of the blocked interval and MUST NOT accumulate a production backlog.
- **FR-034**: A successfully processed offline interval MUST be accounted for exactly once. Reopening, reloading or retrying from the resulting state MUST NOT duplicate its production.
- **FR-035**: Offline duration MUST be derived from local UTC device time. A missing, negative or otherwise invalid interval MUST grant no offline production, preserve the valid saved/running state and produce explicit feedback. A positive interval MUST be capped at eight credited hours. This local foundation requires no additional clock-manipulation detection or online time source.
- **FR-036**: After a valid return, the player MUST receive understandable feedback about the measured offline duration, effective production time and resource result, including whether production was limited by inactivity, missing input or output capacity. A runtime rotation or other configuration change that the player does not leave is not such a return: it MUST NOT show this feedback.
- **FR-037**: A manual save and every reported transition to suspension/background or regular shutdown MUST attempt to store a coherent lifecycle checkpoint containing foundational state and its local UTC departure time. Offline duration MUST start from the checkpoint that is actually restored. A failed attempt MUST preserve the previous successful checkpoint; abrupt termination without a reported lifecycle event MAY fall back to that prior checkpoint.

### Key Entities *(include if feature involves data)*

- **Factory Area**: One logical development world with declared cell availability, occupancy and placed building instances.
- **Building Type**: Stable definition identity, required footprint, optional orientation rules and applicable production definition.
- **Placed Building**: Persistent instance identity, type, location, active state and independently owned storage/production state.
- **Resource Definition**: Stable identity and development label for a material, without an implied final catalogue or economic value.
- **Resource Storage**: Quantities and declared finite capacity constraints, associated with explicit owners and controllable additions/removals.
- **Production Definition**: Required resource quantities, positive duration and complete output quantities shared by compatible instances.
- **Production State**: Current cycle progress, active state and currently unmet input/output conditions; resource exchange occurs only on completion.
- **Save Snapshot / Lifecycle Checkpoint**: Versioned, coherent foundational state plus its local UTC departure time, produced by manual save or a reported suspension/background/regular-shutdown transition and validated before equivalent continuation.
- **Offline Interval**: The elapsed absence associated with one valid saved state and return, consumed exactly once and converted to effective production time by the approved efficiency and cap rules.

## Success Criteria *(mandatory)*

### Measurable Outcomes

These are acceptance targets, not claims that the feature has been implemented.

- **SC-001**: All valid and invalid placement cases in US1 pass, including complete multi-cell occupancy and two distinct instances; rejected actions produce zero unintended changes.
- **SC-002**: D001 produces exactly one output and consumes exactly two inputs after five productive seconds; the 2.5-second checkpoint changes only progress. A second productive cycle after output removal yields exactly the same exchange.
- **SC-003**: For missing-input, full-output and inactive cases, resource quantities and progress remain unchanged over one hour, 24 hours and seven days of explicitly advanced stopped time. Resolving the condition produces no catch-up bonus.
- **SC-004**: Immediate save/restore comparisons with zero measured offline duration preserve all authoritative state for ready, partially completed, blocked and inactive cases. For a nonzero valid offline duration, the restored result matches the saved snapshot advanced by exactly the approved effective production time; subsequent equivalent active advancement remains equivalent.
- **SC-005**: A reviewer completes the documented navigation → placement → inspection → save → restore path in both a declared touch setup and a declared desktop pointer setup with zero required desktop-only actions on the touch path.
- **SC-006**: All documented time-partition and presentation-recreation comparisons yield equivalent resources, occupancy, state and partial progress, including a partial cycle crossing a save/load boundary.
- **SC-007**: Every invalid storage, placement and save input in the acceptance matrix is rejected without a partial state change; missing-save and simulated save-failure scenarios preserve valid state and provide explicit feedback.
- **SC-008**: Every numeric content value and visual used solely for D001 is labelled as development-only, and the scenario is demonstrable without any system listed as out of scope.
- **SC-009**: For controlled offline intervals, the credited interval is the valid measured duration capped at eight hours, and effective production time is 80% of that credited interval. Eight or more valid offline hours therefore equal 6.4 effective production hours. Resulting resources, blocking and residual progress match an equivalent controlled simulation advanced by exactly that effective time.
- **SC-010**: Offline tests pass for ready, partially completed, missing-input, full-output and inactive machines, including lifecycle-checkpoint success/failure, exactly-once return handling and invalid/backward time; no case creates duplicate output, capacity overflow, negative resources or partial checkpoint/state replacement.

## Assumptions

- **A-001 — Validation authority**: The owner explicitly permits temporary values solely for this development scenario. They are not product balancing or permanent technical limits. Changing them later requires updating the corresponding acceptance fixture, not pretending they were final gameplay decisions.
- **A-002 — D001 fixture**: One 16 × 16 development area; one reusable 2 × 2 test-building type with a fixed development orientation; two stable test resource identities; four input units initially; input capacity four; output capacity one; a process consuming two input units to yield one output unit in five simulation seconds. Distinct input and output storage make capacity behavior explicit without specifying a universal stacking rule. The explicitly named multi-cycle variant changes only output capacity to two (US4.5); it is also non-canonical development content.
- **A-003 — Fixture boundaries**: Placement in D001 has no construction cost; supply, output removal and activation are explicit developer operations. This introduces no final inventory, refund, building-removal or player-resource-transfer mechanic. Multiple placements may be used to verify independent identities; no production network is implied.
- **A-004 — Input and visuals**: Minimal placeholder visuals and temporary documented touch/pointer controls are sufficient. Final gestures, final placement UX, rotation controls, art style, animations and camera presentation are not decided here. Navigation and placement must not accidentally trigger each other.
- **A-005 — Save and offline scope**: Persistence is local, with an explicit save/restore path and automatic lifecycle checkpoints on reported suspension/background/regular-shutdown transitions. Periodic autosave, slot management, cloud synchronization and legacy migrations are not introduced. Valid return applies reduced offline production; complex offline logistics networks, passive production while the process is running in the background, and final economy balancing remain outside this feature.
- **A-006 — Planning dependencies**: Declare the development device/screen/input setup, numerical representation/precision policy, measurable performance workload and budgets during planning. Do not infer shipping platform support or a machine-count/FPS promise from this specification.
- **A-007 — Project constraints**: The active constitution and AGENTS.md remain binding. The existing engine/toolchain is a planning constraint; this specification does not prescribe new classes, libraries, tick frequencies, storage encodings or rendering strategies. Project test commands and assemblies must be established and verified before gameplay implementation.

## Clarifications

### Session 2026-09-13

- **Q: When are cycle inputs consumed, and what happens on interruption?** → **A:** Owner selected material consumption at the end. Progress runs only while all inputs and space for the complete result are available. Losing a condition pauses progress. Inputs and outputs are booked together on completion. Deactivation also preserves progress; stopped time grants no later production bonus. This is the approved rule for Core Game Foundation (FR-011–013), not a choice left to an implementer.
- **Q: What happens when the app is suspended, the screen is locked, or the player returns after closing it?** → **A:** On the next return, measure the elapsed offline interval and recalculate production and resources at a disadvantage relative to active play. Blocking, capacity and atomic resource rules still apply. The owner approved reduced offline production; the following clarification entries resolve its exact efficiency, cap, time source and checkpoint boundary.
- **Q: Is the 80% offline efficiency a game rule or only a temporary validation value?** → **A:** It is the initial, configurable game rule. Valid credited offline time counts as 80% effective production time; changing the value later requires an explicit balancing decision.
- **Q: What is the maximum offline interval credited on one return?** → **A:** Credit at most eight hours. With 80% efficiency this produces at most 6.4 effective production hours per return; additional absence time grants no production credit or backlog.
- **Q: Which time source and clock-manipulation policy governs offline production?** → **A:** Use local UTC device time. A negative or invalid difference grants zero offline production; a positive difference is capped at eight credited hours and processed exactly once. This foundation adds no further manipulation detection or trusted online-time dependency.
- **Q: Which persisted state and departure time start the offline interval?** → **A:** Manual save and every reported suspension/background or regular-shutdown transition attempt to persist a coherent lifecycle checkpoint with local UTC time. The next return uses the last successfully written checkpoint. Failure preserves the previous checkpoint; no periodic autosave is added.

### Session 2026-09-24

- **Q: Does “loading” in US2.8 require checkpoint-load inspection refresh before the persistence UI exists?** → **A:** T021 verifies live inspection refresh after production, supply/removal, and activation changes. Checkpoint-load inspection refresh is deferred to T028, after persistence UI and load behavior are available; T028 must verify the loaded snapshot and resolve or clear stale selection.
- **Q: Does a cold-start `ResumeAndReconcile` that selects the active checkpoint count as fallback?** → **A:** No. Selecting the valid active generation on cold start is the normal return source and reports `UsedFallback=false`. `UsedFallback=true` identifies the in-process FR-037 case where a failed suspension checkpoint requires the last successfully written active checkpoint; if no successful checkpoint exists, the prepared live factory is retained at zero credit and fallback remains false.

### Session 2026-09-27

- **Q: Should the Durable offline return summary appear after a return caused only by a runtime rotation?** → **A:** No (owner decision). On Android a rotation briefly removes window focus, which records an FR-037 departure checkpoint and a sub-second return reconciliation (T058). The player never left the game, so no Durable offline return feedback is shown for it. Genuine suspend/lock/background returns and cold starts keep the FR-036 feedback. The checkpoint and exactly-once accounting rules are not changed by this decision.

## Scope boundaries

Included: one navigable development factory, complete-footprint placement and identities, resource/storage concepts, one test process with finite storage and inspectable blocking, explicit elapsed-time simulation, local versioned save/restore, reduced deterministic offline recalculation on return and independent behavioral validation.

Excluded: final art and audio; final resource/machine catalogues, production ratios, capacities, early-game balancing or progression; construction removal/rebuild/refund policy and final placement UX; complete conveyors, splitters, mergers or inter-region logistics; electricity; research; deposits and terrain gameplay; multiple regions and expansion rules; expeditions and roguelite mechanics; prestige; premium currency and monetization; achievements, events and leaderboards; cloud saves, online accounts, multiplayer and analytics; complex offline production networks and passive background execution.

Future-facing requirements preserve the current concepts and their boundaries; they do not authorize implementations of excluded systems.

## Draft coverage

| Original draft | Specification coverage |
|---|---|
| Purpose, Scope, Quality Requirements, Success Definition | Feature introduction, FR-016/021/028–037, SC-001–010, Assumptions, Scope boundaries |
| Factory World, Building Placement | US1, FR-001–003/031 |
| Resources, Resource Storage | US2, FR-007–010 |
| Production, Production Blocking | US2, FR-009–014/020, approved Clarifications |
| Time-Based Simulation, Deterministic Behavior | US4, FR-017–020, SC-003/006 |
| Logistics Preparation, Offline Progression Preparation | FR-015/016/022/032–037, US3.3/7–9, SC-009/010, Scope boundaries |
| Save and Restore | US3, FR-023–027/034–037, SC-004/007/009/010 |
| Visual Representation, Factory Navigation, Mobile-First Interaction | US1/4, FR-004–006/021, SC-005/006 |
| Factory State Inspection, Validation Scenario | US2.8, FR-014/028–030, D001 |
| Acceptance Scenarios 1–2 / 3–6 / 7 / 8 / 9 | US1.3–4 / US2.1–5 / US4.1–2 / US3.1–3 / US1.1 |
| Explicitly Out of Scope, Human-in-the-Loop Requirements | Scope boundaries, A-001–007, Clarifications |

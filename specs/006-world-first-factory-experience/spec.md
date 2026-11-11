# Feature Specification: World-First Factory Experience

**Feature Branch**: `feat/006-world-first-factory-experience`

**Created**: 2026-11-11

**Status**: Draft

**Input**: User description: "Feature 006 – World-First Factory Experience: make the factory the central visual and interactive experience of the game; the interface exists to help the player understand and control it." Full input: [`Spec-006-Draft.md`](../../Spec-006-Draft.md). It is supplemented by the discovery and feasibility reviews of 2026-11-11 in [`reviews/discovery.md`](reviews/discovery.md): Game Designer/Product Owner, UI/UX, Art Director, and Software Architect with the Technical Art and Performance lenses.

## Context and authority

Features 003 and 005 deliver a complete Iron factory, contracts and prestige loop on a fixed 12-slot grid. Feature 004 (complete on `main` at `1dab60d`: 77/77 tasks, owner acceptance T447, convergence T449) restyles that factory in 2.5D isometric 2:1 pixel art at D24 density with integer camera scaling, URP 2D, Art Bible `F004-PAB v1.0.4`, shape-coded state badges (S3 = variant b at 23 px), lighting B, a shared animation clock, and a reduced-motion preference.

Feature 006 changes **how the factory is presented and operated**, not how it plays. Production, economy, content data, unlock rules and saved games are unchanged. F006 builds on F004 and does not redo its style selection, render migration or accepted assets without a concrete, recorded justification.

### Current player experience (repository evidence)

The evidence comes from the F004 final captures `specs/004-visual-identity/reviews/final-test-evidence/e01`, `e02`, `e05`, `e07` and `e08`; `UI/FactoryRun.uxml`; and the F004 final-acceptance findings.

1. **The factory floats in an empty field.** In portrait (e01) the 12-slot lattice fills roughly a third of the screen height on a plain dark background with scattered dashes. The Art Bible states "no background illustration in v1.0". Nothing communicates a place.
2. **Problems are shown, causes are not.** In e01, nine identical red badges say that machines are stopped, but not why or where the root cause is. Badges overlap the next row's plate, so they read as belonging to the wrong machine (F004 A-03). Every machine draws from one shared store, so the upstream cause (for example, too few Miners for the Smelters) is never visible in the world.
3. **Machine management is a text wall.** The machine sheet (e05) covers about 60 % of the portrait height. It stacks status, two rate lines, a batch explanation, a progress bar, an upgrade line, and three equal-weight buttons (Upgrade, Switch off, Remove). The selected machine is pushed to the edge of the remaining strip.
4. **Permanent chrome competes with the world.** The top strip (Cash, RP, Store bar, Overdrive, Settings), a six-tab bottom bar (Factory, Market, Contracts, Items, Research, Prestige), the zoom buttons and the Mine card are always visible. Industry exists only as a section below the Research graph (Play-Tester batch C, finding C-F1).
5. **Routine flows cost avoidable steps.** Baseline counts from the UI/UX review, to be re-measured at the start of the feature (FR-046):
   - Upgrade: select, Upgrade, then Close to see the factory again (3 interactions).
   - Find which machines are stopped and why: one tap per machine.
   - Open a system and return: tab, act, then the Factory tab.
6. **Nothing shows growth.** An early and an established factory look alike apart from which plates hold machines. The lattice is visually fixed at 12 slots, which presents the vertical slice as the final building model.

### What exists and is reused

The F006 plan must reuse, not duplicate, the following:

- **Authoritative read models.** These cover per-machine phase, blocker, cycle progress and batch factor; per-machine input and output rates; per-resource production, consumption and supply coverage; blocker cause with corrective actions; upgrade preview with affordability and the next rates; slot unlock level; and factory level.
- **F004 state cues.** Six visual states map from the existing badge precedence; no second precedence rule is allowed. The F004 assets include machine families, plates, floor tiles, markers, icons and the shared animation clock with reduced motion. The F004 camera includes integer scales, fit-all, panel-aware framing and a single camera writer. Responsive shell rules carry over: screen classes, the 44 pt / 7 mm touch raise, 100–200 % text scale, the confirmation dialog, the exact-value line and rotation cancel.

### Gaps relevant to this feature

Feasibility review findings:

- No exact per-machine "cycle completed" signal exists.
- "Upgrade available" and "bottleneck" are derivable but not modeled as presentation states.
- The simulation has **no machine-to-machine flow topology**: production uses one global store.
- The 12-slot assumption lives only in presentation code. The domain slot count already comes from authored content.

### Wiki recovery

`/speckit-wiki-query` (2026-11-11) returned **partial** coverage.

- **Covered:** the run shell, the six-tab layout and sheet sizing ([run-shell-and-panels](../../wiki/pages/run-shell-and-panels.md), [run-panels](../../wiki/pages/run-panels.md)); the device profile and text scale ([run-shell-device-profile-and-text-scale](../../wiki/pages/run-shell-device-profile-and-text-scale.md)); integer camera scaling ([pixel-camera-scale](../../wiki/pages/pixel-camera-scale.md)); the S3/S4 cue and lighting decisions ([f004-s3-s4-cue-and-lighting-decision](../../wiki/pages/f004-s3-s4-cue-and-lighting-decision.md)); the US8 polish findings ([f004-us8-polish-review](../../wiki/pages/f004-us8-polish-review.md)); the presentation boundary ([unity-presentation-boundary](../../wiki/pages/unity-presentation-boundary.md)); and visual identity ([visual-identity-art-direction](../../wiki/pages/visual-identity-art-direction.md)).
- **Coverage gaps:**
  - The future free-building vision (`Core-Idea.md` has not been ingested).
  - Environment and floor art beyond F004's floor family.
  - The final animation-clock and effect rules after F004 batch 10–12.
  - The F004 final-acceptance minor findings.

  These are taken directly from `Core-Idea.md`, `docs/art/F004-art-bible-v1.md` and `specs/004-visual-identity/reviews/final-acceptance.md`. The plan phase should ingest them.

### Current functionality versus future vision

| Area | Exists today (F006 presents it) | Future vision (separate gameplay features, out of F006 scope) |
|---|---|---|
| Building | Fixed slots unlocked by factory level, one machine per slot | Free placement on a 2D grid, rotation, undo |
| Flow | One global store; machines draw and deliver to it | Conveyors, storage, splitters, mergers, physical connections |
| Bottlenecks | Per-machine blockers and per-resource supply coverage | Spatial bottlenecks such as belt throughput and routing mistakes |
| World | One factory | Regions, inter-region logistics |
| Systems | Market, Contracts, Items, Research with Industry, Overdrive, Prestige | Power, industrial projects, expeditions |

## Clarifications

### Session 2026-11-11

- Q: What replaces the permanently visible six-tab bar (FR-020)? → A: A single hub entry on the factory screen that opens a sheet (phone portrait) or side panel (larger classes) of cards for every system, each with an attention indicator. The entry MUST NOT be named "Systems". It MAY be icon-only, provided it has an accessible name (screen-reader label, tooltip on pointer devices) and meets the 44 pt target rule. Its final name or icon is chosen in the visual-direction gate (FR-040).
- Q: How does the world show connections, given one shared store and no machine-to-machine flow (FR-013)? → A: Every machine is visibly connected to a central store or depot, which is truthful to the current simulation. There are no decorative conveyors or pipes between machines.
- Q: Which existing data defines the factory development stages (FR-036)? → A: Factory level (the unlocked building locations) is the primary driver. Machine levels and the number of active machines add secondary visual density. The presentation resets with each prestige run, because these values reset; prestige count is not used.
- Q: What must the owner see before approving the visual direction (FR-040/041)? → A: Rendered mockups only. No separate in-engine prototype is built before implementation.
- Q: Besides Cash, store fill, the hub entry and attention indicators, which controls stay permanently on the factory screen? → A: Manual mining and Overdrive, both as compact controls. Zoom buttons are no longer permanently shown on touch profiles, and an accessible non-gesture zoom path remains (FR-008, FR-008a, FR-010).
- Q: How binding are the proposed thresholds in SC-001, SC-002, SC-003, SC-004, SC-006 and SC-010? → A: They are guidance only. They steer design and are measured and reported, but only the owner's visual acceptance and the binding criteria gate the feature.
- Q: Where do the final visual acceptance and the timed trials happen? → A: The emulator and Windows player captures, as in F004. A physical phone is optional and is recorded as unverified in the acceptance record (A-9).
- Q: How should F006 handle the risk that the UX rebuild and the new environment and effect art stall each other? → A: Keep everything in one feature and put the art first. Environment and effect art is produced and integrated before the UX batches, so the UX is built on the final look (A-11).

### Session 2026-11-11 (plan review)

- Q: How should the hub entry look on touch, where no tooltip is available? → A: An icon plus a short visible text label, readable at 200 % text. The wording is chosen at the direction gate and is not "Systems" (FR-020).
- Q: How tall may the machine card be in phone portrait at large text sizes? → A: At most 30 % of the safe height at 100–140 % text. At larger sizes it may grow to about 45 %, so that the name, state and Upgrade stay visible without scrolling (FR-016).
- Q: What setting document and identity are fixed before the direction mockups? → A: A minimal World Bible v0.1 that describes a neutral starter industrial platform on a dark base. It gives the site name, mood and the naming and copy rules. Ground art is swappable for future regions. It is frozen before the mockup briefs (FR-047).
- Q: Should the factory world show an ore vein for manual mining? → A: Yes. A visible ore-vein marker shows activity while manual mining is in progress. In F006 it is not an input; the screen control remains the input (FR-010a).

Further decisions with a recorded default are listed under Assumptions. `/speckit-clarify` may revisit any of them.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — The factory is the screen (Priority: P1)

A player opens the game and immediately sees their factory as the dominant element of the screen. Essential information (Cash, store fill and anything that needs attention) is visible in a compact form. Everything else is one deliberate step away, not permanently on screen.

**Why this priority**: This is the feature's core promise ("the factory is the game"). Every other story depends on the screen hierarchy it establishes.

**Independent Test**: Capture the factory screen with no panel open on each supported screen class. Measure the share of the usable screen given to the factory world and to persistent chrome. Confirm that every routine action and every progression system is still reachable from this screen.

**Acceptance Scenarios**:

1. **Given** a running factory on a phone in portrait, **When** no panel is open, **Then** the factory world occupies the largest share of the screen and persistent interface elements aim for the SC-001 guidance budget.
2. **Given** the same factory in phone landscape and on a tablet, **When** no panel is open, **Then** the same hierarchy holds and nothing essential is cut off.
3. **Given** any persistent element removed from the always-visible layer, **When** the player needs it, **Then** it is reachable in at most the interaction count in SC-003, and its availability is signalled (for example a badge when something is ready).
4. **Given** text scale at 200 %, **When** the factory screen is shown, **Then** the persistent layer remains readable without clipping and does not grow into the world beyond the SC-001 200 % guidance.

---

### User Story 2 — A convincing industrial place (Priority: P1)

The factory sits in a coherent industrial environment: ground, foundations, edges and restrained decoration that make it read as one facility. Active production areas are visually distinct from unused or locked space.

**Why this priority**: Without a place, US1 only enlarges disconnected sprites. The owner names the missing sense of place as a primary problem.

**Independent Test**: Compare before/after captures of the same factory state on each screen class. Re-run the F004 readability trials on the new backdrop to confirm that state cues lost no legibility. Have the owner accept the environment per screen class.

**Acceptance Scenarios**:

1. **Given** the factory screen, **When** the player looks at it, **Then** machines stand on visible ground that forms one continuous facility, not isolated tiles on an empty field.
2. **Given** occupied, empty-unlocked and locked building locations, **When** shown side by side, **Then** each is distinguishable by shape and value, not only by colour.
3. **Given** the new environment, **When** the F004 blind-naming and timed cue trials are repeated, **Then** results do not regress (SC-005).
4. **Given** reduced motion is on, **When** the environment is shown, **Then** no decorative element moves.
5. **Given** any decoration, **When** it overlaps a state cue, selection frame, marker or hit area, **Then** the gameplay element draws above it and stays tappable.

---

### User Story 3 — Inspect and act on a machine in place (Priority: P1)

The player taps a machine. It is clearly selected, a compact contextual card shows what it is doing and offers the common actions, and the selected machine stays visible. Details are one step further. Closing returns the player to the factory exactly as they left it.

**Why this priority**: Machine management is the most frequent interaction. Today it hides the factory behind a text wall.

**Independent Test**: Run a scripted journey on each screen class: select a machine, upgrade it, switch it off and on, open details, and close. Count the interactions, verify that the machine stays visible, and verify that the camera returns.

**Acceptance Scenarios**:

1. **Given** the factory screen, **When** the player taps a machine, **Then** it gets clear selection feedback and a compact card shows: name and level, the current state in words, what is limiting it if anything, and the primary action (Upgrade, with cost and affordability).
2. **Given** the card is open, **When** the player looks at the world, **Then** the selected machine is fully visible and not covered by the card or any other element (SC-002).
3. **Given** the card, **When** the player taps Upgrade with enough Cash, **Then** the upgrade happens through the existing rules, the machine responds visually, and the card updates in place without closing.
4. **Given** the card, **When** the player chooses to expand it, **Then** the details are shown in a larger view, including:
   - input and output rates;
   - cycle progress;
   - the batch note;
   - every blocker with its corrective action;
   - switch on/off;
   - removal;
   - relevant Items or boosts already offered by existing systems.
5. **Given** any open card or detail view, **When** the player taps empty world, presses Back or Escape, or taps another machine, **Then** the view closes or switches selection, and the camera returns to the player's previous view unless the player moved it.
6. **Given** the player chooses Remove, **When** they confirm, **Then** the existing removal and refund rules apply. Removal is never reachable in a single tap from the compact card and always needs confirmation.
7. **Given** an empty unlocked location, **When** the player taps it, **Then** the build flow (US5 scenario 1) starts in context rather than in a full-screen view.

---

### User Story 4 — Read the factory's health at a glance (Priority: P1)

Without opening anything, the player can see which machines are working, which are stopped, and what kind of problem each has: missing input, full output, waiting, paused or switched off. The player can also see where an upgrade is affordable and where the most important bottleneck lies. Running machines visibly work.

**Why this priority**: Understanding production visually, rather than reading statistics, is the second core promise of the brief and of `Core-Idea.md` ("transparent production systems", "identify and solve bottlenecks").

**Independent Test**: Run timed identification trials on captures of seeded factory states with mixed problems. Use blind cue naming under grayscale and colour-vision filters. Compare a live capture with a reduced-motion capture to confirm that no information is lost.

**Acceptance Scenarios**:

1. **Given** a factory with machines in each state, **When** the player looks at it for at most the trial time, **Then** they can say which machines are stopped and which problem category each has (SC-004).
2. **Given** several machines stopped by the same missing resource, **When** the factory is shown, **Then** the presentation conveys the shared cause and its resource. Each machine does not appear as an unrelated alarm.
3. **Given** a machine that is running, **When** shown, **Then** it visibly operates in step with the authoritative simulation and carries no alarm cue. **When** a cycle completes, **Then** a restrained completion signal appears that never shows more output than was produced.
4. **Given** an affordable upgrade, **When** shown, **Then** an unobtrusive "upgrade available" indication appears that does not compete with problem cues.
5. **Given** reduced motion is on, **When** the factory is shown, **Then** every state remains distinguishable without animation.
6. **Given** any state indication, **When** viewed in grayscale or under the three colour-vision filters, **Then** it is distinguishable by shape.
7. **Given** no machine has a problem, **When** the factory is shown, **Then** no warning cue appears anywhere.

---

### User Story 5 — Building and progress feel rewarding (Priority: P2)

Building, upgrading and reaching milestones produce restrained, satisfying feedback. The building flow shows where a machine can go, previews it, and makes insufficient resources and cancellation obvious. An established factory looks clearly more developed than a fresh one, using only progress the game already tracks.

**Why this priority**: The owner wants progression to be seen. US1–US4 already deliver the readable, operable factory; this story adds reward and growth on top.

**Independent Test**: Run the building journey with enough and with too little Cash, then cancel, on each screen class. A blind reviewer orders shuffled captures of factories at different development stages. Inspect upgrade and build feedback with reduced motion on and off.

**Acceptance Scenarios**:

1. **Given** the player starts building, **When** they choose a machine type, **Then**:
   - available locations are highlighted and occupied or locked ones are clearly not;
   - a preview shows the machine at the chosen location;
   - the cost is shown with affordability;
   - confirm and cancel are equally easy to reach.
2. **Given** insufficient Cash, **When** the player tries to build or upgrade, **Then** the action is clearly unavailable, the shortfall is stated, and nothing changes.
3. **Given** a successful build or upgrade, **When** it completes, **Then** the machine gives an immediate, restrained visual response. Reduced motion replaces any motion with a static change.
4. **Given** factories at the development stages defined in FR-036, **When** captures are shown in shuffled order, **Then** a reviewer can order them by development (SC-006).
5. **Given** a factory-level or prestige milestone that the game already records, **When** it is reached, **Then** a milestone presentation acknowledges it without blocking play and without inventing a reward.

---

### User Story 6 — All game systems in one coherent, reachable structure (Priority: P1)

Market, Contracts, Items, Research, Industry, Overdrive, Prestige, Settings, the offline summary, notices, errors and confirmations all look and behave like parts of one game. Each is reachable quickly from the factory, and the way back to the factory is always obvious.

**Why this priority**: The owner drafted P2. The discovery wave raised it to P1, because US1 cannot meet its chrome budget while six permanent tabs remain, and because progression systems must not become harder to find (owner constraint; Play-Tester finding C-F1). The owner can reverse this in clarification.

**Independent Test**: From the factory screen, reach every system, act, and return, on each screen class, using touch, pointer and keyboard. Count the interactions. Capture each surface for a consistency review against the Art Bible and the panel conventions.

**Acceptance Scenarios**:

1. **Given** the factory screen, **When** the player wants any system, **Then** the navigation structure in FR-020 shows it within the interaction count of SC-003. **When** something in that system needs attention (a claimable contract, available research, prestige ready), **Then** the entry point shows it.
2. **Given** any system view, **When** the player presses Back or Escape, uses the close control, or taps the visible factory, **Then** they return to the factory in at most one interaction.
3. **Given** phones in portrait, **When** a system or detail view opens, **Then** it uses a contextual sheet. On phone landscape and larger screens, a side panel keeps the factory visible.
4. **Given** every surface, **When** compared, **Then** headers, primary and secondary actions, spacing, icons, disabled, empty, loading and error states follow one shared visual system consistent with the Art Bible.
5. **Given** Industry, **When** the player looks for it, **Then** it is discoverable from the navigation structure, not only below the Research graph.
6. **Given** an offline return that the existing rules say to summarize, **When** the game resumes, **Then** the summary shows in the shared style, states what happened (including stopped production), and closes into the factory.
7. **Given** a destructive or irreversible action, **When** it is requested, **Then** the shared confirmation behaviour applies: focus starts on Cancel, Back cancels, and rotation cancels with a notice.

---

### User Story 7 — A world presentation that is not bound to 12 slots (Priority: P2)

Everything this feature adds keeps working when the factory has a different number or arrangement of building locations. This covers layout, environment, framing, selection, hit-testing, building previews and state cues. The presentation therefore does not present the 12-slot vertical slice as the final model and does not block the future free-building features.

**Why this priority**: The owner requires that the UX not assume a permanent 12-slot factory. The PO lens notes that the player sees no direct value. The story is kept because its outcome is observable and testable, and P2 reflects that.

**Independent Test**: In a test configuration (not shipped content), load layouts with fewer and more building locations than 12 and with a non-rectangular arrangement. Verify layout, framing, selection, hit-testing, building previews and cues without code changes.

**Acceptance Scenarios**:

1. **Given** a test layout with N ≠ 12 locations, **When** the factory screen opens, **Then** the world, environment and fit-all framing adapt, and every location is selectable and correctly cued.
2. **Given** a layout whose content exceeds one screen at the readable scale, **When** the player pans and zooms, **Then** world boundaries, framing and selection focus behave correctly.
3. **Given** a machine's identity, **When** its location's on-screen position changes between layouts, **Then** its selection, state, and contextual card are unaffected. Identity is not derived from screen position.

---

### User Story 8 — Owner-approved direction and owner-accepted result (Priority: P1)

The owner approves a concrete design direction from rendered alternatives before significant implementation. The owner then accepts the shipped result from real builds across representative devices and interaction states.

**Why this priority**: Visual quality is a primary acceptance criterion and is subjective. Automated tests cannot judge it, so the owner is the final authority.

**Independent Test**: Review the recorded direction decision with its alternative sheets. Review the final before/after acceptance record with per-surface verdicts from real builds.

**Acceptance Scenarios**:

1. **Given** the current build, **When** the design phase starts, **Then** current-state screenshots per screen class and the ranked problem list are recorded first.
2. **Given** that baseline, **When** alternatives are presented, **Then** there are at least two concrete directions, each with mockups for phone portrait, phone landscape and tablet. Each set shows an active factory with several machines, a contextual machine interaction and at least one progression or system screen. Each set is rendered in the Art Bible palette at real pixel scale (no placeholder rectangles) and compared against `Core-Idea.md` and this spec, with a recommendation.
3. **Given** the owner's decision, **When** it is recorded, **Then** implementation of significantly changed interaction or visuals starts only after it. Changes to an approved direction require a new recorded decision.
4. **Given** the implemented feature, **When** final acceptance runs, **Then** before/after sheets from real builds cover each screen class, the key interaction states (no panel, card open, details open, building, system open, offline summary, reduced motion, 200 % text) and the progression stages. The owner records an accept or reject verdict per surface.

### Edge Cases

- **A selected machine is removed, replaced, or changes state while its card is open** (for example, it becomes blocked or the player cannot afford the upgrade any more). The card updates in place or closes with a notice. A stale action never executes.
- **The world is replaced while a selection is pending** (load, prestige, recovery). The selection and any pending action are cancelled, and the camera resets to fit-all. Presentation-only state is never saved.
- **Rotation or a screen-class change while a card, detail view or build preview is open.** Pending confirmations cancel with the existing notice. The selection stays when its target still exists. The camera re-frames to keep it visible at an allowed integer scale.
- **The minimum readable integer scale cannot fit the factory plus an open card on a small screen.** Readability wins: the card or sheet takes priority over fit, and the player can pan. Shrinking below the readable floor is not allowed.
- **Many simultaneous completions or state changes**, such as an offline return or batch completions. Feedback is capped and coalesced, with no burst of effects, and never shows more output than the simulation produced.
- **Every machine is stopped by the same cause.** The presentation surfaces one shared cause rather than many identical alarms (US4 scenario 2).
- **No machines yet (first run).** The world still reads as a place. The first-build hint points into the world at a buildable location, and the manual-mining control stays reachable.
- **All locations are locked except the occupied ones.** Locked locations show their unlock condition from existing data without numbers that crowd the world.
- **Text scale at 200 % on the smallest supported screen** with the card open. The card's essential lines (state and primary action) stay visible without scrolling. Details scroll.
- **Reduced motion is toggled while effects play.** Effects stop at once, and the static state is shown.
- **Pointer devices.** Hover never replaces a tap-reachable path. Keyboard focus can reach every machine and every action in a defined order.
- **A missing environment or effect sprite.** The world falls back as defined by the F004 fallback rules, with no start-up failure for decorative art.
- **A save from before F006.** It loads unchanged. No F006 element writes to the save.

## Requirements *(mandatory)*

### Functional Requirements

**Invariants (all stories)**

- **FR-001**: The feature MUST NOT change production, economy, content data, unlock rules, offline progress, prestige rules or the save format. Every value shown MUST come from existing authoritative state or be derived from it without changing it.
- **FR-002**: Every visual indication of machine activity, completion, problem or progress MUST reflect authoritative simulation state. The presentation MUST NOT show production, flow or output that the simulation did not produce, and MUST NOT simulate individual resource items for decoration.
- **FR-003**: Presentation-only state (selection, card and panel state, camera, previews, effect state) MUST NOT be persisted in the save. Any new player preference MUST use the existing local display-preferences store.
- **FR-004**: The feature MUST keep F004's projection, art density, integer pixel scaling, readability floors, palette cap and state-cue model. It MUST extend the Art Bible only by additive, versioned changes approved by the Art Director and the owner. Any change that would require a new major Art Bible version needs a separate owner decision.
- **FR-005**: The feature MUST support every existing screen class (phone portrait, phone landscape, tablet, desktop), touch and pointer input, keyboard focus and activation, 100–200 % text scale, 44 pt / 7 mm touch targets, and the reduced-motion preference. These are acceptance concerns, not polish.
- **FR-006**: Recurring presentation updates MUST NOT allocate managed memory in steady state. New visuals MUST stay within the F004 frozen performance and asset budgets, or a recorded owner decision MUST change a budget.

**Factory-first screen (US1)**

- **FR-007**: With no panel open, the factory world MUST be the dominant screen element on every screen class, and persistent interface elements SHOULD stay within the SC-001 guidance budget. Dominance is judged in the owner's acceptance.
- **FR-008**: The persistent layer MUST show Cash, the store fill level, the hub entry (FR-020), the attention indicators, the manual-mining control and the Overdrive control (clarified 2026-11-11). Overdrive MAY collapse to a compact icon that visibly changes when Overdrive is ready. Every other system or value MUST be reachable on demand (FR-020) and MUST signal when it needs attention.
- **FR-008a**: On touch profiles, zoom buttons MUST NOT be permanently visible. Pinch is the default there. A non-gesture zoom path MUST remain available for accessibility, for example an option that shows the zoom buttons. On pointer profiles, wheel zoom and visible zoom controls remain.
- **FR-009**: Persistent text on the factory screen MUST be reduced against the recorded baseline (SC-010). The exact value of any compact number MUST stay available through the existing exact-value behaviour.
- **FR-010**: The manual-mining action MUST remain a permanently visible, compact control reachable with one interaction from the factory screen, with an accessible non-gesture path (clarified 2026-11-11). Its placement is decided in the visual-direction gate.
- **FR-010a**: The factory world MUST show an ore-vein marker at a composed position outside every building location. The marker MUST show activity while manual mining is in progress (a static highlight under reduced motion). In F006 it MUST NOT be an input, and it MUST NOT change mining yield, rate or pacing (clarified 2026-11-11).

**Environment (US2)**

- **FR-011**: The factory world MUST be presented as one continuous industrial facility: ground, foundations under building locations, edges, and restrained decoration consistent with the isometric projection and layering.
- **FR-012**: Occupied, empty-unlocked and locked building locations MUST be distinguishable by shape and value, not by colour alone. Active production areas MUST be visually distinct from unused space.
- **FR-013**: The world MUST show every machine visibly connected to a central store or depot that represents the shared store. It MUST NOT show conveyors, pipes or other links between machines, because they would imply routing the simulation does not have. Activity on a connection MUST follow FR-002 and FR-029, and the store's appearance MAY reflect its existing fill level. The depiction MUST work for any number and arrangement of building locations (FR-037). (Clarified 2026-11-11.)
- **FR-014**: Decoration MUST NOT overlap state cues, markers, the selection frame or hit areas in a way that hides or captures them. Gameplay elements MUST draw above decoration.
- **FR-015**: Decorative environment elements MUST NOT move when reduced motion is on. When it is off, ambient motion MUST stay within the Art Bible's flash and motion limits.

**Contextual machine interaction (US3)**

- **FR-016**: Selecting a machine MUST give clear selection feedback and open a compact contextual card containing:
  - the machine name and level;
  - the current state in words;
  - the limiting cause, if any;
  - the primary action with cost and affordability.

  The card MUST NOT cover the selected machine (SC-002). In phone portrait the card SHOULD stay within 30 % of the safe height at 100–140 % text. At larger text sizes it MAY grow to at most 45 %. Name, state and the primary action MUST be visible without scrolling at every text scale (clarified 2026-11-11).
- **FR-017**: The camera MUST keep the selected machine visible when a card, detail view or sheet opens or resizes, at an allowed integer scale. When it closes, the camera MUST restore the player's previous view unless the player moved it.
- **FR-018**: An expanded detail view MUST provide everything the current machine panel provides:
  - input and output rates;
  - cycle progress and the batch note;
  - all blockers with corrective actions;
  - switch on/off;
  - removal;
  - the existing Item and boost entry points.

  No current machine capability may be lost.
- **FR-019**: Removal MUST require at least two deliberate interactions, including the shared confirmation. It MUST NOT sit next to the primary action in the compact card. Upgrade from the card MUST update the card in place.

**Navigation and supporting screens (US6)**

- **FR-020**: All systems MUST be reachable from the factory screen through one coherent navigation structure: Market (with inventory, priorities and expansion), Contracts, Items, Research, Industry, Overdrive, Prestige and Settings. Each entry point MUST show when its system needs attention. The permanent six-tab bar MUST be replaced by a single hub entry on the factory screen that opens a sheet (phone portrait) or a side panel (larger classes) of system cards. The hub entry MUST show an icon and a short visible text label that stays readable at 200 % text, has an accessible name and meets the 44 pt target rule. The label MUST NOT be "Systems". The entry MUST show an attention indicator when a system needs attention. Production problems are signalled in the world and the persistent strip instead (FR-026). (Clarified 2026-11-11, label revised in the plan review.)
- **FR-021**: Every system view MUST offer a return to the factory in at most one interaction (Back, Escape, close, or tapping the visible factory). Back and Escape MUST follow one consistent back order across all views: dialog, pinned value, card or detail, system view, then factory.
- **FR-022**: On phone portrait, contextual and system views MUST use bottom sheets. On phone landscape and larger classes, side panels MUST keep the factory visible.
- **FR-023**: All surfaces MUST share one visual system consistent with the Art Bible: headers, primary and secondary action styles, spacing, typography tokens, icons, and the disabled, empty, loading and error states. The offline summary, notices, errors and confirmations MUST use it.
- **FR-024**: Existing behaviour of the shared confirmation, the exact-value line, rotation cancel and the stable layout rule (actions do not move when blockers appear) MUST be preserved.

**Visual production information (US4)**

- **FR-025**: The world MUST indicate, for each machine, the F004 visual states (running, waiting, missing input, output/storage full, paused, inactive) using the existing badge precedence. A running machine MUST carry no alarm cue.
- **FR-026**: When several machines are stopped by the same missing resource, the presentation MUST make the shared cause and its resource identifiable without a tap on each machine.
- **FR-027**: The presentation MUST indicate "upgrade available" (an upgrade exists and is affordable) unobtrusively and at a lower visual priority than problem states.
- **FR-028**: The presentation MUST identify the most important current input shortage. This is the resource that currently blocks machines, ranked by the existing supply coverage. It is derived only from existing state (machine blockers and per-resource supply coverage), without a new gameplay rule. It MUST be worded as a shortage ("Short on …"), not as a general bottleneck verdict. The plan MUST define and test the derivation. Game Systems reviews it when the definition goes beyond a direct mapping of existing values.
- **FR-029**: Running machines MUST show activity paced by the authoritative cycle. A completed cycle MUST produce a restrained completion signal that never overstates output. Bursts MUST be coalesced.
- **FR-030**: Detailed numbers (rates, efficiency, coverage) MUST appear only in contextual views or on demand, never permanently above every machine.
- **FR-031**: All state indications MUST remain distinguishable by shape in grayscale and under protanopia, deuteranopia and tritanopia filters, and without animation.

**Building and progression feedback (US5)**

- **FR-032**: The building flow MUST highlight available locations, mark occupied and locked ones, preview the chosen machine at the chosen location with cost and affordability, and offer equally reachable confirm and cancel actions. It MUST be startable from an empty location in context.
- **FR-033**: Insufficient resources MUST make the action visibly unavailable, state the shortfall from existing data, and change nothing.
- **FR-034**: A completed build, upgrade or switch-on MUST trigger an immediate, restrained visual response on the machine. Reduced motion MUST replace motion with a static change.
- **FR-035**: Existing milestones (factory level-up, slot unlock, prestige completion) MUST receive a milestone presentation that acknowledges them without blocking play and without new rewards.
- **FR-036**: The presentation MUST make factory development visible across stages derived only from existing tracked progress. Factory level (the unlocked building locations) is the primary driver; machine levels and the number of active machines add secondary visual density. The presentation resets with each prestige run, and prestige count MUST NOT drive it. The plan defines at least three stages (early, partially automated, established) as a mapping from these values for SC-006. (Clarified 2026-11-11.)

**Scalable world presentation (US7)**

- **FR-037**: Everything this feature adds to the world presentation MUST work from the authored number and arrangement of building locations, not from a fixed count of 12 or a fixed column layout: environment, framing, fit-all, pan limits, selection, hit-testing, building previews, cues and sorting.
- **FR-038**: Machine identity MUST be independent of its on-screen position, so that a future change of placement model does not change selection, cues or contextual views.
- **FR-039**: The specification set MUST document which future capabilities need separate gameplay features: free placement, conveyors, storage, splitters and mergers, spatial bottlenecks, regions and power. It MUST also document the migration needs they imply, including a per-machine position in the save and a mapping from slot to position. F006 MUST NOT implement them.

**Visual design process and acceptance (US8)**

- **FR-040**: Before significant implementation, the feature MUST record:
  - current-state in-game screenshots per screen class;
  - a ranked list of visual and usability problems;
  - at least two concrete visual directions with rendered mockups for phone portrait, phone landscape and tablet, each including an active factory with several machines, a contextual machine interaction and at least one progression or system screen;
  - a comparison against `Core-Idea.md` and this spec;
  - a recommendation;
  - the owner's explicit decision.
- **FR-041**: The owner approves the direction from rendered mockups alone, and no separate in-engine prototype is required before implementation (clarified 2026-11-11). The mockups MUST use the Art Bible palette at real pixel scale. They MUST also show the interaction states that a static image can otherwise hide: the card next to a selected machine, the camera framing with the card open, the hub open, and one 200 % text variant. Placeholder rectangles or text-only descriptions are insufficient.
- **FR-042**: Final acceptance MUST include before/after sheets from real builds per screen class and per key interaction state, and an owner verdict per surface.
- **FR-043**: Readability trials from F004 (blind naming under grayscale and three colour-vision filters, and the owner's timed trial) MUST be repeated on the final world presentation.
- **FR-044**: New or changed visual assets MUST follow the F004 production pipeline: briefs, provenance, deterministic checks, Art Director and Technical Art review, and the "producers never review their own output" rule.
- **FR-045**: Player-visible changes MUST pass an independent UI/UX review. Because the feature changes the player-visible core-loop flow, the Play-Tester role MUST run on each implementation batch, and the owner reviews its report.
- **FR-047**: A minimal World Bible v0.1 MUST be approved by the owner before the direction mockups are briefed. It covers the site concept (a neutral starter industrial platform on a dark base), the site name, mood, the naming and copy rules, and the rule that ground art is swappable for future regions. Every F006 world asset brief, mockup and world-facing string (hub label, milestone copy, stage names, site name) MUST cite its version and conform to it (clarified 2026-11-11).
- **FR-046**: The baseline for every comparative success criterion (SC-001, SC-003, SC-010) MUST be measured on the current build with the same method before implementation and recorded with the feature.

### Key Entities

- **Building location**: An authored place in the factory world where a machine can stand. It has a stable identity, an unlock condition from existing content, and an occupancy (empty, locked, or holding a machine). Today there are 12, but the presentation does not assume the count or arrangement.
- **World layout**: The presentation's description of where building locations, environment and boundaries appear on screen. It is derived from authored content and the screen class. It sets extent, framing limits, hit areas and drawing order.
- **Machine presentation state**: A per-machine view derived from authoritative state. It covers the F004 visual state, the limiting cause, upgrade availability, the activity pace and the latest completion. It is never saved.
- **Attention signal**: A derived indication that something needs the player: a shared missing resource, the most important bottleneck, a claimable contract, available research, prestige ready. Each has a defined source, a priority, and a place where it shows.
- **Contextual machine card**: The compact view for a selected machine, with a defined content set and an expandable detail view.
- **Navigation entry**: The way a game system is reached from the factory, with its attention indicator and return behaviour.
- **Factory development stage**: A presentation classification of how developed the factory is, derived only from existing tracked progress (FR-036).
- **World Bible**: The versioned setting document. Version v0.1 describes a neutral starter site. It sets the site name, mood, naming and copy rules, and the swappable-ground rule. Assets, mockups and world-facing strings cite its version.
- **Visual direction record**: The owner's decision on the design direction, with its alternatives, evidence and date. It governs implementation scope.

## Success Criteria *(mandatory)*

**Gating rule (clarified 2026-11-11).** Criteria marked *(guidance)* are design targets, not gates:

- Their values are measured against the FR-046 baseline and reported in the final acceptance record, and they inform the owner's verdict.
- Missing a target fails no QA gate by itself.
- A guidance value that gets worse than its recorded baseline (for example more interactions than today) is reported as a finding for the owner.

The **binding** criteria are SC-005, SC-007, SC-008, SC-009, SC-011, SC-012 and SC-013. The owner's per-surface acceptance (SC-012) is the final gate.

### Measurable Outcomes

- **SC-001 — Factory dominance** *(guidance)*. With no panel open, the visible factory world (ground and building area, excluding interface elements) covers at least 60 % of the safe screen area on each screen class. Persistent interface elements cover at most 15 %. Method: layout geometry from the screenshot matrix at 100 % text, plus a 200 % text check where chrome may grow to at most 20 %. Baseline: e01/e02 equivalents re-measured.
- **SC-002 — The machine stays visible** *(guidance)*. With a contextual card open, the selected machine is 100 % visible on every screen class and text scale. At least 50 % of the factory world area stays unobscured on phone portrait, and at least 70 % on phone landscape and tablet. Method: Play Mode geometry assertions plus the screenshot matrix.
- **SC-003 — Fewer steps** *(guidance)*. Measured by a scripted journey on each screen class, counting taps, clicks or key presses from the factory screen:
  - upgrade a machine and see the factory again in at most 2 interactions (baseline 3);
  - learn the problem category of every stopped machine in 0 taps;
  - learn a specific machine's limiting cause in 1 tap;
  - reach any system in at most 2 and return in at most 1;
  - build a machine in an empty location in at most 3.
- **SC-004 — Problems at a glance** *(guidance)*. In the owner's timed trial on seeded captures with mixed states, the owner names which machines are stopped and their problem category in at least 9 of 10 trials within 3.0 s each. They name the shared missing resource of a multi-machine stoppage in at least 9 of 10 trials within 5.0 s.
- **SC-005 — No readability regression**. On the final world presentation:
  - blind naming of every cue state scores 9/9 in grayscale and under each of the three colour-vision filters;
  - the F004 timed cue trial scores at least 10/10 within 2.0 s with a median no worse than 1.077 s;
  - cue and lock readability stays at or above the F004 floors per screen class, including the accepted deviations DEV-2 and DEV-4.
- **SC-006 — Progress is visible** *(guidance)*. An independent reviewer orders shuffled captures of the same factory at three development stages (FR-036) correctly in at least 9 of 10 trials. The owner confirms that the difference is meaningful in the final acceptance.
- **SC-007 — No functional regression**:
  - all Edit Mode, Play Mode, tagged Windows-player and art-pipeline suites pass;
  - save fixtures from before F006 load and re-save byte-identically;
  - the balance trace is unchanged;
  - no change to content data is required;
  - the existing journeys pass after a re-baseline that is limited to presentation differences and recorded old-to-new.
- **SC-008 — Performance within budget**:
  - zero managed allocations per frame in steady state on the reference workload;
  - texture memory, atlas page size, SetPass and shader-variant counts within the F004 frozen budgets;
  - frame time on the Windows player at or below the F004 B3 result plus the budget tolerance;
  - every measurement uses F004's method and labels.
- **SC-009 — Accessible on every class**:
  - every view passes the shell layout assertions on all screen classes at 100 %, 150 % and 200 % text with worst-case content, with no clipping and no mid-word header breaks;
  - all interactive targets measure at least 44 pt at the applied scale;
  - with reduced motion on, zero decorative pixels move and every state remains identifiable;
  - every action is keyboard-reachable on pointer profiles.
- **SC-010 — Less text, same information** *(guidance)*. The number of persistent text labels on the factory screen is at most 50 % of the baseline, with no loss of reachable information (FR-018, FR-020).
- **SC-011 — Not bound to 12** (US7). Test layouts with at least two location counts other than 12 (one smaller, one larger than a screen at the readable scale) and one non-rectangular arrangement pass the layout, framing, selection, hit-test and cue tests unchanged.
- **SC-012 — Owner approval**:
  - a recorded owner decision on the visual direction exists before significant implementation starts (FR-040);
  - the final acceptance record holds an owner "accept" verdict for every surface and screen class in the before/after sheets (FR-042).
- **SC-013 — Fits the approved world** (binding). A World Designer checklist review of the delivered deck, depot, links, props, vein, stage dressing and every world-facing string finds zero conflicts with the approved World Bible version and no world-facing string without a recorded owner (FR-047).

## Assumptions

- **A-1 Foundation**: F004 is complete on `main` (`1dab60d`). Its Art Bible `v1.0.4`, asset families, animation clock, camera model and budgets are the baseline. F006 work starts from `main` after this specification set is merged.
- **A-2 Scope**: There are no new recipes, resources, regions, prestige mechanics, power, conveyor logistics, free-grid construction, or economy or balance changes. Every progression visual uses existing data. Anything requiring new gameplay is recorded as a future candidate (see below), not built.
- **A-3 Environment direction**: The environment direction is chosen through the FR-040 visual-direction gate with rendered mockups, not in this spec. The Art Director's recommendation is a modular platform deck that grows with unlocked locations, dressed with foundations, on a dark industrial base. It is one candidate among at least two. Any direction must fit Art Bible v1.x (palette cap 48, layer limits); otherwise the owner decides on a major version.
- **A-4 Manual mining and Overdrive**: Resolved in clarification 2026-11-11. Both stay permanently on the factory screen as compact controls (FR-008, FR-010).
- **A-5 Removal** (default): Removal lives in the expanded detail view behind the shared confirmation (FR-019).
- **A-6 Attention indicators**: "Needs attention" for systems uses only conditions the existing systems already expose, such as claimable or expiring contracts, affordable research, prestige ready and Overdrive ready. No new notification system is introduced.
- **A-7 Completion signal**: An exact per-machine completion signal does not exist today. The plan decides between deriving it in presentation and adding an additive, non-persisted read-model value in the domain layer. Either option is allowed under FR-001, because neither changes simulation results or saves. The owner sees the decision in the plan review.
- **A-8 Audio**: Sound is not part of this feature. Audio feedback is a future candidate.
- **A-9 Devices** (clarified 2026-11-11): Final visual acceptance and the owner's timed trials use the Android emulator (1080x2400 @ 420 dpi) and Windows player captures, as in F004. The screen-class matrix also covers the 720x1280 and 1280x720 phone classes at n_min 2 and the 1536x2048 tablet class through the established capture rigs. A physical phone is optional extra evidence. Feel and performance on real hardware therefore remain unverified, and the acceptance record states this as a known limitation.
- **A-10 Roles** (updated in the plan review on 2026-11-11):
  - Mandatory gates: Game Designer/Product Owner, Software Architect, Test/QA, UI/UX, Art Director, Technical Art (new or changed assets, atlases, import), Performance (render and allocation budgets), Play-Tester (per batch), and World Designer. The World Designer is a new role (owner decision 2026-11-11). It owns the World Bible, the world composition, and the tone of world-facing copy.
  - Light reviews at Low tier:
    - Game Systems/Economy: the "short on" derivation, the stage function, and the no-pacing check for the vein marker.
    - Security/Privacy: the display-preferences v2 reader.
    - DevOps: the sorting-layer settings change.
  - Reasons are recorded in `reviews/discovery.md` and `reviews/plan-reviews.md`.
- **A-11 Implementation readiness**: Implementation starts only after clarification, plan, tasks and analysis, and after the FR-040 direction decision for the affected surfaces. Delivery order (clarified 2026-11-11): F006 stays one feature and leads with art. After the direction decision, the environment, central-store depiction, activity and effect art (US2, and the visual parts of US4 and US5) are produced and integrated first. The UX batches (US1, US3, US6, and the remaining parts of US4 and US5) are then built on the final look. Because the environment must already satisfy FR-037, the plan is expected to place the enabling presentation refactoring before environment integration: a no-behaviour-change split of the large slot-grid view and a world-layout seam. The mockups for the direction gate still cover the UX states (FR-041), so the art is designed against the approved screen layout.

### Future feature candidates (documented, not in scope)

| Candidate | Why it is separate | Dependency or migration it implies |
|---|---|---|
| Free 2D placement and rotation | New placement rules and occupancy simulation | Per-machine position in the save (schema change); slot-to-position mapping; placement validation |
| Conveyors, storage, splitters, mergers | New flow simulation replacing the global store | Flow solver with offline parity; spatial bottleneck model; save schema |
| Spatial bottleneck attribution | Needs physical connections | Depends on conveyors |
| Visual factory tiers unlocked by new progress | Would be a new reward or unlock | Content and unlock schema |
| Power | New system per `Core-Idea.md` | Separate specification |
| Regions, inter-region logistics | New world structure | Region model, save schema |
| Audio feedback and ambience | Not in brief | Sound Designer role activation |
| Industrial projects as milestone presentations | New progression system | Content schema |

# Feature 006 – World-First Factory Experience

Owner input for `/speckit-specify`, received 2026-11-11. Preserved verbatim in substance as the feature's input record.

## Context

We are developing `idle-factory-game`, a mobile-first idle incremental factory game in Unity.
Repository: https://github.com/fjure/idle-factory-game
Our original product vision is documented in `Core-Idea.md`.

The core fantasy is: **Turn work into systems.**

Players start with manual mining and gradually develop increasingly complex, automated industrial facilities with production chains, logistics, research, prestige, and eventually multiple interconnected regions.

Our primary inspirations are:

- Idle Obelisk Miner: Incremental progression, automation, prestige, long-term motivation.
- Factorio: Factory building, production chains, bottlenecks, logistical optimization.
- Satisfactory: Industrial atmosphere, visually satisfying production and factory expansion.
- Captain of Industry: Industrial complexity, production infrastructure, and interconnected systems.

We do not want to copy these games directly. We want our own recognizable visual and gameplay identity.

## Problem

While the project already has substantial technical foundations and implemented gameplay systems, the current player experience does not sufficiently communicate our original vision. The game currently feels too much like a technical prototype or a management dashboard. The factory itself does not yet feel like a convincing industrial world. In particular:

- The interface competes with the factory for the player's attention.
- Too much interaction relies on menus, tabs, panels, and textual information.
- The factory lacks a sufficiently strong sense of place, atmosphere, activity, and industrial progression.
- Players should experience production visually, not merely read numerical production values.
- Machine selection, building, upgrades, and bottleneck diagnosis should feel like natural interactions with the factory.
- Progression should be experienced through increasingly impressive industrial infrastructure.
- The current presentation risks making the 12-slot vertical slice feel like the final intended factory-building model.

We need a significant improvement to the overall visual experience, presentation hierarchy, and world interaction design. This feature must deliver an actual in-game transformation, not merely design documentation or cosmetic adjustments.

## Existing implementation and dependencies

Feature 004 (`specs/004-visual-identity/`) establishes our visual identity: 2.5D pixel-art/cartoon presentation; isometric 2:1 projection; D24 pixel-art density and integer scaling; a defined Art Bible and palette; restyled machines, floor tiles, resources, items, and interface icons; animation, machine-state cues, and camera-related improvements; URP 2D rendering.

Feature 006 builds on the completed Feature 004. Do not restart or replace Feature 004's art production, render migration, or technical foundation.

Before specifying this feature, examine the repository's actual state, including `Core-Idea.md`, `AGENTS.md`, `.specify/memory/constitution.md`, `specs/003-first-factory-prestige/`, `specs/004-visual-identity/`, `specs/005-economy-contracts/`, `docs/art/F004-art-bible-v1.md`, `Assets/IdleFactory/Unity/Presentation/`, `Assets/IdleFactory/Unity/UI/`, and existing Unity scenes, prefabs, sprites, UI Toolkit layouts, cameras, and runtime presentation components. Identify what already exists, what Feature 004 delivers, and which visual/UX shortcomings remain. If F004 is not fully completed and merged, specification planning may proceed, but implementation must not begin on an unstable F004 foundation.

## Primary objective

Make the factory the central visual and interactive experience of the game. The player should feel like they are watching, operating, improving, and expanding a real industrial facility. The factory must be visually attractive, readable, responsive, and satisfying to observe, even when the player is not actively interacting. The interface should support this experience without dominating it. We want to transform the existing game into something that feels like a polished mobile factory game rather than a technical demonstration.

## 1. World-first user experience

Redesign the gameplay screen around the factory world. Requirements: the factory is the primary visual focus; reduce unnecessary persistent interface elements; establish a clear information hierarchy; keep essential resources and progression information accessible; use contextual information rather than permanently displaying everything; prefer interaction with machines and the world over navigating multiple menus; make it easy to return to the factory after interacting with a panel; ensure that common actions require minimal interaction effort.

Review the existing six-tab navigation system critically. Consider whether features such as Market, Contracts, Items, Research, and Prestige should remain directly visible or move into a more structured navigation system. Do not remove functionality or make important progression systems difficult to discover. The final UX must remain appropriate for short mobile sessions and one-handed interaction where practical.

## 2. Industrial atmosphere and environmental presentation

The factory must feel like a physical industrial environment rather than a collection of disconnected sprites. Explore and specify: coherent factory ground and surrounding environment; visually meaningful industrial floor details; foundations, borders, mechanical structures, and environmental decoration; depth and layering consistent with the isometric projection; visual distinction between active production areas and unused space; subtle lighting and machine activity indicators; a consistent industrial color language; a recognizable atmosphere and strong visual identity.

The environment must not introduce excessive visual noise. Pixel-art consistency and mobile performance remain mandatory. Decorative elements should help communicate the factory fantasy without obscuring important gameplay information.

## 3. A factory that feels alive

Improve the moment-to-moment presentation of production. The player should be able to look at the factory and immediately understand that industrial processes are happening. Consider: machine movement and mechanical animation; production cycles that visually communicate activity; small effects such as sparks, steam, indicator lights, or moving machine components where appropriate; output feedback when machines complete production; visual communication of machine efficiency and activity; smooth but restrained feedback for upgrades and state changes; appropriate feedback when a machine stops or encounters a bottleneck.

Avoid excessive particle effects, flashing, or distracting animations. Visual feedback must reflect the authoritative simulation rather than inventing independent production behavior. Respect reduced-motion settings. Do not simulate every individual resource item solely for decorative effects.

## 4. Contextual machine interaction

Redesign how players interact with machines and factory structures. Desired interaction:

1. The player taps a machine.
2. The machine receives clear selection feedback.
3. Relevant status and production information becomes visible.
4. The player can perform common actions directly.
5. Advanced information remains available through an expanded panel.
6. Closing the interaction returns the player naturally to the factory.

Explore contextual controls for: upgrading; activation and deactivation; production status; input/output information; bottleneck explanations; machine removal; relevant items or temporary boosts. Avoid turning each interaction into a full-screen management workflow. The existing simulation, economy, and machine rules must remain authoritative.

## 5. Visual production information

Players must understand their factory without constantly opening statistics panels. Create a clear visual language for: running machines; missing inputs; full output storage; waiting; paused or inactive production; available upgrades; production improvements; important bottlenecks. Build upon Feature 004's established machine-state cues rather than replacing them with an unrelated system.

Introduce contextual production information, such as rates or status overlays, only when useful. For example, selecting a machine might reveal: Input: Iron Ore – 60/min; Output: Iron Plates – 30/min; Efficiency: 75%; Bottleneck: Insufficient Iron Ore. These values are illustrative only, not new gameplay balance requirements. Do not permanently display detailed numerical labels above every machine.

## 6. UI redesign and navigation

Review the existing Unity UI Toolkit implementation and develop a cohesive mobile-first interface. Priorities: clear visual hierarchy; strong distinction between primary and secondary actions; compact, readable HUD; consistent panel styling; appropriate spacing and typography; contextual bottom sheets on phones; side panels where appropriate on larger displays; clear navigation between game systems; consistent icons, states, and transitions; accessible touch targets and text scaling.

Evaluate the following screens and flows: main factory view; building and machine selection; machine management; market and inventory; research; contracts; items; prestige; offline progress summary; notifications, errors, and confirmations.

The interface must look like one cohesive game rather than multiple independently designed tools. Do not introduce a new visual identity that contradicts the approved F004 Art Bible.

## 7. Camera and spatial presentation

Build on the camera system established in Feature 004. Evaluate improvements to: factory framing; camera focus when selecting machines; panning and zooming; visibility when opening panels; camera behavior during construction; world boundaries; factory overview and detail views. The factory should remain readable on portrait and landscape phones as well as tablets. Preserve integer pixel scaling and the agreed readability constraints. Do not sacrifice accessibility or visual clarity merely to fit more content on screen.

## 8. Building experience

Improve the visual experience of the existing construction workflow. Explore: clear building selection; placement previews; occupied and available building locations; visually satisfying building placement; clear feedback for insufficient resources; immediate visual response after constructing or upgrading machinery; easy cancellation and contextual navigation. The current fixed-slot gameplay must remain functional. However, do not design the UX in a way that assumes the game will permanently remain a 12-slot factory.

## 9. Preparation for future free factory building

Our original vision explicitly requires a freely buildable 2D factory world with: arbitrarily placed machines; conveyors; storage; splitters and mergers; physical production connections; spatial bottlenecks; larger production chains.

The existing 12-slot model is a vertical slice, not the final target. Feature 006 should prepare the presentation and UX architecture for this future without silently implementing an entirely new simulation. Where reasonable, design new presentation components around scalable spatial structures rather than hardcoded 12-slot assumptions. Identify architectural dependencies and migration requirements. Document which future capabilities require separate gameplay features. Do not implement conveyor logistics or replace the factory simulation as part of this feature unless explicitly approved.

## 10. Progression should look rewarding

Long-term progression is central to our product identity. The player should experience a meaningful difference between: a tiny early factory; a partially automated factory; an established industrial operation.

Within the mechanics and content already implemented, specify opportunities to visually reinforce progression. Examples include: stronger visual feedback for upgrades; increasing production activity; more impressive machinery; environmental development; major milestone presentations. Do not invent gameplay tiers, production values, unlocks, or new reward systems. Where new gameplay mechanics would be required, document them as future feature candidates.

## 11. Visual design process

Visual quality is a primary acceptance criterion. Do not consider an interface successful merely because it functions correctly or passes automated tests. Before full implementation:

1. Analyze the current player experience using actual in-game screenshots.
2. Identify the most important visual and usability problems.
3. Produce concrete visual design alternatives.
4. Present mobile portrait, mobile landscape, and tablet examples.
5. Include realistic examples of an active factory with several machines.
6. Include contextual machine interactions and at least one progression screen.
7. Compare alternatives against our original game vision.
8. Recommend a clear design direction.
9. Obtain explicit owner approval before committing to a significantly changed interaction or visual direction.

Use visual mockups, wireframes, in-engine prototypes, and comparison screenshots as appropriate. Placeholder rectangles and text-only descriptions are insufficient evidence for final visual approval. The owner is the final authority for subjective visual acceptance.

## 12. Technical constraints

Unity and existing URP 2D foundation; existing isometric pixel-art identity; mobile-first development; deterministic simulation remains separate from presentation; no gameplay balance changes; no save-game incompatibilities; no unnecessary allocations in recurring presentation updates; respect existing performance requirements; support portrait and landscape layouts; preserve accessibility, readable text scaling, and reduced motion; reuse existing assets and infrastructure where appropriate; minimize duplication of existing UI/presentation systems; prefer maintainable, reusable UI components; maintain compatibility with future factory expansion; follow repository architecture and testing conventions.

## 13. Expected user stories

At minimum, develop independently testable user stories covering:

- US1 – Factory-centered gameplay screen (P1): the player sees the factory as the dominant experience, with essential controls and information available without excessive visual clutter.
- US2 – Convincing industrial environment (P1): the factory appears to occupy a coherent, attractive industrial world rather than a collection of disconnected slots.
- US3 – Contextual machine interaction (P1): players can inspect and manage machines quickly without losing their place in the factory.
- US4 – Understand production visually (P1): players can identify machine activity, production problems, and important changes at a glance.
- US5 – Living factory and satisfying feedback (P2): production, upgrades, construction, and progression feel active and rewarding through appropriate visual feedback.
- US6 – Cohesive navigation and supporting screens (P2): existing progression systems remain accessible through an intuitive, visually unified interface.
- US7 – Scalable world interaction foundation (P2): new presentation and interaction concepts remain suitable for the future freely buildable factory system.
- US8 – Visual validation and owner acceptance (P1): the feature can only be considered complete after its implementation has been visually reviewed in real game builds across representative devices and interaction states.

These stories are a starting point. Refine or split them based on repository analysis.

## 14. Success criteria

Establish measurable, verifiable acceptance criteria. They should demonstrate that: the factory is clearly the primary visual focus; routine machine interactions are faster and require fewer navigation steps; important production problems are identifiable without navigating through multiple panels; the interface remains readable on representative mobile devices; the game maintains visual consistency with Feature 004; screens and interactions work with existing gameplay data; the implementation does not regress save compatibility, simulation correctness, or performance; the user can distinguish an early factory from a more advanced one through the presentation; actual in-game screenshots demonstrate a meaningful before/after improvement; the owner explicitly approves the new design.

Propose measurable thresholds and validation methods during specification, rather than declaring unverified numbers as established facts. Automated tests are necessary but do not replace visual acceptance.

## 15. Scope boundaries

In scope: major factory-screen UX improvement; HUD and navigation restructuring; contextual machine interactions; environmental and atmospheric presentation; visual production feedback; improved presentation of upgrades and progression; UI consistency across existing systems; camera and interaction polish; reusable presentation architecture; real screenshot-based visual review.

Out of scope: new production recipes; new resource types; new regions; new prestige mechanics; economy rebalance; replacing the simulation engine; full free-grid construction; new conveyor logistics simulation; new power systems; repeating Feature 004's art-style selection; rebuilding already accepted F004 assets without a concrete justification.

## 16. Specification-driven workflow

Use the repository's existing Spec Kit workflow and constitution. Treat this as the next independent feature, provisionally numbered `006-world-first-factory-experience`, after the existing Features 001–005. Follow established repository conventions for specification documents, architecture analysis, requirements, planning, tasks, reviews, and MAQA orchestration.

During specification: separate player-facing requirements from technical implementation decisions; identify reusable existing systems; identify specific existing implementation problems using repository evidence; clearly distinguish current functionality from future vision; record dependencies on completed F004 work; identify risks and migration requirements; avoid premature technical decisions that belong in `/speckit.plan`; keep the specification focused and independently testable; use the project's human-in-the-loop decision process for unresolved product decisions.

Do not create implementation tasks or start coding during the initial specification step.

## Final expectation

The outcome should be a major, recognizable improvement in how Idle Factory looks, feels, and operates on a mobile device. After this feature, a player opening the game should immediately see an attractive, active industrial factory. They should feel that they are operating and expanding a real production environment. They should not feel that they are managing a collection of disconnected upgrade menus.

Guiding principle: **The factory is the game. The interface exists to help the player understand and control it.**

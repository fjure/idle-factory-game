---
title: Feature 006 plan overview (planned)
type: decision
sources: [S056, S057]
updated: 2026-11-11
---

# Feature 006 plan overview (planned, not implemented)

F006 "World-First Factory Experience" makes the factory the screen. Gameplay, content and saves stay unchanged; the plan (revision 2) passed seven review lenses including the new World Designer. Status: planning complete; batch P0 (T502-T508) merged, P1 not started (see [baseline](./f006-p0-baseline.md)). (S056, S057)

## Phases and gates

- **P0**: baseline probes (R02), minimal World Bible v0.1 for a neutral starter industrial platform on a dark base, and rendered mockups of at least two directions. Gate OD-9: owner approves the World Bible text, the direction and the hub label.
- **P1**: enabling refactor, no behaviour change: pure-move split of the slot-grid view, `ShellTab` shim, `FactoryWorldLayout` seam with exact camera parity, new sorting layers, session-owned `CompletedCycles`, `WorldCompositionSettings`.
- **P2**: world art first. Art Bible v1.1 (additive, amendment list, gate OD-10), Tilemap deck, depot with trunks and branches, ore-vein marker, effects capped by flash rate. A-03 badge anchor chosen from measured candidates (OD-12).
- **P3**: presentation, attention and "short on" models, docked machine card, labelled hub with one back stack, contextual build card, milestones and stages, display preferences v2.
- **P4**: B4 against the frozen F004 budgets, trials, owner acceptance. Task IDs start at T500. (S056)

See [world presentation design](./f006-world-presentation-design.md) for the mechanisms.

## Owner decisions (2026-11-11)

- OD-1 a hub replaces the six-tab bar, never named "Systems"; OD-13 it has icon plus a short visible label.
- OD-2 machines link to a central store, never to each other (truthful to the single shared store).
- OD-3 stages derive from factory level first, machine levels and count second; they reset with prestige.
- OD-4 direction approved from rendered mockups only; OD-8 one feature, art first.
- OD-5 Mining and Overdrive stay on screen; zoom buttons hidden on touch with an accessible path.
- OD-7 acceptance on emulator plus Windows; a physical phone is optional and recorded as unverified.
- OD-14 machine card may reach about 45 % of phone-portrait height at large text; OD-15 minimal World Bible; OD-16 ore-vein marker, not an input.
- OD-9 resolved in P0 (2026-11-11): World Bible v0.1 frozen (OD-9a), direction A with B's wall as stage-2 candidate, hub label "Office", reviewer conditions binding (OD-9b); see [direction decision](./f006-p0-direction-decision.md). (S057)
- Pending: OD-10, OD-12; OD-11 (revised `CompletedCycles` design) to confirm at the next owner touchpoint. (S056)

## Binding versus guidance criteria

OD-6: the proposed thresholds (SC-001, 002, 003, 004, 006, 010) are guidance, measured and reported. Binding are SC-005, SC-007, SC-008, SC-009, SC-011, SC-012, SC-013 plus owner visual acceptance. (S056)

## World Bible gate

FR-047: World Bible v0.1 (`docs/world/world-bible-v0.1.md`, World Designer) must be approved before mockups are briefed. Every world asset brief, mockup and world-facing string (hub label, milestone copy, stage names, site name) cites its version. Ground art is swappable for future regions. (S056)

## Review waves and constraints

Discovery and plan reviews are recorded under `specs/006-world-first-factory-experience/reviews/`. Sound Designer omitted (audio out of scope). No new package or asmdef; `com.unity.modules.tilemap` is already in the manifest but F006 is its first runtime use, so an Android IL2CPP and Player check applies. Budgets are the F004 frozen ones on workload W006-V. (S056) Related: [Feature 005 plan overview](./f005-plan-overview.md), [visual identity](./visual-identity-art-direction.md), [future capabilities](./future-free-building-vision.md).

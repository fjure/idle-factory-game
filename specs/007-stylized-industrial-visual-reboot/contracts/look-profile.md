# Contract: Look profile (presentation seam)

**Scope**: FR-001, FR-009, FR-035a; research R07, R09 and R15.

**Status**: planned. The Software Architect reviews it at the High tier before the first M2 implementation task.

## Purpose

A look profile selects which art set, environment, indicator family, layout pitch and camera policy the world presentation uses. The world views draw the same authoritative state under every profile. Only the appearance and the on-screen geometry differ.

## Rules

1. **Presentation only.** A profile MUST NOT change simulation inputs, outputs, timing, save data or content. A Play Mode determinism parity test runs identical inputs under `F006` and under each other profile, and the simulation snapshots must be equal.
2. **Single source of state.** Machine state, cue precedence, attention, development stage, depot fill and effects come from the existing read models (F006 `contracts/presentation-model.md`). A profile changes only how they are drawn.
3. **Fail closed.** Every profile declares its required keys, and resolution validates them at start-up, as with the F004 catalog registry. A missing key is a start-up error in development builds and a build-time error for the shipped profile. There are no silent fallbacks between profiles.
4. **Isolation.**
   - Prototype profiles (`isPrototype = true`) and their assets are referenced only from `IdleFactory.DevTools`, which is compiled only with `IDLEFACTORY_DEV`.
   - A release build MUST contain no prototype asset. This is verified by the build report in M2.
   - The shipped default is named in the shipped presentation settings and is never a prototype.
5. **Switching.** In a development build, switching a profile triggers the existing world rebuild path (layout change). The selection, card state and camera follow the F006 rules: selection identity comes from the location id, never from the screen position. A switch while the card is open keeps the card and re-frames the camera.
6. **Layout.**
   - The location count, ids and unlock order come from content.
   - A profile may set the pitch and arrangement (`LayoutPitchSettings`). Hit areas MUST NOT overlap and MUST be at least 44 pt.
   - The layout-independence tests (6, 12, 20 and L layouts) run for every profile.
7. **Overhang.** Art may exceed the location cell. A tap resolves through `WorldTapResolver` on location hit shapes and markers only, never through art bounds. Indicators anchor to their own machine and do not overlap a neighbour's indicator. This is checked on the dense layout.
8. **Camera.** In M2, every profile uses the integer camera model through its PPU. A `Continuous` camera policy may be introduced only at M4, with explicit re-baselining (R15). The camera keeps a single writer (F004).
9. **Renderer.** A profile may name the `Universal` world renderer (C1) only as a prototype in M2. The shipped default renderer changes only after the M3/M4 decision, with DevOps and Technical Art review of the variants and the parity tests.
10. **Reduced motion and allocations.** Every profile honours reduced motion (frame 0, no decorative motion, static readable cues). On the steady path, every profile has 0 allocations per frame, measured by the tagged player test per profile.

## Required keys per profile (minimum)

| Group | Required keys |
|---|---|
| Machines | Miner, Smelter, Press, Assembler: stage 0 body plus the run loop. M2 also requires stage 1 for one type. |
| Machine passes | Shadow, plus an emissive mask (B, C) |
| Depot | Depot, plus the fill states EMPTY, LOW, HIGH, FULL |
| Environment | Floor, foundations, infrastructure set, world boundary |
| Indicators | Indicator glyphs for the six non-running states, plus the upgrade-available glyph |
| Selection | The selection frame and the build preview (ghost) |
| Effects | The completion effect, plus one process effect per machine type |

## Tests that own this contract

| Test | Mode |
|---|---|
| `LookProfileResolutionTests` | Edit Mode |
| `LookProfileDeterminismParityTests` | Play Mode |
| `LookProfileLayoutIndependenceTests` | Edit Mode and Play Mode |
| `OverhangTapTests` | Play Mode |
| `LookProfileAllocationTests` | Play Mode, `[Category("StandalonePlayer")]` |
| `ReleaseBuildExcludesPrototypeAssets` | Build-report check script |

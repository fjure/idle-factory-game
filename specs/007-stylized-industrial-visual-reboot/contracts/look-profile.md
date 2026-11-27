# Contract: Look profile (presentation seam)

**Scope**: FR-001, FR-009, FR-035a; research R07, R09 and R15.

**Status**: planned. The Software Architect reviews it at the High tier before the first M2 implementation task.

## Purpose

A look profile selects which art set, environment, indicator family, layout pitch and camera policy the world presentation uses. The world views draw the same authoritative state under every profile. Only the appearance and the on-screen geometry differ.

## Rules

1. **Presentation only.** A profile MUST NOT change simulation inputs, outputs, timing, save data or content. The parity test is specified in research R19:
   - per-tick hashes of the full authoritative state;
   - scripted inputs, with taps through `WorldTapResolver`;
   - a fixed time source;
   - a mid-run switch case;
   - identical save bytes;
   - a negative control.
2. **Single source of state.** Machine state, cue precedence, attention, development stage, depot fill and effects come from the existing read models (F006 `contracts/presentation-model.md`). A profile changes only how they are drawn.
3. **Fail closed.** Every profile declares its own requirement table (keys, sizes and pivots; ARCH-5), and resolution validates it at start-up, as with the F004 catalog registry. A missing key is a start-up error in development builds and a build-time error for the shipped profile. There are no silent fallbacks between profiles.
4. **Isolation.**
   - Prototype profiles (`isPrototype = true`) and their assets are referenced only by a dev-only scene with a DevTools holder. `PlatformBuildCommands.Run` adds that scene only for `IDLEFACTORY_DEV` builds. There are no `Resources/` paths (ARCH-1). The release check inspects `BuildReport.packedAssets`, with a positive control on the dev build (QA-3).
   - A release build MUST contain no prototype asset. This is verified by the build report in M2.
   - The shipped default is named in the shipped presentation settings and is never a prototype.
5. **Switching.** The bootstrap owns a single `LookProfileResolver` and passes it to the views, the camera and the effect pool. DevTools only injects an `ILookProfileSource` at `BeforeSceneLoad` (ARCH-6). In a development build, switching a profile triggers the existing world rebuild path (layout change). An unknown persisted id falls back to the shipped default with a log entry, and release builds ignore the key (ARCH-11). The selection, card state and camera follow the F006 rules: selection identity comes from the location id, never from the screen position. A switch while the card is open keeps the card and re-frames the camera.
6. **Layout.**
   - The location ids and unlock order come from content. The shipped count is the constant 12 in `SlotGridLayout` (ARCH-9).
   - A profile may set the pitch and arrangement (`LayoutPitchSettings`). Hit areas MUST NOT overlap and MUST be at least 44 pt.
   - The layout-independence tests (6, 12, 20 and L layouts) run for every profile.
7. **Overhang.** Art may exceed the location cell. A tap resolves through `WorldTapResolver` on location hit shapes and markers. These are the requirements:
   - A tap on an opaque pixel of the front-most machine selects that machine.
   - The selection frame stays fully visible.
   - Hit shapes are separated in pt at `n_min` (UX-4).
   - Indicators anchor to their own machine and do not overlap a neighbour's indicator.

   All of this is checked on the dense layout for every class and at both text scales.
8. **Camera.** In M2, every profile uses the integer camera model through its PPU. A `Continuous` camera policy may be introduced only at M4, with explicit re-baselining (R15). The camera keeps a single writer (F004).
9. **Renderer.** A profile may name the `Universal` world renderer (C1) only as a prototype in M2. It does so through a **dev-only second URP asset** that DevTools assigns before the first frame. The shipped URP asset keeps exactly one 2D renderer (ARCH-2). The shipped default renderer changes only after the M3/M4 decision, with DevOps and Technical Art review of the variants and the parity tests.
10. **Reduced motion and allocations.** Every profile honours reduced motion (frame 0, no decorative motion, static readable cues). On the steady path, every profile has 0 allocations per frame, measured by the tagged player test with the profile as a parameter. The steady state starts after the warm-up and excludes a profile switch (PERF-10, QA-11).

## Required keys per profile (minimum)

| Group | Required keys |
|---|---|
| Machines | Miner, Smelter, Press, Assembler: stage 0 body plus the run loop. M2 also requires stage 1 for one type, and finish tiers 0 and 1 for one type plus a site patch (FR-027c). |
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

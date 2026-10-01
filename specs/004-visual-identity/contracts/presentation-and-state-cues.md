# Contract: Presentation Catalog, State Cues and Animation

**Scope**: spec FR-002, FR-003, FR-010, FR-013–FR-015, FR-019, FR-020, FR-024, US1, US2, US4; research R04–R06, R10, R11.

## Catalog

- Builds on the T110 resolution-agnostic `PresentationSpriteCatalog` (world size + pivot validation, optional frame list, merged in #253).
- Feature 003 keys keep their identifiers; Feature 004 attaches new frames to them. New keys (cue icons, body variants, markers) are additive with the prefix `F004-`.
- Every F004 entry may name a `fallbackKey` (its F003 placeholder). On a load or validation failure of an F004 entry, the catalog serves the fallback, logs `presentation.art.fallback` once per key without paths, and the view stays interactive with its state cues (FR-024). Start-up still fails closed only for keys that have neither art nor fallback (`presentation.key.unresolved`, unchanged).

## State-cue mapping

- `SlotGridView` maps each machine state and slot condition to the state-cue set (data-model) of the Art-Bible-selected variant. It reads only the existing allocation-free presentation states; it adds no gameplay reads.
- Exactly one state cue per machine at a time (precedence from the F003 state model); markers are independent.
- Running machines show no state cue. Every other state has a distinct shape cue. Color is never the only difference (FR-010).
- Cue icons, markers and the selection frame render on the overlay sorting layer above all machine art; machine art sorts by slot row; each machine is one Sorting Group (R04).
- Label text appears only at scales where it meets the minimum text size; the machine panel always names the state.

## Animation

- One shared presentation clock advances every animated entry in a single update: `frame = ⌊t · fps⌋ mod frameCount`. No per-machine update, no `Animator`, zero steady-state allocations (FR-026).
- Only running machines animate (`animated = true` in the cue set). Effective reduced motion forces frame 0 for every entry from the next frame (FR-020).

## Display preferences

- Store: `persistentDataPath/IdleFactory/Preferences/display-v1.json`, `{ "version": 1, "reducedMotion": "followSystem" | "on" | "off" }`, strict reader, atomic write.
- Missing, unreadable, unknown version or invalid value → `followSystem`, diagnostic `preferences.display.invalid` once, file rewritten on the next change. Never touches the save directories.
- UI: a three-state control in the existing settings or HUD surface, keyboard reachable with visible focus and at least 44×44 pt (spec FR-018).

## Tests

- Edit Mode: catalog fallback and additive-key rules; cue mapping table (each state → distinct shape key, precedence); reduced-motion resolution matrix; preferences reader (valid, missing, truncated, wrong version, unknown value, oversized).
- Play Mode: fully occupied 12-slot frame with every cue visible and none hidden by overhang; tap on a tall machine's overhang selects the slot behind it; reduced-motion toggle freezes loops; missing-art fallback keeps the slot tappable.

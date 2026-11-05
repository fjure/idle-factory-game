# SC-001 naming vocabulary (for the independent reviewer)

You receive the sheets of one or more sets (`<set>/sheet_grayscale.png`, `sheet_deuteranopia.png`, `sheet_protanopia.png`, `sheet_tritanopia.png`). Every sheet is a grid of 22 numbered tiles. Each tile is one slot of the game's factory floor, cropped from an in-game capture of the shipped build at the stated device pixel scale (tiles are shown at 1x: one tile pixel is one device pixel). Other slots are removed; the floor is the real one.

**The tile order differs on every sheet** (a separate shuffle per filter and per set). Name each sheet on its own; do not carry numbers over. Open only the sheets and this vocabulary: no answer key exists in this folder.

For every tile, report these fields:

| Field | Allowed names |
|---|---|
| Slot condition | `empty plate` (buildable), `locked plate` (also give the level digit 2 to 5 if you can read it, else `digit unreadable`), `occupied` (a plate with a machine) |
| Machine type (occupied only) | `miner`, `assembler`, `other` (describe it). Other machine types exist in the game; a given set may not contain all of them |
| State (occupied only) | `running`, `waiting`, `missing input`, `storage full`, `paused`, `inactive` |
| Markers | `none`, `Boosted`, `Overdrive`, `both`. Describe the marker glyph in your own words as well (shape and where it sits). Markers are small glyphs above the machine, separate from the state cue |
| Selection frame | `yes` (a frame around the plate) or `no` |
| Certainty | `sure` or `unsure`, and the visual cue that decided it (shape, glyph, brightness, position) |

Rules for the reviewer:

- Decide from shape and brightness first. The sheets are deliberately grayscale or colour-vision simulated. Say when a name rests on a guess about what a glyph means.
- Running machines have no state cue by design; a machine with no cue is `running`.
- List every pair of tiles on a sheet that you could not separate with confidence ("hard pairs"), and every tile you named with `unsure`.
- A zero-confusion result needs every field right on every tile of every sheet and filter. "Unsure" is reported but counted as named-as-given when the name matches.

Return one table per sheet, then the hard pairs and an overall note. The evidence author compares your table with the key afterwards; the author has not opened the key and does not name tiles.

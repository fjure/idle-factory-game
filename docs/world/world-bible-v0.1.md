# World Bible v0.1

**Version**: `F006-WB v0.1`
**Status**: Frozen v0.1 — approved by the owner 2026-11-11 (OD-9a, T504). Changes need a new version under section 7.
**Owner**: World Designer. Visual style, palette and shape language stay with the Art Director ([F004 Art Bible](../art/F004-art-bible-v1.md)); UI flows and string fit stay with UI/UX; numbers stay with Game Systems/Economy.
**Scope**: Feature 006 ([spec](../../specs/006-world-first-factory-experience/spec.md) FR-047, SC-013; [research](../../specs/006-world-first-factory-experience/research.md) R22; [contract](../../specs/006-world-first-factory-experience/contracts/world-presentation.md) section 7).
**Freeze**: frozen 2026-11-11 on owner approval (OD-9a), before the P0 direction briefs. Every F006 brief, mockup and world-facing string cites `F006-WB v0.1`.

## 1. Site concept

The player runs one **neutral starter industrial platform on a dark base**: a flat working deck, built out on dark ground, where machines sit at their locations and feed one central **depot**. It is a place of work, not a story. v0.1 names no company, no planet, no people and no history. Nothing here may be read as a promise about later regions, power, expeditions or lore.

- The platform grows with the factory: open deck, then walled yard, then gantry line (the three development stages, R15).
- Machines send output to the depot along **trunks** (static lanes, R07). Machines do not link to each other (OD-2).
- One **ore vein** marks the mining spot (R23, OD-16). It is scenery plus mining feedback, not an input.

## 2. Site name and tagline

| | Text | Note |
|---|---|---|
| Site name | **Meridian Works** | Two words, 14 characters. Neutral and industrial. |
| Tagline | **Build the line. Feed the depot.** | Two short imperatives. Title surface and store copy only, never a persistent HUD label. |

## 3. Mood words

**Quiet. Methodical. Steady. Industrious. Dim-lit.** A calm night shift with working lights on: not a disaster, not a joke, not a showroom. Moods to avoid: grim, apocalyptic, cute, corporate-glossy.

## 4. Naming and copy rules

**Tone.** Plain, short, present tense, active voice. Say what the thing is or does. Sentence case on every label. No exclamation marks, no jargon, no sarcasm.

**Tone vocabulary (prefer).** works, deck, location, depot, trunk, line, stage, level, output, stock, vein, ore, crew, yard, gantry.

**Naming rules.**
1. World nouns are singular, concrete and physical: Depot, Ore vein, Deck.
2. Existing game terms stay unchanged: Factory Sale, Contracts, Market, Research, Overdrive, Items, Mining. v0.1 renames no mechanic.
3. One thing, one word. The central store is always "Depot", never "warehouse", "store" or "hub" in player text. ("Hub" stays an internal term for the menu entry.)
4. Stages are named by what the deck looks like, not by rank or praise.
5. A banner states what changed and what it unlocks. It carries no numbers beyond the level and unlock count.
6. "Plate" is an item word ("Iron Plate") and is never used for a machine location; a machine stands on a **location**.

**Forbidden terms.**
- **"Systems"** as the hub label (OD-1). Also bare "Menu" and "More", which say nothing.
- Lore proper names, company or planet names, any fiction not in this document.
- Rank or hype words: epic, legendary, mega, ultimate, boss, empire, tycoon.
- Real brands and real places.
- Marketing exclamations ("Awesome!", "Level up!").
- Terms for features that do not exist yet: power, grid, expedition, region names.

## 5. Swappable-ground rule

Ground, deck and base art is a **family keyed by region id**. Exactly one region exists today, `starter-platform`, on a dark base. A future region supplies its own ground family under a new id and does not edit this one. Machines, depot, trunks, vein and UI are region-neutral and must read on any ground. Changing the starter ground, or adding the first second region, needs a new World Bible version and an explicit migration decision for existing assets.

## 6. Deposit notes for later regions

Later regions own real deposit placement. v0.1 fixes only this: in F006 deposits are margin-ring or empty-cell scenery, never a hit target and never a pacing change. Yields, sizes and any placement that affects progression need Game Systems/Economy review. Vein dressing varies by stage only. A deposit kind (ore today) is a ground-independent sprite family; a region may restyle it through its ground family but may not rename it without a new version.

## 7. Version and semver rules

- Format: `F006-WB vMAJOR.MINOR`, with a patch digit only for typo fixes.
- **MAJOR**: the setting or site identity changes (new site name, new concept). Existing assets and strings need a migration decision.
- **MINOR**: additive. A new region id, a new approved term, a new deposit kind.
- **Patch**: wording or typo fixes that change no conformance result.
- Only the owner approves a version; the World Designer authors it. Assets, briefs and strings cite the version they were checked against and are rechecked on a MAJOR or MINOR bump.
- Conformance is checked by the World Designer checklist (`reviews/world-bible-conformance.md`, after P2 and at P4). String inventory: [world-copy.md](../../specs/006-world-first-factory-experience/reviews/world-copy.md).

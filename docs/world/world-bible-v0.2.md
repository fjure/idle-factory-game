# World Bible v0.2

**Version**: `F006-WB v0.2`
**Status**: Frozen v0.2, approved by the owner 2026-11-19 (CF-1, T547). It supersedes `F006-WB v0.1` (frozen by OD-9a, 2026-11-11). Changes need a new version under section 7.
**Owner**: World Designer. Visual style, palette and shape language stay with the Art Director ([F004 Art Bible](../art/F004-art-bible-v1.md)); UI flows and string fit stay with UI/UX; numbers stay with Game Systems/Economy.
**Scope**: Feature 006 ([spec](../../specs/006-world-first-factory-experience/spec.md) FR-047, SC-013; [research](../../specs/006-world-first-factory-experience/research.md) R22; [contract](../../specs/006-world-first-factory-experience/contracts/world-presentation.md) section 7).
**Freeze**: v0.2 frozen 2026-11-19 on owner approval (CF-1, T547), after the T547 conformance review found that naming rule 3 and the shipped "Store" strings conflicted. It is a MINOR, additive change: it renames no string and adds one approved term. Every F006 brief, mockup and world-facing string cites `F006-WB v0.2`; items checked against v0.1 stay valid except the string rows listed in section 8.

## 1. Site concept

The player runs one **neutral starter industrial platform on a dark base**: a flat working deck, built out on dark ground, where machines sit at their locations and feed one central **depot**. It is a place of work, not a story. v0.1 names no company, no planet, no people and no history. Nothing here may be read as a promise about later regions, power, expeditions or lore.

- The platform grows with the factory: open deck, then walled yard, then gantry line (the three development stages, R15).
- Machines send output to the depot along **trunks** (static lanes, R07). Machines do not link to each other (OD-2). The depot holds the factory's **Store**; when the Store is full, the depot shows full.
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

**Tone vocabulary (prefer).** works, deck, location, depot, store, trunk, line, stage, level, output, stock, vein, ore, crew, yard, gantry.

**Naming rules.**
1. World nouns are singular, concrete and physical: Depot, Ore vein, Deck.
2. Existing game terms stay unchanged: Factory Sale, Contracts, Market, Research, Overdrive, Items, Mining, Store. v0.2 renames no mechanic.
3. One thing, one word. The central structure on the deck is always the **Depot**, never "warehouse", "store" or "hub" for that structure. Its capacity is the **Store**: the HUD pill, "Store is full", "Store full" and "Store 9% full" name the capacity, and the Depot is where the Store is shown in the world. The Depot's fill is the Store's fill. A string that names the structure says "Depot"; a string that names the capacity says "Store". ("Hub" stays an internal term for the menu entry.)
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
- Conformance is checked by the World Designer checklist (`reviews/world-bible-conformance.md`, after P2 and at P4; final record T547). String inventory: [world-copy.md](../../specs/006-world-first-factory-experience/reviews/world-copy.md).

## 8. Changes from v0.1

**Version change.** `F006-WB v0.1` to `F006-WB v0.2`, **MINOR** under section 7: additive (a new approved term and a clarified rule); the setting, site identity and region identity do not change. Owner decision 2026-11-19 (CF-1, T547, option A).

**What changed.**
1. "Store" is an approved term and the capacity term in player text (section 4: rule 2 list, tone vocabulary).
2. Rule 3 is **reworded**, not only extended: v0.1 rule 3 said the central store is "Depot" and never "store" in player text; v0.2 rule 3 separates the structure ("Depot") from its capacity ("Store"). The owner approved this clarification together with the new term (CF-1, 2026-11-19); the project treats it as MINOR because no existing string, asset or mechanic changes and no conformant v0.1 content becomes non-conformant.
3. Section 1 states that the depot holds the Store.
Nothing was removed. No string, asset, mechanic or sprite is renamed. The v0.1 file is left unchanged under its freeze; this section is the record of the supersession.

**Recheck consequence under section 7 (MINOR): strings only, rechecked by this record.** The T547 conformance review (`reviews/world-bible-conformance.md` Part 2) rechecked every string that says "Store" or "storage" or cites rule 3, and all conform to v0.2 as shipped:
- HUD: `Assets/IdleFactory/Unity/UI/FactoryRun.uxml:18` caption "Store"; `UI/RunShellDocument.cs:918-920` pill ("FULL", "N%", tooltip "Store full" or "Store N percent used") and `:2953-2955` exact value ("Store: a of b ...", "Store: no capacity").
- Machine card: `UI/MachineCard.cs:71` "Store is full"; `:90` cause detail (T547 proposes "Store full, so makers cannot deliver").
- Vein: `UI/OreVeinCopy.cs:19` "Store full" (also `FactoryRun.uxml:63`); `:71` accessible name ", store full".
- Hub: `UI/Panels/HubPanel.cs:581` "Store N% full".
- Inventory: `UI/Panels/InventoryRatesPanel.cs:100`, `:320`, `:348`, `:349`; `UI/Panels/InventoryRatesPanel.uxml:8`.
- Startup: `UI/Panels/StartupStatusPanel.cs:807` ("... than the first store level holds.").
- Rejections and notices: `UI/RunRejectionCopy.cs:43` StorageFull; `UI/Panels/OfflineSummaryPanel.cs:567` ("the store is full"); `UI/Panels/MachinePanelPresenter.cs:389`; `UI/Panels/PrioritiesPanel.cs:70` and `UI/Panels/PrioritiesPanel.uxml:3` ("store space").
Strings that say "storage" for the same capacity (Expansion "Storage" and "Upgrade storage", machine panel "Storage is full", research effect "starting storage level", prestige "Stored resources" and "Storage and factory upgrades", system messages in the startup and session-log panels) are not forbidden by rule 3 and are not renamed. A later one-word pass may unify them under a separate decision.
**Assets: no recheck.** No sprite family carries text, the depot art is unchanged, and the swappable-ground rule (section 5) is untouched. Site name, tagline, stage names and the hub label are unchanged.

**Migration decision.** None needed; existing content is valid as shipped.

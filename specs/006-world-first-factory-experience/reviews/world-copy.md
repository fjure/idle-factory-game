# World-facing copy inventory

**World Bible**: `F006-WB v0.1` ([docs/world/world-bible-v0.1.md](../../../docs/world/world-bible-v0.1.md)), Frozen v0.1, approved by the owner 2026-11-11 (OD-9a, T504). The hub label choice is OD-9b (T509).
**Spec**: FR-047, SC-013; research R22, R14, R15, R23.
**Ownership**: tone and wording by the World Designer; length and fit by UI/UX; wiring by the assigned worker.

## Fit method and caveat

At 200 % text scale body text (14 pt) becomes about 28 pt (`--run-fs-body`; wiki page run-shell-device-profile-and-text-scale). Estimate used: average glyph width 0.55 em, so about 15 pt per character at 200 %. A phone-portrait safe width of about 360 pt holds about 24 characters per line; a chip or button with padding holds about 20. All widths below are **estimates from character counts, not measurements**. Existing HUD strings such as "Factory Sale available" (22 chars) and "Contracts !" already wrap or crowd the compact strip at 200 %, so persistent labels here are kept short. UI/UX must measure each string on its real element through `ShellRig` at 200 % for every `ShellScreens.All()` class.

## 1. Site name and tagline

| Id | String | Owner | WB version | Where shown | Chars | Fit at 200 % |
|---|---|---|---|---|---|---|
| S-01 | Meridian Works | World Designer (tone), UI/UX (fit) | v0.1 | Title surface, about line | 14 | One line (about 210 pt). Fits. |
| S-02 | Build the line. Feed the depot. | World Designer, UI/UX | v0.1 | Title surface only, not the HUD | 31 | Two lines. Acceptable on the title surface; not for any persistent label. |

## 2. Hub label candidates (OD-9b chooses; none is "Systems")

| Id | String | Owner | WB version | Chars | Fit at 200 % (chip with icon) | Note |
|---|---|---|---|---|---|---|
| H-01 | Works | World Designer, UI/UX | v0.1 | 5 | About 75 pt. Fits beside an icon. | Matches the site name. Short. Recommended. |
| H-02 | Office | World Designer, UI/UX | v0.1 | 6 | About 90 pt. Fits. | Reads as management and contracts. |
| H-03 | Operations | World Designer, UI/UX | v0.1 | 10 | About 150 pt. Tight beside an icon in a compact strip. | Clearest meaning, longest. Wrap risk at 200 %. |

All three pass the forbidden-term rule (no "Systems", no bare "Menu").

## 3. Milestone banner texts (R14; at most 2.5 s, non-blocking)

`{n}` and `{k}` are integers. A banner states the change and the unlock, with no hype words.

| Id | String | Trigger | Owner | WB | Chars (n=12, k=2) | Fit at 200 % |
|---|---|---|---|---|---|---|
| B-01 | Level {n}. {k} new locations open. | Factory level up with unlocks | World Designer | v0.1 | 31 | Two lines. Acceptable for a banner; must not truncate. |
| B-02 | Level {n} reached. | Factory level up, no unlock | World Designer | v0.1 | 17 | One line. Fits. |
| B-03 | New run begins. | Prestige completion (Factory Sale) | World Designer | v0.1 | 15 | One line. Fits. |
| B-04 | Deck extended. | Stage change (R15), with B-01 or B-02 | World Designer | v0.1 | 14 | One line. Fits. |

Offline milestones appear only in the offline summary (R14), with the same strings and no banner.

## 4. Stage names (R15; edges railing, wall, gantry)

| Id | String | Stage | Owner | WB | Chars | Fit at 200 % |
|---|---|---|---|---|---|---|
| G-01 | Open deck | Early (railing) | World Designer | v0.1 | 9 | One line. Fits. |
| G-02 | Walled yard | Middle (wall) | World Designer | v0.1 | 11 | One line. Fits. |
| G-03 | Gantry line | Late (gantry) | World Designer | v0.1 | 11 | One line. Fits. |

Shown only if UI/UX decides to show stages on the machine card or details. Stages reset with prestige (R15).

## 5. Depot and vein names (only if shown)

| Id | String | Owner | WB | Where | Chars | Fit at 200 % |
|---|---|---|---|---|---|---|
| D-01 | Depot | World Designer | v0.1 | Output-destination line on the machine card | 5 | Fits. |
| D-02 | Ore vein | World Designer | v0.1 | Accessible name and optional caption of the vein marker | 8 | Fits. |
| D-03 | Trunk | World Designer | v0.1 | Not shown in F006; accessible-name use only | 5 | n/a |

## 6. Existing strings kept (no change in v0.1)

"Factory Sale", "Contracts", "Market", "Research", "Overdrive", "Items" and "Mining" stay as they are (naming rule 2). They belong to earlier features and are listed so SC-013 finds no unowned world-facing string.

## Self-check read-throughs (producer self-check, not independent approval)

**Art Director lens (self-check).** The bible and strings set no palette, shape, perspective or material. "Dark base" is a mood and ground constraint only; the Art Director decides the exact value. Stage names map to the R15 edge sequence without specifying art. Open note for the Art Director: confirm that railing, wall, gantry is a fair visual reading of Open deck, Walled yard, Gantry line.

**UI/UX lens (self-check).** Persistent labels are at most 10 characters. Banners are at most 31 characters and may take two lines at 200 %; they stay static under reduced motion (R14). No string depends on color, an icon alone or a tooltip. Widths are unmeasured estimates; a measured 200 % pass on every screen class is still required. The tagline is kept off persistent surfaces because of its length.

Neither read-through replaces the independent Art Director or UI/UX review.

## Owner decision OD-9a (T504, 2026-11-11)

The owner approved World Bible v0.1 and froze it as `F006-WB v0.1`. QA PASS on 72f0ecf (#679, merged in #721).

- Site name "Meridian Works" and tagline "Build the line. Feed the depot.": approved as written.
- Stage names Open deck, Walled yard, Gantry line: approved as written.
- Vocabulary: "plate" and "bay" are removed from the tone vocabulary, because "Plate" already names an item ("Iron Plate", "Plate contract"). A machine stands on a **location**, matching the spec. The bible gains naming rule 6, and §1 now reads "machines sit at their locations".
- B-01 now reads "Level {n}. {k} new locations open." (31 characters at n=12, k=2; two lines at 200 %, must not truncate).
- Character counts corrected: S-02 is 31 characters.
- Everything else: approved and frozen. The hub label (H-01 to H-03) is still open and is decided in OD-9b (T509).
- Still open: the Art Director confirms "dark base" against the Art Bible v1.0.x ground value in the T506 briefs, and UI/UX measures the 200 % fit on real elements.

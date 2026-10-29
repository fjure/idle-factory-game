# Art Bible v1.0 review (Feature 004, T425, #537, FR-009)

Subject: `docs/art/F004-art-bible-v1.md` and `docs/art/F004-asset-briefs-v1.md` as merged by T424 (#536, PR #613, commit 62e1642, main a8b3eb6).

Reviewers (independent, read-only, none authored T424): a second-session Art Director, a UI/UX Reviewer and a Game Designer/Product Owner. They worked in parallel on the same commit. No reviewer opened an owner trial `key.json`. The parent recorded the three reports verbatim below and added the consolidated list. The parent did not change any verdict.

## Combined verdict

All three reviews: **APPROVE WITH CONDITIONS.** The owner decisions S1–S4 are encoded faithfully, and no gameplay, content or save scope is added. The F003-PAB supersession table is complete. The contrast figures in §9.4 reproduce.

The Art Director review raises one **blocker** (R1): the rim rule C5 contradicts §6.1 and §2, and the spike Miner fails it. The Art Bible therefore cannot be frozen unchanged at T426. The conditions below must be resolved first, either by a revision of the two documents or by a recorded owner decision.

## Consolidated conditions for T426 (parent synthesis)

| ID | Condition | Sources | Proposed owner |
|---|---|---|---|
| R1 | Rim and outline contrast rule. Pick option (a), a 1 px inner rim in the lightest ramp tone on floor-facing faces (amends §6.1 and §2), or option (b), a pass if the ink outline OR the rim reaches 3:1 against each backdrop tone, measured as a composite-mask percentage. Amend §2, §6.1, §9.3 and C5. | AD F1 (blocker); GD 11 | Owner decision; Art Bible revision |
| R2 | Backdrop under badge, marker and frame boxes. Replace "floor only" in C1 and C3 with an allowed backdrop tone set. Add a numeric acceptance: sample the backdrop under every box in the densest 12-slot frame at `n_min`. | AD F2 | Art Bible revision; T456, T429 acceptance |
| R3 | Footprint definition. §2 says a 70x35 diamond, §3.2 says 68x30. State the shape used for hit testing and the pivot-to-diamond offset. Make the dimensions whole and P1-compliant. | AD F3 | Art Bible revision |
| R4 | Lock-level digit. At scale 2 it is hidden on the emulator, 460 dpi, the landscape tablet and the 1280x720 phone. Either scale 3 (cap height 15.0 pt or more on every class) or an owner decision to accept hidden digits. In both cases the slot panel must name the level, verified at every class. | AD F4; UI 1; GD F1 | Owner decision; T429 acceptance |
| R5 | Lit pixels and accent masks. Define where lamp pixels live, how frame-0 lights-on is measured (body or body+accent), that a lit lamp on a stalled machine is accepted, and the rules accent mask ⊆ body mask, outline excluded, accent layers exempt from `check_outline`. The floor never sits on a lit layer. | AD F5, F13 | Art Bible revision |
| R6 | Running with reduced motion. Add a reduced-motion variant of SC-002 to T445. Add a test that every non-running `BadgeFor` result renders a non-empty badge. | UI 3 | T445, T429 |
| R7 | Markers (11x11). State a minimum size in pt or record a named deviation. Add markers to the T439 shuffled sheets and make marker and CVD naming blocking in T445, including 1280x720 at n=2. The marker at row 28 touches the plate apex. | UI 2; AD F10; GD 10 | Owner decision; T439, T445 |
| R8 | Empty running badge. Use a manifest entry with `intentionallyEmpty` and no PNG, which keeps SC-003 with no special case in `BadgeFor`. | AD F9; GD owner 1 | Owner decision; T429, T430, T437, T440 |
| R9 | New tooling task "Art-pipeline v1 checks" (Medium, no resources), after T426 and before T434–T438 produce their reports. Scope: integral-pivot assertion, id-level palette allowance, overlay keep-out, rim and backdrop contrast on composites, accent ⊆ body, the `intentionallyEmpty` manifest flag, and producing `palette-v1.json`. | AD F7; GD owner 2; PAB O-4 | Owner decision; new task |
| R10 | `tasks.md` text drift. T429 (cue variant a, anchor, accents, `labelPolicy`), T431 (accent allocation), T434 (no body variants; accents), T435 and T437 (lock digits are labels; floor tile size vs T456 order), T438 (UI icons scripted only), T430 and T440 (`intentionallyEmpty`), T442 and T444 (allocation run, DEV-1/DEV-2), T445 (markers), T426 (`palette-v1.json`). | GD 2–10; AD F6 | Parent; ledger refresh and maqa-sync |
| R11 | Crispness definitions (§10). Anchor the test to the world art grid. Require every run to be a multiple of n, not that the gcd equals n. Add an odd-dimension viewport case. The float-tolerant pivot assertion is `< 1e-3`. | AD F8 | Art Bible revision; T427, T430 |
| R12 | ComfyUI pin v0.43.0. Patch `contracts/art-pipeline.md`, `plan.md` and `research.md` (v0.38.0 text) before T436. | AD F12; GD owner 3 | Revision |
| R13 | T441 acceptance additions: tinted-contrast table, blind naming of confusable icon pairs at 24 pt and at 200 % text, 2 px stroke capture at 420 dpi, and a USS spinner stop under reduced motion. | UI 7–10 | T441 text |
| R14 | APK margin of 1.01 MB. Accept the budget, and measure an APK delta checkpoint after the first imported family (T440). Any readability cut needs an owner decision. | GD owner 4 | T440 text |
| R15 | Minor notes. Background seam: `ui_bg` #1b1f2a vs HUD `--bg` #111827. C4 names `ui_line` and `ui_panel`, which no asset uses. Make the flash headroom for the Assembler (3/s) a T434 requirement. Define loop resume after waiting or paused (T429). DEV-1, DEV-2, AR-1 and AR-2 are acceptable as written, and T442 asserts the actual n per class. | UI 4, 5, 10, B; AD F11, F13; GD 11 | Art Bible revision; T429, T434, T442 |

## Art Director, second session (independent, read-only; Sonnet)

**Verdict: APPROVE WITH CONDITIONS.** It is complete and mostly consistent, and the supersession table is complete. Four items must be fixed before T426 freezes it (F1 to F4). I did not edit, comment, or open any `key.json`. I re-ran `art_pipeline.color.contrast_ratio` on the palette and token pairs. All 39 pass and 14 fail figures in §9.4 reproduce. I also analysed the committed spike sprites `s3-lightB-body-run_00.png` and `s3-lightB-accent-run_00.png`.

### Findings

1. **BLOCKER. Rim rule C5 contradicts §6.1 and §2, and the spike does not meet it.** §9.4 C5 and §9.3 require a light rim tone (`steel_2`, `steel_3`, `copper_2`, `teal_1`) on the whole body silhouette over floor. §6.1 forbids selective or coloured outlining. §2 says right and front faces are darkest.
   - Measured on the spike Miner, `s3-lightB-body-run`: only 42 of 100 rim pixels reach 3:1 against `floor_1`. 39 are `steel_1` (2.0 to 3.1 against the floor).
   - So the S3 and S4 bodies that passed readability would fail C5. The 14 failures are not really solved. They are redefined, and C5 accepts the ink outline as the carrier over plates (`ink_0` against `plate_1`, 4.14) after §9.3 rejects exactly that argument over floor.
   - Fix before T426, owner choice:
     - (a) Allow an explicit inner 1 px rim in the lightest ramp tone on floor-facing faces only. This amends §6.1 and §2.
     - (b) Use a single rule: a graphic passes if its ink outline OR its rim reaches 3:1 against each backdrop tone it can sit on. Make the pass criterion a composite-mask measurement with an agreed percentage.
   - T434 cannot pass its own check as written.

2. **MAJOR. C1 and C3 (badge and frame over floor only) look unsatisfiable on the lattice.** The badge box is rows 10..32, 23 px tall and 24 px above the footprint top. With a 60x30 pitch, the back-right neighbour's plate (rows 14..49) lies behind it. §3.5 and R-2 defer this to T456 with no number.
   - Badge fill against `plate_2`: `cue_alert` 1.26, `cue_info` 1.46, `cue_warn` 1.51.
   - The `ink_0` badge outline then carries it. That works over light tones but not over `steel_0`, `copper_0`, `teal_0`, `dim_1`, `warn_0`, `plate_0` or `floor_2`. There ink is 1.9 to 2.9 and several fills are below 3.
   - Fix: replace "floor only" with "backdrop tone set under every badge, marker and frame box ⊆ the allowed list". Add a numeric T456 and T429 acceptance: sample the backdrop under every box in the densest 12-slot frame at `n_min`. Owner: T456 and T429, text amended at T426.

3. **MAJOR. "Footprint" is defined two ways.** §2 says the plate is a 2:1 diamond of 70x35 px. §3.2 gives the slot, machine and plate footprint as 68x30, which is 2.27:1.
   - The diamond centre is at row 46.5 and the pivot is at row 49. That is 2.5 px apart.
   - 35 is odd and breaks P1.
   - T456 "footprint-only hit testing" and the floor tile (60x30 is the natural candidate) cannot be built without a definition.
   - Fix: state whether the footprint is a bounding box or a diamond, state which shape hit testing uses, and name the pivot-to-diamond offset.

4. **MAJOR. Lock digit at scale 2 (cap 10 art px) is hidden on the emulator and most classes.** Cap heights from §8.4: emulator 11.4 pt, tablet landscape 10.7 pt, 460 dpi 10.4 pt, 1280x720 phone 10.0 pt. The 12 pt rule hides it everywhere except the 320 dpi phone and portrait tablet.
   - The player then cannot tell which level unlocks a locked slot on the project's own validation device.
   - Fix: scale 3 (cap 15 art px, a 9x15 digit on a 70x35 plate). Cap height is then 15.0 pt or more on every class (15.6 pt on 460 dpi, 17.1 pt on the emulator, 15.0 pt on the 1280x720 phone).
   - Contrast of `ui_text` on `dim_0` / `dim_1` is 11.55 / 7.49, so it passes.

5. **MAJOR. Lit pixels are under-specified, and the "no body variant" decision has a side effect.**
   - The spike body frame 0 already holds 24 `lit_*` pixels, and the accent adds 42. Non-running machines (inactive, paused) therefore show lit lamps. Bible §6.2 says lit tones appear only on lamps and the accent. `check_frame0_lights_on` counts lit pixels in the frames it is given.
   - I accept the decision itself, because SC-001 and SC-002 passed with it, but §6.2 and §2.1 must state:
     - where lamp pixels live;
     - that frame 0 lights-on is measured on the body plus accent composite, or on the body alone;
     - that a lit lamp on a stalled machine is accepted.
   - The accent pixels in the spike are 100 % inside the body mask and never on ink. Make that a rule: accent mask ⊆ body mask, outline excluded, and `check_outline` exempt for accent layers.

6. **MAJOR. `tasks.md` text drifts from the brief.**
   - T434 still says "cue-variant body treatments".
   - T435 says "scripted or licensed digits" for the locked plates, but the brief makes the digits separate labels.
   - Nothing assigns `F004-ACCENT-*` or `F004-LABEL-LOCK-*`. The brief §7 gives accents to T434, but the T434 text is Blender only.
   - `palette-v1.json` is promised "at the freeze", but T426 is parent-only with no repository code.
   - Fix at T426: amend T434, T435 and T437, and name a producer for the palette file.

7. **MAJOR. O-4 ownership.** T437 owns `tools/art-pipeline`, but it runs in parallel with T434 to T436, which need those checks.
   - Recommend one new small task "Art-pipeline v1 checks" (Medium, no resources), after T426 and before T434 to T438 produce their reports. Scope:
     - `check_pivot` integral-pivot assertion;
     - id-level palette allowance (`steel_2`, `steel_3` for resources);
     - overlay keep-out (rows 0..33, columns 15..71);
     - rim and backdrop contrast on composites (F1, F2);
     - accent ⊆ body;
     - `intentionallyEmpty` manifest flag;
     - `palette-v1.json`.
   - T430 only imports.

8. **MINOR. Crispness rules (§10) are sound but need sharper definitions for T427.**
   - "Vertical edges on the grid" must be anchored to the world art grid, not screen position. Under a one-device-pixel pan the edge moves, so run lengths are the right test.
   - "gcd of runs equals n" fails on flat areas where every run is 2n. Use "every run is a multiple of n" instead.
   - Add an odd-dimension viewport case.
   - Pivot (16,5) / 24 = 0.208333 is not float-exact (15/64 is). T430 should assert `|pivotPx − round| < 1e-3`.
   - P1 and the 20x10 footprint are otherwise sound.

9. **MINOR. Empty running badge.**
   - `check_pivot` requires a non-empty bounding box, so a transparent PNG fails it.
   - Prefer a manifest entry with `intentionallyEmpty` and no file.
   - Decide this at T426, since T429, T430 and T437 depend on it.

10. **MINOR. Markers (11x11).**
    - The 9x9 interior makes 2 px strokes tight.
    - `cvd_green` and `cvd_pink` luma are about 121 and 142, close in grayscale, so shape must carry them.
    - The markers have no SC-001 evidence until T445. Add them to the T439 shuffled sheets.
    - Marker row 28 touches the plate apex at row 29.

11. **MINOR. UI tokens versus palette `ui` hexes.**
    - Keeping the F003 token hexes is fine.
    - The camera clears to `ui_bg` #1b1f2a, but the HUD `--bg` is #111827, so a seam is visible.
    - C4 mentions `ui_line` and `ui_panel`, which no asset uses.
    - Note both in the Bible.

12. **MINOR. ComfyUI v0.43.0 versus the contract's v0.38.0.**
    - The pin is correct (the live-validated version).
    - Condition: revise `contracts/art-pipeline.md` at T426, before T436 starts.

13. **Lighting B (checked, sound).**
    - The L1 accent arithmetic holds: 491,520 B, 61 renderers, 0 Light2D.
    - The L2 envelope is sensible and reserved.
    - Add: the floor must not sit on a lit layer, or §9 contrast is invalid.
    - Flash: the Assembler is already at 3 per second, so the composite with the accent needs headroom.

14. **Supersession table: complete.** Every F003-PAB section 1 to 13 and AD-1 to AD-10 is mapped.

### Conditions for T426 sign-off
1. Decide F1 (rim rule) and amend §6.1, §2, §9.3 and C5 accordingly.
2. Amend C1 and C3 as in F2, with a numeric T456 and T429 backdrop acceptance.
3. Define the footprint and pivot offset (F3).
4. Set the lock digit to scale 3 (F4), or the owner accepts hidden digits.
5. Add the lit-pixel and accent mask rules (F5).
6. Fix the `tasks.md` drift and name the `palette-v1.json` producer (F6).
7. Create the new checks task (F7).
8. Tighten the §10 definitions (F8).
9. Decide the empty running badge (F9).
10. Revise the ComfyUI contract text (F12).

## UI/UX Reviewer (independent, read-only; Sonnet)

Reviewed: `docs/art/F004-art-bible-v1.md` (§4, 8, 9, 10, 11, 17, 19) and `docs/art/F004-asset-briefs-v1.md` (§5) at a8b3eb6. I did not re-open the spike measurements, spec.md, the contracts or the wiki pages. I took the S3/S4 numbers as quoted in the Art Bible. I did not open any owner trial `key.json`.

**Verdict: APPROVE WITH CONDITIONS.** The state-cue model is sound, and shape is the primary carrier. No blockers. Three majors need a decision or a recorded owner before the T426 freeze.

### Findings

1. **Major. The lock digit is hidden on the primary test device. Art Bible §8.4, "Locked-plate digit".**
   - Evidence: the 12 pt threshold gives cap heights of 11.40 pt (emulator 420 dpi), 10.71 pt (tablet landscape), 10.41 pt (460 dpi) and 9.98 pt (1280x720 at n=2). The digit shows only on 320 dpi phones and the portrait tablet.
   - Padlock and hatch still identify "locked", so identification is not lost. The unlock level is lost at a glance on most classes. The emulator misses the threshold by 0.6 pt.
   - Fix: either record that the emulator, 460 dpi and landscape tablet never show the level in the world and that the panel is the only source, or apply a documented tolerance. A 1 % tolerance, as in `n_min`, would not rescue 11.40.
   - Required regardless: before sign-off, T425/T426 must confirm that tapping a locked slot opens a panel that names the level (§8.4 delegates this check to T425). I could not verify the panel text from the documents I read. T429 or the T456 hit-test must cover it.
   - Owner: T426 decision, T429 (`labelPolicy`).

2. **Major. Markers are not validated for legibility or CVD. Art Bible §8.6, §3.2.**
   - Evidence: 11x11 art px at `n_min` is about 16.5 pt (320 dpi, n=3), about 12.5 pt (emulator), about 11 pt (n=2, 1280x720). That is roughly half the 24 pt cue rule. §8.6 states "Markers are not part of the S3 gate evidence".
   - `cvd_green` and `cvd_pink` are separated by shape only (double chevron vs bolt), at 11 px. Fill vs floor is 3.23 and 3.61 against `floor_1`, which is only marginal against the 3:1 threshold.
   - Boosted vs Overdrive is a new, unproven pair, and no minimum point size is stated.
   - Fix: state an explicit marker minimum in pt, or accept the sub-24 pt size as a named deviation. Make marker naming, including CVD, a blocking part of T445 (already O-7). Run the T445 check at the smallest class (1280x720 at n=2) too.
   - Owner: T445, T426.

3. **Major. There is no positive "running" cue when reduced motion is on. Art Bible §6.4 and §8.1.**
   - Evidence: running is "read by absence" of a badge, normally backed by the animation. Reduced motion (FR-020) forces frame 0 for every entry, so a running machine is a static frame with no badge.
   - A machine whose badge failed to render, or whose state is not yet resolved, looks identical to running. §6.4 says frame 0 is lights-on "so a frozen running machine still reads as running". That is the same lights-on frame as the paused body, since S3=b keeps the body unchanged. Only the badge distinguishes states.
   - Acceptability: SC-002 passed 10/10, median 1.077 s, with motion. Absence-as-running is acceptable with motion on. The reduced-motion case was not measured.
   - Fix: add an SC-002 reduced-motion variant in T445. State that the machine panel names the state in text, which it does. Add a test that every non-running `BadgeFor` result renders a non-empty badge, so a missing sprite cannot silently show as running.
   - Owner: T445, T429.

4. **Minor. DEV-1 and DEV-2 (§4.3) are acceptable.**
   - DEV-1: 0.05 pt short, so physically negligible. DEV-2: 22.95 pt, 4.4 % short, on a 23 px glyph in a non-interactive world cue. The 44 pt touch rule applies to chrome, not to world cues, so no touch-target regression.
   - Owners T427 and T442 are adequate, since both gate on measured shipped-build figures.
   - Condition: DEV-2 exists only if the T427 tolerance gives n=2. T442 should assert the actual `n` per class rather than the derived figure.

5. **Minor. AR-1 and AR-2 (§4.2).**
   - Fit-all-only on 3 classes (phone 720x1280, phone 1280x720, portrait tablet) means no zoom-out, with 12 slots always fitting. That is acceptable for readability, and it is the largest scale at which all slots fit. The risk is a HUD-aware overlap at fit-all, owned by T427, and T442 is the confirming gate.
   - Gap: the portrait tablet carries a 28.3 % margin while zoom-in tops out at `maxScale` 6. No finding, only a note for T442.

6. **Minor. Grayscale and CVD of badges (§8.2).**
   - Waiting and storage full have luma 180 vs 182, so grayscale cannot separate them and the glyph does the work (hourglass vs stacked bars). The smallest deuteranopia distance is 24.8, with 9/9 blind naming in all four modes. That is adequate for screening.
   - "Cool waiting, warm problem" is supplemental and the glyph carries the meaning, which is compliant with FR-010.

7. **Minor. UI icons and tint contrast (§11, briefs §5).**
   - Run R2 evaluates `ui_text` before tint only. The rule says "contrast is evaluated after tint" (§11), but no tinted value is reported for `--text-muted` (disabled) or `--text-primary` against `--surface-raised` #374151.
   - Fix: add the tinted ratios to T441. For non-disabled glyphs the 3:1 non-text floor applies.
   - Owner: T441.

8. **Minor. Confusable icon pairs at 24 px (briefs §5, `confusionPeers`).**
   - The hardest are `RESEARCH-NODE-OWNED` vs `MAXED` (check with a level tick vs check with a double ring, at most 6 primitives), `OVERDRIVE-READY` vs `ACTIVE`, `INFO` vs `WARNING`, and `CASH` vs `UPGRADE-COST`.
   - Fix: T441 should run a blind shape-naming pass on these pairs at 24 pt, as well as at 200 % text.

9. **Minor. Non-integer UI scaling.**
   - §11 admits UI Toolkit scales by a non-integer factor, so 2 art px strokes can render unevenly at, for example, 2.631 px/pt, with point filtering and no mipmaps.
   - The 24 pt glyph is excluded from the crispness rule. T441 should capture on the emulator at 420 dpi and confirm a 2 px stroke never vanishes.
   - Related: icons are fixed at 24 pt while text scales to 200 %. T441 owns the bounds check.

10. **Minor. Spinner and reduced motion (briefs §5, row 66).**
    - "Static frame, rotated by USS" is not covered by the reduced-motion rule, which is stated only for world art (§6.4). State that the USS rotation stops under reduced motion and that a static frame plus text ("Saving...") carries meaning.
    - The flash rule is world-only. The Assembler run measures 3 flashes per second, exactly at the limit. T434 "should aim lower", and I recommend making that a requirement, not a preference.

11. **Minor. Palette vs token mismatch (§11 observation).**
    - The token hexes (#111827 / #1F2937 / #374151) and palette `ui` hexes differ but are the same slate family. This is a risk for T441 integration if both appear side by side.
    - It is correctly marked out of scope, so there is no action unless a UI sprite is placed against `ui_bg`. Rule C4 already covers `ui_line`.

### Empty / error / disabled coverage
Empty states are text only and error states use `WARNING`, with `RETRY` and `CLOSE` reused. Disabled uses `--text-muted`. This is adequate. The only gap I found is finding 7.

### Ambiguities for T429 / T433 / T441
- A. The §8.4 `labelPolicy` is stated only for the locked digit. "No label" is allowed, but the Art Bible does not say whether a hidden digit leaves any in-world level cue. Add one sentence, per finding 1.
- B. §8.1 says "runs frame 0" but does not say whether a machine entering a waiting or paused state resets the loop on resume. T429 should define it to avoid a visible jump.
- C. O-4 (tooling checks) is unowned in `tasks.md`; the parent should assign it before T426.

### Conditions for T426
1. Record the owner decision for finding 1: the level digit is not shown on the emulator, 460 dpi and landscape-tablet classes, and the panel names the level. Include confirming that the panel text exists.
2. Define a marker minimum point size or accept a named deviation (finding 2). Make marker plus CVD naming a T445 blocker.
3. Add a reduced-motion variant to T445 SC-002 and the non-empty-badge assertion (finding 3).
4. Add T441 acceptance items: tinted-contrast table, confusable-pair naming, 2 px stroke capture, spinner reduced-motion rule (findings 7 to 10).
5. Assign O-4 to a task.

## Game Designer / Product Owner (independent, read-only; Sonnet)

Reviewed `docs/art/F004-art-bible-v1.md` (PAB) and `docs/art/F004-asset-briefs-v1.md` (AB) at a8b3eb6. Read-only. I did not open `key.json`. I read the files directly. I did not re-check the style-spike record or the wiki pages against the Art Bible's quotations of them.

**Verdict: APPROVE WITH CONDITIONS.** The owner decisions are encoded faithfully, and no gameplay, content or save scope is added. The findings below are ownership mismatches in `tasks.md` and open owner calls. None reopens a style decision.

### Q1 Owner decisions
- **S1, S2, S3, S4 and IoU** are encoded as decided (PAB s0, s2.1, s6.5, s7, s8).
- **S3 = b** is encoded with 23 px on every screen class, anchor (59,33), no badge on running machines, and the T460 hourglass.
- **IoU** applies to bodies only. Badges and accents are excluded.
- **S4 = lighting B** ships as L1, an accent sprite. L2 (Light2D) is reserved, needs a minor version, and is not adopted.
- **Scope check:** the AB adds F004-ACCENT-*, F004-LABEL-LOCK-L2..L5, F004-FLOOR-TILE and F004-UI-ICON-SETTINGS. Each is a presentation key. The settings gear is already in scope through FR-020a and T433.

### Q2 Gameplay meaning
- All 29 F003 keys keep their ids (AB s2). The 11 `presentationKey` values are unchanged. Precedence is carried over from `SlotGridModel.BadgeFor`.
- Under S3 = b every non-running state shows frame 0 of the run loop, so the **badge is the only state carrier**. Paused, inactive, waiting and the other badge states look identical on the body. This is deliberate and matches the owner's trial. The badge confusion peers are recorded: missing-input vs storage-full, paused vs inactive.
- The lock-level digit is hidden below 12 pt cap height. That affects 460 dpi phones (10.41 pt) and 1280x720 phones (9.98 pt). The lock itself stays visible through the padlock and hatch.

### Findings
1. **Medium, PAB 8.4 / T425 checklist.** The hidden digit loses the lock level only if the build/expand panel does not name it. The PAB asks T425 to check this, and it cannot be proved from these two documents. Fix: the T429 QA checks that the panel text names the level at every screen class.
2. **High, `tasks.md` T429.** Its text still says "body-state field ... for cue variant a" and "cue top-right in the overhang band, markers top-left". The Art Bible anchors the badge at bottom-centre (59,33) and uses the marker boxes M1/M2. T429's text also does not mention the F004-ACCENT-* renderers (at most 12), the empty running-badge handling, or `labelPolicy`. These are PAB O-6 and s2.2. Fix: refresh the T429 text at T426.
3. **High, no task owns accent integration.** Nothing in `tasks.md` wires F004-ACCENT-* into the catalog and view, or drives the accent frame index from the shared clock. PAB s2.2 assigns the allocation test to T431, but T431's text covers neither. Fix: add accent scope to T428/T429 and T431 at T426.
4. **Medium, T434 (the author's remark).** The text still lists "still states and the cue-variant body treatments". PAB s8.1 and AB s2 say these do not apply. Fix: edit the T434 text to "frame 0 of the run loop is the still state, no body variants".
5. **Medium, T435 vs T456.** T435 says locked plates have "scripted or licensed digits". The AB makes the digits separate overlay labels (F004-LABEL-LOCK-*) owned by T437, and T437's text omits them. The floor tile size comes from T456, but T435 depends only on T426 and T456 depends on T427/T428. Fix: clarify T435 and T437, and order T456's lattice period before T435's floor tile or make T435 propose the size.
6. **Medium, T438.** The text allows ComfyUI for illustrative UI icons. The AB routes every UI icon to scripted and says a route change needs a brief revision. Fix: align the T438 text.
7. **Medium, T430.** The text does not mention the manifest flag for an intentionally empty sprite. Fix: add it to T430 and to T440 (the AB s2 names T429, T430 and T440).
8. **Medium, T426.** The text omits producing `palette-v1.json`. PAB s18 expects it at the freeze. Fix: add it to the T426 text.
9. **Low, T442/T444.** PAB s2.2 credits T442 with the Windows-player allocation run. The 600-frame run is in T444's text. T442 is also silent on DEV-1/DEV-2 confirmation. Fix: align the text.
10. **Low, T445.** The PAB says markers are "named in T445". T445's text does not say so. Fix: add it.
11. **Low, PAB s9.3, s11.** The outline-contrast reading ("rim tone") and the token-hex vs `ui` palette difference are flagged "for T425 to challenge". I do not challenge either. Both need the owner's recorded acceptance.

### Q5 Status, semver and supersession
**Sufficient.** The supersession table (s17) is section-level, semver is in s16, and the status line is explicit. Conditions at T426:
- Flip "draft" to approved in both headers (a patch change).
- Record the reviewed commit hash.
- Produce `palette-v1.json`.
- Update the task texts per findings 2 to 10.
- Decide the spike import path.

### Owner decisions needed at T426
1. **Running badge.** Keep the declared empty frame. This keeps SC-003's "every key resolves to F004" and avoids a special case in `BadgeFor`. The cost is the manifest flag (finding 7).
2. **O-4 tooling follow-ups** (pivot integral check, id-level palette allowance, keep-out check, rim-contrast check). They have no owner, and T434 to T437 run in parallel and need these checks. Recommend adding one small tooling task after T426 and before T434.
3. **ComfyUI pin.** Pin v0.43.0, the version validated live. Patch `contracts/art-pipeline.md` and also `plan.md` line 17 and `research.md` line 94, which carry the v0.38.0 text. PAB R-4 mentions only the contract.
4. **APK margin of 1.01 MB** (budget 10, PAB R-3). Accept the budget. Add an early checkpoint: measure the APK delta once the first family is imported (T440), and require an owner decision before any readability cut.
5. **Rim-contrast interpretation, DEV-1 and DEV-2, and the token/palette hex difference.** Accept all three as written.
6. **Palette cap.** It is full at 48, so any future growth is major. Accept this and record R-5.


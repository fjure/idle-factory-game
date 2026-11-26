# UI/UX review, F006 final gate (T543, issue #715)

**Status: independent UI/UX Reviewer report (read-only, Medium tier).** I did not write the reviewed UI. I did not read `playtest-agent-f006-final.md` or its captures before writing this. Findings and dispositions are proposals for the owner. Captures are in `uiux-f006-final/` (full size, device pixels; the prefix letter groups a series).

## 1. Header

| Item | Value |
|---|---|
| Build | merged main `437cb46c` (branch HEAD `d55dcc9d` only adds the Play-Tester's docs). The dev APK installed on the AVD by the Play-Tester (`-noProfilerAutoconnect`, installed 09:27) was reused, no rebuild, no Unity run |
| Device | AVD `IdleFactory_Test_API_36`, headless host GPU. Input only `adb shell input` (tap, keyevent), captures only `adb exec-out screencap`. No host mouse, keyboard or window, no UnityMCP |
| Classes | Phone portrait 1080x2400 at 420 dpi, 100 % and 200 % text. Phone landscape 2400x1080 at 420 dpi. Small phone 720x1280 at 320 dpi (360x640 dp). 640x360 at 160 dpi, 200 % text. Tablet landscape 2560x1600 at 240 dpi. Desktop window: the existing Windows StandalonePlayer capture (T562, `trials-f006/sc005/tabL/p3`, 2048x1536) only; I ran no Windows session |
| Fixtures | Scratch (not committed): the Play-Tester's L2 checkpoint (factory level 2, 3 Miners, 1 Smelter, 5.0M Cash, store full after offline credit) and variants made with a script that rewrites the data file and re-derives the manifest (same method as `Get-W005LSet`): `N` (L2, departure just now, empty store, no Welcome back, no problem badge), `G` (factory level 5, 12 machines: 6 Miners and 6 Smelters), `n2` (L2 with two Miners). A fixture with 100000 Iron Ore, and factory level 6 or 9, were rejected by the save invariants and gave the designed "The saved factory could not be loaded" dialog; I kept one capture as an error-state sample (`e01-land-invalid-fixture-error-dialog`) |
| Cleanup | `font_scale` 1.0, `wm size` and `wm density` reset (1080x2400, 420). `adb emu kill`, `adb kill-server`, the leftover `emulator.exe` stub killed; no emulator, qemu or adb process remains. The game stays installed. Leases untouched (held by `issue-715`). No tracked file edited; no APK or build output created by me |

## 2. Verdict

**ACCEPT_WITH_NOTES.** No blocker and no Major. Every carried batch-C finding I could re-check on the device is fixed or reads as intended: the closed-state camera now frames the depot (U-C1), the store-full marker is a distinct glyph from the machine badge (U-C2), Back works on every sheet (C-85), the build card at 640x360 and 200 % has whole-word Cancel and Confirm (U-C5), and the tablet build card is clean (U-C7). What stays is Minor or Info and is listed in section 4. Not covered: keyboard focus order (adb key events produced no visible focus movement), a Windows build card (no existing capture), zoom and Back-state threshold fractions.

## 3. Carry results

| Carry | Result |
|---|---|
| **C-86** device Back on every sheet | **PASS.** Portrait: hub open, then each of Market, Contracts, Items, Research, Industry, Overdrive, Factory Sale, Settings; one Back returns straight to the closed factory every time (`m-*-back1`), a second Back is harmless. Landscape: hub, Contracts, one Back to the closed factory (`e05`, `e06`). Machine card: one Back closes it (`c02`, `c03`). The hub-to-factory order is unchanged from T535 (U-C9). After Back the Office button keeps a white focus ring in some frames (`m-overdrive-back1`, `m-settings-back1`): consistent with a focus-visible ring, not a defect |
| **C-86** focus ring on Upgrade after the strip-chip tap | **PASS.** The chip tap selects the Smelter, opens the card and Upgrade carries a clear white ring (`c01-chip-tap`) |
| **C-86 / D1** 640x360 at 200 %, build card Confirm/Cancel vs the dev-build label | **PASS with a dev-only note.** Cancel and Confirm are whole words (the mid-word break of U-C5 is gone), each about 50 px high at 160 dpi (at least 48 dp). Ghost and selected slot are visible with a blue ring; the card shows 2 type rows (it scrolls). The Development Build label overlaps the bottom edge of Confirm (`h-640x360-fs200-build`): dev builds only (F-1) |
| **C-86** tablet build card | **PASS** (`j-tablet-build`): all 4 type rows visible, Cancel and Confirm large, ghost and first column clear of the side panel. The idle hint is replaced by the card (U-C7 closed) |
| **C-88** marker tap at 40 px | **PASS at 40 px, edge between 40 and 60 px.** Marker (Iron Ore, trunk junction) centre taken from `c04`, 1080x2400 at 2.63 px/pt. A tap 40 px up, left, right or down selects the Smelter (`d-off-up-40`, byte-identical to the centre-tap frame `c04`: same selected state); 60 px and more up selects nothing (`d-off-up-80`). Left 60 and down 70 still selected (confounded by neighbouring machine shapes). So the effective upward reach is about 15 to 23 pt, consistent with the 24 pt radius of a 48 pt diameter halo (about 63 px). Centre estimate is accurate to about 10 px. Q1 answered by the code (section 9) |
| **C-88** marker, store-full and chip taps | Store-full marker tap selects the first full Miner ("Store is full", `c02`), shortage marker tap selects the Smelter ("Short on Iron Ore", `c04`), chip tap selects the Smelter (`c01`). The T563 upgrade marker tap selects its machine (`b04`) |
| **C-88** Back-state threshold | **Not measured**, no state change by Back seen in any capture. Provisional |
| **C-88** landscape crop right edge vs HUD column | **PASS.** At 2400x1080 the world (depot plate included) ends about 8 % of the width left of the HUD column (`e03`); at 2560x1600 the depot is left of the side panel (`j-tablet-G12`) |
| **C-88 / C-82** landscape, zoom, hub tap fractions | Hub entry measured landscape (0.951, 0.062) and Contracts row (0.73, 0.317) work; portrait hub entry (0.093, 0.971) and row centres at y = 0.507, 0.565, 0.623, 0.679, 0.738, 0.794, 0.851, 0.908 (x 0.46) work. **Zoom fraction and card-column fractions not run: provisional** |
| **C-98** upgrade marker per class | See section 5. Drawn on every captured class, on a full 12-machine grid and in the neutral state. Hidden on boosted machines (accepted, not filed) |
| **D4** plate tint | **PASS** on phone and tablet; grayscale and deuteranopia checked on a phone capture (section 6). Desktop: **not covered** (no build-card capture exists on the Windows path) |
| **D5** store-full marker at n=2 and n=3 | **PASS.** n=3 (`a02`, `e03`) and n=2 (`k-n2-storefull-p100`): a white box glyph on the depot plate, crisp at integer scale, clearly unlike the yellow per-machine badges. Shimmer on pan or zoom: not testable with adb (C-75 stays an art decision after P4, record only) |

## 4. Findings

Severity: Major = a spec or checklist item fails; Medium = a clear usability or visual defect on a supported class; Minor = rough edge; Info = observation or positive result.

| ID | Sev | Observed | Evidence | Proposed disposition |
|---|---|---|---|---|
| F-1 | Minor (dev only) | At 640x360 and 200 % text the "Development Build" label overlaps the bottom of the Confirm button. Not present in release builds | `h-640x360-fs200-build` | Record. If wanted, move the label in dev builds only |
| F-2 | Minor | The tablet side-panel hint ends with a one-word last line ("it.") and stays on screen in every closed state, including with problems | `j-tablet-N`, `j-tablet-L2` (C-75 N4) | Copy tweak, art/copy task after P4 |
| F-3 | Minor, unconfirmed | The first world tap about 20 s after a cold start was ignored in 2 of 6 attempts (portrait 200 % build slot, machine card test); the second identical tap worked. Could be my 20 s wait or a first-tap focus effect. Not reproduced under a timer | `i-p200-N` then `i-p200-build` | Reproduce with a longer settle before filing |
| F-4 | Info (owner decided: Overdrive confirm goes on the polish list after F006) | Tapping the Overdrive hub row activates Overdrive immediately (one tap, no confirm). My sweep did this by accident; later captures show the boosted look (pink markers, no upgrade markers). Hidden-on-boosted is the accepted decision | `m-overdrive`, `m-sale` | Owner decision: polish list after F006 |
| F-5 | Info | Keyboard focus order is not covered: adb `KEYCODE_TAB` and `DPAD_*` produced no visible focus change on the open machine card | `n0-keyboard-tab-no-visible-focus-card` | Covered by the Play Mode focus tests; not device-verified |
| F-6 | Info | In the 12-machine grid at 640x360, the Smelters out of ore show the red missing-input badge and no upgrade marker, as designed (hidden under a problem badge) | `h-640x360-fs200-G12` | None |
| F-7 | Info (positive) | U-C1 closed-state fit: the L2 portrait state now shows the vein, depot, shortage marker (Iron Ore at the trunk junction) and store-full marker without opening a sheet. U-C5 and U-C7 fixed (above). Items empty state ("Nothing here yet. Earn them from contracts.") and the error dialog (three clear actions) read well | `a02`, `m-items`, `e01-land-invalid-fixture-error-dialog` | Closed |
| F-8 | Info | Welcome back at 640x360 and 200 % text scrolls, with Continue always visible; at 1080x2400 it shows the full text. Contracts sheet text in landscape at 100 % is small but readable | `h-640x360-fs200-welcome-modal`, `e05` | None |

## 5. C-98 detail: upgrade marker per class

A world cell is 24 art px (`SlotLattice.Density`); a plate is 70 art px, about 2.9 cells. The marker cutoff is panel points per world cell: `MarkersVisibleAt(ppc) => ppc >= 24 * 0.99` (`SlotGridView.MarkerMinimumPixelsPerCell`), so the visible threshold is 23.76 pt per cell. I measured the plate width on each capture (about 10 % accuracy), divided by the nominal px per point (2.63 at 420 dpi after the 7 mm raise, 2.0 at 320 dpi, 1.5 at 240, 1.0 at 160) and by 2.9.

| Class | Plate width px | pt per cell | Margin over 23.76 | Marker drawn | Evidence |
|---|---|---|---|---|---|
| Phone portrait 1080x2400, 100 % | 205 | about 27 | +3 | yes, also 12 markers on the full grid | `b01`, `f01-grid12-p100` |
| Phone portrait, 200 % text | 205 | about 27 | +3 | yes | `i-p200-N` |
| Phone landscape 2400x1080 | 190 | about 25 | +1 | yes (Smelter; Miners show store-full badges) | `e03` |
| Small phone 720x1280 (360x640 dp) | 140 | about 24 | +0.3 (at the cutoff) | yes, 12 on the full grid and neutral | `g-small720-G12`, `-N` |
| 640x360 at 200 % | 80 | about 28 | +4 | yes, about 16 px wide | `h-640x360-fs200-N`, `-G12` |
| Tablet 2560x1600 | 250 | about 58 | +34 | yes, 12 on the full grid | `j-tablet-G12`, `-N` |
| Desktop window (Windows, 2048x1536) | n/a | n/a (96 dpi, T562 note) | n/a | yes, 12 on 3 rows, readable under deuteranopia | `w-tabL-win-deuteranopia` |

No captured class loses the marker, but the phone classes sit at or just above the cutoff: the small phone is within measurement error of it, and a smaller screen class or a coarser density would lose the marker, as the T562 note says. The neutral-badge state is the `N` fixture (marker on all four machines). The chevron is white with a dark outline and reads on the dark plates; on boosted machines it is hidden (accepted by the owner, not filed).

## 6. D4 plate tint, grayscale and deuteranopia

Capture `g-small720-build` (build card open, phone). Sampled sRGB of an empty plate is (90, 99, 120), of an occupied or dimmed plate (45, 50, 60): luminance about 99 against 50 of 255, roughly 2:1. The tint is a luminance difference, so it survives grayscale (99 vs 50) and a deuteranopia matrix (Vienot, applied in gamma space as an approximation: plates (93, 93, 114) vs (47, 47, 57)). The selected ghost keeps a blue ring and a lit teal body in all three renderings (`p-d4-small720-normal-deuteranopia-gray`). Tablet (`j-tablet-build`) and portrait 200 % (`i-p200-build`) read the same. Desktop: the existing Windows sheets (`w-tabL-win-deuteranopia`, `w-tabL-win-grayscale`) show the closed factory, not a build card.

## 7. Per surface

- **Phone portrait 100 and 200 %**: closed state, machine card (Upgrade, Details, cause line), build card (selected type, ghost, rows, Cancel, Confirm), hub and all eight sheets pass. At 200 % the HUD strip stays on one line.
- **Phone landscape**: HUD column left, action column right, depot clear of the HUD; hub and Contracts open as a side panel; Back works.
- **Small phone, 640x360 at 200 %, tablet**: as above. The 360x640 build card fits with two type rows visible.
- **U-10 interaction checkpoint**: build in 2 taps (slot, Confirm), ghost and selection frame always visible, marker and chip taps select, Back order as T535. Hand-over recentre (D7): not re-checked by me (Play-Tester's part).
- **Empty and error states**: Items empty, store-full cause lines, error dialog with Retry, Use previous save, Discard damaged save. Reduced motion and offline return with a level-up banner: not re-run, the batch C result stands.

## 8. Not covered

Windows session captures beyond the existing T562 path; the insufficient-Cash build card state ("Need $X more", Confirm disabled) and the desktop build card were not captured; keyboard and TalkBack; zoom fractions and the Back-state threshold; build with Cash (dust, hand-over); banner and reduced motion; pan and zoom shimmer; 844x390 and 150 % text.

## 9. Owner questions and answers

1. Q1 (answered): the 48 pt marker halo is a diameter (`AttentionMarkerView.MinimumHitDiameterPoints` = 48, radius 24). My upward reach of 15 to 23 pt matches it.
2. Q2 (answered): the cutoff is panel points per world cell of 24 art px (`SlotGridView.MarkerMinimumPixelsPerCell`, `MarkersVisibleAt`), not per plate. In those units the phone classes read 24 to 28 pt per cell, at or just above the cutoff, which matches the T562 statement. My earlier reading used plate width in the wrong unit and is corrected in section 5.
3. Q3 (decided): Overdrive hub row confirm goes on the polish list after F006.
4. Q4 (decided): F-3 first-tap is reproduced in T548 and filed only if confirmed.

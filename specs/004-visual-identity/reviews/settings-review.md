# T455 Settings sheet and gear: independent UI/UX review

Issue #547 (T455), reviewer: independent read-only UI/UX Reviewer (not the author of T433). Reviewed commit: `a419744` (origin/main, contains T433 PR #654, `0a37451`). Covers FR-020a, FR-020 (control and states). Sources: `contracts/presentation-and-state-cues.md` › Display preferences, plan OD-8, T433 QA and review comments on #546.

## Verdict

| Screen class | Verdict |
|---|---|
| Compact portrait 360x640 | ACCEPT WITH FOLLOW-UPS (F1, F2) |
| Compact landscape 640x360 and 844x390 | ACCEPT WITH FOLLOW-UPS (F1, F2, F3) |
| Tablet 1024x768 and 768x1024 | ACCEPT (F2 cosmetic only) |
| Keyboard only | ACCEPT (automated and rig evidence; no device keyboard pass, see Method) |
| Pointer and touch | ACCEPT (touch verified on the emulator; mouse pointer not separately observable, see Method) |
| TalkBack and OS font scale 2.0 | Font scale 2.0: ACCEPT. TalkBack: FOLLOW-UP F4 (the game exposes no accessible nodes) |
| **Overall** | **ACCEPT WITH FOLLOW-UPS**. No blocking finding. |

## Method (what was and was not observed)

- Windows Play Mode rig (`ShellRig`, panel cleared before capture per workstation pitfalls), temporary capture test, not committed. Classes 360x640, 640x360, 844x390, 1024x768 and 768x1024, each at 100 % and 200 % text, each in four sheet states (A follow system, system reduces; B system unavailable; C user On; D user On with failed write). 40 sheet captures plus 10 closed-HUD captures and a layout fact dump (bounds, text measure, scroll extents, focus order). A subset is committed in `settings-review/`.
- Android emulator `emulator-5554` (Medium_Phone_API_36.0, 1080x2400 at 420 dpi, about 411x914 dp, so wider than the 360 pt class), development GLES3 APK built from this worktree (46,375,523 bytes), real touch input (`adb input tap`), `KEYCODE_BACK`, `font_scale` 2.0 and TalkBack (Android Accessibility Suite, Play Store image).
- Not observed: a Windows mouse pointer (UI Toolkit drops device pointer events in the batch editor, per pitfalls; the rig sends navigation-submit events), a hardware keyboard on the device (only `KEYCODE_BACK` was sent), spoken TalkBack output (the emulator's TTS was not ready when TalkBack started, and the accessibility tree is empty, see F4), and a failed write on the device (storage was writable). The "Not saved" state was observed only in the rig.
- The rig's `SettingsTestHarness` builds the presenter without an icon provider, so the warning icon of the "Not saved" note is not visible in the rig captures; the note is amber text plus words. The icon is unverified.

## Findings

| Id | Severity | Screen and state | Expected | Observed | Acceptance impact |
|---|---|---|---|---|---|
| F1 | Medium | Compact portrait 360x640 at 100 % and 200 %; compact landscape 640x360 and 844x390 at 100 % and 200 %; all states | The live effective line and the inline "Not saved" note are visible when a choice is made (contract: the note "stays until the next successful write", no modal; FR-020a usable at 200 % text) | The note and effective line sit below the three radios inside a scrolling sheet. 360x640 at 100 %: effective line is cut at the viewport bottom, note fully off-screen. 360x640 at 200 %: only heading, group label and hint fit; radios are below the fold in the rig (on the emulator at OS 2.0 the radios fit and the line is half cut). 640x360 at 200 %: about one radio visible, the note is about 2 screens down. A failed save is therefore easy to miss on every phone class | Follow-up. Not a blocker because the sheet scrolls, shows a scrollbar and the choice still applies. Fix: put the effective line and note directly under the hint (above the radios), or pin them |
| F2 | Low | All classes, sheet open | One "Settings" title | The sheet header (panel title) and the in-sheet `settings-heading` both read "Settings" (see captures). It costs about 30 pt of the scarce height on phones and adds a duplicate stop for a screen reader | Follow-up: remove the in-sheet heading |
| F3 | Low | 640x360 and 844x390 at 200 %, any state | Radio labels stay readable | The sheet column is 178 to 257 pt wide: "Follow system (selected)" wraps to 3 lines, "Reduced motion" heading wraps, effective line wraps to 3 lines. Readable, no clipping, 44 pt targets kept | Follow-up (optional): shorten or drop the "(selected)" suffix text where the filled mark already shows it, as long as the selected state stays non-colour |
| F4 | Medium | Android, TalkBack on, any screen | Gear named "Settings", radio group labelled and selected state exposed (contract; the contract also calls screen-reader output a platform limit checked by a device pass) | `uiautomator dump` with TalkBack on shows one node for the game: `unitySurfaceView` (`android.view.SurfaceView`), no text, no content description, no children. No gear, radio, effective line or "Not saved" note reaches the accessibility service. The names exist only as UI Toolkit tooltips and label text, which Android never receives. Matches the Feature 005 R-21 platform limit, so this is a recorded, not a new, gap | Follow-up, not a T433 regression. The "accessible name" requirement is met in the UI Toolkit tree only. Needs a decision: accept R-21 for the shipping build, or wire Unity's accessibility hierarchy for the shell |
| F5 | Low | Unavailable-signal copy | Unambiguous | "Following system: not available, motion on" is ambiguous ("motion on" can read as reduced motion on). It also wraps to 2 lines at 640x360 at 100 % (233 pt column). Suggest "Following system: not available, animation plays" | Follow-up (carried from Product review P-1) |
| F6 | Low | Phone at OS font scale 2.0 (emulator) and rig 360x640 at 200 % | Gear is the last strip element, right-aligned | The strip wraps to two rows; the gear sits directly after "Overdrive Locked" at the left of row 2, not at the right edge. Still last in focus order, 44x44, inside the safe area | Cosmetic follow-up |
| F7 | Info | Store caption at 200 % on compact portrait (carry-in) | No "S…" | Not reproduced. Rig 360x640 at 200 %: caption cell 189 pt, text 58 pt, no truncation; emulator at OS 2.0: "Store" complete; at 1.0 the caption shows with the glyph hidden as designed | Closed |
| F8 | Info | "Not saved" at 200 % on a 360 pt sheet (carry-in) | Wraps, no clipping | 360x640 at 200 %: note 318 pt wide, wraps to 4 lines (128 pt), no clipping, readable (amber text plus words). Real reach issue is F1, not the wrapping | Closed |
| F9 | Info | Gear glyph (carry-in) | Reads as a gear at 44 pt | The USS placeholder gear reads clearly on the emulator at 1.0 and 2.0. Strip glyphs at 20 pt are legible. A final monochrome gear remains an art follow-up (Art Director review) | Accept for now |
| F10 | Info | Radio keyboard model (carry-in) | Documented | Three tab stops (Follow, On, Off) plus arrow keys that change the choice and move focus (not roving). Focus order on 360x640: cash, store, overdrive, settings, zoom-in, zoom-out, panel-close, radios (3), then tabs. Acceptable and consistent with the other panels; Tab only moves focus, arrows also change the choice | Accept |
| F11 | Info | Sheet visuals not yet reviewed by Art Director (carry-in) | Reviewed | Reviewer note only: contrast of cyan selected border on dark fill, white focus ring (visible on gear and radios, rig and device) and amber note on the raised surface are legible in every capture. The formal Art Director visual sign-off stays with that role | Not a UI/UX blocker |

## Screenshot matrix

Committed captures are in `specs/004-visual-identity/reviews/settings-review/` (PNG, rig size = screen size in points, device size 1080x2400). Other cells were captured and measured (see "Layout facts") but not committed.

| Class | 100 % text | 200 % text |
|---|---|---|
| 360x640 portrait | `hud-360x640-100.png` (closed HUD); `360x640-100-A-follow-reduce-on.png` (state A, focus on On); `360x640-100-D-notsaved.png` (D) | `hud-360x640-200.png`; `360x640-200-A-follow-reduce-on.png`; `360x640-200-D-notsaved.png` |
| 640x360 compact landscape | `640x360-100-D-notsaved.png` | `640x360-200-A-follow-reduce-on.png`; `640x360-200-D-notsaved.png` |
| 844x390 compact landscape | not committed (B, A, C, D captured) | `844x390-200-D-notsaved.png` |
| 1024x768 tablet landscape | `1024x768-100-B-unavailable.png` | `1024x768-200-D-notsaved.png` |
| 768x1024 tablet portrait | `768x1024-100-D-notsaved.png` | `768x1024-200-B-unavailable.png` |
| Android emulator, OS font 1.0, touch | `device-1080x2400-100-follow.png` (sheet via touch on gear, focus on gear); `device-1080x2400-100-on.png` (after tapping On) | |
| Android emulator, OS font 2.0 | | `device-1080x2400-os200-hud.png`; `device-1080x2400-os200-sheet.png` |

Interaction modes: keyboard only is shown by the focus ring on the On radio (`360x640-100-A-follow-reduce-on.png`, `640x360-200-A-follow-reduce-on.png`) and by the gear ring after Back (device captures); touch by the device captures.

### Layout facts (from the rig, all classes)

- Gear 44x44 at every class and text scale, inside the strip and safe area, no overlap with Cash, Store or Overdrive (phones). Close 44x44 on phones (66x55 at 200 % on 360x640); hidden on tablet side panel by design.
- Radios 44 pt high or more everywhere; the sheet never overflows its viewport horizontally.
- Scrollable at every phone cell (see F1); tablets show the whole sheet at 100 % and nearly all at 200 % (1024x768 at 200 %: the note is cut at the bottom, scrolls).
- Text measurement: wrapped labels report unwrapped text width wider than the column; that is wrapping, not truncation. No label with `nowrap` or ellipsis is clipped in the sheet.

## State table (observed)

| State | Radio | Effective line (observed) | Note | Observed in |
|---|---|---|---|---|
| No stored file, system reduces | Follow system selected | "Following system: reduced motion on" | none | rig all classes; device shows "reduced motion off" with the emulator's system setting off |
| System unavailable | Follow system selected | "Following system: not available, motion on" (ambiguous, F5) | none | rig `...-B-unavailable` |
| User On | On selected ("On (selected)") | "Reduced motion on (your choice)" | none | rig; device (file `{"version":1,"reducedMotion":"on"}` written to `Android/data/<pkg>/files/IdleFactory/Preferences/display-v1.json`) |
| User Off | Off selected | "Reduced motion off (your choice)" | none | automated test only (T433 `TheLiveLineFollowsTheStateTable`) |
| Last write failed | chosen value stays applied | as the user-choice line | "Not saved. Your choice applies now but could not be stored. Choosing again retries." in amber, stays on reopen, clears after a successful write | rig `...-D-notsaved`; the icon is not exercised (see Method) |

The selected state is text ("(selected)") plus a filled mark and border, never colour alone.

## Accessible names

- Gear: tooltip and accessible name "Settings" (UI Toolkit tooltip; rig and code `SettingsName`).
- Sheet title "Settings"; group label "Reduced motion" (also the group tooltip); hint "Stops looping machine animation. Nothing else changes."
- Radios: label text "Follow system", "On", "Off" with " (selected)" appended; tooltips "Reduced motion: <choice>[, selected]".
- Effective line and "Not saved" note are plain text labels.
- Close: "Close". Focus returns to the gear on Close, Escape and Android Back (device: Back closed the sheet, focus ring on the gear).
- None of these is visible to Android's accessibility services (F4).

## Device-pass line

Emulator `emulator-5554` (Android 36, x86_64 host with arm64 translation, 1080x2400 at 420 dpi), development APK built from `a419744` (46,375,523 bytes), 2026-11-04: touch opened the sheet, tapped On (persisted to `display-v1.json`), `KEYCODE_BACK` closed it and focus returned to the gear: PASS. OS font scale 2.0 (`settings put system font_scale 2.0`, cold launch): HUD and sheet usable, gear 44x44 reachable, radios and Close fully visible, effective line half-cut at the viewport bottom (F1): PASS with F1/F6. TalkBack on (Android Accessibility Suite): the app's accessibility tree is one empty `SurfaceView`, no names exposed, no spoken output could be verified (TTS not ready at start; nothing to read): NOT VERIFIED, recorded as F4. No pass for TalkBack is claimed. After the run: font scale 1.0, TalkBack off and its notification grant revoked, app data cleared, the Unity active target switched back to Windows and settings noise restored. The emulator was left running under the parent's lease, which was not released. Deviations: the first install failed with `INSUFFICIENT_STORAGE` (about 460 MB free of 5.8 GB), the previous build of the game was uninstalled to make room; the first launch after `force-stop` showed grey frames for about 10 s.

## Follow-ups (suggested, none blocking)

1. F1: move the effective line and "Not saved" note above the radios, or pin them; re-run the T433 layout tests.
2. F2: remove the in-sheet "Settings" heading.
3. F3 and F5: shorten the selected suffix on narrow columns; reword the unavailable-signal line to "animation plays".
4. F4: decide whether R-21 stays accepted for the shell, or wire an accessibility hierarchy (gear, radios, live line) so TalkBack can read them; then redo the TalkBack pass on a device.
5. F6: right-align the gear on a wrapped strip.
6. Real "Not saved" device check and the warning icon in the rig harness (pass an icon provider).

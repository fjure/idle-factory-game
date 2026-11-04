# T441 UI icon integration record (#555)

Scope: restyled `F003-UI-ICON-*` / `F004-UI-ICON-SETTINGS` icons (T440, `Assets/IdleFactory/Art/F004/ui-icons/`) on the Feature 003 surfaces. `reviewedBy` is not filled here (second commit, after independent review).

## What changed
- The shipped scene catalog already served the restyled sprite for all 50 UI keys (checked by `UiIconIntegrationEditTests`). The panel test rigs still resolved the Feature 003 placeholders; they now resolve through `UiIconSprites` so every layout suite runs with the restyled icons.
- The HUD gear was a hand-drawn USS shape (mixed style). It is now `F004-UI-ICON-SETTINGS` (USS shapes stay only as fallback without a resolver); the 44 pt target and the "Settings" name are unchanged.
- New bindings: start-up spinner and import icon (spinner stops under reduced motion), Offline headline, contract countdown (timer), Research effect line (EFFECT-*), Prestige FV and IP, Industry title and IP.
- Not bound by any UI code (text-only buttons in Feature 003; adding leading icons to buttons is a layout change beyond T441): ABANDON, ACCEPT, ACTIVATE, AUTOSELL, CANCEL, CONFIRM, DELIVER, PRIORITY-UP, PRIORITY-DOWN, REMOVE, RETRY, TOGGLE-ON, TOGGLE-OFF, UPGRADE (see `keys-not-bound-by-ui-code.txt` of the test run). INDUSTRY, IP, FV are now bound.

## Tinted contrast (glyph #F2F5FA x token tint, non-text 3:1)
| Tint token | Glyph x tint | vs --bg | vs --surface | vs --surface-raised |
|---|---|---:|---:|---:|
| (none) | #F2F5FA | 16.23 | 13.43 | 9.43 |
| --text-strong | #EBF0F7 | 15.49 | 12.82 | 9.00 |
| --text-primary | #D9DEE6 | 13.13 | 10.86 | 7.63 |
| --text-secondary | #C1CDDD | 11.02 | 9.12 | 6.40 |
| --text-muted | #8C9DB4 | 6.42 | 5.31 | 3.73 |
| --accent | #35B6F3 | 7.72 | 6.39 | 4.48 |
| --positive | #46D57D | 9.35 | 7.74 | 5.44 |
| --warning | #EEB823 | 9.73 | 8.05 | 5.65 |
| --danger | #EB6D6F | 5.87 | 4.86 | 3.41 |
| --on-accent on --accent | #0A0F1F | | | 8.90 |

All pass. The lowest is --danger on --surface-raised (3.41). In-context scans (`UiIconIntegrationTests.EveryIconKeepsThreeToOneAfterTheTokenTint`) pass for every shell surface at 100 and 200 %.

## 2 px stroke at 420 dpi (1080x2400, touch, authored panel)
Pixels per point 2.631; the HUD strip glyph is 20 pt, so one art pixel is 2.21 device px and a 2 art px stroke is about 4.4 device px; measured runs on the coin centre row: 4, 5, 4 px.

## Findings
- F1 (risk): the pin hint (`*-pin-hint`, INFO glyph) is 8 pt, a 3:1 downscale of the 24 px art; it renders as a dotted circle in captures. It is an F003 design (RunHudLayoutTests F5 keeps a 12 pt margin); a change to 12 pt needs a layout decision. The bounds test allows 8 pt for this one class only.
- F2: the Research node icon is a USS background on a 184 pt button; bounds are checked from the resolved background position and size.

## Blind-naming result (independent namer; parent scored against the key)
OD-17: the ring-cluster naming is a target, not a gate. This is the in-context outcome. Most in-context icons were named correctly. Misses and confusions:
- INFO pin hint at 8 pt reads as a missing or placeholder dotted circle on every sheet.
- CASH is recognised only with low confidence and is confused with TAB-PRESTIGE (same split circle).
- INDUSTRY (gear with plus) is confused with SETTINGS (gear).
- IP is low confidence without its label.

Unlabelled confusable set:
- The chevron/arrow-over-bar group is confused: CLOSE, PRIORITY-DOWN/UP, UPGRADE, EFFECT-OUTPUT, ACCEPT, DELIVER.
- RETRY, SPINNER and AUTOSELL are confused with each other.
- RESEARCH-NODE-OWNED is confused with RESEARCH-NODE-MAXED.
- ABANDON and STORAGE are misread.

## Decisions
- The 14 text-only Feature 003 button keys (ABANDON, ACCEPT, ACTIVATE, AUTOSELL, CANCEL, CONFIRM, DELIVER, PRIORITY-UP, PRIORITY-DOWN, REMOVE, RETRY, TOGGLE-ON, TOGGLE-OFF, UPGRADE) are accepted as unused (Product APPROVE; button icons need a separate layout task).
- The cash icon keeps its adjacent "Cash" label wherever it is used (Art Director condition).
- T474 is closed as satisfied by T438 + T441 (owner decision 2026-11-05).
- Art Director reviewedBy (independent agent, 2026-11-04, reviewed commit bced7d4) is recorded in the dict schema in provenance for the locked plates L2-L5 (`art-source/f004/slots/plates/f003-slot-locked-l2..l5`, reviewedRevision 2) and the cash icon (`art-source/f004/ui-icons/f003-ui-icon-cash`, reviewedRevision 1). The revision is not bumped, as T439 stamped its records (reviewedRevision equals revision). The import manifest does not mirror reviewedBy; no PNG was re-imported.

## Follow-ups
- Pin hint: 12 pt versus 8 pt layout decision (F1 above).
- The gear stroke is heavier than the line icons (AD A1).
- Where the Research EFFECT icons render (AD A2).
- `StartupStatusPanel.ApplyReducedMotion` has no production caller (QA F3).
- Make the blind-naming capture test Explicit or category-gated (QA F4).
- INDUSTRY / SETTINGS gear confusion.

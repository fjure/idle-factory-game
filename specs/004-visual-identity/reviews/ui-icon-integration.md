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

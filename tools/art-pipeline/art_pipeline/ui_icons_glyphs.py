"""T438 glyph table: the 49 F003 UI icon rows 30 to 78 plus `F004-UI-ICON-SETTINGS` (brief section 5), drawn with
the `ui_icons_draw.Canvas` toolkit on a 24 x 24 grid. Each entry is
(brief row, key, surface, glyph text, confusion peers, draw function). Peers are key suffixes after `F003-UI-ICON-`
(`F004-UI-ICON-SETTINGS` is written in full); `EFFECT-*` and `RESEARCH-NODE-*` expand to every such key.
"""
from __future__ import annotations

import math

from .ui_icons_draw import Canvas

P = "F003-UI-ICON-"


def _bolt(scale: float = 1.0, dx: float = 0, dy: float = 0):
    base = [(14, 2), (5, 13), (11, 13), (9, 22), (19, 10), (13, 10)]
    return [(dx + x * scale, dy + y * scale) for x, y in base]


def _bolt_small():
    return [(11, 2), (5, 11), (9, 11), (8, 18), (15, 8), (11, 8)]


def cash(c):
    c.ring(12, 12, 10).rect(11, 5, 13, 19).rect(7, 9, 17, 11)


def rp(c):
    c.polyline([(9, 3), (9, 9), (4, 20), (20, 20), (15, 9), (15, 3)])
    c.line(7, 3, 17, 3).rect(11, 14, 13, 16)


def ip(c):
    c.poly([(3, 21), (3, 14), (5, 14), (12, 18), (12, 14), (14, 14), (21, 18), (21, 21)])
    c.radial(12, 13, 5, 10, [-150, -110, -70, -30], w=3)


def fv(c):
    c.rect(3, 15, 8, 21).rect(10, 9, 15, 21).rect(17, 3, 22, 21)


def storage(c):
    c.box(3, 6, 21, 21).rect(8, 11, 16, 13)


def expand_factory(c):
    c.box(8, 8, 16, 16)
    c.polylines([[(3, 8), (3, 3), (8, 3)], [(21, 8), (21, 3), (16, 3)], [(3, 16), (3, 21), (8, 21)],
                 [(21, 16), (21, 21), (16, 21)]])


def tab_factory(c):
    c.rect(3, 11, 21, 21).rect(14, 3, 19, 11)


def tab_market(c):
    c.ring(8, 8, 6).line(11, 22, 21, 12).polyline([(14, 12), (21, 12), (21, 19)])


def tab_contracts(c):
    c.polyline([(14, 2), (5, 2), (5, 22), (19, 22), (19, 7)])
    c.polyline([(14, 2), (14, 7), (19, 7)]).polyline([(8, 15), (11, 18), (16, 12)])


def tab_items(c):
    c.polyline([(12, 2), (21, 7), (21, 17), (12, 22), (3, 17), (3, 7)], closed=True)
    c.polyline([(3, 7), (12, 12), (21, 7)]).line(12, 12, 12, 22)


def tab_research(c):
    c.polyline([(6, 17), (12, 6), (18, 17)]).rings([(6, 17), (12, 6), (18, 17)], 4)


def tab_prestige(c):
    c.ring(12, 16, 7).line(12, 22, 12, 3).polyline([(7, 7), (12, 2), (17, 7)])


def industry(c):
    c.ring(12, 12, 7, 3).radial(12, 12, 6, 10, [0, 60, 120, 180, 240, 300], w=4)
    c.rect(11, 9, 13, 15).rect(9, 11, 15, 13)


def build(c):
    c.line(5, 20, 14, 11, 3).line(11, 6, 19, 14, 4)


def upgrade(c):
    c.polyline([(5, 13), (12, 6), (19, 13)], w=3).rect(8, 17, 16, 20)


def remove(c):
    c.rect(3, 5, 21, 8).rect(9, 2, 15, 5).box(6, 8, 18, 22).rects([(10, 12, 12, 19), (12, 12, 14, 19)])


def toggle_on(c):
    c.pill(2, 6, 22, 18).disc(16, 12, 4, erase=True).disc(16, 12, 2)


def toggle_off(c):
    c.stadium(2, 6, 22, 18).disc(8, 12, 2.5)


def _arc_head(c, cx, cy, r_mid, end_deg, size=5.0):
    """Clockwise arrow head whose tip continues the arc past `end_deg` along the tangent."""
    a = math.radians(end_deg)
    px, py = cx + r_mid * math.cos(a), cy + r_mid * math.sin(a)
    tx, ty = -math.sin(a), math.cos(a)
    c.arrow_head((px + tx * 3.5, py + ty * 3.5), end_deg + 90, size, w=3)


def autosell(c):
    c.arc(12, 12, 10, 3, 0, 270)
    _arc_head(c, 12, 12, 8.5, 270)
    c.disc(12, 12, 4)


def priority_up(c):
    c.poly([(11, 3), (13, 3), (20, 11), (20, 13), (4, 13), (4, 11)]).rect(4, 17, 20, 20)


def priority_down(c):
    c.poly([(4, 4), (20, 4), (20, 6), (13, 14), (11, 14), (4, 6)]).rect(4, 17, 20, 20)


def drag_handle(c):
    c.rects([(6, 4, 10, 8), (14, 4, 18, 8), (6, 10, 10, 14), (14, 10, 18, 14), (6, 16, 10, 20), (14, 16, 18, 20)])


def accept(c):
    c.polyline([(14, 3), (9, 3), (9, 21), (21, 21), (21, 8)]).polyline([(14, 3), (14, 8), (21, 8)])
    c.line(2, 14, 12, 14).polyline([(9, 10), (13, 14), (9, 18)])


def deliver(c):
    c.box(2, 5, 13, 19).line(13, 12, 21, 12).polyline([(17, 8), (21, 12), (17, 16)])


def abandon(c):
    c.box(3, 3, 17, 21).line(2, 22, 22, 2, 3)


def activate(c):
    c.ring(12, 12, 10).poly([(9, 7), (11, 7), (17, 11), (17, 13), (11, 17), (9, 17)])


def confirm(c):
    c.polyline([(4.5, 12.5), (9.5, 17.5), (19.5, 6.5)], w=3)


def cancel(c):
    c.line(5.5, 5.5, 18.5, 18.5, 3).line(18.5, 5.5, 5.5, 18.5, 3)


def retry(c):
    c.arc(12, 12, 8, 3, 0, 270)
    _arc_head(c, 12, 12, 6.5, 270)


def close(c):
    c.polyline([(5, 6), (12, 13), (19, 6)], w=3).rect(5, 17, 19, 20)


def info(c):
    c.ring(12, 12, 10).rect(11, 6, 13, 8).rect(11, 10, 13, 18)


def warning(c):
    c.polyline([(12, 3), (22, 20), (2, 20)], closed=True).rect(11, 8, 13, 14).rect(11, 16, 13, 18)


def lock(c):
    c.rect(4, 11, 20, 22).arc(12, 11, 7, 3, 180, 360)


def timer(c):
    c.ring(12, 12, 10).polyline([(12, 6), (12, 12), (17, 15)])


def offline(c):
    c.disc(9, 12, 8).disc(14, 9, 6.5, erase=True)
    c.rect(16, 16, 21, 21)


def import_(c):
    c.polyline([(3, 11), (3, 21), (21, 21), (21, 11)]).line(12, 2, 12, 14).polyline([(7, 9), (12, 14), (17, 9)])


def spinner(c):
    c.arc(12, 12, 10.5, 3, 270, 180)


def overdrive_ready(c):
    c.polyline(_bolt(), closed=True)


def overdrive_active(c):
    c.ring(12, 12, 10).poly([(13, 5), (17, 5), (14, 9), (17, 9), (17, 11), (11, 19), (9, 19), (10, 14), (7, 14), (7, 12)])


def overdrive_cooldown(c):
    c.polyline(_bolt_small(), closed=True).ring(17, 17, 6).polyline([(17, 14), (17, 17), (20, 17)])


def overdrive_locked(c):
    c.polyline(_bolt_small(), closed=True).rect(14, 16, 22, 22).arc(18, 16, 3.5, 2, 180, 360)


def node_locked(c):
    c.ring(12, 12, 10).rect(8, 12, 16, 18).arc(12, 12, 3.5, 2, 180, 360)


def node_available(c):
    c.ring(12, 12, 10).rect(11, 7, 13, 17).rect(7, 11, 17, 13)


def node_owned(c):
    c.ring(12, 12, 10).polyline([(8, 10), (11, 13), (16, 8)]).rect(9, 16, 15, 18)


def node_maxed(c):
    c.ring(12, 12, 11).ring(12, 12, 8).polyline([(9, 12), (11, 14), (15, 10)])


def effect_speed(c):
    c.polylines([[(4, 5), (11, 12), (4, 19)], [(12, 5), (19, 12), (12, 19)]], w=3)


def effect_output(c):
    c.line(12, 11, 12, 3).polyline([(8, 7), (12, 3), (16, 7)]).rect(6, 12, 18, 16).rect(6, 18, 18, 22)


def effect_sale_price(c):
    c.polyline([(3, 12), (12, 3), (21, 3), (21, 12), (12, 21)], closed=True).disc(16, 8, 2)


def effect_upgrade_cost(c):
    c.rect(5, 2, 7, 15).polyline([(2, 12), (6, 16), (10, 12)]).ring(17, 12, 5)


def settings(c):
    c.ring(12, 12, 7, 3).radial(12, 12, 6, 10, [0, 45, 90, 135, 180, 225, 270, 315], w=4)


# (brief row, key, surface, glyph, peers, draw)
ROWS = [
    (30, P + "CASH", "HUD top strip", "coin: circle with a vertical bar and a cross bar", ["TAB-MARKET"], cash),
    (31, P + "RP", "HUD top strip, Research", "flask outline with a rising dot", ["EFFECT-*", "TAB-RESEARCH"], rp),
    (32, P + "IP", "HUD, Prestige, Industry", "factory roof silhouette over four short radial lines", ["TAB-FACTORY"], ip),
    (33, P + "FV", "HUD, Prestige", "three bars in ascending height", ["EFFECT-OUTPUT"], fv),
    (34, P + "STORAGE", "HUD store usage, Inventory and rates", "open crate: rectangle with a top opening line", ["DELIVER"], storage),
    (35, P + "EXPAND-FACTORY", "Expansion", "square with four outward corner arrows", [], expand_factory),
    (36, P + "TAB-FACTORY", "tab bar", "factory silhouette: block plus chimney", ["IP"], tab_factory),
    (37, P + "TAB-MARKET", "tab bar", "coin over an up-right arrow", ["CASH", "AUTOSELL"], tab_market),
    (38, P + "TAB-CONTRACTS", "tab bar", "document with a folded corner and a check line", ["ACCEPT", "ABANDON"], tab_contracts),
    (39, P + "TAB-ITEMS", "tab bar", "cube with three visible-face lines", [], tab_items),
    (40, P + "TAB-RESEARCH", "tab bar", "node graph: three circles joined by lines", ["RESEARCH-NODE-*"], tab_research),
    (41, P + "TAB-PRESTIGE", "tab bar", "up arrow through a ring", ["UPGRADE", "RETRY"], tab_prestige),
    (42, P + "INDUSTRY", "Industry", "gear with a plus", ["F004-UI-ICON-SETTINGS"], industry),
    (43, P + "BUILD", "Build sheet, fresh-run hint", "hammer: T-shape at 45 degrees", [], build),
    (44, P + "UPGRADE", "Machine panel", "up chevron over a bar", ["PRIORITY-UP"], upgrade),
    (45, P + "REMOVE", "Machine panel, confirmation", "bin: rectangle with a lid line", ["CANCEL"], remove),
    (46, P + "TOGGLE-ON", "Machine panel", "pill with the knob on the right, filled", ["TOGGLE-OFF"], toggle_on),
    (47, P + "TOGGLE-OFF", "Machine panel", "pill with the knob on the left, outlined", ["TOGGLE-ON"], toggle_off),
    (48, P + "AUTOSELL", "Inventory and rates", "coin with a circular arrow", ["CASH", "RETRY"], autosell),
    (49, P + "PRIORITY-UP", "Priorities", "up triangle over a bar", ["UPGRADE", "PRIORITY-DOWN"], priority_up),
    (50, P + "PRIORITY-DOWN", "Priorities", "down triangle over a bar", ["PRIORITY-UP"], priority_down),
    (51, P + "DRAG-HANDLE", "Priorities", "six dots in two columns", [], drag_handle),
    (52, P + "ACCEPT", "Contracts", "arrow entering a document", ["DELIVER"], accept),
    (53, P + "DELIVER", "Contracts", "crate with a right arrow", ["ACCEPT", "STORAGE"], deliver),
    (54, P + "ABANDON", "Contracts", "document with a diagonal strike", ["TAB-CONTRACTS"], abandon),
    (55, P + "ACTIVATE", "Items", "play triangle inside a ring", ["OVERDRIVE-READY"], activate),
    (56, P + "CONFIRM", "dialogs (Prestige, Remove, Abandon, Items)", "check mark", ["RESEARCH-NODE-OWNED"], confirm),
    (57, P + "CANCEL", "dialogs", "X mark", ["REMOVE"], cancel),
    (58, P + "RETRY", "Prestige failure, Startup, invalid clock", "circular arrow with a head at the top right", ["AUTOSELL", "SPINNER"], retry),
    (59, P + "CLOSE", "every sheet", "chevron down over a bar", ["PRIORITY-DOWN"], close),
    (60, P + "INFO", "exact values, details", "circle with a dot and a bar (lowercase i)", ["WARNING"], info),
    (61, P + "WARNING", "rejection, errors, Settings 'Not saved' note", "triangle with an exclamation bar", ["INFO"], warning),
    (62, P + "LOCK", "locked or disabled requirement text", "padlock: body plus shackle", ["OVERDRIVE-LOCKED", "RESEARCH-NODE-LOCKED"], lock),
    (63, P + "TIMER", "Contracts (timed), Items countdowns", "clock: circle with two hands", ["OVERDRIVE-COOLDOWN"], timer),
    (64, P + "OFFLINE", "Offline summary", "crescent moon beside a small square", [], offline),
    (65, P + "IMPORT", "Startup and import status", "arrow entering a box", ["ACCEPT"], import_),
    (66, P + "SPINNER", "Startup, saving (static frame, rotated by USS)", "three-quarter ring with a gap", ["RETRY"], spinner),
    (67, P + "OVERDRIVE-READY", "Overdrive control (HUD)", "bolt outline", ["OVERDRIVE-ACTIVE"], overdrive_ready),
    (68, P + "OVERDRIVE-ACTIVE", "Overdrive control", "filled bolt inside a ring", ["OVERDRIVE-READY"], overdrive_active),
    (69, P + "OVERDRIVE-COOLDOWN", "Overdrive control", "bolt outline with a clock corner", ["TIMER"], overdrive_cooldown),
    (70, P + "OVERDRIVE-LOCKED", "Overdrive control", "bolt outline with a padlock corner", ["LOCK"], overdrive_locked),
    (71, P + "RESEARCH-NODE-LOCKED", "Research graph", "node circle with a padlock", ["LOCK"], node_locked),
    (72, P + "RESEARCH-NODE-AVAILABLE", "Research graph", "node circle with a plus", ["BUILD"], node_available),
    (73, P + "RESEARCH-NODE-OWNED", "Research graph", "node circle with a check and a level tick", ["RESEARCH-NODE-MAXED"], node_owned),
    (74, P + "RESEARCH-NODE-MAXED", "Research graph", "node circle with a check and a double ring", ["RESEARCH-NODE-OWNED"], node_maxed),
    (75, P + "EFFECT-SPEED", "Research, Items, Industry effect lists", "double right chevron", [], effect_speed),
    (76, P + "EFFECT-OUTPUT", "effect lists", "up arrow over two stacked squares", ["FV"], effect_output),
    (77, P + "EFFECT-SALE-PRICE", "effect lists", "price tag", [], effect_sale_price),
    (78, P + "EFFECT-UPGRADE-COST", "effect lists", "down arrow beside a coin", ["CASH"], effect_upgrade_cost),
    (86, "F004-UI-ICON-SETTINGS", "HUD top strip (opens the Settings sheet, FR-020a)",
     "gear (24 x 24; distinct from INDUSTRY: no plus, eight teeth)", ["INDUSTRY"], settings),
]


def expand_peers(key: str, peers: list[str]) -> list[str]:
    keys = [r[1] for r in ROWS]
    out: list[str] = []
    for p in peers:
        if p.endswith("*"):
            out += [k for k in keys if k.startswith(P + p[:-1]) and k != key]
        else:
            out.append(p if p.startswith("F00") else P + p)
    return sorted(set(out) - {key})


def draw(key: str) -> Canvas:
    for _row, k, _s, _g, _p, fn in ROWS:
        if k == key:
            c = Canvas()
            fn(c)
            return c
    raise KeyError(key)

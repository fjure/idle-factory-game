"""Art-pipeline v1 checks (T462; Art Bible v1.0 sections 3.4, 9.3, 9.4 and rule L-4; FR-021a, FR-022).

* `check_overlay_keepout`    - no machine pixel in the badge/marker band (section 3.4).
* `check_outline_contrast`   - ink outline OR rim reaches 3:1 per backdrop tone, composite-mask share
                               (owner decision R1, section 9.3, rule C5). The counting method is written out
                               in the function docstring and nowhere approximated.
* `check_overlay_backdrop`   - backdrop ring under badges, markers and frames lies in the allowed tone set
                               (owner decision R2, rules C1/C3/C8).
* `check_accent_subset`      - the accent mask is a subset of the body mask and never on ink or the boundary
                               (rule L-4).

The `intentionallyEmpty` flag and the integral-pivot assertion live in `checks.py`
(`check_intentionally_empty`, `check_pivot`).
"""
from __future__ import annotations

from PIL import Image

from .checks import CONTRAST_NONTEXT, CheckResult
from .color import RGB, contrast_ratio
from .palette import INK_GROUP, Palette

# Section 3.4: no machine pixel in canvas rows 0..33 of columns 15..71 (inclusive, y down).
KEEPOUT_ROWS = (0, 33)
KEEPOUT_COLS = (15, 71)
SHARE_THRESHOLD = 0.90  # section 9.3 step 5

# Rule C5: allowed backdrop tones for machine bodies.
BODY_BACKDROP_TONES = frozenset({"plate_1", "plate_2", "ink_0", "floor_0", "floor_1"})
# Rule C3: the selection frame sits on every tone except these.
FRAME_FORBIDDEN = frozenset({"warn_0", "plate_0", "res_ore_0"})
# Rule C1: the allowed backdrop set for badges and markers (A_ov), verbatim from section 9.4.
A_OV = frozenset({
    "ink_0", "ink_1", "steel_1", "steel_2", "steel_3", "copper_1", "copper_2", "teal_1", "dim_0", "lit_0", "lit_1",
    "floor_0", "floor_1", "plate_1", "plate_2", "res_ore_1", "res_coal", "res_crystal", "res_leaf", "res_gold",
    "res_water", "res_plank", "ui_bg", "ui_panel", "ui_line", "ui_text"})
BARE_FLOOR_TONES = ("floor_0", "floor_1")

_N4 = ((1, 0), (-1, 0), (0, 1), (0, -1))  # right, left, down, up: the section 9.3 rim order


def a_fr(palette: Palette) -> frozenset[str]:
    """A_fr (rule C3): every palette tone except `warn_0`, `plate_0` and `res_ore_0`."""
    return frozenset(c.id for c in palette.colors) - FRAME_FORBIDDEN


# --- overlay keep-out ------------------------------------------------------------------------

def check_overlay_keepout(frames: list[Image.Image], rows: tuple[int, int] = KEEPOUT_ROWS,
                          cols: tuple[int, int] = KEEPOUT_COLS) -> CheckResult:
    """No opaque pixel of any body frame inside the overlay keep-out region (inclusive row and column bounds,
    y down from the top of the canvas). The default region is section 3.4: rows 0..33, columns 15..71 of the
    80 x 64 machine canvas. Reports the number of offending pixels and the first one per frame."""
    bad = []
    for i, f in enumerate(frames):
        px = f.convert("RGBA").load()
        hits = [(x, y) for y in range(max(rows[0], 0), min(rows[1], f.height - 1) + 1)
                for x in range(max(cols[0], 0), min(cols[1], f.width - 1) + 1) if px[x, y][3]]
        if hits:
            bad.append({"frame": i, "pixels": len(hits), "first": list(hits[0])})
    return CheckResult("overlay_keepout", not bad, {"rows": list(rows), "cols": list(cols), "badFrames": bad})


# --- outline / rim contrast on the composite (section 9.3) -------------------------------------

def _opaque(px, w: int, h: int, x: int, y: int) -> bool:
    return 0 <= x < w and 0 <= y < h and px[x, y][3] != 0


def outline_pairs(body: Image.Image, backdrop: Image.Image | None, palette: Palette):
    """Yield one record per (outline pixel, outward 4-neighbour) pair, exactly as section 9.3 counts.

    1. Neighbourhood is 4-neighbour. The outline is the set of opaque body pixels with at least one
       4-neighbour that is transparent in the body or outside the canvas. The rim of an outline pixel is its
       first inward 4-neighbour (order right, left, down, up) that is opaque and not itself an outline pixel;
       `rim` is None when there is none.
    2. One pair per (outline pixel, outward 4-neighbour). A corner pixel with two outward neighbours gives
       two pairs; all pairs count, none is merged. Off-canvas neighbours count.
    3. The backdrop pixel is the pixel of `backdrop` at the outward neighbour's canvas position. Opaque: its
       palette id is the tone. Transparent or outside the canvas (or `backdrop` None): bare floor, which is
       evaluated twice (`floor_0` and `floor_1`), so it yields one record per floor tone.
    A record is `(outline_rgb, rim_rgb | None, tone)`; an opaque backdrop colour outside the palette yields
    the tone `"unknown:#rrggbb"`.
    """
    b = body.convert("RGBA")
    w, h = b.size
    bp = b.load()
    kp, kw, kh = (None, 0, 0)
    if backdrop is not None:
        k = backdrop.convert("RGBA")
        kp, kw, kh = k.load(), k.width, k.height

    def is_outline(x: int, y: int) -> bool:
        return bp[x, y][3] != 0 and any(not _opaque(bp, w, h, x + dx, y + dy) for dx, dy in _N4)

    for y in range(h):
        for x in range(w):
            if not is_outline(x, y):
                continue
            rim = None
            for dx, dy in _N4:
                nx, ny = x + dx, y + dy
                if _opaque(bp, w, h, nx, ny) and not is_outline(nx, ny):
                    rim = tuple(bp[nx, ny][:3])
                    break
            ink = tuple(bp[x, y][:3])
            for dx, dy in _N4:
                nx, ny = x + dx, y + dy
                if _opaque(bp, w, h, nx, ny):
                    continue
                if kp is not None and 0 <= nx < kw and 0 <= ny < kh and kp[nx, ny][3] != 0:
                    rgb = tuple(kp[nx, ny][:3])
                    tone = palette.id_of(rgb) or "unknown:#%02x%02x%02x" % rgb
                    yield ink, rim, tone
                else:
                    for tone in BARE_FLOOR_TONES:
                        yield ink, rim, tone


def outline_contrast_shares(body: Image.Image, backdrop: Image.Image | None, palette: Palette,
                            ratio: float = CONTRAST_NONTEXT) -> dict[str, dict]:
    """Per backdrop tone T: pairs, pairs where the ink term, the rim term and either term reach `ratio`.

    4. `share_T` = pairs where `max(ratio(ink, T), ratio(rim tone, T)) >= 3` divided by all pairs with
       backdrop T (an absent rim contributes nothing). Unknown backdrop colours never reach the ratio."""
    acc: dict[str, dict] = {}
    for ink, rim, tone in outline_pairs(body, backdrop, palette):
        d = acc.setdefault(tone, {"pairs": 0, "ink": 0, "rim": 0, "either": 0})
        d["pairs"] += 1
        if tone.startswith("unknown:"):
            continue
        t = palette.rgb(tone)
        ink_ok = contrast_ratio(ink, t) >= ratio
        rim_ok = rim is not None and contrast_ratio(rim, t) >= ratio
        d["ink"] += ink_ok
        d["rim"] += rim_ok
        d["either"] += ink_ok or rim_ok
    for d in acc.values():
        d["share"] = d["either"] / d["pairs"]
    return acc


def check_outline_contrast(body: Image.Image, backdrop: Image.Image | None, palette: Palette,
                           tones: set[str] | frozenset[str] | None = None,
                           threshold: float = SHARE_THRESHOLD) -> CheckResult:
    """Owner decision R1, option (b): a graphic passes FR-014 against backdrop tone T when its ink outline OR
    its rim reaches 3:1 against T, measured as the composite-mask share of section 9.3 (see `outline_pairs` and
    `outline_contrast_shares` for the exact counting) and the share must be at least `threshold` (90 %) for
    EVERY tone T present behind the outline. The threshold is not tunable by the caller in production use.

    `tones` (optional) restricts the evaluated tones, e.g. `BODY_BACKDROP_TONES` for rule C5; tones outside
    it are listed under `ignoredTones`, not scored. With `tones=None` every tone found is scored, which
    reproduces the section 9.3 table. An outline with no outward pair at all fails (nothing was measured).
    """
    shares = outline_contrast_shares(body, backdrop, palette)
    scored = {t: d for t, d in shares.items() if tones is None or t in tones}
    ignored = sorted(t for t in shares if t not in scored)
    failing = sorted(t for t, d in scored.items() if d["share"] < threshold)
    ok = bool(scored) and not failing
    return CheckResult("outline_contrast", ok, {
        "threshold": threshold,
        "tones": {t: {"pairs": d["pairs"], "inkShare": round(d["ink"] / d["pairs"], 4),
                      "rimShare": round(d["rim"] / d["pairs"], 4), "share": round(d["share"], 4),
                      "passed": d["share"] >= threshold} for t, d in sorted(scored.items())},
        "failingTones": failing, "ignoredTones": ignored})


# --- backdrop under overlays (rules C1, C3, C8) -------------------------------------------------

def check_overlay_backdrop(overlay: Image.Image, backdrop: Image.Image, allowed: set[str] | frozenset[str],
                           palette: Palette, exclude: Image.Image | None = None) -> CheckResult:
    """C8 numeric acceptance for one overlay (badge, marker or frame) rendered over the world layers without
    the overlay layer (`backdrop`, same canvas size): take the 1 art px ring of backdrop pixels just outside the
    opaque mask of `overlay` (4-neighbour dilation, off-canvas ring pixels are not sampled), look up each ring
    pixel's palette tone, and require 100 % of them in `allowed` (`A_OV` for badges and markers, `a_fr(palette)`
    for the frame). Transparent backdrop is bare floor and counts as `floor_0` and `floor_1`, both of which must
    be allowed. Ring pixels where `exclude` (another overlay's mask) is opaque are skipped."""
    o, k = overlay.convert("RGBA"), backdrop.convert("RGBA")
    if o.size != k.size:
        return CheckResult("overlay_backdrop", False, {"problem": "size mismatch", "overlay": list(o.size),
                                                       "backdrop": list(k.size)})
    w, h = o.size
    op, kp = o.load(), k.load()
    ex = exclude.convert("RGBA").load() if exclude is not None else None
    ring = set()
    for y in range(h):
        for x in range(w):
            if op[x, y][3]:
                continue
            if any(_opaque(op, w, h, x + dx, y + dy) for dx, dy in _N4):
                ring.add((x, y))
    bad: dict[str, int] = {}
    sampled = 0
    for x, y in sorted(ring):
        if ex is not None and ex[x, y][3]:
            continue
        sampled += 1
        if kp[x, y][3]:
            rgb = tuple(kp[x, y][:3])
            tone = palette.id_of(rgb) or "unknown:#%02x%02x%02x" % rgb
            tones = (tone,)
        else:
            tones = BARE_FLOOR_TONES
        for t in tones:
            if t not in allowed:
                bad[t] = bad.get(t, 0) + 1
    return CheckResult("overlay_backdrop", sampled > 0 and not bad,
                       {"ringPixels": sampled, "forbiddenTones": dict(sorted(bad.items()))})


# --- accent subset of body (rule L-4) -------------------------------------------------------------

def check_accent_subset(accent_frames: list[Image.Image], body_frames: list[Image.Image], palette: Palette) -> CheckResult:
    """Rule L-4: per frame (matched by index), every opaque accent pixel is an opaque body pixel, and not an
    outline or boundary pixel of the body (opaque with a transparent or off-canvas 4-neighbour) and not an `ink`
    group colour in the body. The frame counts must match."""
    if len(accent_frames) != len(body_frames):
        return CheckResult("accent_subset", False, {"frames": [len(accent_frames), len(body_frames)]})
    ink = {c.rgb for c in palette.colors_in(INK_GROUP)}
    bad = []
    for i, (a, b) in enumerate(zip(accent_frames, body_frames)):
        if a.size != b.size:
            bad.append({"frame": i, "problem": "size mismatch"})
            continue
        ap, bp = a.convert("RGBA").load(), b.convert("RGBA").load()
        w, h = b.size
        outside = on_boundary = on_ink = 0
        for y in range(h):
            for x in range(w):
                if not ap[x, y][3]:
                    continue
                if not bp[x, y][3]:
                    outside += 1
                    continue
                if any(not _opaque(bp, w, h, x + dx, y + dy) for dx, dy in _N4):
                    on_boundary += 1
                if tuple(bp[x, y][:3]) in ink:
                    on_ink += 1
        if outside or on_boundary or on_ink:
            bad.append({"frame": i, "outsideBody": outside, "onBoundary": on_boundary, "onInk": on_ink})
    return CheckResult("accent_subset", not bad, {"badFrames": bad})

"""Palette v0 group-budget test on the S2 density spike (T418, research R16): the S1 set on the winning
projection at D = 16 and D = 24. Reads the committed pixel-pass frames under art-source/f004/spike/s2/pixel/
and the committed check-report; the frames are spike evidence, so a missing set is a failure, not a skip."""
import json
from pathlib import Path

import pytest
from PIL import Image

from art_pipeline.palette import RESERVED_CUE_GROUP

S2 = Path(__file__).resolve().parents[3] / "art-source" / "f004" / "spike" / "s2"
DENSITIES = ("D16", "D24")


def _frames(tag):
    files = sorted((S2 / "pixel" / tag).glob("*.png"))
    assert files, "no S2 frames under %s" % (S2 / "pixel" / tag)
    return {f.name: Image.open(f).convert("RGBA") for f in files}


def _used_by_group(palette, frames):
    union, unknown = {}, set()
    for img in frames.values():
        for px in set(img.getdata()):
            if px[3] == 0:
                continue
            assert px[3] == 255, "alpha must be 0 or 255"
            rgb = tuple(px[:3])
            g = palette.group_of(rgb)
            if g is None:
                unknown.add(rgb)
            else:
                union.setdefault(g, set()).add(rgb)
    return union, unknown


@pytest.mark.parametrize("tag", DENSITIES)
def test_s2_set_is_complete(tag):
    # 2 machines x (6 run + paused + warning + inactive) + 4 slot plates + 2 resources
    assert len(_frames(tag)) == 2 * 9 + 4 + 2


@pytest.mark.parametrize("tag", DENSITIES)
def test_s2_palette_v0_group_budgets_hold(palette, tag):
    union, unknown = _used_by_group(palette, _frames(tag))
    assert not unknown, "colors outside palette v0: %s" % sorted(unknown)
    for group, cols in union.items():
        assert len(cols) <= palette.budgets[group], (
            "%s: group %r uses %d colors, budget %d" % (tag, group, len(cols), palette.budgets[group]))
    assert RESERVED_CUE_GROUP not in union, "reserved state-cue colors must stay out of the spike bodies"
    assert sum(len(c) for c in union.values()) <= palette.cap


@pytest.mark.parametrize("tag", DENSITIES)
def test_s2_per_asset_budgets_hold(palette, tag):
    for name, img in _frames(tag).items():
        union, unknown = _used_by_group(palette, {name: img})
        assert not unknown, name
        for group, cols in union.items():
            assert len(cols) <= palette.budgets[group], "%s %s group %r" % (tag, name, group)


@pytest.mark.parametrize("tag", DENSITIES)
def test_s2_check_report_agrees_with_the_frames(palette, tag):
    report = json.loads((S2 / "check-report.json").read_text(encoding="utf-8"))
    entry = next(c for c in report["global"] if c["name"] == "palette_group_budget[%s]" % tag)
    assert entry["passed"]
    union, _ = _used_by_group(palette, _frames(tag))
    for group, figures in entry["details"]["groups"].items():
        assert figures["usedInSet"] == len(union.get(group, ())), group
        assert figures["budget"] == palette.budgets[group], group

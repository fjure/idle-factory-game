import dataclasses

from art_pipeline.checks import check_palette_definition
from art_pipeline.palette import RESERVED_CUE_GROUP, validate_palette


def test_palette_v0_respects_cap_and_group_budgets(palette):
    assert palette.cap == 48
    assert len(palette.colors) <= 48
    assert sum(palette.budgets.values()) <= 48
    for group, budget in palette.budgets.items():
        assert len(palette.colors_in(group)) <= budget
    assert validate_palette(palette) == []
    assert check_palette_definition(palette).passed


def test_palette_declares_required_groups(palette):
    for g in ("ink", "machine", "floor", "resources", "ui", RESERVED_CUE_GROUP, "cvd_safe"):
        assert g in palette.budgets
    assert any(c.emissive for c in palette.colors_in("machine"))


def test_palette_over_cap_is_rejected(palette):
    extra = dataclasses.replace(palette.colors[0], id="extra", rgb=(1, 2, 3))
    bad = dataclasses.replace(palette, colors=palette.colors + (extra,))
    assert any("cap" in p for p in validate_palette(bad))
    assert not check_palette_definition(bad).passed


def test_group_over_budget_is_rejected(palette):
    extra = dataclasses.replace(palette.colors_in("ink")[0], id="ink_x", rgb=(9, 9, 9))
    bad = dataclasses.replace(palette, cap=60, colors=palette.colors + (extra,))
    assert any("'ink'" in p for p in validate_palette(bad))


def test_duplicate_colors_are_rejected(palette):
    dup = dataclasses.replace(palette.colors[1], id="dup")
    bad = dataclasses.replace(palette, cap=60, colors=palette.colors + (dup,))
    assert any("duplicate" in p for p in validate_palette(bad))

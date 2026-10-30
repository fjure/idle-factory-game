"""Palette loading and definition validation."""
from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path

from .color import RGB, hex_to_rgb

DEFAULT_PALETTE_PATH = Path(__file__).resolve().parent.parent / "palette" / "palette-v0.json"
PALETTE_V1_PATH = Path(__file__).resolve().parent.parent / "palette" / "palette-v1.json"
RESERVED_CUE_GROUP = "state_cues"
INK_GROUP = "ink"


@dataclass(frozen=True)
class PaletteColor:
    id: str
    rgb: RGB
    group: str
    emissive: bool


@dataclass(frozen=True)
class Palette:
    palette_id: str
    version: str
    cap: int
    budgets: dict[str, int]
    colors: tuple[PaletteColor, ...]  # ordered: group order, then entry order (tie-break order)

    def by_id(self, color_id: str) -> PaletteColor:
        for c in self.colors:
            if c.id == color_id:
                return c
        raise KeyError(color_id)

    def rgb(self, color_id: str) -> RGB:
        return self.by_id(color_id).rgb

    def group_of(self, rgb: RGB) -> str | None:
        for c in self.colors:
            if c.rgb == rgb:
                return c.group
        return None

    def id_of(self, rgb: RGB) -> str | None:
        for c in self.colors:
            if c.rgb == rgb:
                return c.id
        return None

    def colors_in(self, group: str) -> tuple[PaletteColor, ...]:
        return tuple(c for c in self.colors if c.group == group)

    def rgbs(self, groups: set[str] | None = None) -> list[RGB]:
        return [c.rgb for c in self.colors if groups is None or c.group in groups]


def load_palette(path: Path | str = DEFAULT_PALETTE_PATH) -> Palette:
    doc = json.loads(Path(path).read_text(encoding="utf-8"))
    colors: list[PaletteColor] = []
    budgets: dict[str, int] = {}
    for g in doc["groups"]:
        budgets[g["id"]] = int(g["budget"])
        for c in g["colors"]:
            colors.append(PaletteColor(c["id"], hex_to_rgb(c["hex"]), g["id"], bool(c.get("emissive", False))))
    return Palette(doc["paletteId"], doc["version"], int(doc["cap"]), budgets, tuple(colors))


def validate_palette(palette: Palette) -> list[str]:
    """Return a list of problems (empty = valid): cap, per-group budgets, uniqueness."""
    problems: list[str] = []
    if len(palette.colors) > palette.cap:
        problems.append(f"palette has {len(palette.colors)} colors, cap is {palette.cap}")
    if sum(palette.budgets.values()) > palette.cap:
        problems.append(f"group budgets sum to {sum(palette.budgets.values())}, cap is {palette.cap}")
    for group, budget in palette.budgets.items():
        n = len(palette.colors_in(group))
        if n > budget:
            problems.append(f"group {group!r} has {n} colors, budget is {budget}")
    ids = [c.id for c in palette.colors]
    if len(set(ids)) != len(ids):
        problems.append("duplicate color ids")
    rgbs = [c.rgb for c in palette.colors]
    if len(set(rgbs)) != len(rgbs):
        problems.append("duplicate color values")
    for c in palette.colors:
        if c.group not in palette.budgets:
            problems.append(f"color {c.id!r} in unknown group {c.group!r}")
    return problems

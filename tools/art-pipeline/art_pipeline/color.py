"""Color math: sRGB, CIELAB (D65), WCAG luminance/contrast, Machado 1.0 CVD simulation."""
from __future__ import annotations

from functools import lru_cache

RGB = tuple[int, int, int]

_XN, _YN, _ZN = 0.95047, 1.0, 1.08883


def hex_to_rgb(value: str) -> RGB:
    v = value.lstrip("#")
    if len(v) != 6:
        raise ValueError(f"bad hex color {value!r}")
    return (int(v[0:2], 16), int(v[2:4], 16), int(v[4:6], 16))


def rgb_to_hex(rgb: RGB) -> str:
    return "#%02x%02x%02x" % rgb


@lru_cache(maxsize=None)
def _lin(c8: int) -> float:
    c = c8 / 255.0
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def _delin(c: float) -> int:
    c = min(1.0, max(0.0, c))
    v = c * 12.92 if c <= 0.0031308 else 1.055 * (c ** (1 / 2.4)) - 0.055
    return int(v * 255.0 + 0.5)


def relative_luminance(rgb: RGB) -> float:
    return 0.2126 * _lin(rgb[0]) + 0.7152 * _lin(rgb[1]) + 0.0722 * _lin(rgb[2])


def contrast_ratio(a: RGB, b: RGB, light: float = 1.0) -> float:
    """WCAG contrast ratio; `light` scales both luminances (multiplicative lighting)."""
    la, lb = relative_luminance(a) * light, relative_luminance(b) * light
    hi, lo = max(la, lb), min(la, lb)
    return (hi + 0.05) / (lo + 0.05)


def _f(t: float) -> float:
    return t ** (1 / 3) if t > 216 / 24389 else (24389 / 27 * t + 16) / 116


@lru_cache(maxsize=None)
def rgb_to_lab(rgb: RGB) -> tuple[float, float, float]:
    r, g, b = (_lin(c) for c in rgb)
    x = 0.4124564 * r + 0.3575761 * g + 0.1804375 * b
    y = 0.2126729 * r + 0.7151522 * g + 0.0721750 * b
    z = 0.0193339 * r + 0.1191920 * g + 0.9503041 * b
    fx, fy, fz = _f(x / _XN), _f(y / _YN), _f(z / _ZN)
    return (116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz))


def lab_distance_sq(a: tuple[float, float, float], b: tuple[float, float, float]) -> float:
    return (a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2 + (a[2] - b[2]) ** 2


def luma(rgb: RGB) -> int:
    """Rec.709 luma on encoded values, used for the grayscale review sheet."""
    return int(0.2126 * rgb[0] + 0.7152 * rgb[1] + 0.0722 * rgb[2] + 0.5)


# Machado, Oliveira, Fernandes 2009, severity 1.0, applied in linear RGB.
CVD_MATRICES = {
    "protanopia": ((0.152286, 1.052583, -0.204868), (0.114503, 0.786281, 0.099216), (-0.003882, -0.048116, 1.051998)),
    "deuteranopia": ((0.367322, 0.860646, -0.227968), (0.280085, 0.672501, 0.047413), (-0.011820, 0.042940, 0.968881)),
    "tritanopia": ((1.255528, -0.076749, -0.178779), (-0.078411, 0.930809, 0.147602), (0.004733, 0.691367, 0.303900)),
}


def simulate_cvd(rgb: RGB, kind: str) -> RGB:
    m = CVD_MATRICES[kind]
    lin = (_lin(rgb[0]), _lin(rgb[1]), _lin(rgb[2]))
    return tuple(_delin(sum(m[i][j] * lin[j] for j in range(3))) for i in range(3))  # type: ignore[return-value]

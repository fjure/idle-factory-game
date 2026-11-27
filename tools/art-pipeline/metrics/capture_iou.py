"""SC-003 silhouette IoU on captures (research R18).

A silhouette is the non-key pixels of the body-mask frame inside one machine's body bounds. One silhouette per machine type (the first machine of that
type in slot order) is cut out; the pairwise IoU is computed with bottom-centre alignment, at full size and on a 32 x 32 coverage downsample, the same
method as the F004 `silhouette_iou` check (`art_pipeline.checks`). The limit is the F004 limit 0.70 or the revised Art Bible's, whichever is stricter.

    python -m metrics.capture_iou <body-mask.png> <layout.json>
"""
from __future__ import annotations

import json
import sys
from itertools import combinations
from pathlib import Path

from PIL import Image, ImageChops

from . import METRICS_VERSION
from .common import KEY_COLOUR, count, key_mask, load_layout, rect_px

IOU_LIMIT = 0.70
SMALL = 32


def silhouettes(mask_image: Image.Image, layout: dict, key: tuple[int, int, int] = KEY_COLOUR) -> dict[str, Image.Image]:
    """One cropped L silhouette (255 = body) per machine type."""
    layout = load_layout(layout)
    body = ImageChops.invert(key_mask(mask_image, key))
    out: dict[str, Image.Image] = {}
    for m in sorted(layout.get("machines", []), key=lambda m: m["slot"]):
        if m["machine"] in out:
            continue
        l, t, r, b = rect_px(m["body"], *mask_image.size)
        if r <= l or b <= t:
            continue
        crop = body.crop((l, t, r, b))
        bbox = crop.getbbox()
        if bbox is None:
            continue
        out[m["machine"]] = crop.crop(bbox)
    return out


def _pad_bottom_centre(mask: Image.Image, size: tuple[int, int]) -> Image.Image:
    out = Image.new("L", size, 0)
    out.paste(mask, ((size[0] - mask.width) // 2, size[1] - mask.height))
    return out


def _iou(a: Image.Image, b: Image.Image) -> float:
    inter = count(ImageChops.multiply(a, b))
    union = count(ImageChops.lighter(a, b))
    return inter / union if union else 0.0


def _downsample(mask: Image.Image) -> Image.Image:
    return mask.resize((SMALL, SMALL), Image.Resampling.BOX).point(lambda v: 255 if v >= 128 else 0)


def pairwise_iou(masks: dict[str, Image.Image]) -> dict:
    pairs = []
    for a, b in combinations(sorted(masks), 2):
        size = (max(masks[a].width, masks[b].width), max(masks[a].height, masks[b].height))
        pa, pb = _pad_bottom_centre(masks[a], size), _pad_bottom_centre(masks[b], size)
        pairs.append({"pair": [a, b], "full": round(_iou(pa, pb), 4), "small": round(_iou(_downsample(pa), _downsample(pb)), 4)})
    worst = max((max(p["full"], p["small"]) for p in pairs), default=0.0)
    return {"metricsVersion": METRICS_VERSION, "types": sorted(masks), "pairs": pairs, "worst": worst, "limit": IOU_LIMIT, "passesLimit": worst <= IOU_LIMIT}


def capture_iou(mask_image: Image.Image, layout: dict, key: tuple[int, int, int] = KEY_COLOUR) -> dict:
    return pairwise_iou(silhouettes(mask_image, layout, key))


def main(argv: list[str]) -> int:
    if len(argv) != 2:
        print(__doc__)
        return 2
    print(json.dumps(capture_iou(Image.open(argv[0]), Path(argv[1])), indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

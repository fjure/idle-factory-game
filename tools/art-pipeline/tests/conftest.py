import sys
from pathlib import Path

import pytest
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT))
sys.path.insert(0, str(Path(__file__).resolve().parent))

from art_pipeline.palette import load_palette  # noqa: E402

FIXTURES = Path(__file__).resolve().parent / "fixtures"


@pytest.fixture(scope="session")
def palette():
    return load_palette()


def solid(size, rgb, alpha=255):
    return Image.new("RGBA", size, tuple(rgb) + (alpha,))


def load_frames(route, prefix):
    out = []
    i = 0
    while (FIXTURES / route / f"{prefix}_{i:02d}.png").exists():
        out.append(Image.open(FIXTURES / route / f"{prefix}_{i:02d}.png").convert("RGBA"))
        i += 1
    return out

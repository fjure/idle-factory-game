"""F004 T436: ComfyUI generation for the resource and item-pictogram families (route `comfy`).

Runs the committed workflow `tools/art-pipeline/comfy/z-image-turbo-pixel-lora.json` (Z-Image-Turbo + LoRA
`pixel_art_style_z_image_turbo`, pinned in `tools/art-pipeline/models.json`) on the LOCAL ComfyUI at 127.0.0.1:8188.
Never Comfy Cloud, no partner nodes, no credits. Every model file the workflow loads (diffusion model, text encoder, VAE, LoRA) is verified against models.json
before every run and a mismatch refuses the run. Set COMFY_MODELS_DIR to the local ComfyUI `models` folder. Nothing is downloaded.

  python art-source/f004/resources/comfy_gen.py candidates <outdir> <asset> <seed> [<seed> ...]
  python art-source/f004/resources/comfy_gen.py final                      (the selected seeds, into the family raw/ folders)

The prompt, negative prompt, seed and size per asset are the single source of truth here; the executed API graph is
written next to each raw as `workflow.json` (hashed in provenance).
"""
from __future__ import annotations

import copy
import hashlib
import json
import sys
import time
import urllib.request
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]
ART = HERE.parent
sys.path.insert(0, str(REPO / "tools" / "art-pipeline"))

from art_pipeline.provenance import (ModelsRootError, load_models, model_entry_path, resolve_models_root,  # noqa: E402
                                     verify_model_file, verify_workflow_models)

SERVER = "http://127.0.0.1:8188"
WORKFLOW = REPO / "tools" / "art-pipeline" / "comfy" / "z-image-turbo-pixel-lora.json"
SIZE = (1024, 1024)
NEGATIVE = "blurry, text, watermark, multiple objects, shadow, ground, gradient background, noise"
STYLE = ("game item icon, chunky large pixels, limited palette, bold black outline, three-quarter isometric view from above, "
         "a single object centered, floating with no shadow, plain flat magenta background.")
LORA_TRIGGER = "Pixel art style."

# asset -> (family, subject text). The full prompt is "<subject> <STYLE>".
SUBJECTS = {
    "f003-res-iron-ore": ("resources", "Pixel art style. A single chunk of raw iron ore, a jagged tall rock covered in rusty reddish-brown and orange-tan ore patches with grey stone,"),
    "f003-res-iron-bar": ("resources", "Pixel art style. A single iron ingot bar, a grey steel trapezoid ingot block with a lighter top face and darker sides,"),
    "f003-res-iron-plate": ("resources", "Pixel art style. A single flat square iron plate, a thin grey steel sheet with beveled edges and four small rivets at the corners,"),
    "f003-res-gear": ("resources", "Pixel art style. A single iron cog gear wheel, grey steel gear with round teeth around the rim and a round hole in the centre,"),
    "f003-item-diamond-drill-head": ("items", "Pixel art style. A single diamond drill head, a steel conical spiral drill bit with a bright blue diamond crystal tip,"),
    "f003-item-flux-catalyst": ("items", "Pixel art style. A single flux catalyst, a small round glass flask with a cork stopper filled with glowing golden liquid,"),
    "f003-item-overclock-core": ("items", "Pixel art style. A single overclock core, a square metal microchip with short pins and a glowing golden lightning bolt in the middle,"),
}

# Selected seeds (candidate-selection record: `<family>/candidate-selection.json`).
SELECTED = {
    "f003-res-iron-ore": 306, "f003-res-iron-bar": 103, "f003-res-iron-plate": 102, "f003-res-gear": 102,
    "f003-item-diamond-drill-head": 202, "f003-item-flux-catalyst": 202, "f003-item-overclock-core": 203,
}


def prompt_for(asset: str) -> str:
    return SUBJECTS[asset][1] + " " + STYLE


def models_root() -> Path:
    """ComfyUI `models/` folder from $COMFY_MODELS_DIR; exits with a clear message when it is not set."""
    try:
        return resolve_models_root()
    except ModelsRootError as exc:
        raise SystemExit(str(exc))


def verify_lora() -> dict:
    entry = next(m for m in load_models()["models"] if m["id"] == "pixel_art_style_z_image_turbo")
    verify_model_file(entry, model_entry_path(entry, models_root()))  # raises ModelHashError on mismatch
    return entry


def verify_models(graph: dict | None = None) -> list[dict]:
    """Verify every model the workflow loads (diffusion model, text encoder, VAE, LoRA) against models.json."""
    return verify_workflow_models(graph if graph is not None else json.loads(WORKFLOW.read_text(encoding="utf-8")),
                                  models_root())


def build_graph(asset: str, seed: int) -> dict:
    g = json.loads(WORKFLOW.read_text(encoding="utf-8"))
    g["6"]["inputs"]["text"] = prompt_for(asset)
    g["7"]["inputs"]["text"] = NEGATIVE
    g["9"]["inputs"]["seed"] = seed
    g["8"]["inputs"]["width"], g["8"]["inputs"]["height"] = SIZE
    g["11"]["inputs"]["filename_prefix"] = "f004/%s_%d" % (asset, seed)
    return g


def generate(asset: str, seed: int, out_png: Path, out_graph: Path | None = None) -> dict:
    g = build_graph(asset, seed)
    verify_models(g)
    req = urllib.request.Request(SERVER + "/prompt", json.dumps({"prompt": g}).encode(), {"Content-Type": "application/json"})
    pid = json.load(urllib.request.urlopen(req))["prompt_id"]
    t0 = time.time()
    while True:
        h = json.load(urllib.request.urlopen(SERVER + "/history/" + pid))
        if pid in h:
            break
        time.sleep(1)
    if h[pid]["status"]["status_str"] != "success":
        raise SystemExit("generation failed: %s" % h[pid]["status"])
    im = h[pid]["outputs"]["11"]["images"][0]
    data = urllib.request.urlopen("%s/view?filename=%s&subfolder=%s&type=%s" % (SERVER, im["filename"], im["subfolder"], im["type"])).read()
    out_png.parent.mkdir(parents=True, exist_ok=True)
    out_png.write_bytes(data)
    if out_graph is not None:
        out_graph.write_text(json.dumps(g, indent=2, sort_keys=True) + "\n", encoding="utf-8", newline="\n")
    return {"asset": asset, "seed": seed, "sha256": hashlib.sha256(data).hexdigest(), "seconds": round(time.time() - t0, 1)}


def main(argv: list[str]) -> int:
    if argv[0] == "candidates":
        out = Path(argv[1])
        for sd in argv[3:]:
            print(generate(argv[2], int(sd), out / ("%s_%s.png" % (argv[2], sd))))
        return 0
    if argv[0] == "final":
        for asset, (fam, _s) in sorted(SUBJECTS.items()):
            d = ART / fam / asset / "raw"
            print(generate(asset, SELECTED[asset], d / (asset + ".png"), d / "workflow.json"))
        return 0
    return 2


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))

"""F004 T436: record the generation environment (ComfyUI core, torch, GPU, model files with SHA-256) into
`art-source/f004/resources/environment.json`. Reads the local ComfyUI at 127.0.0.1:8188 and the model files; never
downloads. Run once per generation session (the base-model hashes take about a minute)."""
from __future__ import annotations

import json
import sys
import urllib.request
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import comfy_gen as g  # noqa: E402
from art_pipeline.provenance import file_sha256, load_models  # noqa: E402

ROOT = g.models_root()
FILES = {"unet": ROOT / "diffusion_models" / "z_image_turbo_bf16.safetensors",
         "textEncoder": ROOT / "text_encoders" / "qwen_3_4b.safetensors",
         "vae": ROOT / "vae" / "ae.safetensors"}


def main() -> int:
    lora = g.verify_lora()
    stats = json.load(urllib.request.urlopen(g.SERVER + "/system_stats"))
    s = stats["system"]
    dev = stats["devices"][0]
    doc = {
        "schemaVersion": 1,
        "comfyUiCore": s["comfyui_version"], "pytorch": s["pytorch_version"], "python": s["python_version"].split()[0],
        "os": s["os"], "gpu": dev["name"], "deployEnvironment": s["deploy_environment"],
        "packages": {p["name"]: p["installed"] for p in s["comfy_package_versions"]},
        "nodesUsed": {"UNETLoader": "core", "CLIPLoader": "core", "VAELoader": "core", "LoraLoader": "core",
                      "ModelSamplingAuraFlow": "core", "CLIPTextEncode": "core", "EmptySD3LatentImage": "core",
                      "KSampler": "core", "VAEDecode": "core", "SaveImage": "core"},
        "partnerNodes": "none", "comfyCloud": "never used; local server only",
        "models": {k: {"fileName": p.name, "sizeBytes": p.stat().st_size, "sha256": file_sha256(p),
                       "license": "Apache-2.0 (Z-Image-Turbo, Tongyi-MAI)",
                       "registry": "tools/art-pipeline/comfy/z-image-turbo.json (base files are not in models.json)"}
                   for k, p in FILES.items()},
        "lora": {k: lora[k] for k in ("id", "fileName", "version", "sha256", "license", "commercialUse", "triggerWords",
                                      "trainingDataNote", "sourceUrl", "approval")},
        "loraHashVerifiedBeforeRun": True,
    }
    (HERE / "environment.json").write_text(json.dumps(doc, indent=2, sort_keys=True) + "\n", encoding="utf-8", newline="\n")
    print("wrote environment.json")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

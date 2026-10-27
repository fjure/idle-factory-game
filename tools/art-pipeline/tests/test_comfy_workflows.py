"""Structure checks for the committed ComfyUI API-format workflows (task T404). No ComfyUI needed."""
import json
from pathlib import Path

import pytest

COMFY = Path(__file__).resolve().parent.parent / "comfy"
SLOTS = json.loads((COMFY / "slots.json").read_text(encoding="utf-8"))
NAMES = sorted(SLOTS["workflows"])


def load(name):
    return json.loads((COMFY / name).read_text(encoding="utf-8"))


def of_class(graph, cls):
    return {k: v for k, v in graph.items() if v["class_type"] == cls}


def test_every_workflow_file_is_listed_and_vice_versa():
    on_disk = sorted(p.name for p in COMFY.glob("*.json") if p.name != "slots.json")
    assert on_disk == NAMES


@pytest.mark.parametrize("name", NAMES)
def test_slots_exist_with_expected_classes(name):
    g = load(name)
    s = SLOTS["slots"]
    assert g[s["prompt"]["node"]]["class_type"] == "CLIPTextEncode"
    assert g[s["negativePrompt"]["node"]]["class_type"] == "CLIPTextEncode"
    assert s["prompt"]["node"] != s["negativePrompt"]["node"]
    assert isinstance(g[s["prompt"]["node"]]["inputs"][s["prompt"]["input"]], str)
    assert isinstance(g[s["negativePrompt"]["node"]]["inputs"][s["negativePrompt"]["input"]], str)
    k = g[s["sampler"]["node"]]
    assert k["class_type"] == "KSampler" and s["seed"]["node"] == s["sampler"]["node"]
    assert isinstance(k["inputs"]["seed"], int)
    assert k["inputs"]["control_after_generate"] == "fixed"
    for key in s["sampler"]["inputs"]:
        assert key in k["inputs"], key
    assert g[s["size"]["node"]]["class_type"] == "EmptySD3LatentImage"


@pytest.mark.parametrize("name", NAMES)
def test_sampler_settings_are_explicit_for_a_turbo_model(name):
    i = load(name)[SLOTS["slots"]["sampler"]["node"]]["inputs"]
    assert (i["sampler_name"], i["scheduler"]) == ("res_multistep", "simple")
    assert i["steps"] == 8 and i["cfg"] == 1.0 and i["denoise"] == 1.0


@pytest.mark.parametrize("name", NAMES)
def test_lora_present_only_in_the_lora_workflow(name):
    g = load(name)
    loras = of_class(g, "LoraLoader")
    want = SLOTS["workflows"][name]["lora"]
    assert bool(loras) is want
    if want:
        (nid, node), = loras.items()
        assert nid == SLOTS["slots"]["loraStrength"]["node"]
        assert node["inputs"]["lora_name"] == SLOTS["expectedFiles"]["lora"]
        assert g["5"]["inputs"]["model"] == [nid, 0]  # sampling shift reads the LoRA-patched model
        assert g["6"]["inputs"]["clip"] == [nid, 1] == g["7"]["inputs"]["clip"]
    else:
        assert not any("lora" in json.dumps(n).lower() for n in g.values())


@pytest.mark.parametrize("name", NAMES)
def test_model_file_names_match_expected_files_and_links_resolve(name):
    g = load(name)
    f = SLOTS["expectedFiles"]
    assert of_class(g, "UNETLoader")["1"]["inputs"]["unet_name"] == f["unet"]
    assert of_class(g, "CLIPLoader")["2"]["inputs"]["clip_name"] == f["textEncoder"]
    assert of_class(g, "VAELoader")["3"]["inputs"]["vae_name"] == f["vae"]
    for node in g.values():
        for v in node["inputs"].values():
            if isinstance(v, list):
                assert v[0] in g and isinstance(v[1], int)
    assert of_class(g, "SaveImage")


def test_no_user_specific_paths_or_downloads_recorded():
    blob = "".join(p.read_text(encoding="utf-8") for p in COMFY.glob("*.json"))
    assert "C:\\" not in blob and "/Users/" not in blob and "http" not in blob.replace(
        "tarn59/pixel_art_style_lora_z_image_turbo", "")
    registry = json.loads((COMFY.parent / "models.json").read_text(encoding="utf-8"))
    assert registry["models"] == [] or all(m.get("sha256") for m in registry["models"])

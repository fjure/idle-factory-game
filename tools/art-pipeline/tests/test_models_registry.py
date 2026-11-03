"""models.json registry and workflow-level model hash verification (task T465). No ComfyUI or model files needed."""
import copy
import hashlib
import json
from pathlib import Path

import pytest

from art_pipeline import provenance as pv

COMFY = Path(__file__).resolve().parent.parent / "comfy"
WORKFLOW = json.loads((COMFY / "z-image-turbo-pixel-lora.json").read_text(encoding="utf-8"))
DOC = pv.load_models()
ENV = json.loads((Path(__file__).resolve().parents[3] / "art-source" / "f004" / "resources" / "environment.json")
                 .read_text(encoding="utf-8"))


def test_registry_valid():
    assert pv.validate_models(DOC) == []


def test_base_model_entries_match_workflows_and_environment():
    by_file = {m["fileName"]: m for m in DOC["models"]}
    for wf in COMFY.glob("z-image-turbo*.json"):
        for name in pv.workflow_model_files(json.loads(wf.read_text(encoding="utf-8"))):
            assert name in by_file, f"{wf.name} loads unregistered {name}"
    for key in ("unet", "textEncoder", "vae"):
        env = ENV["models"][key]
        entry = by_file[env["fileName"]]
        assert entry["sha256"] == env["sha256"] and entry["sizeBytes"] == env["sizeBytes"]
        assert entry["kind"] == "model" and entry["commercialUse"] is True
    assert by_file[ENV["lora"]["fileName"]]["sha256"] == ENV["lora"]["sha256"]


@pytest.mark.parametrize("field", pv.MODEL_REQUIRED)
def test_missing_field_rejected(field):
    doc = copy.deepcopy(DOC)
    del doc["models"][0][field]
    assert any(field in p for p in pv.validate_models(doc))


def test_bad_size_and_folder_rejected():
    doc = copy.deepcopy(DOC)
    doc["models"][0]["sizeBytes"] = 0
    doc["models"][0]["comfyFolder"] = "elsewhere"
    problems = pv.validate_models(doc)
    assert any("sizeBytes" in p for p in problems) and any("comfyFolder" in p for p in problems)


def _fake_install(tmp_path, tamper=None, skip=None):
    """A fake ComfyUI models folder plus a registry whose hashes match the fake files."""
    doc = copy.deepcopy(DOC)
    for i, m in enumerate(doc["models"]):
        data = f"fake-{m['id']}".encode()
        m["sha256"] = hashlib.sha256(data).hexdigest()
        if m["id"] == skip:
            continue
        p = pv.model_entry_path(m, tmp_path)
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_bytes(b"tampered" if m["id"] == tamper else data)
    return doc


def test_all_workflow_models_verified(tmp_path):
    doc = _fake_install(tmp_path)
    ids = {m["id"] for m in pv.verify_workflow_models(WORKFLOW, tmp_path, doc)}
    assert ids == {"z_image_turbo_bf16", "qwen_3_4b", "z_image_ae", "pixel_art_style_z_image_turbo"}


@pytest.mark.parametrize("mid", ["z_image_turbo_bf16", "qwen_3_4b", "z_image_ae", "pixel_art_style_z_image_turbo"])
def test_hash_mismatch_refused_without_local_paths(tmp_path, mid):
    doc = _fake_install(tmp_path, tamper=mid)
    with pytest.raises(pv.ModelHashError) as exc:
        pv.verify_workflow_models(WORKFLOW, tmp_path, doc)
    msg = str(exc.value)
    assert mid in msg and "mismatch" in msg
    assert str(tmp_path) not in msg and "\\" not in msg and not Path(tmp_path.anchor).as_posix() in msg


def test_missing_file_and_unregistered_file_refused_without_paths(tmp_path):
    doc = _fake_install(tmp_path, skip="qwen_3_4b")
    with pytest.raises(pv.ModelHashError) as exc:
        pv.verify_workflow_models(WORKFLOW, tmp_path, doc)
    assert "qwen_3_4b" in str(exc.value) and str(tmp_path) not in str(exc.value)
    g = copy.deepcopy(WORKFLOW)
    g["1"]["inputs"]["unet_name"] = "other_model.safetensors"
    with pytest.raises(pv.ModelHashError, match="not registered"):
        pv.verify_workflow_models(g, tmp_path, _fake_install(tmp_path))


def test_models_root_from_env_and_clear_failure(tmp_path):
    assert pv.resolve_models_root({"COMFY_MODELS_DIR": str(tmp_path)}) == tmp_path
    assert pv.resolve_models_root({"COMFY_LORAS_DIR": str(tmp_path / "loras")}) == tmp_path
    with pytest.raises(pv.ModelsRootError) as exc:
        pv.resolve_models_root({})
    assert "COMFY_MODELS_DIR" in str(exc.value)


def test_comfy_gen_has_no_local_user_path():
    src = (Path(__file__).resolve().parents[3] / "art-source" / "f004" / "resources" / "comfy_gen.py").read_text(encoding="utf-8")
    assert "Users" not in src and "AppData" not in src

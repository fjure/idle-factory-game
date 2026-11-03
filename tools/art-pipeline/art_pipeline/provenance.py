"""Provenance records (data-model) and models.json (hash-verified model/LoRA registry)."""
from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path

DEFAULT_MODELS_PATH = Path(__file__).resolve().parent.parent / "models.json"

COMMON_FIELDS = [
    "assetId", "revision", "route", "sources", "tools", "parameters", "touchUp", "pixelPass", "outputs",
    "license", "trainingDataNote", "producedBy", "reviewedBy", "candidateSelection", "artBibleVersion",
    "briefVersion",
]
ROUTE_FIELDS = {
    "blender": ["rigVersion", "renderEnv"],
    "comfy": ["comfyNodeVersions", "parameters.prompt", "parameters.seed", "parameters.workflowHash"],
    "scripted": [],
}
AFTER_REVIEW_FIELDS = ["checkReportHash"]  # known only after checks ran against the manifest
EMPTY_OK = {"touchUp"}  # an empty scripted touch-up list is a valid statement


def _get(rec: dict, dotted: str):
    cur = rec
    for part in dotted.split("."):
        if not isinstance(cur, dict) or part not in cur:
            return None
        cur = cur[part]
    return cur


def _blank(value, allow_empty: bool) -> bool:
    if value is None:
        return True
    if isinstance(value, str) and not value.strip():
        return True
    if isinstance(value, (list, dict)) and not value and not allow_empty:
        return True
    return False


def missing_provenance_fields(record: dict, route: str, for_manifest: bool = False) -> list[str]:
    """Names of missing/blank fields. `for_manifest` omits fields that only exist after checks."""
    fields = list(COMMON_FIELDS) + ROUTE_FIELDS.get(route, [])
    if not for_manifest:
        fields += AFTER_REVIEW_FIELDS
    missing = [f for f in fields if _blank(_get(record, f), f in EMPTY_OK)]
    reviewed = record.get("reviewedBy")
    if isinstance(reviewed, dict):
        for sub in ("role", "session", "date"):
            if _blank(reviewed.get(sub), False):
                missing.append(f"reviewedBy.{sub}")
    if record.get("route") not in (None, route):
        missing.append("route(mismatch)")
    return sorted(set(missing))


# --- models.json -----------------------------------------------------------------

MODEL_REQUIRED = ["id", "kind", "fileName", "comfyFolder", "sourceUrl", "version", "license", "sha256", "sizeBytes",
                  "trainingDataNote", "approval"]
MODEL_KINDS = {"model", "lora"}


class ModelHashError(Exception):
    """Raised on a hash mismatch; the message never contains local paths."""


def load_models(path: Path | str = DEFAULT_MODELS_PATH) -> dict:
    return json.loads(Path(path).read_text(encoding="utf-8"))


def validate_models(doc: dict) -> list[str]:
    problems = []
    if doc.get("schemaVersion") != 1:
        problems.append("schemaVersion must be 1")
    models = doc.get("models")
    if not isinstance(models, list):
        return problems + ["models must be a list"]
    seen = set()
    for m in models:
        mid = m.get("id", "<no id>")
        for f in MODEL_REQUIRED:
            if _blank(m.get(f), False):
                problems.append(f"{mid}: missing {f}")
        if m.get("kind") not in MODEL_KINDS:
            problems.append(f"{mid}: kind must be one of {sorted(MODEL_KINDS)}")
        if m.get("commercialUse") is not True:
            problems.append(f"{mid}: license must permit commercial use (FR-021)")
        sha = m.get("sha256", "")
        if not (isinstance(sha, str) and len(sha) == 64 and all(c in "0123456789abcdef" for c in sha)):
            problems.append(f"{mid}: sha256 must be 64 lowercase hex characters")
        size = m.get("sizeBytes")
        if not (isinstance(size, int) and not isinstance(size, bool) and size > 0):
            problems.append(f"{mid}: sizeBytes must be a positive integer")
        if not str(m.get("comfyFolder", "")).startswith("models/"):
            problems.append(f"{mid}: comfyFolder must start with 'models/'")
        if mid in seen:
            problems.append(f"{mid}: duplicate id")
        seen.add(mid)
    return problems


def file_sha256(path: Path | str) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def verify_model_file(entry: dict, path: Path | str) -> None:
    """Verify a model file's SHA-256 before use. Refuses a mismatch without exposing local paths."""
    actual = file_sha256(path)
    if actual != entry["sha256"]:
        raise ModelHashError(f"model {entry['id']!r} version {entry['version']!r}: SHA-256 mismatch "
                             f"(expected {entry['sha256'][:12]}..., got {actual[:12]}...)")


# --- workflow-level verification ---------------------------------------------------

MODELS_DIR_ENV = "COMFY_MODELS_DIR"
LEGACY_LORAS_DIR_ENV = "COMFY_LORAS_DIR"  # <models>/loras; kept for existing callers

# ComfyUI loader node class -> input holding the model file name.
LOADER_INPUTS = {"UNETLoader": "unet_name", "CLIPLoader": "clip_name", "VAELoader": "vae_name",
                 "LoraLoader": "lora_name"}


class ModelsRootError(Exception):
    """The ComfyUI models folder is not configured. The message is actionable and names no local path."""


def resolve_models_root(env: dict | None = None) -> Path:
    """ComfyUI `models/` folder from $COMFY_MODELS_DIR (or the parent of the legacy $COMFY_LORAS_DIR)."""
    env = os.environ if env is None else env
    direct = (env.get(MODELS_DIR_ENV) or "").strip()
    if direct:
        return Path(direct)
    legacy = (env.get(LEGACY_LORAS_DIR_ENV) or "").strip()
    if legacy:
        return Path(legacy).parent
    raise ModelsRootError(f"ComfyUI models folder is not configured: set the {MODELS_DIR_ENV} environment variable "
                          f"to the 'models' directory of the local ComfyUI install (the folder that contains "
                          f"diffusion_models, text_encoders, vae and loras).")


def model_entry_path(entry: dict, models_root: Path | str) -> Path:
    sub = entry["comfyFolder"]
    sub = sub[len("models/"):] if sub.startswith("models/") else sub
    return Path(models_root) / sub / entry["fileName"]


def workflow_model_files(graph: dict) -> list[str]:
    """File names loaded by the loader nodes of an API-format ComfyUI graph, in node order, de-duplicated."""
    files: list[str] = []
    for key in sorted(graph, key=lambda k: (len(str(k)), str(k))):
        node = graph[key]
        inp = LOADER_INPUTS.get(node.get("class_type"))
        if inp and isinstance(node.get("inputs", {}).get(inp), str) and node["inputs"][inp] not in files:
            files.append(node["inputs"][inp])
    return files


def verify_workflow_models(graph: dict, models_root: Path | str | None = None, doc: dict | None = None) -> list[dict]:
    """Verify every model file a workflow loads (diffusion model, text encoder, VAE, LoRAs) against models.json.

    Refuses (ModelHashError) a loaded file that is not registered, missing on disk, or has a different SHA-256.
    Diagnostics name the model id and file name only, never local paths. Returns the registry entries verified.
    """
    doc = load_models() if doc is None else doc
    root = resolve_models_root() if models_root is None else Path(models_root)
    by_file = {m["fileName"]: m for m in doc["models"]}
    entries = []
    for name in workflow_model_files(graph):
        entry = by_file.get(name)
        if entry is None:
            raise ModelHashError(f"workflow loads {name!r}, which is not registered in models.json")
        path = model_entry_path(entry, root)
        if not path.is_file():
            raise ModelHashError(f"model {entry['id']!r} version {entry['version']!r}: file {name!r} not found "
                                 f"under the configured ComfyUI models folder")
        verify_model_file(entry, path)
        entries.append(entry)
    return entries

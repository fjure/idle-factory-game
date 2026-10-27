"""Provenance records (data-model) and models.json (hash-verified model/LoRA registry)."""
from __future__ import annotations

import hashlib
import json
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

MODEL_REQUIRED = ["id", "kind", "sourceUrl", "version", "license", "sha256", "trainingDataNote"]
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

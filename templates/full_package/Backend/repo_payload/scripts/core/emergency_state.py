from __future__ import annotations

import json
import shutil
from pathlib import Path

from scripts.core.path_utils import ensure_directory
from scripts.core.schema_validator import SchemaValidationError, validate_data
from scripts.core.time_utils import utc_now_iso


DEFAULT_EMERGENCY_STATE = {
    "schema_version": "1.0.0",
    "active": False,
    "activated_at_utc": None,
    "activated_by": None,
    "activation_reason": None,
    "reset_at_utc": None,
    "reset_by": None,
    "reset_reason": None,
    "last_audit_id": None,
}


class EmergencyStateCorrupt(RuntimeError):
    pass


def emergency_state_path(state_dir: str | Path) -> Path:
    return Path(state_dir) / "emergency_stop.json"


def initialize_emergency_state(state_dir: str | Path) -> Path:
    path = emergency_state_path(state_dir)
    ensure_directory(path.parent)
    if not path.exists():
        path.write_text(json.dumps(DEFAULT_EMERGENCY_STATE, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    return path


def quarantine_corrupt_state(path: Path) -> Path:
    quarantine_dir = ensure_directory(path.parent / "quarantine")
    quarantine_path = quarantine_dir / f"{utc_now_iso().replace(':', '')}_{path.name}"
    shutil.copy2(path, quarantine_path)
    return quarantine_path


def read_emergency_state(state_dir: str | Path, *, initialize: bool = True) -> dict:
    path = emergency_state_path(state_dir)
    if initialize:
        initialize_emergency_state(state_dir)
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
        validate_data("emergency_stop_state", data)
    except (json.JSONDecodeError, OSError, SchemaValidationError) as exc:
        if path.exists():
            quarantine_corrupt_state(path)
        raise EmergencyStateCorrupt(f"corrupt emergency stop state: {path}") from exc
    return data


def is_emergency_active(state_dir: str | Path) -> bool:
    return bool(read_emergency_state(state_dir).get("active"))


def write_emergency_state(state_dir: str | Path, state: dict) -> Path:
    validate_data("emergency_stop_state", state)
    path = emergency_state_path(state_dir)
    ensure_directory(path.parent)
    path.write_text(json.dumps(state, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    return path

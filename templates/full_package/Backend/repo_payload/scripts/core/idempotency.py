from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from scripts.core.audit_chain import sha256_json
from scripts.core.path_utils import ensure_directory
from scripts.core.time_utils import utc_now_iso


def idempotency_key(parts: dict[str, Any]) -> str:
    return sha256_json(parts)


def record_exists(store_path: str | Path, key: str) -> bool:
    path = Path(store_path)
    if not path.exists():
        return False
    for line in path.read_text(encoding="utf-8").splitlines():
        if not line.strip():
            continue
        try:
            record = json.loads(line)
        except json.JSONDecodeError:
            continue
        if record.get("idempotency_key") == key:
            return True
    return False


def append_record(store_path: str | Path, *, key: str, operation: str, run_id: str, artifact_path: str) -> Path:
    path = Path(store_path)
    ensure_directory(path.parent)
    record = {
        "schema_version": "1.0.0",
        "idempotency_key": key,
        "operation": operation,
        "run_id": run_id,
        "created_at_utc": utc_now_iso(),
        "artifact_path": artifact_path,
    }
    with path.open("a", encoding="utf-8") as handle:
        handle.write(json.dumps(record, sort_keys=True) + "\n")
    return path

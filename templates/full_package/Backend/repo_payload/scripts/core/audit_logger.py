from __future__ import annotations

import json
import uuid
from pathlib import Path
from typing import Any

from scripts.core.audit_chain import previous_hash, sha256_json
from scripts.core.path_utils import ensure_directory
from scripts.core.secret_masker import mask_secrets
from scripts.core.time_utils import utc_now_iso


def append_audit_event(
    audit_dir: str | Path,
    *,
    run_id: str,
    event_type: str,
    actor: str,
    operation: str,
    input_hash: str,
    output_hash: str,
    details: dict[str, Any] | None = None,
) -> Path:
    timestamp = utc_now_iso()
    audit_path = ensure_directory(audit_dir) / f"{timestamp[:10]}.jsonl"
    safe_details = mask_secrets(details or {})
    record = {
        "schema_version": "1.0.0",
        "audit_id": str(uuid.uuid4()),
        "timestamp_utc": timestamp,
        "run_id": run_id,
        "event_type": event_type,
        "actor": actor,
        "operation": operation,
        "input_hash": input_hash,
        "output_hash": output_hash,
        "previous_audit_hash": previous_hash(audit_path),
        "audit_hash": "",
        "secret_masking_applied": True,
        "details": safe_details,
    }
    record["audit_hash"] = sha256_json({key: value for key, value in record.items() if key != "audit_hash"})
    with audit_path.open("a", encoding="utf-8") as handle:
        handle.write(json.dumps(record, sort_keys=True) + "\n")
    return audit_path

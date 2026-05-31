from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any

from scripts.core.audit_chain import sha256_json
from scripts.core.path_utils import ensure_directory
from scripts.core.secret_masker import mask_secrets
from scripts.core.time_utils import utc_now_iso


def add_common_args(parser: argparse.ArgumentParser) -> argparse.ArgumentParser:
    parser.add_argument("--config", required=True)
    parser.add_argument("--runtime-mode", required=True)
    parser.add_argument("--run-id", required=True)
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--audit-dir", required=True)
    parser.add_argument("--state-dir", required=True)
    parser.add_argument("--kanban-task-id", default="")
    parser.add_argument("--json-out", default="")
    return parser


def build_envelope(
    *,
    ok: bool,
    script: str,
    operation: str,
    runtime_mode: str,
    run_id: str,
    started_at_utc: str,
    exit_code: int,
    outputs: dict[str, Any] | None = None,
    warnings: list[str] | None = None,
    errors: list[str] | None = None,
    safe_to_continue: bool = True,
    broker_response: dict[str, Any] | None = None,
    kanban_task_id: str = "",
    kanban_status: str = "done",
    kanban_summary: str = "",
) -> dict[str, Any]:
    safe_outputs = outputs or {}
    artifact_paths = safe_outputs.get("artifact_paths", [])
    return {
        "schema_version": "1.0.0",
        "ok": ok,
        "script": script,
        "operation": operation,
        "runtime_mode": runtime_mode,
        "run_id": run_id,
        "started_at_utc": started_at_utc,
        "finished_at_utc": utc_now_iso(),
        "exit_code": exit_code,
        "error_code": None if ok else str(exit_code),
        "inputs_hash": sha256_json({"script": script, "operation": operation, "runtime_mode": runtime_mode, "run_id": run_id}),
        "outputs": {
            "json_path": safe_outputs.get("json_path"),
            "markdown_report_path": safe_outputs.get("markdown_report_path"),
            "audit_event_paths": safe_outputs.get("audit_event_paths", []),
            "artifact_paths": artifact_paths,
        },
        "warnings": warnings or [],
        "errors": errors or [],
        "safe_to_continue": safe_to_continue,
        "broker_response": mask_secrets(broker_response) if broker_response is not None else None,
        "kanban": {
            "task_id": kanban_task_id,
            "recommended_status": kanban_status,
            "summary": kanban_summary,
            "artifact_paths": artifact_paths,
        },
    }


def write_envelope(envelope: dict[str, Any], output_dir: str | Path, json_out: str | Path | None = None) -> Path:
    if json_out:
        path = Path(json_out)
        ensure_directory(path.parent)
    else:
        path = ensure_directory(output_dir) / f"{envelope['run_id']}_{envelope['operation']}_result.json"
    envelope["outputs"]["json_path"] = str(path)
    path.write_text(json.dumps(envelope, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(path)
    return path

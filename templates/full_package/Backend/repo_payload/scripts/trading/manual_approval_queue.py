from __future__ import annotations

import argparse
import json
import sys
from datetime import UTC, datetime, timedelta
from pathlib import Path

if __package__ is None or __package__ == "":
    sys.path.append(str(Path(__file__).resolve().parents[2]))

from scripts.core.audit_chain import sha256_json
from scripts.core.audit_logger import append_audit_event
from scripts.core.emergency_state import EmergencyStateCorrupt, initialize_emergency_state, read_emergency_state
from scripts.core.path_utils import ensure_directory, project_root, resolve_path
from scripts.core.policy_validator import validate_runtime_mode
from scripts.core.result_envelope import add_common_args, build_envelope, write_envelope
from scripts.core.schema_validator import SchemaValidationError, validate_data, validate_json_file
from scripts.core.time_utils import utc_now, utc_now_iso


SCRIPT_NAME = "scripts/trading/manual_approval_queue.py"
OPERATION = "manual_approval_queue"


def build_parser() -> argparse.ArgumentParser:
    parser = add_common_args(argparse.ArgumentParser(description="Create and manage deterministic manual approval artifacts."))
    parser.add_argument("--action", required=True, choices=["create", "approve", "reject", "expire", "status"])
    parser.add_argument("--order-intent-hash", required=True)
    parser.add_argument("--actor", required=True)
    parser.add_argument("--reason", default="")
    parser.add_argument("--risk-decision", default="")
    parser.add_argument("--expires-in-minutes", type=int, default=60)
    return parser


def _queue_path(state_dir: str) -> Path:
    return ensure_directory(resolve_path(state_dir)) / "manual_approval_queue.json"


def _load_queue(path: Path) -> list[dict]:
    if not path.exists():
        return []
    data = json.loads(path.read_text(encoding="utf-8"))
    return data if isinstance(data, list) else []


def _write_queue(path: Path, queue: list[dict]) -> None:
    path.write_text(json.dumps(queue, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def _approval_hash(artifact: dict) -> str:
    basis = {key: value for key, value in artifact.items() if key != "approval_hash"}
    return sha256_json(basis)


def run(args: argparse.Namespace) -> tuple[dict, int]:
    started = utc_now_iso()
    warnings: list[str] = []
    errors: list[str] = []
    artifact_paths: list[str] = []
    exit_code = 0

    mode_result = validate_runtime_mode(args.runtime_mode)
    if not mode_result.ok:
        errors.extend(mode_result.errors)
        exit_code = mode_result.exit_code

    try:
        initialize_emergency_state(args.state_dir)
        emergency_state = read_emergency_state(args.state_dir)
        if emergency_state["active"]:
            raise EmergencyStateCorrupt("emergency stop is active")
    except EmergencyStateCorrupt as exc:
        errors.append(str(exc))
        exit_code = 8

    queue_file = _queue_path(args.state_dir)
    queue = _load_queue(queue_file)
    approval_artifact = None
    if not errors:
        match = next((item for item in reversed(queue) if item.get("order_intent_hash") == args.order_intent_hash), None)
        now = utc_now()
        if args.action == "create":
            if not args.risk_decision:
                errors.append("risk decision artifact is required to create manual approval")
                exit_code = 2
            else:
                risk_decision = validate_json_file("risk_decision", resolve_path(args.risk_decision))
                approval_artifact = {
                    "schema_version": "1.0.0",
                    "approval_id": sha256_json({"order_intent_hash": args.order_intent_hash, "run_id": args.run_id}),
                    "order_intent_hash": args.order_intent_hash,
                    "approver": args.actor,
                    "decision": "pending",
                    "reason": args.reason,
                    "decision_timestamp_utc": utc_now_iso(),
                    "expires_at_utc": (now + timedelta(minutes=args.expires_in_minutes)).strftime("%Y-%m-%dT%H:%M:%SZ"),
                    "risk_decision_id": risk_decision["risk_decision_id"],
                    "approval_hash": "",
                }
                approval_artifact["approval_hash"] = _approval_hash(approval_artifact)
                queue.append(approval_artifact)
        elif match is None:
            errors.append("no approval artifact found for the provided order_intent_hash")
            exit_code = 1
        else:
            approval_artifact = dict(match)
            expires_at = datetime.fromisoformat(approval_artifact["expires_at_utc"].replace("Z", "+00:00"))
            if args.action == "status" and expires_at < now.replace(tzinfo=UTC) and approval_artifact["decision"] == "pending":
                approval_artifact["decision"] = "expired"
                approval_artifact["reason"] = "approval expired before use"
                approval_artifact["decision_timestamp_utc"] = utc_now_iso()
            elif args.action == "approve":
                approval_artifact["decision"] = "approved"
                approval_artifact["reason"] = args.reason
                approval_artifact["approver"] = args.actor
                approval_artifact["decision_timestamp_utc"] = utc_now_iso()
            elif args.action == "reject":
                approval_artifact["decision"] = "rejected"
                approval_artifact["reason"] = args.reason
                approval_artifact["approver"] = args.actor
                approval_artifact["decision_timestamp_utc"] = utc_now_iso()
            elif args.action == "expire":
                approval_artifact["decision"] = "expired"
                approval_artifact["reason"] = args.reason or "approval explicitly expired"
                approval_artifact["approver"] = args.actor
                approval_artifact["decision_timestamp_utc"] = utc_now_iso()
            approval_artifact["approval_hash"] = _approval_hash(approval_artifact)
            queue = [item for item in queue if item.get("approval_id") != approval_artifact["approval_id"]]
            queue.append(approval_artifact)

        if approval_artifact is not None and not errors:
            validate_data(
                "manual_approval",
                {key: value for key, value in approval_artifact.items() if key != "risk_decision_id"},
            )
            approval_path = ensure_directory(project_root() / "data" / "risk" / "manual_approvals") / f"{args.run_id}_{approval_artifact['approval_id']}.json"
            approval_path.write_text(json.dumps(approval_artifact, indent=2, sort_keys=True) + "\n", encoding="utf-8")
            artifact_paths.append(str(approval_path))
            _write_queue(queue_file, queue)
            artifact_paths.append(str(queue_file))

    ok = not errors and approval_artifact is not None
    audit_path = append_audit_event(
        args.audit_dir,
        run_id=args.run_id,
        event_type="manual_approval_queue",
        actor=args.actor,
        operation=args.action,
        input_hash=sha256_json({"order_intent_hash": args.order_intent_hash, "action": args.action, "reason": args.reason}),
        output_hash=sha256_json({"ok": ok, "artifact_paths": artifact_paths, "errors": errors}),
        details={"artifact_paths": artifact_paths, "errors": errors, "warnings": warnings},
    )

    envelope = build_envelope(
        ok=ok,
        script=SCRIPT_NAME,
        operation=OPERATION,
        runtime_mode=args.runtime_mode,
        run_id=args.run_id,
        started_at_utc=started,
        exit_code=exit_code,
        outputs={"json_path": None, "markdown_report_path": None, "audit_event_paths": [str(audit_path)], "artifact_paths": artifact_paths},
        warnings=warnings,
        errors=errors,
        safe_to_continue=ok and approval_artifact.get("decision") == "approved" if approval_artifact else False,
        kanban_task_id=args.kanban_task_id,
        kanban_status="done" if ok else "blocked",
        kanban_summary=f"Manual approval action {args.action} completed." if ok else "Manual approval action failed.",
    )
    write_envelope(envelope, args.output_dir, args.json_out or None)
    return envelope, exit_code


def main() -> None:
    _, exit_code = run(build_parser().parse_args())
    raise SystemExit(exit_code)


if __name__ == "__main__":
    main()

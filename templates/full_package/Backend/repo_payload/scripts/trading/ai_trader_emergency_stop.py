from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

if __package__ is None or __package__ == "":
    sys.path.append(str(Path(__file__).resolve().parents[2]))

from scripts.core.audit_chain import sha256_json
from scripts.core.audit_logger import append_audit_event
from scripts.core.emergency_state import (
    EmergencyStateCorrupt,
    initialize_emergency_state,
    read_emergency_state,
    write_emergency_state,
)
from scripts.core.policy_validator import validate_runtime_mode
from scripts.core.result_envelope import add_common_args, build_envelope, write_envelope
from scripts.core.time_utils import utc_now_iso


SCRIPT_NAME = "scripts/trading/ai_trader_emergency_stop.py"
OPERATION = "emergency_stop"


def build_parser() -> argparse.ArgumentParser:
    parser = add_common_args(argparse.ArgumentParser(description="Read or mutate the global emergency stop state."))
    parser.add_argument("--action", required=True, choices=["status", "activate", "reset"])
    parser.add_argument("--reason", default="")
    parser.add_argument("--actor", required=True)
    return parser


def _mutate_state(state: dict, action: str, actor: str, reason: str) -> dict:
    now = utc_now_iso()
    if action == "activate":
        if not reason:
            raise ValueError("activation reason is required")
        state.update(
            {
                "active": True,
                "activated_at_utc": now,
                "activated_by": actor,
                "activation_reason": reason,
            }
        )
    elif action == "reset":
        if not reason:
            raise ValueError("reset reason is required")
        state.update(
            {
                "active": False,
                "reset_at_utc": now,
                "reset_by": actor,
                "reset_reason": reason,
            }
        )
    return state


def run(args: argparse.Namespace) -> tuple[dict, int]:
    started = utc_now_iso()
    warnings: list[str] = []
    errors: list[str] = []
    exit_code = 0

    mode_result = validate_runtime_mode(args.runtime_mode)
    if not mode_result.ok:
        errors.extend(mode_result.errors)
        exit_code = mode_result.exit_code

    try:
        initialize_emergency_state(args.state_dir)
        state = read_emergency_state(args.state_dir)
        if not errors and args.action in {"activate", "reset"}:
            state = _mutate_state(state, args.action, args.actor, args.reason)
            write_emergency_state(args.state_dir, state)
    except EmergencyStateCorrupt as exc:
        state = None
        errors.append(str(exc))
        exit_code = 8
    except ValueError as exc:
        state = None
        errors.append(str(exc))
        exit_code = 2

    state_path = Path(args.state_dir) / "emergency_stop.json"
    artifact_paths = [str(state_path)] if state_path.exists() else []
    audit_path = append_audit_event(
        args.audit_dir,
        run_id=args.run_id,
        event_type="emergency_stop",
        actor=args.actor,
        operation=args.action,
        input_hash=sha256_json({"action": args.action, "actor": args.actor, "reason": args.reason}),
        output_hash=sha256_json(state or {"errors": errors}),
        details={"action": args.action, "state": state, "errors": errors},
    )

    ok = not errors
    if ok and state and state.get("active"):
        warnings.append("emergency stop is active")
    envelope = build_envelope(
        ok=ok,
        script=SCRIPT_NAME,
        operation=OPERATION,
        runtime_mode=args.runtime_mode,
        run_id=args.run_id,
        started_at_utc=started,
        exit_code=exit_code,
        outputs={
            "json_path": None,
            "markdown_report_path": None,
            "audit_event_paths": [str(audit_path)],
            "artifact_paths": artifact_paths,
        },
        warnings=warnings,
        errors=errors,
        safe_to_continue=ok and bool(state and not state.get("active")),
        kanban_task_id=args.kanban_task_id,
        kanban_status="done" if ok else "blocked",
        kanban_summary=f"Emergency stop {args.action} completed." if ok else "Emergency stop action failed.",
    )
    write_envelope(envelope, args.output_dir, args.json_out or None)
    return envelope, exit_code


def main() -> None:
    _, exit_code = run(build_parser().parse_args())
    raise SystemExit(exit_code)


if __name__ == "__main__":
    main()

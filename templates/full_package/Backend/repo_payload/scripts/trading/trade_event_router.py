from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

if __package__ is None or __package__ == "":
    sys.path.append(str(Path(__file__).resolve().parents[2]))

from scripts.core.audit_chain import sha256_json
from scripts.core.audit_logger import append_audit_event
from scripts.core.config_loader import ConfigError, load_json_file
from scripts.core.emergency_state import EmergencyStateCorrupt, initialize_emergency_state, read_emergency_state
from scripts.core.path_utils import ensure_directory, resolve_path
from scripts.core.policy_validator import validate_runtime_mode
from scripts.core.result_envelope import add_common_args, build_envelope, write_envelope
from scripts.core.schema_validator import SchemaValidationError, validate_json_file, validate_data
from scripts.core.time_utils import utc_now_iso


SCRIPT_NAME = "scripts/trading/trade_event_router.py"
OPERATION = "trade_event_router"


def build_parser() -> argparse.ArgumentParser:
    parser = add_common_args(argparse.ArgumentParser(description="Create deterministic routing state from a normalized event and runtime mode."))
    parser.add_argument("--normalized-event", required=True)
    parser.add_argument("--route-policy", required=True)
    return parser


def _planned_stages(runtime_mode: str) -> list[str]:
    if runtime_mode == "observe_only":
        return ["source_intake", "normalization", "reporting"]
    if runtime_mode == "analysis_only":
        return ["model_analysis", "reporting"]
    if runtime_mode == "risk_review":
        return ["risk_review", "manual_approval", "reporting"]
    if runtime_mode in {"paper_manual", "paper_auto", "live_manual"}:
        return ["risk_review", "manual_approval", "broker_submission", "reporting"]
    return ["reporting"]


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
        normalized_event = validate_json_file("normalized_trade_event", resolve_path(args.normalized_event))
        route_policy = load_json_file(resolve_path(args.route_policy))
    except (ConfigError, FileNotFoundError, SchemaValidationError) as exc:
        errors.append(str(exc))
        exit_code = 5 if isinstance(exc, SchemaValidationError) else 1
        normalized_event = {}
        route_policy = {}
    except EmergencyStateCorrupt as exc:
        errors.append(str(exc))
        exit_code = 8
        normalized_event = {}
        route_policy = {}

    daily_state_path = None
    if not errors:
        stages = []
        for stage_name in _planned_stages(args.runtime_mode):
            status = "pending"
            summary = "awaiting prior stage outputs"
            if stage_name == "model_analysis" and args.runtime_mode == "analysis_only":
                status = "ready"
                summary = "normalized event is ready for Hermes-attached analysis"
            elif stage_name == "risk_review" and args.runtime_mode in {"risk_review", "paper_manual", "paper_auto", "live_manual"}:
                status = "ready"
                summary = "normalized event is ready for deterministic risk evaluation"
            elif stage_name == "manual_approval":
                status = "ready" if args.runtime_mode in {"paper_manual", "live_manual"} else "skipped"
                summary = "manual approval required for manual submission modes" if status == "ready" else "manual approval not required for this mode"
            elif stage_name == "broker_submission":
                status = "ready" if args.runtime_mode in {"paper_manual", "paper_auto", "live_manual"} else "skipped"
                summary = "submission allowed only after risk and approval prerequisites pass"
            elif stage_name == "reporting":
                status = "ready"
                summary = "deterministic reporting stage can summarize the route plan"
            elif stage_name in {"source_intake", "normalization"}:
                status = "completed"
                summary = "upstream artifact already exists for this routing pass"
            stages.append({"name": stage_name, "status": status, "summary": summary})

        daily_state = {
            "schema_version": "1.0.0",
            "run_id": args.run_id,
            "cycle_date": utc_now_iso()[:10],
            "runtime_mode": args.runtime_mode,
            "started_at_utc": started,
            "finished_at_utc": utc_now_iso(),
            "stages": stages,
        }
        validate_data("daily_run_state", daily_state)
        daily_state_path = ensure_directory(resolve_path(args.state_dir)) / f"{args.run_id}_route_state.json"
        daily_state_path.write_text(json.dumps(daily_state, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        artifact_paths.append(str(daily_state_path))

    ok = not errors
    audit_path = append_audit_event(
        args.audit_dir,
        run_id=args.run_id,
        event_type="trade_event_router",
        actor="script",
        operation=OPERATION,
        input_hash=sha256_json({"normalized_event": args.normalized_event, "route_policy": args.route_policy, "runtime_mode": args.runtime_mode}),
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
        safe_to_continue=ok,
        kanban_task_id=args.kanban_task_id,
        kanban_status="done" if ok else "blocked",
        kanban_summary="Routing state created." if ok else "Routing failed; review deterministic route state errors.",
    )
    write_envelope(envelope, args.output_dir, args.json_out or None)
    return envelope, exit_code


def main() -> None:
    _, exit_code = run(build_parser().parse_args())
    raise SystemExit(exit_code)


if __name__ == "__main__":
    main()

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from types import SimpleNamespace

if __package__ is None or __package__ == "":
    sys.path.append(str(Path(__file__).resolve().parents[2]))

from scripts.core.audit_chain import sha256_json
from scripts.core.audit_logger import append_audit_event
from scripts.core.config_loader import ConfigError, load_json_file
from scripts.core.emergency_state import EmergencyStateCorrupt, initialize_emergency_state, read_emergency_state
from scripts.core.failure_notifications import dispatch_failure_notifications
from scripts.core.path_utils import ensure_directory, resolve_path
from scripts.core.policy_validator import validate_runtime_mode
from scripts.core.result_envelope import add_common_args, build_envelope, write_envelope
from scripts.core.schema_validator import validate_data
from scripts.core.time_utils import utc_now_iso
from scripts.validation.ai_trader_health_check import run as run_health_check


SCRIPT_NAME = "scripts/trading/ai_trader_daily_cycle.py"
OPERATION = "ai_trader_daily_cycle"

DEFAULT_STAGE_PLANS = {
    "health_only": ["health_check"],
    "observe_only": ["health_check", "source_intake", "normalization", "reporting"],
    "analysis_only": ["health_check", "source_intake", "normalization", "model_analysis", "reporting"],
    "risk_review": ["health_check", "source_intake", "normalization", "model_analysis", "broker_context", "risk_review", "manual_approval", "reporting"],
    "paper_manual": ["health_check", "source_intake", "normalization", "model_analysis", "broker_context", "risk_review", "manual_approval", "broker_submission", "order_monitoring", "reporting"],
    "paper_auto": ["health_check", "source_intake", "normalization", "model_analysis", "broker_context", "risk_review", "broker_submission", "order_monitoring", "reporting"],
    "live_manual": ["health_check", "source_intake", "normalization", "model_analysis", "broker_context", "risk_review", "manual_approval", "broker_submission", "order_monitoring", "reporting"],
    "live_auto": ["health_check", "source_intake", "normalization", "model_analysis", "broker_context", "risk_review", "broker_submission", "order_monitoring", "reporting"],
}

CRITICAL_STAGES = {
    "health_check",
    "source_intake",
    "normalization",
    "model_analysis",
    "broker_context",
    "risk_review",
    "manual_approval",
    "broker_submission",
    "order_monitoring",
}


def build_parser() -> argparse.ArgumentParser:
    parser = add_common_args(argparse.ArgumentParser(description="Persist deterministic daily cycle state for a selected stage set."))
    parser.add_argument("--cycle-date", required=True)
    parser.add_argument("--stages", required=True)
    parser.add_argument("--stage-gates-json")
    return parser


def _parse_stage_names(runtime_mode: str, stages_arg: str) -> list[str]:
    requested = [stage.strip() for stage in stages_arg.split(",") if stage.strip()]
    if not requested:
        raise ValueError("at least one stage is required")
    if len(requested) == 1 and requested[0].lower() == "all":
        return list(DEFAULT_STAGE_PLANS.get(runtime_mode, ["health_check", "reporting"]))
    if "health_check" not in requested:
        requested.insert(0, "health_check")
    return requested


def _load_stage_gates(path: str | None) -> dict[str, dict[str, str]]:
    if not path:
        return {}
    payload = load_json_file(resolve_path(path))
    if isinstance(payload, dict):
        if isinstance(payload.get("stages"), list):
            stage_rows = payload["stages"]
        else:
            stage_rows = [
                {"name": name, **details} if isinstance(details, dict) else {"name": name, "status": str(details)}
                for name, details in payload.items()
            ]
    elif isinstance(payload, list):
        stage_rows = payload
    else:
        raise ConfigError("stage gates must be a list or object")

    gates: dict[str, dict[str, str]] = {}
    for row in stage_rows:
        if not isinstance(row, dict) or not row.get("name"):
            raise ConfigError("each stage gate must include a non-empty name")
        gates[str(row["name"])] = {
            "status": str(row.get("status", "unknown")),
            "summary": str(row.get("summary", "")),
        }
    return gates


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
        stage_names = _parse_stage_names(args.runtime_mode, args.stages)
        stage_gates = _load_stage_gates(args.stage_gates_json)
    except (ValueError, ConfigError, EmergencyStateCorrupt) as exc:
        errors.append(str(exc))
        exit_code = 8 if isinstance(exc, EmergencyStateCorrupt) else 2
        stage_names = []
        stage_gates = {}

    daily_state_path = None
    stages = []
    notification_artifacts: list[str] = []
    if not errors:
        health_args = SimpleNamespace(
            config=args.config,
            runtime_mode=args.runtime_mode,
            run_id=f"{args.run_id}_health_gate",
            output_dir=args.output_dir,
            audit_dir=args.audit_dir,
            state_dir=args.state_dir,
            kanban_task_id=args.kanban_task_id,
            json_out=None,
            check="all",
        )
        health_envelope, health_exit_code = run_health_check(health_args)
        artifact_paths.extend(health_envelope["outputs"]["artifact_paths"])
        artifact_paths.extend(health_envelope["outputs"]["audit_event_paths"])

        critical_failure_stage = None
        critical_failure_summary = ""
        if not health_envelope["ok"]:
            critical_failure_stage = "health_check"
            critical_failure_summary = "blocked by failed health check"
            warnings.append("health_check failed; downstream stages blocked")
            exit_code = health_exit_code or 1

        first_executable_stage = next((name for name in stage_names if name != "health_check"), None)

        for stage_name in stage_names:
            stage_status = "pending"
            stage_summary = "awaiting prior critical stage completion"

            if stage_name == "health_check":
                stage_status = "completed" if health_envelope["ok"] else "failed"
                stage_summary = "health check passed" if health_envelope["ok"] else "health check failed; downstream stages blocked"
            elif critical_failure_stage:
                stage_status = "blocked"
                stage_summary = f"blocked by failed critical stage: {critical_failure_stage}"
            else:
                gate = stage_gates.get(stage_name)
                if gate:
                    stage_status = gate["status"]
                    stage_summary = gate["summary"] or f"stage gate reported {stage_status}"
                    if stage_status in {"failed", "blocked"} and stage_name in CRITICAL_STAGES:
                        critical_failure_stage = stage_name
                        critical_failure_summary = stage_summary
                elif stage_name == first_executable_stage:
                    stage_status = "ready"
                    stage_summary = "first executable stage is ready"
                elif stage_name == "reporting":
                    stage_status = "ready"
                    stage_summary = "reporting is available only after upstream stages succeed"
                else:
                    stage_status = "pending"
                    stage_summary = "waiting for prior critical stage completion"

            stages.append({"name": stage_name, "status": stage_status, "summary": stage_summary})

        if critical_failure_stage:
            ok = False
            if critical_failure_stage != "health_check":
                warnings.append(f"{critical_failure_stage} reported a critical failure; downstream stages blocked")
            if critical_failure_summary:
                errors.append(critical_failure_summary)
            exit_code = exit_code or 9
            notification_warnings, notification_artifact = dispatch_failure_notifications(
                run_id=args.run_id,
                runtime_mode=args.runtime_mode,
                stage_name=critical_failure_stage,
                summary=critical_failure_summary or f"{critical_failure_stage} failed",
                output_dir=args.output_dir,
            )
            warnings.extend(notification_warnings)
            if notification_artifact:
                notification_artifacts.append(notification_artifact)
        else:
            ok = True

        daily_state = {
            "schema_version": "1.0.0",
            "run_id": args.run_id,
            "cycle_date": args.cycle_date,
            "runtime_mode": args.runtime_mode,
            "started_at_utc": started,
            "finished_at_utc": utc_now_iso(),
            "stages": stages,
        }
        validate_data("daily_run_state", daily_state)
        daily_state_path = ensure_directory(resolve_path(args.state_dir)) / f"{args.run_id}_daily_cycle.json"
        daily_state_path.write_text(json.dumps(daily_state, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        artifact_paths.append(str(daily_state_path))
        artifact_paths.extend(notification_artifacts)
    else:
        ok = False
        if errors:
            notification_warnings, notification_artifact = dispatch_failure_notifications(
                run_id=args.run_id,
                runtime_mode=args.runtime_mode,
                stage_name="preflight",
                summary="; ".join(errors),
                output_dir=args.output_dir,
            )
            warnings.extend(notification_warnings)
            if notification_artifact:
                artifact_paths.append(notification_artifact)

    audit_path = append_audit_event(
        args.audit_dir,
        run_id=args.run_id,
        event_type="ai_trader_daily_cycle",
        actor="script",
        operation=OPERATION,
        input_hash=sha256_json({"cycle_date": args.cycle_date, "stages": args.stages, "runtime_mode": args.runtime_mode, "stage_gates_json": args.stage_gates_json}),
        output_hash=sha256_json({"ok": ok, "artifact_paths": artifact_paths, "errors": errors, "stages": stages}),
        details={"artifact_paths": artifact_paths, "errors": errors, "warnings": warnings, "stages": stages},
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
        safe_to_continue=ok and not any(stage.get("status") in {"failed", "blocked"} for stage in stages),
        kanban_task_id=args.kanban_task_id,
        kanban_status="done" if ok else "blocked",
        kanban_summary="Daily cycle state updated." if ok else "Daily cycle blocked; downstream stages were not executed.",
    )
    write_envelope(envelope, args.output_dir, args.json_out or None)
    return envelope, exit_code


def main() -> None:
    _, exit_code = run(build_parser().parse_args())
    raise SystemExit(exit_code)


if __name__ == "__main__":
    main()

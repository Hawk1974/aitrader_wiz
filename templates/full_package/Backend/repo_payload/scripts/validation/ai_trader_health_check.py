from __future__ import annotations

import argparse
import json
import os
import sys
from pathlib import Path

if __package__ is None or __package__ == "":
    sys.path.append(str(Path(__file__).resolve().parents[2]))

from scripts.core.audit_chain import sha256_json
from scripts.core.audit_logger import append_audit_event
from scripts.core.config_loader import ConfigError, credential_presence, load_json_file
from scripts.core.emergency_state import EmergencyStateCorrupt, initialize_emergency_state, read_emergency_state
from scripts.core.path_utils import ensure_directory, ensure_project_layout, project_root
from scripts.core.policy_validator import broker_mode_for_runtime, validate_runtime_mode
from scripts.core.result_envelope import add_common_args, build_envelope, write_envelope
from scripts.core.schema_validator import SchemaValidationError, load_schema, validate_data
from scripts.core.time_utils import utc_now_iso


SCRIPT_NAME = "scripts/validation/ai_trader_health_check.py"
OPERATION = "health_check"
WORKSPACE_CONTEXT_FILES = ("AGENTS.md", "SOUL.md", "MEMORY.md", "USER.md")
PROFILE_CONTEXT_FILES = (
    "AGENTS.md",
    "SOUL.md",
    "HEARTBEAT.md",
    "memories/MEMORY.md",
    "memories/USER.md",
)


def build_parser() -> argparse.ArgumentParser:
    parser = add_common_args(argparse.ArgumentParser(description="Validate AlTrader environment health."))
    parser.add_argument("--check", default="all", choices=["all", "config", "credentials", "folders", "schemas", "state", "broker", "reports"])
    return parser


def _check(name: str, ok: bool, detail: str = "") -> dict:
    return {"name": name, "ok": ok, "detail": detail}


def _resolve_profile_home() -> Path:
    hermes_home = os.getenv("HERMES_HOME", "").strip()
    if hermes_home:
        return Path(hermes_home)
    return Path.home() / ".hermes"


def _validate_context_layout(root: Path) -> tuple[bool, str]:
    workspace_missing = [name for name in WORKSPACE_CONTEXT_FILES if not (root / name).exists()]
    if not workspace_missing:
        return True, "workspace-root context files are present"

    profile_home = _resolve_profile_home()
    profile_missing = [name for name in PROFILE_CONTEXT_FILES if not (profile_home / name).exists()]
    tooling_ok = (profile_home / "TOOLS.md").exists() or (profile_home / "TOOLING.md").exists()
    if not tooling_ok:
        profile_missing.append("TOOLS.md|TOOLING.md")
    if not profile_missing:
        return True, f"profile-backed context files are present under {profile_home}"

    return False, (
        "missing required context layout; "
        f"workspace missing {', '.join(workspace_missing)} and "
        f"profile home {profile_home} missing {', '.join(profile_missing)}"
    )


def run(args: argparse.Namespace) -> tuple[dict, int]:
    started = utc_now_iso()
    warnings: list[str] = []
    errors: list[str] = []
    checks: list[dict] = []
    root = project_root()

    mode_result = validate_runtime_mode(args.runtime_mode)
    checks.append(_check("runtime_mode", mode_result.ok, args.runtime_mode))
    if not mode_result.ok:
        errors.extend(mode_result.errors)

    try:
        config = load_json_file(args.config)
        checks.append(_check("config_file", True, args.config))
    except ConfigError as exc:
        config = {}
        checks.append(_check("config_file", False, str(exc)))
        errors.append(str(exc))

    try:
        ensure_project_layout(root)
        ensure_directory(args.output_dir)
        ensure_directory(args.audit_dir)
        ensure_directory(args.state_dir)
        checks.append(_check("folders", True, "required folders are writable"))
    except OSError as exc:
        checks.append(_check("folders", False, str(exc)))
        errors.append(str(exc))

    context_ok, context_detail = _validate_context_layout(root)
    checks.append(_check("context_layout", context_ok, context_detail))
    if not context_ok:
        errors.append(context_detail)

    schema_errors: list[str] = []
    for schema_path in sorted((root / "schemas").glob("*.schema.json")):
        schema_name = schema_path.name.removesuffix(".schema.json")
        try:
            load_schema(schema_name)
        except SchemaValidationError as exc:
            schema_errors.append(str(exc))
    checks.append(_check("schemas", not schema_errors, f"{len(schema_errors)} schema load errors"))
    errors.extend(schema_errors)

    try:
        initialize_emergency_state(args.state_dir)
        emergency_state = read_emergency_state(args.state_dir)
        checks.append(_check("emergency_stop", not emergency_state["active"], "inactive" if not emergency_state["active"] else "active"))
        if emergency_state["active"]:
            errors.append("emergency stop is active")
    except EmergencyStateCorrupt as exc:
        checks.append(_check("emergency_stop", False, str(exc)))
        errors.append(str(exc))

    broker_mode = broker_mode_for_runtime(args.runtime_mode)
    if broker_mode:
        presence = credential_presence(broker_mode)
        missing = [name for name, present in presence.items() if not present]
        ok = not missing
        checks.append(_check("credentials", ok, f"broker_mode={broker_mode}; missing={','.join(missing)}"))
        if missing:
            errors.append(f"missing required {broker_mode} credentials: {', '.join(missing)}")
    else:
        checks.append(_check("credentials", True, "no broker credentials required for this runtime mode"))

    if config.get("source_policy_requires_resolution"):
        warnings.append("source policy endpoint requires user-approved resolution")

    health_report = {
        "schema_version": "1.0.0",
        "run_id": args.run_id,
        "checked_at_utc": utc_now_iso(),
        "runtime_mode": args.runtime_mode,
        "ok": not errors,
        "checks": checks,
        "warnings": warnings,
        "errors": errors,
    }

    try:
        validate_data("health_report", health_report)
    except SchemaValidationError as exc:
        errors.append(str(exc))
        health_report["ok"] = False

    report_path = Path(args.output_dir) / f"{args.run_id}_health_report.json"
    report_path.write_text(json.dumps(health_report, indent=2, sort_keys=True) + "\n", encoding="utf-8")

    audit_path = append_audit_event(
        args.audit_dir,
        run_id=args.run_id,
        event_type="health_check",
        actor="script",
        operation=OPERATION,
        input_hash=sha256_json({"config": args.config, "runtime_mode": args.runtime_mode, "check": args.check}),
        output_hash=sha256_json(health_report),
        details={"ok": health_report["ok"], "checks": checks},
    )

    exit_code = 0 if health_report["ok"] else (mode_result.exit_code if not mode_result.ok else 1)
    envelope = build_envelope(
        ok=health_report["ok"],
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
            "artifact_paths": [str(report_path)],
        },
        warnings=warnings,
        errors=errors,
        safe_to_continue=health_report["ok"],
        kanban_task_id=args.kanban_task_id,
        kanban_status="done" if health_report["ok"] else "blocked",
        kanban_summary="Health check passed." if health_report["ok"] else "Health check failed; review artifacts.",
    )
    write_envelope(envelope, args.output_dir, args.json_out or None)
    return envelope, exit_code


def main() -> None:
    _, exit_code = run(build_parser().parse_args())
    raise SystemExit(exit_code)


if __name__ == "__main__":
    main()

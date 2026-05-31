from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

if __package__ is None or __package__ == "":
    sys.path.append(str(Path(__file__).resolve().parents[2]))

from scripts.core.audit_chain import sha256_json
from scripts.core.audit_logger import append_audit_event
from scripts.core.path_utils import ensure_directory
from scripts.core.result_envelope import add_common_args, build_envelope, write_envelope
from scripts.core.time_utils import utc_now_iso


SCRIPT_NAME = "scripts/validation/validate_vision_reasoning_fallback.py"
OPERATION = "validate_vision_reasoning_fallback"


def build_parser() -> argparse.ArgumentParser:
    return add_common_args(argparse.ArgumentParser(description="Validate presence of fallback vision/reasoning resources."))


def run(args: argparse.Namespace) -> tuple[dict, int]:
    started = utc_now_iso()
    root = Path(__file__).resolve().parents[2]
    warnings: list[str] = []
    errors: list[str] = []

    candidate_paths = [
        root / "hermes" / "skills" / "domain" / "validate_vision_reasoning_fallback" / "SKILL.md",
        root / "docs" / "handoff" / "hermes_ai_trader_agent_standup" / "skills" / "validate_vision_reasoning_fallback" / "SKILL.md",
    ]
    existing = [str(path) for path in candidate_paths if path.exists()]
    ok = bool(existing)
    if not ok:
        errors.append("missing fallback validation resources: validate_vision_reasoning_fallback skill not found")

    artifact_dir = ensure_directory(args.output_dir)
    report_path = artifact_dir / f"{args.run_id}_validate_vision_reasoning_fallback_report.json"
    report = {
        "schema_version": "1.0.0",
        "run_id": args.run_id,
        "runtime_mode": args.runtime_mode,
        "checked_at_utc": utc_now_iso(),
        "ok": ok,
        "resolved_resource_paths": existing,
        "checked_paths": [str(path) for path in candidate_paths],
        "errors": errors,
        "warnings": warnings,
    }
    report_path.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")

    audit_path = append_audit_event(
        args.audit_dir,
        run_id=args.run_id,
        event_type=OPERATION,
        actor="script",
        operation=OPERATION,
        input_hash=sha256_json({"runtime_mode": args.runtime_mode, "checked_paths": report["checked_paths"]}),
        output_hash=sha256_json(report),
        details={"ok": ok, "resolved_resource_paths": existing},
    )

    exit_code = 0 if ok else 1
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
            "artifact_paths": [str(report_path)],
        },
        warnings=warnings,
        errors=errors,
        safe_to_continue=ok,
        kanban_task_id=args.kanban_task_id,
        kanban_status="done" if ok else "blocked",
        kanban_summary="Fallback validation resources are present." if ok else "Fallback validation resources are missing.",
    )
    write_envelope(envelope, args.output_dir, args.json_out or None)
    return envelope, exit_code


def main() -> None:
    _, exit_code = run(build_parser().parse_args())
    raise SystemExit(exit_code)


if __name__ == "__main__":
    main()

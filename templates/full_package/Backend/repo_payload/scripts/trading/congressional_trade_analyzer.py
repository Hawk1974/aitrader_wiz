from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path
from typing import Any

if __package__ is None or __package__ == "":
    sys.path.append(str(Path(__file__).resolve().parents[2]))

from scripts.core.audit_chain import sha256_json
from scripts.core.audit_logger import append_audit_event
from scripts.core.config_loader import ConfigError, load_json_file
from scripts.core.emergency_state import EmergencyStateCorrupt, initialize_emergency_state, read_emergency_state
from scripts.core.path_utils import ensure_directory, ensure_project_layout, resolve_path
from scripts.core.policy_validator import validate_runtime_mode
from scripts.core.result_envelope import add_common_args, build_envelope, write_envelope
from scripts.core.schema_validator import SchemaValidationError, validate_json_file
from scripts.core.time_utils import utc_now_iso


SCRIPT_NAME = "scripts/trading/congressional_trade_analyzer.py"
OPERATION = "congressional_trade_analyzer"
FORBIDDEN_PATTERNS = [
    r"\bquantity\b",
    r"\bqty\b",
    r"\bnotional\b",
    r"\blimit[_ -]?price\b",
    r"\bstop[_ -]?price\b",
    r"\btake[_ -]?profit\b",
    r"\bclient[_ -]?order[_ -]?id\b",
    r"\bbroker[_ -]?endpoint\b",
    r"\bbroker[_ -]?payload\b",
    r"\bbypass risk gate\b",
    r"\bbypass manual approval\b",
]


def build_parser() -> argparse.ArgumentParser:
    parser = add_common_args(argparse.ArgumentParser(description="Validate a Hermes-produced congressional trade model artifact."))
    parser.add_argument("--normalized-event", required=True)
    parser.add_argument("--prompt", required=True)
    parser.add_argument("--model-artifact-out", required=True)
    parser.add_argument("--model-artifact-in", default="")
    return parser


def _detect_forbidden_content(model_artifact: dict[str, Any]) -> list[str]:
    text = json.dumps(model_artifact, sort_keys=True).lower()
    findings: list[str] = []
    for pattern in FORBIDDEN_PATTERNS:
        if re.search(pattern, text):
            findings.append(pattern)
    return findings


def run(args: argparse.Namespace) -> tuple[dict, int]:
    started = utc_now_iso()
    warnings: list[str] = []
    errors: list[str] = []
    artifact_paths: list[str] = []
    exit_code = 0
    safe_to_continue = True

    mode_result = validate_runtime_mode(args.runtime_mode)
    if not mode_result.ok:
        errors.extend(mode_result.errors)
        exit_code = mode_result.exit_code

    try:
        load_json_file(args.config)
        ensure_project_layout()
        initialize_emergency_state(args.state_dir)
        emergency_state = read_emergency_state(args.state_dir)
        if emergency_state["active"]:
            raise EmergencyStateCorrupt("emergency stop is active")
    except (ConfigError, OSError) as exc:
        errors.append(str(exc))
        exit_code = 2 if isinstance(exc, ConfigError) else 7
    except EmergencyStateCorrupt as exc:
        errors.append(str(exc))
        exit_code = 8

    validated_output_path = resolve_path(args.model_artifact_out)
    candidate_input_path = resolve_path(args.model_artifact_in or args.model_artifact_out)
    if not errors:
        try:
            normalized_event = validate_json_file("normalized_trade_event", resolve_path(args.normalized_event))
            prompt_path = resolve_path(args.prompt)
            if not prompt_path.exists():
                raise FileNotFoundError(f"missing prompt file: {prompt_path}")
            prompt_name = prompt_path.stem
            prompt_path.read_text(encoding="utf-8")
            model_artifact = validate_json_file("model_decision", candidate_input_path)

            if model_artifact["normalized_event_id"] != normalized_event["normalized_event_id"]:
                raise SchemaValidationError("model_decision normalized_event_id does not match normalized event input")
            if model_artifact["prompt_name"] != prompt_name:
                raise SchemaValidationError(
                    f"model_decision prompt_name {model_artifact['prompt_name']} does not match required prompt {prompt_name}"
                )
            if model_artifact["forbidden_fields_present"]:
                raise SchemaValidationError("model_decision indicates forbidden fields were present")
            if model_artifact["decision"] == "review_candidate" and not model_artifact["requires_human_review"]:
                raise SchemaValidationError("review_candidate artifacts must require human review")

            forbidden_findings = _detect_forbidden_content(model_artifact)
            if forbidden_findings:
                raise SchemaValidationError(
                    "model_decision contains forbidden broker/trade directives: " + ", ".join(forbidden_findings)
                )

            ensure_directory(validated_output_path.parent)
            validated_output_path.write_text(json.dumps(model_artifact, indent=2, sort_keys=True) + "\n", encoding="utf-8")
            artifact_paths.append(str(validated_output_path))
        except (FileNotFoundError, json.JSONDecodeError, SchemaValidationError) as exc:
            errors.append(str(exc))
            exit_code = 10 if isinstance(exc, SchemaValidationError) else 1
            safe_to_continue = False

    audit_path = append_audit_event(
        args.audit_dir,
        run_id=args.run_id,
        event_type="congressional_trade_analyzer",
        actor="script",
        operation=OPERATION,
        input_hash=sha256_json(
            {
                "normalized_event": args.normalized_event,
                "prompt": args.prompt,
                "model_artifact_in": str(candidate_input_path),
                "model_artifact_out": str(validated_output_path),
            }
        ),
        output_hash=sha256_json({"ok": not errors, "artifact_paths": artifact_paths, "warnings": warnings, "errors": errors}),
        details={
            "artifact_paths": artifact_paths,
            "warnings": warnings,
            "errors": errors,
        },
    )

    envelope = build_envelope(
        ok=not errors,
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
        safe_to_continue=safe_to_continue and not errors,
        kanban_task_id=args.kanban_task_id,
        kanban_status="done" if not errors else "blocked",
        kanban_summary="Hermes model artifact validated." if not errors else "Hermes model artifact rejected; review schema and forbidden content.",
    )
    write_envelope(envelope, args.output_dir, args.json_out or None)
    return envelope, exit_code


def main() -> None:
    _, exit_code = run(build_parser().parse_args())
    raise SystemExit(exit_code)


if __name__ == "__main__":
    main()

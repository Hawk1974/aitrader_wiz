from __future__ import annotations

import argparse
import sys
from pathlib import Path

if __package__ is None or __package__ == "":
    sys.path.append(str(Path(__file__).resolve().parents[2]))

from scripts.core.audit_chain import sha256_json
from scripts.core.audit_logger import append_audit_event
from scripts.core.path_utils import ensure_directory, project_root
from scripts.core.result_envelope import add_common_args, build_envelope, write_envelope
from scripts.core.secret_masker import mask_secrets
from scripts.core.time_utils import utc_now_iso
from scripts.integrations.alpaca_common import (
    SafeFailure,
    alpaca_get,
    prepare_broker_context,
    resolve_broker_mode,
    validate_and_write_json,
)


SCRIPT_NAME = "scripts/integrations/alpaca_account_status.py"
OPERATION = "alpaca_account_status"


def build_parser() -> argparse.ArgumentParser:
    parser = add_common_args(argparse.ArgumentParser(description="Fetch a masked Alpaca account status snapshot."))
    parser.add_argument("--broker-mode", required=True, choices=["paper", "live"])
    return parser


def run(args: argparse.Namespace) -> tuple[dict, int]:
    started = utc_now_iso()
    warnings: list[str] = []
    errors: list[str] = []
    artifact_paths: list[str] = []
    broker_response = None

    broker_mode = resolve_broker_mode(config_path=args.config, runtime_mode=args.runtime_mode, explicit_broker_mode=args.broker_mode)
    context_or_failure = prepare_broker_context(args=args, broker_mode=broker_mode)
    if isinstance(context_or_failure, SafeFailure):
        errors.extend(context_or_failure.errors)
        broker_response = context_or_failure.broker_response
        exit_code = context_or_failure.exit_code
        ok = False
    else:
        context = context_or_failure
        try:
            payload = alpaca_get(context, url=f"{context.trading_base_url}/v2/account")
            broker_response = mask_secrets(payload if isinstance(payload, dict) else {"payload": payload})
            account_status = {
                "schema_version": "1.0.0",
                "broker_mode": context.broker_mode,
                "fetched_at_utc": utc_now_iso(),
                "account_id_masked": mask_secrets(str(payload.get("id", ""))),
                "status": str(payload.get("status", "unknown")),
                "currency": str(payload.get("currency", "USD")),
                "buying_power": float(payload.get("buying_power", 0.0)),
                "cash": float(payload.get("cash", 0.0)),
                "portfolio_value": float(payload.get("portfolio_value", 0.0)),
                "trading_blocked": bool(payload.get("trading_blocked", False) or payload.get("account_blocked", False)),
            }
            output_path = ensure_directory(project_root() / "data" / "state") / f"{args.run_id}_account_status_{context.broker_mode}.json"
            validate_and_write_json(schema_name="account_status", artifact=account_status, path=output_path)
            artifact_paths.append(str(output_path))
            exit_code = 0
            ok = True
        except RuntimeError as exc:
            errors.append(f"alpaca account request failed: {exc}")
            broker_response = {"error": str(exc)}
            exit_code = 12 if broker_mode == "live" else 4
            ok = False

    audit_path = append_audit_event(
        args.audit_dir,
        run_id=args.run_id,
        event_type="alpaca_account_status",
        actor="script",
        operation=OPERATION,
        input_hash=sha256_json({"runtime_mode": args.runtime_mode, "broker_mode": broker_mode}),
        output_hash=sha256_json({"ok": ok, "artifact_paths": artifact_paths, "errors": errors}),
        details={"artifact_paths": artifact_paths, "errors": errors, "warnings": warnings, "broker_response": broker_response},
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
        broker_response=broker_response,
        kanban_task_id=args.kanban_task_id,
        kanban_status="done" if ok else "blocked",
        kanban_summary="Account status snapshot captured." if ok else "Account status failed; review masked broker payload.",
    )
    write_envelope(envelope, args.output_dir, args.json_out or None)
    return envelope, exit_code


def main() -> None:
    _, exit_code = run(build_parser().parse_args())
    raise SystemExit(exit_code)


if __name__ == "__main__":
    main()

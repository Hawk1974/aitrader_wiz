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
from scripts.integrations.alpaca_common import SafeFailure, alpaca_get, prepare_broker_context, resolve_broker_mode, validate_and_write_json


SCRIPT_NAME = "scripts/integrations/alpaca_position_manager.py"
OPERATION = "alpaca_position_manager"


def build_parser() -> argparse.ArgumentParser:
    parser = add_common_args(argparse.ArgumentParser(description="Fetch a current Alpaca position snapshot."))
    parser.add_argument("--broker-mode", required=True, choices=["paper", "live"])
    parser.add_argument("--symbols", default="")
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
        filter_symbols = {symbol.strip().upper() for symbol in args.symbols.split(",") if symbol.strip()}
        try:
            payload = alpaca_get(context, url=f"{context.trading_base_url}/v2/positions")
            positions_payload = payload if isinstance(payload, list) else []
            broker_response = mask_secrets({"positions": positions_payload})
            positions = []
            total_market_value = 0.0
            for item in positions_payload:
                symbol = str(item.get("symbol", "")).upper()
                if filter_symbols and symbol not in filter_symbols:
                    continue
                market_value = float(item.get("market_value", 0.0))
                positions.append({"symbol": symbol, "qty": float(item.get("qty", 0.0)), "market_value": market_value})
                total_market_value += market_value
            snapshot = {
                "schema_version": "1.0.0",
                "broker_mode": context.broker_mode,
                "fetched_at_utc": utc_now_iso(),
                "positions": positions,
                "total_market_value": total_market_value,
            }
            output_path = ensure_directory(project_root() / "data" / "positions") / f"{args.run_id}_positions_{context.broker_mode}.json"
            validate_and_write_json(schema_name="position_snapshot", artifact=snapshot, path=output_path)
            artifact_paths.append(str(output_path))
            exit_code = 0
            ok = True
        except RuntimeError as exc:
            errors.append(f"alpaca positions request failed: {exc}")
            broker_response = {"error": str(exc)}
            exit_code = 12 if broker_mode == "live" else 4
            ok = False

    audit_path = append_audit_event(
        args.audit_dir,
        run_id=args.run_id,
        event_type="alpaca_position_manager",
        actor="script",
        operation=OPERATION,
        input_hash=sha256_json({"runtime_mode": args.runtime_mode, "broker_mode": broker_mode, "symbols": args.symbols}),
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
        kanban_summary="Position snapshot captured." if ok else "Position snapshot failed; review masked broker payload.",
    )
    write_envelope(envelope, args.output_dir, args.json_out or None)
    return envelope, exit_code


def main() -> None:
    _, exit_code = run(build_parser().parse_args())
    raise SystemExit(exit_code)


if __name__ == "__main__":
    main()

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


SCRIPT_NAME = "scripts/integrations/alpaca_order_monitor.py"
OPERATION = "alpaca_order_monitor"


def build_parser() -> argparse.ArgumentParser:
    parser = add_common_args(argparse.ArgumentParser(description="Fetch Alpaca order status artifacts."))
    parser.add_argument("--broker-mode", required=True, choices=["paper", "live"])
    parser.add_argument("--order-id", default="")
    parser.add_argument("--since-utc", default="")
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
            if args.order_id:
                payload = alpaca_get(context, url=f"{context.trading_base_url}/v2/orders/{args.order_id}")
                orders_payload = [payload] if isinstance(payload, dict) else []
            else:
                params = {"status": "all", "direction": "desc", "limit": 100}
                if args.since_utc:
                    params["after"] = args.since_utc
                payload = alpaca_get(context, url=f"{context.trading_base_url}/v2/orders", params=params)
                orders_payload = payload if isinstance(payload, list) else []
            broker_response = mask_secrets({"orders": orders_payload})
            output_dir = ensure_directory(project_root() / "data" / "orders")
            for item in orders_payload:
                artifact = {
                    "schema_version": "1.0.0",
                    "broker_order_id": str(item.get("id", "")),
                    "order_request_id": str(item.get("client_order_id", item.get("id", ""))),
                    "broker_mode": context.broker_mode,
                    "symbol": str(item.get("symbol", "")).upper(),
                    "side": str(item.get("side", "")).lower(),
                    "order_type": str(item.get("order_class") or item.get("type") or "unknown"),
                    "submitted_qty": int(float(item.get("qty", 0.0))) if item.get("qty") not in {None, ""} else None,
                    "submitted_notional_estimate": float(item.get("notional", 0.0)) if item.get("notional") not in {None, ""} else None,
                    "status": str(item.get("status", "unknown")),
                    "submitted_at_utc": (str(item.get("submitted_at"))[:19] + "Z") if item.get("submitted_at") else None,
                    "updated_at_utc": (str(item.get("updated_at"))[:19] + "Z") if item.get("updated_at") else utc_now_iso(),
                    "broker_response_masked": mask_secrets(item),
                }
                output_path = output_dir / f"{args.run_id}_order_{artifact['broker_order_id'] or 'unknown'}.json"
                validate_and_write_json(schema_name="order_status", artifact=artifact, path=output_path)
                artifact_paths.append(str(output_path))
            if not orders_payload:
                warnings.append("no orders matched the current query")
            exit_code = 0
            ok = True
        except RuntimeError as exc:
            errors.append(f"alpaca order monitor request failed: {exc}")
            broker_response = {"error": str(exc)}
            exit_code = 12 if broker_mode == "live" else 4
            ok = False

    audit_path = append_audit_event(
        args.audit_dir,
        run_id=args.run_id,
        event_type="alpaca_order_monitor",
        actor="script",
        operation=OPERATION,
        input_hash=sha256_json({"runtime_mode": args.runtime_mode, "broker_mode": broker_mode, "order_id": args.order_id, "since_utc": args.since_utc}),
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
        kanban_summary="Order status artifacts captured." if ok else "Order monitor failed; review masked broker payload.",
    )
    write_envelope(envelope, args.output_dir, args.json_out or None)
    return envelope, exit_code


def main() -> None:
    _, exit_code = run(build_parser().parse_args())
    raise SystemExit(exit_code)


if __name__ == "__main__":
    main()

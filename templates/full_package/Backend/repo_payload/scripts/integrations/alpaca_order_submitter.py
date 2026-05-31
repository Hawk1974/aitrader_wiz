from __future__ import annotations

import argparse
import json
import os
import sys
from pathlib import Path

import requests

if __package__ is None or __package__ == "":
    sys.path.append(str(Path(__file__).resolve().parents[2]))

from scripts.core.audit_chain import sha256_json
from scripts.core.audit_logger import append_audit_event
from scripts.core.idempotency import append_record, record_exists
from scripts.core.path_utils import ensure_directory, project_root, resolve_path
from scripts.core.policy_validator import validate_runtime_mode
from scripts.core.result_envelope import add_common_args, build_envelope, write_envelope
from scripts.core.schema_validator import validate_data, validate_json_file
from scripts.core.secret_masker import mask_secrets
from scripts.core.time_utils import utc_now_iso
from scripts.integrations.alpaca_common import SafeFailure, alpaca_headers, prepare_broker_context, resolve_broker_mode, validate_and_write_json


SCRIPT_NAME = "scripts/integrations/alpaca_order_submitter.py"
OPERATION = "alpaca_order_submitter"


def build_parser() -> argparse.ArgumentParser:
    parser = add_common_args(argparse.ArgumentParser(description="Submit a compliant Alpaca order request."))
    parser.add_argument("--broker-mode", required=True, choices=["paper", "live"])
    parser.add_argument("--order-request", required=True)
    parser.add_argument("--risk-decision", required=True)
    parser.add_argument("--approval", default="")
    return parser


def _request_idempotency_key(*, runtime_mode: str, broker_mode: str, order_request: dict, live_fingerprint: str = "") -> str:
    parts = {
        "runtime_mode": runtime_mode,
        "broker_mode": broker_mode,
        "symbol": order_request["symbol"],
        "side": order_request["side"],
        "qty_or_notional": order_request["qty"] if order_request["qty"] is not None else order_request["notional"],
        "order_type": order_request["order_type"],
        "risk_decision_id": order_request["risk_decision_id"],
        "approval_id": order_request["approval_id"],
    }
    if broker_mode == "live":
        parts["live_credential_fingerprint"] = live_fingerprint
    return sha256_json(parts)


def _build_submit_payload(*, order_request: dict, risk_decision: dict) -> dict:
    approved_order = risk_decision.get("approved_order")
    if not isinstance(approved_order, dict):
        raise ValueError("risk decision is missing approved_order")
    if approved_order["symbol"] != order_request["symbol"]:
        raise ValueError("approved_order symbol does not match order request")
    if approved_order["report_id"] != order_request["report_id"]:
        raise ValueError("approved_order report_id does not match order request")
    entry_order_type = str(order_request.get("order_type") or "market").lower()
    if entry_order_type == "bracket":
        entry_order_type = "market"
    return {
        "symbol": approved_order["symbol"],
        "side": approved_order["side"],
        "type": entry_order_type,
        "time_in_force": approved_order["time_in_force"],
        "qty": approved_order["qty"],
        "order_class": "bracket",
        "take_profit": {"limit_price": approved_order["take_profit_price"]},
        "stop_loss": {"stop_price": approved_order["stop_loss_price"]},
        "client_order_id": order_request["order_request_id"],
    }


def run(args: argparse.Namespace) -> tuple[dict, int]:
    started = utc_now_iso()
    warnings: list[str] = []
    errors: list[str] = []
    artifact_paths: list[str] = []
    broker_response = None

    mode_result = validate_runtime_mode(args.runtime_mode)
    if not mode_result.ok:
        errors.extend(mode_result.errors)
        exit_code = mode_result.exit_code
        envelope = build_envelope(
            ok=False,
            script=SCRIPT_NAME,
            operation=OPERATION,
            runtime_mode=args.runtime_mode,
            run_id=args.run_id,
            started_at_utc=started,
            exit_code=exit_code,
            outputs={"json_path": None, "markdown_report_path": None, "audit_event_paths": [], "artifact_paths": []},
            warnings=warnings,
            errors=errors,
            safe_to_continue=False,
            kanban_task_id=args.kanban_task_id,
            kanban_status="blocked",
            kanban_summary="Order submission blocked by runtime mode policy.",
        )
        write_envelope(envelope, args.output_dir, args.json_out or None)
        return envelope, exit_code

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
            order_request = validate_json_file("order_request", resolve_path(args.order_request))
            risk_decision = validate_json_file("risk_decision", resolve_path(args.risk_decision))
            if order_request["broker_mode"] != broker_mode:
                raise ValueError("order_request broker_mode does not match requested broker mode")
            if order_request["risk_decision_id"] != risk_decision["risk_decision_id"]:
                raise ValueError("order_request risk_decision_id does not match provided risk decision")
            if not risk_decision["pass"]:
                raise ValueError("risk decision did not pass")
            approval_payload = None
            if args.runtime_mode in {"paper_manual", "live_manual"}:
                if not args.approval:
                    raise ValueError("manual approval artifact is required for manual submission modes")
                raw_approval_payload = json.loads(resolve_path(args.approval).read_text(encoding="utf-8"))
                if not isinstance(raw_approval_payload, dict):
                    raise ValueError("manual approval artifact must be a JSON object")
                approval_payload = {key: value for key, value in raw_approval_payload.items() if key != "risk_decision_id"}
                validate_data("manual_approval", approval_payload)
                if approval_payload["order_intent_hash"] != risk_decision["order_intent_hash"]:
                    raise ValueError("manual approval order_intent_hash does not match risk decision")
                if approval_payload["decision"] != "approved":
                    raise ValueError("manual approval artifact is not approved")
                if approval_payload["approval_id"] != order_request["approval_id"]:
                    raise ValueError("order_request approval_id does not match approval artifact")
                if approval_payload["expires_at_utc"] < utc_now_iso():
                    raise ValueError("manual approval artifact has expired")

            live_fingerprint = sha256_json({"api_key": os.getenv("ALPACA_LIVE_API_KEY", "")}) if broker_mode == "live" else ""
            request_key = _request_idempotency_key(
                runtime_mode=args.runtime_mode,
                broker_mode=broker_mode,
                order_request=order_request,
                live_fingerprint=live_fingerprint,
            )
            idempotency_store = ensure_directory(resolve_path(args.state_dir)) / "idempotency_records.jsonl"
            if record_exists(idempotency_store, request_key):
                raise FileExistsError("duplicate order submission blocked by idempotency")

            submit_payload = _build_submit_payload(order_request=order_request, risk_decision=risk_decision)

            response = requests.post(
                f"{context.trading_base_url}/v2/orders",
                headers=alpaca_headers(context),
                json=submit_payload,
                timeout=30,
            )
            try:
                response_payload = response.json()
            except ValueError:
                response_payload = {"status_code": response.status_code, "text": response.text}
            broker_response = mask_secrets(response_payload)
            if response.status_code >= 400:
                raise RuntimeError(json.dumps(broker_response, sort_keys=True))

            order_status = {
                "schema_version": "1.0.0",
                "broker_order_id": str(response_payload.get("id", "")),
                "order_request_id": order_request["order_request_id"],
                "broker_mode": broker_mode,
                "symbol": submit_payload["symbol"],
                "side": submit_payload["side"],
                "order_type": submit_payload["type"],
                "submitted_qty": int(submit_payload["qty"]),
                "submitted_notional_estimate": float(risk_decision["approved_order"]["estimated_notional"]),
                "status": str(response_payload.get("status", "accepted")),
                "submitted_at_utc": (str(response_payload.get("submitted_at"))[:19] + "Z") if response_payload.get("submitted_at") else utc_now_iso(),
                "updated_at_utc": (str(response_payload.get("updated_at"))[:19] + "Z") if response_payload.get("updated_at") else utc_now_iso(),
                "broker_response_masked": broker_response if isinstance(broker_response, dict) else {"payload": broker_response},
            }
            output_path = ensure_directory(project_root() / "data" / "orders") / f"{args.run_id}_submitted_{order_status['broker_order_id'] or 'unknown'}.json"
            validate_and_write_json(schema_name="order_status", artifact=order_status, path=output_path)
            append_record(idempotency_store, key=request_key, operation=OPERATION, run_id=args.run_id, artifact_path=str(output_path))
            artifact_paths.extend([str(output_path), str(idempotency_store)])
            exit_code = 0
            ok = True
        except FileExistsError as exc:
            errors.append(str(exc))
            exit_code = 9
            ok = False
        except (ValueError, RuntimeError) as exc:
            errors.append(str(exc))
            exit_code = 12 if broker_mode == "live" else 6
            ok = False

    audit_path = append_audit_event(
        args.audit_dir,
        run_id=args.run_id,
        event_type="alpaca_order_submitter",
        actor="script",
        operation=OPERATION,
        input_hash=sha256_json({"runtime_mode": args.runtime_mode, "broker_mode": broker_mode, "order_request": args.order_request}),
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
        kanban_summary="Order submitted successfully." if ok else "Order submission blocked or failed safely.",
    )
    write_envelope(envelope, args.output_dir, args.json_out or None)
    return envelope, exit_code


def main() -> None:
    _, exit_code = run(build_parser().parse_args())
    raise SystemExit(exit_code)


if __name__ == "__main__":
    main()

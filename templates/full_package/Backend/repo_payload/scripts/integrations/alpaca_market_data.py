from __future__ import annotations

import argparse
from datetime import UTC, datetime
import sys
from pathlib import Path

if __package__ is None or __package__ == "":
    sys.path.append(str(Path(__file__).resolve().parents[2]))

from scripts.core.audit_chain import sha256_json
from scripts.core.audit_logger import append_audit_event
from scripts.core.path_utils import ensure_directory, project_root
from scripts.core.result_envelope import add_common_args, build_envelope, write_envelope
from scripts.core.secret_masker import mask_secrets
from scripts.core.time_utils import utc_now, utc_now_iso
from scripts.integrations.alpaca_common import SafeFailure, alpaca_get, prepare_broker_context, resolve_broker_mode, validate_and_write_json


SCRIPT_NAME = "scripts/integrations/alpaca_market_data.py"
OPERATION = "alpaca_market_data"


def build_parser() -> argparse.ArgumentParser:
    parser = add_common_args(argparse.ArgumentParser(description="Fetch Alpaca market context snapshots for one or more symbols."))
    parser.add_argument("--symbols", required=True)
    parser.add_argument("--max-staleness-seconds", required=True, type=int)
    parser.add_argument("--broker-mode", default="", choices=["", "paper", "live"])
    return parser


def _is_stale(timestamp_text: str | None, max_staleness_seconds: int) -> bool:
    if not timestamp_text:
        return True
    normalized = timestamp_text.replace("Z", "+00:00")
    try:
        quote_time = datetime.fromisoformat(normalized)
    except ValueError:
        return True
    if quote_time.tzinfo is None:
        quote_time = quote_time.replace(tzinfo=UTC)
    age_seconds = (utc_now() - quote_time.astimezone(UTC)).total_seconds()
    return age_seconds > max_staleness_seconds


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
        symbols = [symbol.strip().upper() for symbol in args.symbols.split(",") if symbol.strip()]
        try:
            clock_payload = alpaca_get(context, url=f"{context.trading_base_url}/v2/clock")
            quote_payload = alpaca_get(
                context,
                url=f"{context.data_base_url}/v2/stocks/quotes/latest",
                params={"symbols": ",".join(symbols)},
            )
            broker_response = mask_secrets({"clock": clock_payload, "quotes": quote_payload})
            quotes = quote_payload.get("quotes", {}) if isinstance(quote_payload, dict) else {}
            market_open = bool(clock_payload.get("is_open", False)) if isinstance(clock_payload, dict) else False
            output_dir = ensure_directory(project_root() / "data" / "state")
            stale_found = False
            for symbol in symbols:
                quote = quotes.get(symbol, {})
                stale = _is_stale(quote.get("t"), args.max_staleness_seconds)
                stale_found = stale_found or stale
                context_record = {
                    "schema_version": "1.0.0",
                    "symbol": symbol,
                    "fetched_at_utc": utc_now_iso(),
                    "broker_mode": context.broker_mode,
                    "tradable": True,
                    "market_open": market_open,
                    "bid": float(quote["bp"]) if quote.get("bp") is not None else None,
                    "ask": float(quote["ap"]) if quote.get("ap") is not None else None,
                    "last": float(quote["ap"] or quote["bp"]) if quote.get("ap") is not None or quote.get("bp") is not None else None,
                    "stale": stale,
                }
                output_path = output_dir / f"{args.run_id}_market_context_{symbol}.json"
                validate_and_write_json(schema_name="market_context", artifact=context_record, path=output_path)
                artifact_paths.append(str(output_path))
            if stale_found:
                warnings.append("one or more market context artifacts are stale")
            exit_code = 0
            ok = True
        except RuntimeError as exc:
            errors.append(f"alpaca market data request failed: {exc}")
            broker_response = {"error": str(exc)}
            exit_code = 12 if broker_mode == "live" else 4
            ok = False

    audit_path = append_audit_event(
        args.audit_dir,
        run_id=args.run_id,
        event_type="alpaca_market_data",
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
        safe_to_continue=ok and not any("stale" in warning for warning in warnings),
        broker_response=broker_response,
        kanban_task_id=args.kanban_task_id,
        kanban_status="done" if ok else "blocked",
        kanban_summary="Market context artifacts captured." if ok else "Market context failed; review masked broker payload.",
    )
    write_envelope(envelope, args.output_dir, args.json_out or None)
    return envelope, exit_code


def main() -> None:
    _, exit_code = run(build_parser().parse_args())
    raise SystemExit(exit_code)


if __name__ == "__main__":
    main()

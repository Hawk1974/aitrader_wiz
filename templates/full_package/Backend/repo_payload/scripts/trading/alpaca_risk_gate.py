from __future__ import annotations

import argparse
import math
import json
import sys
from pathlib import Path

if __package__ is None or __package__ == "":
    sys.path.append(str(Path(__file__).resolve().parents[2]))

from scripts.core.audit_chain import sha256_json
from scripts.core.audit_logger import append_audit_event
from scripts.core.config_loader import load_json_file, load_runtime_env
from scripts.core.emergency_state import EmergencyStateCorrupt, initialize_emergency_state, read_emergency_state
from scripts.core.idempotency import append_record, record_exists
from scripts.core.path_utils import ensure_directory, project_root, resolve_path
from scripts.core.policy_validator import validate_runtime_mode
from scripts.core.result_envelope import add_common_args, build_envelope, write_envelope
from scripts.core.schema_validator import SchemaValidationError, validate_json_file, validate_data
from scripts.core.telegram import send_telegram_message
from scripts.core.time_utils import utc_now_iso


SCRIPT_NAME = "scripts/trading/alpaca_risk_gate.py"
OPERATION = "alpaca_risk_gate"


def build_parser() -> argparse.ArgumentParser:
    parser = add_common_args(argparse.ArgumentParser(description="Apply deterministic risk checks to an order intent."))
    parser.add_argument("--order-intent", required=True)
    parser.add_argument("--account-status", required=True)
    parser.add_argument("--market-context", required=True)
    parser.add_argument("--risk-policy", required=True)
    return parser


def _load_position_value(symbol: str) -> float:
    positions_dir = project_root() / "data" / "positions"
    position_files = sorted(positions_dir.glob("*_positions_*.json"), key=lambda path: path.stat().st_mtime, reverse=True)
    for path in position_files:
        data = json.loads(path.read_text(encoding="utf-8"))
        for item in data.get("positions", []):
            if str(item.get("symbol", "")).upper() == symbol:
                return float(item.get("market_value", 0.0))
    return 0.0


def _iter_json_files(directory: Path, pattern: str) -> list[Path]:
    if not directory.exists():
        return []
    return sorted(directory.glob(pattern), key=lambda path: path.stat().st_mtime, reverse=True)


def _safe_json(path: Path) -> dict | None:
    try:
        payload = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return None
    return payload if isinstance(payload, dict) else None


def _today_utc_date() -> str:
    return utc_now_iso()[:10]


def _load_today_submitted_notional() -> float:
    total = 0.0
    for path in _iter_json_files(project_root() / "data" / "orders", "*.json"):
        payload = _safe_json(path)
        if not payload:
            continue
        if str(payload.get("submitted_at_utc", ""))[:10] != _today_utc_date():
            continue
        total += float(payload.get("submitted_notional_estimate") or 0.0)
    return total


def _same_day_opposite_side_exists(symbol: str, side: str) -> bool:
    opposite = "sell" if side == "buy" else "buy"
    for path in _iter_json_files(project_root() / "data" / "orders", "*.json"):
        payload = _safe_json(path)
        if not payload:
            continue
        if str(payload.get("submitted_at_utc", ""))[:10] != _today_utc_date():
            continue
        if str(payload.get("symbol", "")).upper() != symbol:
            continue
        if str(payload.get("side", "")).lower() == opposite:
            return True
    return False


def _accumulated_qty_for_scope(*, symbol: str, report_id: str, politician: str) -> tuple[int, int]:
    report_total = 0
    politician_total = 0
    for path in _iter_json_files(project_root() / "data" / "risk", "*_risk_decision.json"):
        payload = _safe_json(path)
        if not payload or not payload.get("pass"):
            continue
        approved_order = payload.get("approved_order")
        if not isinstance(approved_order, dict):
            continue
        if str(approved_order.get("symbol", "")).upper() != symbol:
            continue
        qty = int(approved_order.get("qty") or 0)
        if str(approved_order.get("report_id", "")) == report_id:
            report_total += qty
        if str(approved_order.get("politician", "")).strip().lower() == politician.strip().lower():
            politician_total += qty
    return report_total, politician_total


def _round_price(value: float) -> float:
    return round(value + 1e-9, 2)


def _normalize_requested_qty(order_intent: dict, last_price: float) -> int | None:
    qty = order_intent.get("qty")
    if qty is not None:
        return max(int(math.floor(float(qty))), 0)
    notional = order_intent.get("notional")
    if notional is None or last_price <= 0:
        return None
    return max(int(math.floor(float(notional) / last_price)), 0)


def run(args: argparse.Namespace) -> tuple[dict, int]:
    started = utc_now_iso()
    warnings: list[str] = []
    errors: list[str] = []
    rejection_reasons: list[str] = []
    notification_events: list[dict[str, str]] = []
    artifact_paths: list[str] = []
    exit_code = 0
    load_runtime_env()

    mode_result = validate_runtime_mode(args.runtime_mode)
    if not mode_result.ok:
        errors.extend(mode_result.errors)
        exit_code = mode_result.exit_code

    try:
        initialize_emergency_state(args.state_dir)
        emergency_state = read_emergency_state(args.state_dir)
        if emergency_state["active"]:
            raise EmergencyStateCorrupt("emergency stop is active")
        order_intent = validate_json_file("order_request", resolve_path(args.order_intent))
        account_status = validate_json_file("account_status", resolve_path(args.account_status))
        market_context = validate_json_file("market_context", resolve_path(args.market_context))
        risk_policy = load_json_file(resolve_path(args.risk_policy))
    except EmergencyStateCorrupt as exc:
        errors.append(str(exc))
        exit_code = 8
    except (FileNotFoundError, SchemaValidationError, ValueError) as exc:
        errors.append(str(exc))
        exit_code = 5 if isinstance(exc, SchemaValidationError) else 1

    risk_path = None
    if not errors:
        order_hash = sha256_json(order_intent)
        idempotency_store = ensure_directory(resolve_path(args.state_dir)) / "risk_gate_idempotency.jsonl"
        if record_exists(idempotency_store, order_hash):
            rejection_reasons.append("duplicate order intent blocked by risk idempotency")
            exit_code = 9

        symbol = str(order_intent["symbol"]).upper()
        last_price = float(market_context.get("last") or 0.0)
        qty = order_intent.get("qty")
        notional = order_intent.get("notional")
        available_cash = float(account_status.get("cash", 0.0))
        stop_loss_percent = float(order_intent.get("stop_loss_percent") or risk_policy.get("default_stop_loss_percent", 10))
        take_profit_percent = float(order_intent.get("take_profit_percent") or risk_policy.get("default_take_profit_percent", 10))
        cash_cap_percent = float(risk_policy.get("max_cash_allocation_per_stock_percent", 10))
        cash_cap_notional = available_cash * cash_cap_percent / 100
        requested_qty = _normalize_requested_qty(order_intent, last_price)

        if notional is None and qty is None:
            rejection_reasons.append("order intent must include qty or notional")
        requested_notional = float(notional) if notional is not None else (
            float(requested_qty or 0.0) * last_price if last_price > 0 else None
        )
        if (requested_notional or 0) <= 0:
            rejection_reasons.append("estimated notional must be positive")
        if market_context.get("stale"):
            rejection_reasons.append("market context is stale")
        if order_intent["strategy_class"] != "swing":
            rejection_reasons.append("strategy_class must be swing")
        if risk_policy.get("swing_trade_only", False) and order_intent["side"] != "buy":
            rejection_reasons.append("swing-trade entry system only accepts buy orders; exits are managed by bracket legs")
        if risk_policy.get("pdt_safe_mode", False) and _same_day_opposite_side_exists(symbol, order_intent["side"]):
            rejection_reasons.append("same-day opposite-side order detected for symbol; PDT-safe mode blocked the trade")
        if int(order_intent.get("top_candidate_rank", 999999)) > int(risk_policy.get("top_candidate_limit", 10)):
            rejection_reasons.append("trade candidate rank exceeds allowed top-candidate limit")
        if stop_loss_percent <= 0 or take_profit_percent <= 0:
            rejection_reasons.append("stop loss and take profit percentages must be positive")
        if "bracket" not in risk_policy.get("allowed_order_types", []):
            rejection_reasons.append("risk policy must allow bracket orders")
        if symbol in {item.upper() for item in risk_policy.get("symbol_blocklist", [])}:
            rejection_reasons.append("symbol is blocked by risk policy")

        if requested_qty is None:
            rejection_reasons.append("unable to derive requested share quantity from order intent")
            final_qty = None
        else:
            report_limit = int(risk_policy.get("max_qty_per_ticker_per_report", 10))
            politician_limit = int(risk_policy.get("max_qty_per_ticker_per_politician", 10))
            used_report_qty, used_politician_qty = _accumulated_qty_for_scope(
                symbol=symbol,
                report_id=str(order_intent["report_id"]),
                politician=str(order_intent["politician"]),
            )
            remaining_report_qty = max(report_limit - used_report_qty, 0)
            remaining_politician_qty = max(politician_limit - used_politician_qty, 0)
            cash_cap_qty = int(math.floor(cash_cap_notional / last_price)) if last_price > 0 else 0
            final_qty = min(requested_qty, remaining_report_qty, remaining_politician_qty, cash_cap_qty)
            if final_qty <= 0:
                if cash_cap_qty < 1 and requested_qty >= 1:
                    rejection_reasons.append("one share still exceeds the 10% available-cash cap; trade skipped")
                    message = (
                        f"AlTrader skipped {symbol}: one share exceeds the 10% cash cap "
                        f"(${cash_cap_notional:.2f} available for this stock from ${available_cash:.2f} cash)."
                    )
                    notification_events.append({"channel": "telegram", "event_type": "cash_cap_skip", "message": message})
                    notification_events.append({"channel": "email", "event_type": "cash_cap_skip", "message": message})
                    if risk_policy.get("telegram_notify_on_cash_cap_skip", False):
                        telegram_error = send_telegram_message(message=message)
                        if telegram_error:
                            warnings.append(telegram_error)
                elif remaining_report_qty < 1:
                    rejection_reasons.append("ticker already reached per-report share limit")
                elif remaining_politician_qty < 1:
                    rejection_reasons.append("ticker already reached per-politician share limit")
                else:
                    rejection_reasons.append("requested trade size reduced to zero by deterministic sizing controls")
            auto_reduced = final_qty is not None and requested_qty is not None and final_qty < requested_qty

        estimated_notional = float(final_qty or 0.0) * last_price if last_price > 0 and final_qty else 0.0
        if estimated_notional > float(risk_policy.get("max_notional_per_order", 0)):
            rejection_reasons.append("estimated notional exceeds per-order limit")
        if estimated_notional > float(account_status.get("buying_power", 0.0)):
            rejection_reasons.append("insufficient buying power")
        minimum_buffer = float(risk_policy.get("min_buying_power_buffer_percent", 0.0))
        projected_buying_power = float(account_status.get("buying_power", 0.0)) - estimated_notional
        if float(account_status.get("portfolio_value", 0.0)) > 0:
            remaining_buffer_percent = projected_buying_power / float(account_status["portfolio_value"]) * 100
            if remaining_buffer_percent < minimum_buffer:
                rejection_reasons.append("buying power buffer would fall below policy minimum")
        if _load_today_submitted_notional() + estimated_notional > float(risk_policy.get("max_daily_notional", 0)):
            rejection_reasons.append("daily notional limit would be exceeded")

        current_symbol_value = _load_position_value(symbol)
        projected_symbol_exposure = current_symbol_value + estimated_notional
        portfolio_value = max(float(account_status.get("portfolio_value", 0.0)), 1.0)
        symbol_exposure_percent = projected_symbol_exposure / portfolio_value * 100
        if symbol_exposure_percent > float(risk_policy.get("max_position_exposure_percent", 0.0)):
            rejection_reasons.append("projected symbol exposure exceeds policy limit")
        portfolio_exposure_percent = estimated_notional / portfolio_value * 100
        if portfolio_exposure_percent > float(risk_policy.get("max_portfolio_exposure_percent", 0.0)):
            rejection_reasons.append("projected portfolio exposure exceeds policy limit")
        if order_intent["broker_mode"] == "paper" and args.runtime_mode == "paper_auto" and not risk_policy.get("paper_auto_enabled", False):
            rejection_reasons.append("paper_auto is disabled by risk policy")

        approved_order = None
        if not rejection_reasons and final_qty is not None:
            approved_order = {
                "symbol": symbol,
                "side": "buy",
                "order_type": "bracket",
                "time_in_force": "gtc",
                "qty": final_qty,
                "estimated_notional": _round_price(estimated_notional),
                "stop_loss_percent": stop_loss_percent,
                "take_profit_percent": take_profit_percent,
                "stop_loss_price": _round_price(last_price * (1 - stop_loss_percent / 100)),
                "take_profit_price": _round_price(last_price * (1 + take_profit_percent / 100)),
                "strategy_class": "swing",
                "source_record_id": str(order_intent["source_record_id"]),
                "politician": str(order_intent["politician"]),
                "report_id": str(order_intent["report_id"]),
                "top_candidate_rank": int(order_intent["top_candidate_rank"]),
            }

        risk_decision = {
            "schema_version": "1.0.0",
            "risk_decision_id": sha256_json({"run_id": args.run_id, "order_intent_hash": order_hash}),
            "order_intent_hash": order_hash,
            "pass": not rejection_reasons,
            "rejection_reasons": rejection_reasons,
            "policy_version": str(risk_policy.get("policy_version", "unknown")),
            "account_snapshot_hash": sha256_json(account_status),
            "market_context_hash": sha256_json(market_context),
            "approved_order": approved_order,
            "sizing_decision": {
                "requested_qty": requested_qty,
                "final_qty": approved_order["qty"] if approved_order else None,
                "requested_notional": _round_price(requested_notional) if requested_notional else None,
                "final_notional": approved_order["estimated_notional"] if approved_order else None,
                "last_price": _round_price(last_price) if last_price > 0 else None,
                "available_cash": _round_price(available_cash),
                "cash_cap_percent": cash_cap_percent,
                "cash_cap_notional": _round_price(cash_cap_notional),
                "auto_reduced": auto_reduced if "auto_reduced" in locals() else False,
                "skipped_because_one_share_exceeds_cap": any(
                    item["event_type"] == "cash_cap_skip" for item in notification_events
                ),
            },
            "notification_events": notification_events,
        }
        validate_data("risk_decision", risk_decision)
        risk_path = ensure_directory(project_root() / "data" / "risk") / f"{args.run_id}_risk_decision.json"
        risk_path.write_text(json.dumps(risk_decision, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        artifact_paths.append(str(risk_path))
        append_record(idempotency_store, key=order_hash, operation=OPERATION, run_id=args.run_id, artifact_path=str(risk_path))
        artifact_paths.append(str(idempotency_store))
        if rejection_reasons and exit_code == 0:
            exit_code = 6

    ok = not errors and not rejection_reasons
    errors.extend(rejection_reasons)
    audit_path = append_audit_event(
        args.audit_dir,
        run_id=args.run_id,
        event_type="alpaca_risk_gate",
        actor="script",
        operation=OPERATION,
        input_hash=sha256_json({"order_intent": args.order_intent, "account_status": args.account_status, "market_context": args.market_context}),
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
        kanban_summary="Risk decision passed." if ok else "Risk decision blocked order intent.",
    )
    write_envelope(envelope, args.output_dir, args.json_out or None)
    return envelope, exit_code


def main() -> None:
    _, exit_code = run(build_parser().parse_args())
    raise SystemExit(exit_code)


if __name__ == "__main__":
    main()

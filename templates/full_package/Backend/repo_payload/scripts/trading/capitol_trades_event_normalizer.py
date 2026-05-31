from __future__ import annotations

import argparse
import json
import shutil
import sys
from datetime import datetime, timedelta
from pathlib import Path
from typing import Any

if __package__ is None or __package__ == "":
    sys.path.append(str(Path(__file__).resolve().parents[2]))

from scripts.core.audit_chain import sha256_json
from scripts.core.audit_logger import append_audit_event
from scripts.core.config_loader import ConfigError, load_json_file
from scripts.core.emergency_state import EmergencyStateCorrupt, initialize_emergency_state, read_emergency_state
from scripts.core.idempotency import append_record, idempotency_key, record_exists
from scripts.core.path_utils import ensure_directory, ensure_project_layout, project_root, resolve_path
from scripts.core.policy_validator import validate_runtime_mode
from scripts.core.result_envelope import add_common_args, build_envelope, write_envelope
from scripts.core.schema_validator import SchemaValidationError, validate_data, validate_json_file
from scripts.core.time_utils import utc_now_iso


SCRIPT_NAME = "scripts/trading/capitol_trades_event_normalizer.py"
OPERATION = "capitol_trades_event_normalizer"


def build_parser() -> argparse.ArgumentParser:
    parser = add_common_args(argparse.ArgumentParser(description="Normalize a Capitol Trades raw payload into canonical trade event JSON."))
    parser.add_argument("--raw-payload", required=True)
    parser.add_argument("--source-policy", required=True)
    return parser


def _load_raw_record(raw_payload_path: str) -> tuple[dict[str, Any], str, str]:
    payload_path = resolve_path(raw_payload_path)
    data = json.loads(payload_path.read_text(encoding="utf-8"))
    if isinstance(data, dict):
        try:
            record = validate_json_file("source_payload_record", payload_path)
            nested_path = resolve_path(record["raw_payload_path"])
            nested_data = json.loads(nested_path.read_text(encoding="utf-8"))
            if not isinstance(nested_data, dict):
                raise ValueError("nested raw payload must be a JSON object")
            return nested_data, record["source_record_id"], record["payload_hash"]
        except SchemaValidationError:
            pass
        return data, _source_record_id(data), sha256_json(data)
    if isinstance(data, list) and len(data) == 1 and isinstance(data[0], dict):
        return data[0], _source_record_id(data[0]), sha256_json(data[0])
    raise ValueError("raw payload must resolve to exactly one JSON object")


def _source_record_id(record: dict[str, Any]) -> str:
    for key in ("id", "source_record_id", "record_id", "disclosure_id", "transaction_id", "uuid"):
        value = record.get(key)
        if value:
            return str(value)
    return sha256_json(record).split(":", 1)[1][:16]


def _normalize_transaction_type(value: Any) -> str:
    text = str(value or "").strip().lower()
    if text in {"purchase", "buy", "bought", "purchased"}:
        return "buy"
    if text in {"sale", "sell", "sold"}:
        return "sell"
    if "exchange" in text:
        return "exchange"
    if text:
        return "other"
    return "unknown"


def _normalize_symbol(value: Any) -> str:
    text = str(value or "").strip().upper()
    if not text:
        raise ValueError("missing required symbol/ticker field")
    text = text.split(":", 1)[0]
    text = text.split("/", 1)[0]
    text = text.split(" ", 1)[0]
    text = "".join(ch for ch in text if ch.isalnum() or ch in {".", "-"})
    if not text:
        raise ValueError("symbol/ticker could not be normalized into a broker-safe format")
    return text


def _date_only(value: Any, field_name: str) -> str:
    text = str(value or "").replace("\u00a0", " ").strip()
    if not text:
        raise ValueError(f"missing required date field: {field_name}")
    if "T" in text and len(text) >= 10 and text[:4].isdigit():
        text = text.split("T", 1)[0]

    iso_candidate = text
    if len(iso_candidate) == 10:
        try:
            datetime.strptime(iso_candidate, "%Y-%m-%d")
            return iso_candidate
        except ValueError:
            pass

    lowered = " ".join(text.lower().split())
    if lowered.endswith(" today") or lowered == "today":
        return datetime.now().strftime("%Y-%m-%d")
    if lowered.endswith(" yesterday") or lowered == "yesterday":
        return (datetime.now() - timedelta(days=1)).strftime("%Y-%m-%d")

    for fmt in ("%d %b %Y", "%d %B %Y", "%b %d %Y", "%B %d %Y"):
        try:
            return datetime.strptime(text, fmt).strftime("%Y-%m-%d")
        except ValueError:
            continue
    raise ValueError(f"{field_name} must resolve to YYYY-MM-DD")


def _pick_required(record: dict[str, Any], *keys: str) -> str:
    for key in keys:
        value = record.get(key)
        if value is not None and str(value).strip():
            return str(value).strip()
    raise ValueError(f"missing required field from candidates: {', '.join(keys)}")


def _build_normalized_event(record: dict[str, Any], *, source_name: str, source_record_id: str, raw_payload_hash: str) -> dict[str, Any]:
    disclosed_date = _date_only(
        record.get("disclosed_date") or record.get("disclosure_date") or record.get("published_at"),
        "disclosed_date",
    )
    transaction_date = _date_only(
        record.get("transaction_date") or record.get("trade_date") or record.get("transaction_date_reported"),
        "transaction_date",
    )
    symbol = _normalize_symbol(_pick_required(record, "symbol", "ticker", "asset_symbol"))
    owner = _pick_required(record, "owner", "owner_name", "representative", "member")
    amount_range = _pick_required(record, "amount_range", "amount", "range")
    normalized_id = idempotency_key(
        {
            "source_name": source_name,
            "source_record_id": source_record_id,
            "disclosed_date": disclosed_date,
            "raw_payload_hash": raw_payload_hash,
        }
    )
    return {
        "schema_version": "1.0.0",
        "normalized_event_id": normalized_id,
        "source_record_id": source_record_id,
        "symbol": symbol,
        "transaction_type": _normalize_transaction_type(record.get("transaction_type") or record.get("type")),
        "transaction_date": transaction_date,
        "disclosed_date": disclosed_date,
        "owner": owner,
        "amount_range": amount_range,
        "raw_payload_hash": raw_payload_hash,
    }


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
        source_policy = load_json_file(args.source_policy)
        ensure_project_layout(project_root())
        initialize_emergency_state(args.state_dir)
        emergency_state = read_emergency_state(args.state_dir)
        if emergency_state["active"]:
            raise EmergencyStateCorrupt("emergency stop is active")
    except (ConfigError, OSError) as exc:
        errors.append(str(exc))
        exit_code = 2 if isinstance(exc, ConfigError) else 7
        source_policy = {}
    except EmergencyStateCorrupt as exc:
        errors.append(str(exc))
        exit_code = 8
        source_policy = {}

    quarantine_path = None
    normalized_path = None
    if not errors:
        try:
            raw_record, source_record_id, raw_payload_hash = _load_raw_record(args.raw_payload)
            normalized_event = _build_normalized_event(
                raw_record,
                source_name=str(source_policy.get("source_name", "capitol_trades")),
                source_record_id=source_record_id,
                raw_payload_hash=raw_payload_hash,
            )
            validate_data("normalized_trade_event", normalized_event)

            idempotency_store = ensure_directory(resolve_path(args.state_dir)) / "normalized_event_idempotency.jsonl"
            if record_exists(idempotency_store, normalized_event["normalized_event_id"]):
                raise FileExistsError(f"duplicate normalized event blocked: {normalized_event['normalized_event_id']}")

            normalized_path = ensure_directory(project_root() / "data" / "normalized") / f"{args.run_id}_{source_record_id}.json"
            normalized_path.write_text(json.dumps(normalized_event, indent=2, sort_keys=True) + "\n", encoding="utf-8")
            append_record(
                idempotency_store,
                key=normalized_event["normalized_event_id"],
                operation=OPERATION,
                run_id=args.run_id,
                artifact_path=str(normalized_path),
            )
            artifact_paths.extend([str(normalized_path), str(idempotency_store)])
        except FileExistsError as exc:
            errors.append(str(exc))
            exit_code = 9
            safe_to_continue = False
        except (FileNotFoundError, json.JSONDecodeError, ValueError, SchemaValidationError) as exc:
            errors.append(str(exc))
            exit_code = 5 if isinstance(exc, SchemaValidationError) else 1
            safe_to_continue = False
            quarantine_dir = ensure_directory(resolve_path(args.state_dir) / "quarantine")
            quarantine_path = quarantine_dir / f"{args.run_id}_normalizer_quarantine.json"
            quarantine_payload = {
                "schema_version": "1.0.0",
                "run_id": args.run_id,
                "raw_payload_path": str(resolve_path(args.raw_payload)),
                "errors": errors,
            }
            quarantine_path.write_text(json.dumps(quarantine_payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")
            artifact_paths.append(str(quarantine_path))
            try:
                original_raw_path = resolve_path(args.raw_payload)
                preserved_copy = quarantine_dir / f"{args.run_id}_{original_raw_path.name}"
                shutil.copy2(original_raw_path, preserved_copy)
                artifact_paths.append(str(preserved_copy))
            except OSError:
                warnings.append("failed to copy raw payload into quarantine; original raw path remains the source of truth")

    audit_path = append_audit_event(
        args.audit_dir,
        run_id=args.run_id,
        event_type="capitol_trades_event_normalizer",
        actor="script",
        operation=OPERATION,
        input_hash=sha256_json({"raw_payload": args.raw_payload, "source_policy": args.source_policy}),
        output_hash=sha256_json(
            {
                "ok": not errors,
                "normalized_path": str(normalized_path) if normalized_path else None,
                "quarantine_path": str(quarantine_path) if quarantine_path else None,
                "errors": errors,
            }
        ),
        details={
            "normalized_path": str(normalized_path) if normalized_path else None,
            "quarantine_path": str(quarantine_path) if quarantine_path else None,
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
        kanban_summary="Raw event normalized successfully." if not errors else "Event normalization failed; review quarantine artifacts.",
    )
    write_envelope(envelope, args.output_dir, args.json_out or None)
    return envelope, exit_code


def main() -> None:
    _, exit_code = run(build_parser().parse_args())
    raise SystemExit(exit_code)


if __name__ == "__main__":
    main()

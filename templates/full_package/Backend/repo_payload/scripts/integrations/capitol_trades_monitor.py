from __future__ import annotations

import argparse
import html
import json
import re
import sys
from datetime import UTC, datetime
from pathlib import Path
from typing import Any
from urllib.parse import urlparse

import requests

if __package__ is None or __package__ == "":
    sys.path.append(str(Path(__file__).resolve().parents[2]))

from scripts.core.audit_chain import sha256_json
from scripts.core.audit_logger import append_audit_event
from scripts.core.config_loader import ConfigError, load_json_file
from scripts.core.emergency_state import EmergencyStateCorrupt, initialize_emergency_state, read_emergency_state
from scripts.core.path_utils import ensure_directory, ensure_project_layout, project_root, resolve_path
from scripts.core.policy_validator import validate_runtime_mode
from scripts.core.result_envelope import add_common_args, build_envelope, write_envelope
from scripts.core.schema_validator import SchemaValidationError, validate_data
from scripts.core.time_utils import dated_parts, utc_now, utc_now_iso


SCRIPT_NAME = "scripts/integrations/capitol_trades_monitor.py"
OPERATION = "capitol_trades_monitor"
UNRESOLVED_ENDPOINT_MARKERS = {
    "USER_OR_CODEX_TO_RESOLVE",
    "CODEX_TO_RESOLVE_OR_ASK_USER",
}
CAPITOL_TRADES_BASE_URL = "https://www.capitoltrades.com"


def build_parser() -> argparse.ArgumentParser:
    parser = add_common_args(argparse.ArgumentParser(description="Poll the configured Capitol Trades source and persist raw payload artifacts."))
    parser.add_argument("--source-policy", required=True)
    parser.add_argument("--poll-interval-minutes", required=True, type=int)
    return parser


def _is_unresolved_endpoint(endpoint: str) -> bool:
    upper = endpoint.upper()
    return any(marker in upper for marker in UNRESOLVED_ENDPOINT_MARKERS)


def _load_source_payload(endpoint: str) -> Any:
    parsed = urlparse(endpoint)
    if parsed.scheme in {"http", "https"}:
        response = requests.get(endpoint, timeout=30)
        response.raise_for_status()
        content_type = response.headers.get("content-type", "")
        if "json" in content_type:
            return response.json()
        if "capitoltrades.com" in parsed.netloc:
            return _parse_capitol_trades_html(response.text)
        return response.json()
    if parsed.scheme == "file":
        path = Path(parsed.path)
        text = path.read_text(encoding="utf-8")
        if path.suffix.lower() in {".html", ".htm"}:
            return _parse_capitol_trades_html(text)
        return json.loads(text)
    resolved = resolve_path(endpoint)
    text = resolved.read_text(encoding="utf-8")
    if resolved.suffix.lower() in {".html", ".htm"}:
        return _parse_capitol_trades_html(text)
    return json.loads(text)


def _strip_tags(value: str) -> str:
    return re.sub(r"<[^>]+>", "", html.unescape(value)).strip()


def _extract_texts(cell_html: str) -> list[str]:
    texts = [_strip_tags(match) for match in re.findall(r">([^<>]+)<", cell_html)]
    return [text for text in texts if text]


def _parse_capitol_trades_html(document: str) -> list[dict[str, Any]]:
    tbody_match = re.search(r"<tbody class=\"\">(.*?)</tbody>", document, flags=re.DOTALL)
    if not tbody_match:
        raise ValueError("Capitol Trades page did not contain a trade table body")
    row_html_list = re.findall(r"<tr\b[^>]*>(.*?)</tr>", tbody_match.group(1), flags=re.DOTALL)
    records: list[dict[str, Any]] = []
    for row_html in row_html_list:
        cells = re.findall(r"<td\b[^>]*>(.*?)</td>", row_html, flags=re.DOTALL)
        if len(cells) < 9:
            continue

        politician_match = re.search(r'href="/politicians/[^"]+">([^<]+)</a>', cells[0])
        issuer_match = re.search(r'href="/issuers/[^"]+">([^<]+)</a>', cells[1])
        trade_link_match = re.search(r'href="(/trades/\d+)"', row_html)
        politician_meta = re.findall(r'<span class="q-field [^"]+">([^<]+)</span>', cells[0])
        date_one = _extract_texts(cells[2])
        date_two = _extract_texts(cells[3])
        owner_meta = re.findall(r'<span class="q-label">([^<]+)</span>', cells[5])
        tx_type_match = re.search(r'tx-type--([a-z]+)', cells[6])
        amount_range = _extract_texts(cells[7])
        ticker_match = re.search(r'<span class="q-field issuer-ticker">([^<]+)</span>', cells[1])
        if not (politician_match and issuer_match and trade_link_match and len(date_one) >= 2 and len(date_two) >= 2):
            continue

        record_id = trade_link_match.group(1).split("/")[-1]
        records.append(
            {
                "id": record_id,
                "source_record_id": record_id,
                "politician": politician_match.group(1),
                "party": politician_meta[0] if len(politician_meta) > 0 else "",
                "chamber": politician_meta[1] if len(politician_meta) > 1 else "",
                "state": politician_meta[2] if len(politician_meta) > 2 else "",
                "issuer": issuer_match.group(1),
                "ticker": ticker_match.group(1) if ticker_match else "N/A",
                "transaction_date": f"{date_one[0]} {date_one[1]}",
                "disclosed_date": f"{date_two[0]} {date_two[1]}",
                "owner": owner_meta[0] if owner_meta else "Unknown",
                "transaction_type": tx_type_match.group(1) if tx_type_match else "unknown",
                "amount_range": amount_range[-1] if amount_range else "Unknown",
                "source_url": f"{CAPITOL_TRADES_BASE_URL}{trade_link_match.group(1)}",
            }
        )
    if not records:
        raise ValueError("Capitol Trades page contained no parseable trade rows")
    return records


def _extract_records(payload: Any) -> list[dict[str, Any]]:
    if isinstance(payload, list):
        return [item for item in payload if isinstance(item, dict)]
    if isinstance(payload, dict):
        if isinstance(payload.get("results"), list):
            return [item for item in payload["results"] if isinstance(item, dict)]
        return [payload]
    raise ValueError("source payload must be a JSON object or array of objects")


def _source_record_id(record: dict[str, Any]) -> str:
    for key in ("id", "source_record_id", "record_id", "disclosure_id", "transaction_id", "uuid"):
        value = record.get(key)
        if value:
            return str(value)
    return sha256_json(record).split(":", 1)[1][:16]


def _stale_status(record: dict[str, Any], max_source_age_minutes: int) -> bool:
    for key in ("updated_at", "published_at", "created_at", "disclosed_at", "fetched_at", "timestamp"):
        value = record.get(key)
        if not value or not isinstance(value, str):
            continue
        normalized = value.replace("Z", "+00:00")
        try:
            source_time = datetime.fromisoformat(normalized)
        except ValueError:
            continue
        if source_time.tzinfo is None:
            source_time = source_time.replace(tzinfo=UTC)
        age_minutes = (utc_now() - source_time.astimezone(UTC)).total_seconds() / 60
        return age_minutes > max_source_age_minutes
    return False


def run(args: argparse.Namespace) -> tuple[dict, int]:
    started = utc_now_iso()
    warnings: list[str] = []
    errors: list[str] = []
    artifact_paths: list[str] = []
    audit_paths: list[str] = []
    exit_code = 0
    ok = True
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

    fetched_count = 0
    stale_count = 0
    cursor_path = None
    if not errors:
        endpoint = str(source_policy.get("endpoint", ""))
        enabled = bool(source_policy.get("enabled", False))
        configured_interval = int(source_policy.get("poll_interval_minutes", 30))
        if configured_interval != args.poll_interval_minutes:
            warnings.append(
                f"requested poll interval {args.poll_interval_minutes} does not match source policy interval {configured_interval}"
            )
        if not enabled:
            warnings.append("source polling disabled by source policy")
        elif _is_unresolved_endpoint(endpoint):
            errors.append("source policy endpoint remains unresolved; user clarification is required before live polling")
            exit_code = 2
            safe_to_continue = False
        else:
            try:
                payload = _load_source_payload(endpoint)
                records = _extract_records(payload)
                if not records:
                    warnings.append("source payload returned no records")

                max_source_age_minutes = int(source_policy.get("stale_source_policy", {}).get("max_source_age_minutes", 90))
                year, month, day = dated_parts()
                raw_dir = ensure_directory(project_root() / "data" / "raw" / "capitol_trades" / year / month / day)
                for record in records:
                    fetched_count += 1
                    source_record_id = _source_record_id(record)
                    payload_hash = sha256_json(record)
                    stale = _stale_status(record, max_source_age_minutes)
                    if stale:
                        stale_count += 1
                    raw_payload_path = raw_dir / f"{args.run_id}_{source_record_id}.json"
                    raw_payload_path.write_text(json.dumps(record, indent=2, sort_keys=True) + "\n", encoding="utf-8")

                    payload_record = {
                        "schema_version": "1.0.0",
                        "source_name": str(source_policy.get("source_name", "capitol_trades")),
                        "source_record_id": source_record_id,
                        "fetched_at_utc": utc_now_iso(),
                        "payload_hash": payload_hash,
                        "raw_payload_path": str(raw_payload_path),
                        "source_url": str(record.get("source_url") or endpoint),
                        "stale": stale,
                    }
                    validate_data("source_payload_record", payload_record)
                    record_path = raw_dir / f"{args.run_id}_{source_record_id}_record.json"
                    record_path.write_text(json.dumps(payload_record, indent=2, sort_keys=True) + "\n", encoding="utf-8")
                    artifact_paths.extend([str(raw_payload_path), str(record_path)])

                cursor_path = resolve_path(
                    source_policy.get("pagination", {}).get("cursor_state_path", "data/state/capitol_trades_cursor.json")
                )
                ensure_directory(cursor_path.parent)
                cursor_payload = {
                    "schema_version": "1.0.0",
                    "last_run_id": args.run_id,
                    "last_fetched_at_utc": utc_now_iso(),
                    "last_record_count": fetched_count,
                    "last_source_record_id": _source_record_id(records[-1]) if records else None,
                    "endpoint": endpoint,
                }
                cursor_path.write_text(json.dumps(cursor_payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")
                artifact_paths.append(str(cursor_path))

                if stale_count:
                    warnings.append(f"{stale_count} source payloads were stale; downstream trading should not proceed from stale data")
                    safe_to_continue = False
            except requests.RequestException as exc:
                errors.append(f"source request failed: {exc}")
                exit_code = 4
                safe_to_continue = False
            except (json.JSONDecodeError, ValueError, SchemaValidationError) as exc:
                errors.append(str(exc))
                exit_code = 5 if isinstance(exc, SchemaValidationError) else 1
                safe_to_continue = False

    ok = not errors
    audit_path = append_audit_event(
        args.audit_dir,
        run_id=args.run_id,
        event_type="capitol_trades_monitor",
        actor="script",
        operation=OPERATION,
        input_hash=sha256_json(
            {
                "config": args.config,
                "source_policy": args.source_policy,
                "runtime_mode": args.runtime_mode,
                "poll_interval_minutes": args.poll_interval_minutes,
            }
        ),
        output_hash=sha256_json(
            {
                "ok": ok,
                "fetched_count": fetched_count,
                "stale_count": stale_count,
                "errors": errors,
                "warnings": warnings,
            }
        ),
        details={
            "fetched_count": fetched_count,
            "stale_count": stale_count,
            "cursor_path": str(cursor_path) if cursor_path else None,
            "artifact_paths": artifact_paths,
            "warnings": warnings,
            "errors": errors,
        },
    )
    audit_paths.append(str(audit_path))

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
            "audit_event_paths": audit_paths,
            "artifact_paths": artifact_paths,
        },
        warnings=warnings,
        errors=errors,
        safe_to_continue=safe_to_continue and ok,
        kanban_task_id=args.kanban_task_id,
        kanban_status="done" if ok else "blocked",
        kanban_summary="Capitol Trades polling completed." if ok else "Capitol Trades polling blocked; review source artifacts.",
    )
    write_envelope(envelope, args.output_dir, args.json_out or None)
    return envelope, exit_code


def main() -> None:
    _, exit_code = run(build_parser().parse_args())
    raise SystemExit(exit_code)


if __name__ == "__main__":
    main()

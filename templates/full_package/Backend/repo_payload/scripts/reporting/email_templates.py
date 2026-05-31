from __future__ import annotations

import json
import re
from html import escape, unescape
from pathlib import Path
from typing import Any


def _load_artifact(path: Path) -> Any:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return None


def _clean_text(value: Any) -> str:
    text = unescape(str(value or ""))
    replacements = {
        "â€“": "–",
        "â€”": "—",
        "â€˜": "'",
        "â€™": "'",
        "â€œ": '"',
        "â€�": '"',
        "\xa0": " ",
    }
    for source, target in replacements.items():
        text = text.replace(source, target)
    return text.strip()


def _coerce_trade_record(data: dict[str, Any]) -> dict[str, str]:
    return {
        "politician": _clean_text(data.get("politician", "Unknown")),
        "ticker": _clean_text(data.get("ticker", "N/A")),
        "issuer": _clean_text(data.get("issuer", "Unknown issuer")),
        "transaction_type": _clean_text(data.get("transaction_type", "unknown")).upper(),
        "amount_range": _clean_text(data.get("amount_range", "Unknown")),
        "transaction_date": _clean_text(data.get("transaction_date", "Unknown")),
        "disclosed_date": _clean_text(data.get("disclosed_date", "Unknown")),
        "owner": _clean_text(data.get("owner", "Unknown")),
        "source_url": _clean_text(data.get("source_url", "")),
    }


def _amount_upper_bound(raw: str) -> float:
    cleaned = raw.replace("$", "").replace(",", "").replace(" ", "")
    match = re.findall(r"(\d+(?:\.\d+)?)([KMB]?)", cleaned.upper())
    if not match:
        return 0.0
    value, suffix = match[-1]
    multiplier = {"": 1.0, "K": 1_000.0, "M": 1_000_000.0, "B": 1_000_000_000.0}.get(suffix, 1.0)
    return float(value) * multiplier


def _extract_source_intake_items(artifact_paths: list[Path]) -> list[dict[str, str]]:
    items: list[dict[str, str]] = []
    for path in artifact_paths:
        if not path.name.endswith(".json") or path.name.endswith("_record.json"):
            continue
        data = _load_artifact(path)
        if isinstance(data, dict) and "politician" in data and "issuer" in data:
            items.append(_coerce_trade_record(data))
    return items


def _extract_top_candidate_items(artifact_paths: list[Path]) -> list[dict[str, str]]:
    items: list[dict[str, str]] = []
    for path in artifact_paths:
        if not path.name.endswith("_top_buy_candidates_analysis.json"):
            continue
        data = _load_artifact(path)
        candidates = data.get("top_buy_candidates") if isinstance(data, dict) else None
        if not isinstance(candidates, list):
            continue
        for candidate in candidates:
            if not isinstance(candidate, dict):
                continue
            symbol = _clean_text(candidate.get("symbol") or "")
            items.append(
                {
                    "politician": _clean_text(candidate.get("politician", "Unknown")),
                    "ticker": symbol or _clean_text(candidate.get("broker_symbol", "N/A")),
                    "issuer": _clean_text(candidate.get("issuer", "Unknown issuer")),
                    "transaction_type": _clean_text(candidate.get("transaction_type", "unknown")).upper(),
                    "amount_range": _clean_text(candidate.get("amount_range", "Unknown")),
                    "transaction_date": _clean_text(candidate.get("transaction_date", "Unknown")),
                    "disclosed_date": _clean_text(candidate.get("disclosed_date", "Unknown")),
                    "owner": _clean_text(candidate.get("owner", "Unknown")),
                    "source_url": _clean_text(candidate.get("source_url", "")),
                }
            )
    return items


def _top_buy_candidates(items: list[dict[str, str]], limit: int = 10) -> list[dict[str, str]]:
    buy_items = [item for item in items if item["transaction_type"].lower() == "buy"]
    ranked = sorted(
        buy_items,
        key=lambda item: (_amount_upper_bound(item["amount_range"]), item["disclosed_date"], item["transaction_date"]),
        reverse=True,
    )
    return ranked[:limit]


def _extract_risk_decisions(artifact_paths: list[Path]) -> list[dict[str, Any]]:
    decisions: list[dict[str, Any]] = []
    for path in artifact_paths:
        if not path.name.endswith(".json"):
            continue
        data = _load_artifact(path)
        if isinstance(data, dict) and "risk_decision_id" in data and "notification_events" in data:
            decisions.append(data)
    return decisions


def _extract_record_paths(artifact_paths: list[Path]) -> list[str]:
    return [str(path) for path in artifact_paths if path.name.endswith("_record.json")]


def _extract_order_summaries(artifact_paths: list[Path]) -> list[dict[str, Any]]:
    seen: set[str] = set()
    orders: list[dict[str, Any]] = []
    for path in artifact_paths:
        data = _load_artifact(path)
        if not isinstance(data, dict):
            continue

        payload: dict[str, Any] | None = None
        broker_order_id = ""

        if "broker_order_id" in data and isinstance(data.get("broker_response_masked"), dict):
            payload = data.get("broker_response_masked")
            broker_order_id = str(data.get("broker_order_id", ""))
        elif data.get("operation") == "alpaca_order_monitor":
            broker_response = data.get("broker_response") or {}
            orders_list = broker_response.get("orders") if isinstance(broker_response, dict) else None
            if isinstance(orders_list, list) and orders_list and isinstance(orders_list[0], dict):
                payload = orders_list[0]
                broker_order_id = str(payload.get("id", ""))
        elif data.get("operation") == "alpaca_order_submitter" and isinstance(data.get("broker_response"), dict):
            payload = data.get("broker_response")
            broker_order_id = str(payload.get("id", ""))

        if not payload or not broker_order_id or broker_order_id in seen:
            continue

        seen.add(broker_order_id)
        orders.append(
            {
                "broker_order_id": _clean_text(broker_order_id),
                "symbol": _clean_text(payload.get("symbol", "UNKNOWN")),
                "side": _clean_text(payload.get("side", "unknown")),
                "status": _clean_text(payload.get("status", "unknown")),
                "filled_qty": _clean_text(payload.get("filled_qty", "0")),
                "filled_avg_price": _clean_text(payload.get("filled_avg_price", "")),
                "qty": _clean_text(payload.get("qty", "")),
                "legs": payload.get("legs") if isinstance(payload.get("legs"), list) else [],
            }
        )
    return orders


def _extract_no_action_notes(artifact_paths: list[Path]) -> list[str]:
    notes: list[str] = []
    for path in artifact_paths:
        if not path.name.endswith(".json"):
            continue
        data = _load_artifact(path)
        if not isinstance(data, dict):
            continue
        if str(data.get("status", "")).strip().lower() != "no_action":
            continue
        summary = _clean_text(data.get("summary") or "")
        stage_id = _clean_text(data.get("stage_id") or "unknown_stage")
        if summary:
            notes.append(f"{stage_id}: {summary}")
    return notes


def _latest_trade_lines(items: list[dict[str, str]], limit: int = 8) -> list[str]:
    lines: list[str] = []
    for item in items[:limit]:
        detail = (
            f"- {item['politician']} | {item['transaction_type']} {item['ticker']} "
            f"({item['issuer']}) | {item['amount_range']} | "
            f"trade date {item['transaction_date']} | disclosed {item['disclosed_date']} | "
            f"owner {item['owner']}"
        )
        lines.append(detail)
        if item["source_url"]:
            lines.append(f"  Source: {item['source_url']}")
    return lines


def _render_top_candidate_lines(items: list[dict[str, str]], limit: int = 10) -> list[str]:
    ranked = _top_buy_candidates(items, limit=limit)
    if not ranked:
        return ["- No buy candidates were available in this run."]
    lines: list[str] = []
    for index, item in enumerate(ranked, start=1):
        lines.append(
            f"- #{index} {item['ticker']} | {item['politician']} | {item['issuer']} | "
            f"{item['amount_range']} | trade date {item['transaction_date']} | disclosed {item['disclosed_date']}"
        )
    return lines


def _render_skipped_trade_lines(risk_decisions: list[dict[str, Any]]) -> list[str]:
    lines: list[str] = []
    for decision in risk_decisions:
        sizing = decision.get("sizing_decision") or {}
        if not sizing.get("skipped_because_one_share_exceeds_cap"):
            continue
        approved_order = decision.get("approved_order") or {}
        symbol = _clean_text(approved_order.get("symbol") or "UNKNOWN")
        report_reasons = decision.get("rejection_reasons") or []
        lines.append(f"- {symbol}: {'; '.join(_clean_text(reason) for reason in report_reasons)}")
    return lines


def _render_failure_digest_lines(failure_entries: list[dict[str, Any]]) -> list[str]:
    if not failure_entries:
        return []
    lines: list[str] = []
    for entry in failure_entries:
        lines.append(
            f"- {_clean_text(entry.get('created_at_utc', 'Unknown time'))} | "
            f"{_clean_text(entry.get('failed_stage', 'unknown_stage'))} | "
            f"{_clean_text(entry.get('summary', 'No summary provided'))}"
        )
    return lines


def _render_execution_lines(order_summaries: list[dict[str, Any]]) -> list[str]:
    if not order_summaries:
        return []

    lines: list[str] = []
    for order in order_summaries:
        filled_price = _clean_text(order.get("filled_avg_price") or "n/a")
        lines.append(
            f"- {_clean_text(order.get('symbol', 'UNKNOWN'))} {_clean_text(order.get('side', 'unknown')).upper()} "
            f"status {_clean_text(order.get('status', 'unknown'))} | qty {_clean_text(order.get('filled_qty') or order.get('qty') or '0')} "
            f"| avg fill {filled_price}"
        )
        for leg in order.get("legs", []):
            if not isinstance(leg, dict):
                continue
            leg_type = _clean_text(leg.get("type") or leg.get("order_type") or "unknown").upper()
            leg_status = _clean_text(leg.get("status", "unknown"))
            leg_price = _clean_text(leg.get("limit_price") or leg.get("stop_price") or "n/a")
            lines.append(
                f"  {leg_type} leg | status {leg_status} | qty {_clean_text(leg.get('qty', '0'))} | trigger {leg_price}"
            )
    return lines


def render_email_body(
    *,
    report_type: str,
    run_id: str,
    runtime_mode: str,
    created_at_utc: str,
    markdown_path: Path,
    source_artifact_paths: list[str],
    failure_entries: list[dict[str, Any]] | None = None,
) -> str:
    artifact_paths = [Path(path) for path in source_artifact_paths]
    failure_entries = failure_entries or []

    if report_type == "source_intake":
        items = _extract_top_candidate_items(artifact_paths) or _extract_source_intake_items(artifact_paths)
        lines = [
            "AlTrader Capitol Trades Intake Report",
            "",
            f"Run ID: {run_id}",
            f"Runtime mode: {runtime_mode}",
            f"Created at UTC: {created_at_utc}",
            "",
            f"Summary: {len(items)} Capitol Trades source events were captured in this run.",
            "",
            "Top 10 buy candidates for AlTrader:",
        ]
        lines.extend(_render_top_candidate_lines(items, limit=10))
        lines.extend(["", "Latest captured trades:"])
        lines.extend(_latest_trade_lines(items))
        record_paths = _extract_record_paths(artifact_paths)
        if record_paths:
            lines.extend(["", "Structured source records:"])
            lines.extend(f"- {path}" for path in record_paths[:8])
        lines.extend(["", f"Full report artifact: {markdown_path}"])
        return "\n".join(lines).strip() + "\n"

    if report_type == "daily_cycle":
        items = _extract_top_candidate_items(artifact_paths) or _extract_source_intake_items(artifact_paths)
        risk_decisions = _extract_risk_decisions(artifact_paths)
        order_summaries = _extract_order_summaries(artifact_paths)
        no_action_notes = _extract_no_action_notes(artifact_paths)
        lines = [
            "AlTrader Daily Summary",
            "",
            f"Run ID: {run_id}",
            f"Runtime mode: {runtime_mode}",
            f"Created at UTC: {created_at_utc}",
            "",
            f"Summary: {len(items)} recent Capitol Trades source artifacts were included in this daily summary.",
        ]
        if no_action_notes:
            lines.extend(["", "Outcome: No action was taken in this cycle."])
            lines.extend(f"- {note}" for note in no_action_notes)
        if items:
            lines.extend(["", "Top 10 buy candidates:"])
            lines.extend(_render_top_candidate_lines(items, limit=10))
            lines.extend([""])
            lines.extend(["", "Highlights:"])
            lines.extend(_latest_trade_lines(items, limit=5))
        skipped = _render_skipped_trade_lines(risk_decisions)
        if skipped:
            lines.extend(["", "Skipped trades requiring attention:"])
            lines.extend(skipped)
        execution_lines = _render_execution_lines(order_summaries)
        if execution_lines:
            lines.extend(["", "Order execution summary:"])
            lines.extend(execution_lines)
        failure_lines = _render_failure_digest_lines(failure_entries)
        if failure_lines:
            lines.extend(["", "Errors, issues, and failures captured during this trading day:"])
            lines.extend(failure_lines)
        lines.extend(["", f"Full report artifact: {markdown_path}"])
        return "\n".join(lines).strip() + "\n"

    if report_type == "risk_review":
        risk_decisions = _extract_risk_decisions(artifact_paths)
        lines = [
            "AlTrader Risk Review",
            "",
            f"Run ID: {run_id}",
            f"Runtime mode: {runtime_mode}",
            f"Created at UTC: {created_at_utc}",
        ]
        skipped = _render_skipped_trade_lines(risk_decisions)
        if skipped:
            lines.extend(["", "Skipped trades:"])
            lines.extend(skipped)
        lines.extend(["", f"Full report artifact: {markdown_path}"])
        return "\n".join(lines).strip() + "\n"

    lines = [
        f"AlTrader {report_type.replace('_', ' ').title()}",
        "",
        f"Run ID: {run_id}",
        f"Runtime mode: {runtime_mode}",
        f"Created at UTC: {created_at_utc}",
        "",
        "This report was generated successfully.",
        f"Full report artifact: {markdown_path}",
    ]
    return "\n".join(lines).strip() + "\n"


def _html_list(items: list[str]) -> str:
    if not items:
        return "<p>None.</p>"
    return "<ul>" + "".join(f"<li>{escape(item)}</li>" for item in items) + "</ul>"


def _html_trade_lines(items: list[dict[str, str]], limit: int = 8) -> str:
    rows: list[str] = []
    for item in items[:limit]:
        rows.append(
            "<tr>"
            f"<td>{escape(item['politician'])}</td>"
            f"<td>{escape(item['transaction_type'])}</td>"
            f"<td>{escape(item['ticker'])}</td>"
            f"<td>{escape(item['issuer'])}</td>"
            f"<td>{escape(item['amount_range'])}</td>"
            f"<td>{escape(item['transaction_date'])}</td>"
            f"<td>{escape(item['disclosed_date'])}</td>"
            "</tr>"
        )
    if not rows:
        return "<p>No trade rows were available.</p>"
    return (
        "<table border=\"1\" cellpadding=\"6\" cellspacing=\"0\" style=\"border-collapse:collapse;\">"
        "<thead><tr><th>Politician</th><th>Type</th><th>Ticker</th><th>Issuer</th><th>Amount</th><th>Trade Date</th><th>Disclosed</th></tr></thead>"
        f"<tbody>{''.join(rows)}</tbody></table>"
    )


def _html_candidate_lines(items: list[dict[str, str]], limit: int = 10) -> str:
    ranked = _top_buy_candidates(items, limit=limit)
    if not ranked:
        return "<p>No buy candidates were available in this run.</p>"
    rows: list[str] = []
    for index, item in enumerate(ranked, start=1):
        rows.append(
            "<tr>"
            f"<td>{index}</td>"
            f"<td>{escape(item['ticker'])}</td>"
            f"<td>{escape(item['politician'])}</td>"
            f"<td>{escape(item['issuer'])}</td>"
            f"<td>{escape(item['amount_range'])}</td>"
            f"<td>{escape(item['transaction_date'])}</td>"
            f"<td>{escape(item['disclosed_date'])}</td>"
            "</tr>"
        )
    return (
        "<table border=\"1\" cellpadding=\"6\" cellspacing=\"0\" style=\"border-collapse:collapse;\">"
        "<thead><tr><th>#</th><th>Ticker</th><th>Politician</th><th>Issuer</th><th>Amount</th><th>Trade Date</th><th>Disclosed</th></tr></thead>"
        f"<tbody>{''.join(rows)}</tbody></table>"
    )


def _html_execution_lines(order_summaries: list[dict[str, Any]]) -> str:
    if not order_summaries:
        return "<p>No execution records were included.</p>"
    blocks: list[str] = []
    for order in order_summaries:
        leg_rows: list[str] = []
        for leg in order.get("legs", []):
            if not isinstance(leg, dict):
                continue
            leg_rows.append(
                "<tr>"
                f"<td>{escape(str(leg.get('type') or leg.get('order_type') or 'unknown').upper())}</td>"
                f"<td>{escape(str(leg.get('status', 'unknown')))}</td>"
                f"<td>{escape(str(leg.get('qty', '0')))}</td>"
                f"<td>{escape(str(leg.get('limit_price') or leg.get('stop_price') or 'n/a'))}</td>"
                "</tr>"
            )
        legs_html = ""
        if leg_rows:
            legs_html = (
                "<table border=\"1\" cellpadding=\"6\" cellspacing=\"0\" style=\"border-collapse:collapse; margin-top:8px;\">"
                "<thead><tr><th>Leg Type</th><th>Status</th><th>Qty</th><th>Trigger</th></tr></thead>"
                f"<tbody>{''.join(leg_rows)}</tbody></table>"
            )
        blocks.append(
            "<div style=\"margin-bottom:16px;\">"
            f"<p><strong>{escape(order.get('symbol', 'UNKNOWN'))}</strong> "
            f"{escape(str(order.get('side', 'unknown')).upper())} | status <strong>{escape(str(order.get('status', 'unknown')))}</strong> "
            f"| qty {escape(str(order.get('filled_qty') or order.get('qty') or '0'))} "
            f"| avg fill {escape(str(order.get('filled_avg_price') or 'n/a'))}</p>"
            f"{legs_html}</div>"
        )
    return "".join(blocks)


def render_email_html_body(
    *,
    report_type: str,
    run_id: str,
    runtime_mode: str,
    created_at_utc: str,
    markdown_path: Path,
    source_artifact_paths: list[str],
    failure_entries: list[dict[str, Any]] | None = None,
) -> str:
    artifact_paths = [Path(path) for path in source_artifact_paths]
    failure_entries = failure_entries or []

    sections: list[str] = [
        "<html><body style=\"font-family:Segoe UI,Arial,sans-serif; line-height:1.5; color:#1f2937;\">",
        f"<h1>AlTrader {escape(report_type.replace('_', ' ').title())}</h1>",
        "<div style=\"background:#f3f4f6; padding:12px; border-radius:8px; margin-bottom:16px;\">",
        f"<p><strong>Run ID:</strong> {escape(run_id)}<br>",
        f"<strong>Runtime mode:</strong> {escape(runtime_mode)}<br>",
        f"<strong>Created at UTC:</strong> {escape(created_at_utc)}</p>",
        "</div>",
    ]

    if report_type == "source_intake":
        items = _extract_top_candidate_items(artifact_paths) or _extract_source_intake_items(artifact_paths)
        record_paths = _extract_record_paths(artifact_paths)
        sections.extend(
            [
                f"<p><strong>Summary:</strong> {len(items)} Capitol Trades source events were captured in this run.</p>",
                "<h2>Top 10 Buy Candidates</h2>",
                _html_candidate_lines(items, limit=10),
                "<h2>Latest Captured Trades</h2>",
                _html_trade_lines(items),
            ]
        )
        if record_paths:
            sections.extend(["<h2>Structured Source Records</h2>", _html_list(record_paths[:8])])
    elif report_type == "daily_cycle":
        items = _extract_top_candidate_items(artifact_paths) or _extract_source_intake_items(artifact_paths)
        risk_decisions = _extract_risk_decisions(artifact_paths)
        order_summaries = _extract_order_summaries(artifact_paths)
        no_action_notes = _extract_no_action_notes(artifact_paths)
        sections.append(
            f"<p><strong>Summary:</strong> {len(items)} recent Capitol Trades source artifacts were included in this daily summary.</p>"
        )
        if no_action_notes:
            sections.extend(["<p><strong>Outcome:</strong> No action was taken in this cycle.</p>", _html_list(no_action_notes)])
        if items:
            sections.extend(
                [
                    "<h2>Top 10 Buy Candidates</h2>",
                    _html_candidate_lines(items, limit=10),
                    "<h2>Highlights</h2>",
                    _html_trade_lines(items, limit=5),
                ]
            )
        skipped = _render_skipped_trade_lines(risk_decisions)
        if skipped:
            sections.extend(["<h2>Skipped Trades Requiring Attention</h2>", _html_list(skipped)])
        sections.extend(["<h2>Order Execution Summary</h2>", _html_execution_lines(order_summaries)])
        failure_lines = _render_failure_digest_lines(failure_entries)
        if failure_lines:
            sections.extend(["<h2>Errors, Issues, and Failures</h2>", _html_list(failure_lines)])
    elif report_type == "risk_review":
        risk_decisions = _extract_risk_decisions(artifact_paths)
        skipped = _render_skipped_trade_lines(risk_decisions)
        if skipped:
            sections.extend(["<h2>Skipped Trades</h2>", _html_list(skipped)])
    else:
        sections.append("<p>This report was generated successfully.</p>")

    sections.append(f"<p><strong>Full report artifact:</strong> {escape(str(markdown_path))}</p>")
    sections.append("</body></html>")
    return "".join(sections)

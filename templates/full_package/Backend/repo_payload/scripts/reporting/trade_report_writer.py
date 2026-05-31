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
from scripts.core.config_loader import ConfigError, load_json_file, load_runtime_env
from scripts.core.emergency_state import EmergencyStateCorrupt, initialize_emergency_state, read_emergency_state
from scripts.core.failure_notifications import clear_failure_digest, load_failure_digest
from scripts.core.path_utils import ensure_directory, resolve_path
from scripts.core.policy_validator import validate_runtime_mode
from scripts.core.provider_alerts import classify_provider_capacity_issue, send_provider_capacity_alert
from scripts.core.result_envelope import add_common_args, build_envelope, write_envelope
from scripts.core.schema_validator import SchemaValidationError, validate_data
from scripts.core.secret_masker import mask_secrets
from scripts.core.telegram import send_telegram_message_verbose
from scripts.core.time_utils import dated_parts, utc_now_iso
from scripts.reporting.email_templates import (
    _extract_risk_decisions,
    _extract_order_summaries,
    _extract_no_action_notes,
    _extract_source_intake_items,
    _extract_top_candidate_items,
    _latest_trade_lines,
    _render_execution_lines,
    _render_skipped_trade_lines,
    _render_top_candidate_lines,
    render_email_body,
    render_email_html_body,
)


SCRIPT_NAME = "scripts/reporting/trade_report_writer.py"
OPERATION = "trade_report_writer"
ALLOWED_REPORT_TYPES = {
    "health_check",
    "source_intake",
    "normalization",
    "model_analysis",
    "risk_review",
    "manual_approval",
    "broker_submission",
    "order_reconciliation",
    "position_snapshot",
    "daily_cycle",
    "backtest_replay",
    "exception",
}
AGENTMAIL_BASE_URL = "https://api.agentmail.to/v0"


def build_parser() -> argparse.ArgumentParser:
    parser = add_common_args(argparse.ArgumentParser(description="Write a deterministic markdown report and manifest from source artifacts."))
    parser.add_argument("--report-type", required=True)
    parser.add_argument("--artifact", action="append", required=True)
    parser.add_argument("--report-policy", required=True)
    return parser


def _artifact_summary(path: Path) -> tuple[str, str]:
    try:
        payload = json.loads(path.read_text(encoding="utf-8"))
        safe_payload = mask_secrets(payload)
        excerpt = json.dumps(safe_payload, indent=2, sort_keys=True)
    except (json.JSONDecodeError, OSError):
        excerpt = path.read_text(encoding="utf-8", errors="replace")
    excerpt = excerpt[:2000]
    return path.name, excerpt


def _build_markdown_lines(*, report_type: str, run_id: str, runtime_mode: str, source_artifacts: list[Path]) -> list[str]:
    lines = [
        f"# {report_type.replace('_', ' ').title()}",
        "",
        f"- Run ID: `{run_id}`",
        f"- Runtime mode: `{runtime_mode}`",
        f"- Created at UTC: `{utc_now_iso()}`",
        "",
    ]
    if report_type in {"source_intake", "daily_cycle"}:
        items = _extract_top_candidate_items(source_artifacts) or _extract_source_intake_items(source_artifacts)
        lines.extend(["## Top 10 Buy Candidates", ""])
        lines.extend(_render_top_candidate_lines(items, limit=10))
        lines.extend(["", "## Recent Trade Highlights", ""])
        lines.extend(_latest_trade_lines(items, limit=8))
        lines.extend([""])
    if report_type in {"risk_review", "daily_cycle"}:
        skipped = _render_skipped_trade_lines(_extract_risk_decisions(source_artifacts))
        if skipped:
            lines.extend(["## Skipped Trades", ""])
            lines.extend(skipped)
            lines.extend([""])
    lines.append("## Artifacts")
    return lines


def _should_send_agentmail(report_type: str, agentmail_policy: dict) -> bool:
    if not agentmail_policy.get("enabled"):
        return False
    if report_type == "daily_cycle":
        return bool(agentmail_policy.get("send_daily_summary"))
    if report_type == "exception":
        return bool(agentmail_policy.get("send_exception_reports"))
    return False


def _build_agentmail_subject(report_type: str, run_id: str) -> str:
    label = report_type.replace("_", " ").title()
    return f"AlTrader {label} - {run_id}"


def _build_telegram_summary(
    *,
    report_type: str,
    run_id: str,
    runtime_mode: str,
    source_artifact_paths: list[str],
    failure_entries: list[dict],
) -> str:
    artifact_paths = [Path(path) for path in source_artifact_paths]
    items = _extract_top_candidate_items(artifact_paths) or _extract_source_intake_items(artifact_paths)
    no_action_notes = _extract_no_action_notes(artifact_paths)
    lines = [
        f"AlTrader {report_type.replace('_', ' ').title()}",
        f"Run ID: {run_id}",
        f"Mode: {runtime_mode}",
    ]
    if report_type == "daily_cycle":
        if no_action_notes:
            lines.append("")
            lines.append("Outcome:")
            lines.extend(f"- {note}" for note in no_action_notes[:3])
        ranked = _render_top_candidate_lines(items, limit=3)
        execution_lines = _render_execution_lines(_extract_order_summaries(artifact_paths))
        skipped = _render_skipped_trade_lines(_extract_risk_decisions(artifact_paths))
        if ranked:
            lines.append("")
            lines.append("Top candidates:")
            lines.extend(ranked[:3])
        if execution_lines:
            lines.append("")
            lines.append("Execution:")
            lines.extend(execution_lines[:4])
        if skipped:
            lines.append("")
            lines.append("Skipped:")
            lines.extend(skipped[:3])
        if failure_entries:
            lines.append("")
            lines.append(f"Failures captured today: {len(failure_entries)}")
    return "\n".join(lines).strip()


def _send_agentmail_report(
    *,
    report_type: str,
    run_id: str,
    runtime_mode: str,
    created_at_utc: str,
    markdown_path: Path,
    source_artifact_paths: list[str],
    agentmail_policy: dict,
) -> tuple[dict | None, str | None]:
    api_key = os.getenv("AGENTMAIL_API_KEY", "").strip()
    inbox_id = os.getenv("AGENTMAIL_FROM", "").strip()
    recipient_name = str(agentmail_policy.get("recipient_source", "USER_PRIMARY_EMAIL")).strip()
    recipient = os.getenv(recipient_name, "").strip() if recipient_name else ""

    if not api_key:
        return None, "AgentMail enabled but AGENTMAIL_API_KEY is missing."
    if not inbox_id:
        return None, "AgentMail enabled but AGENTMAIL_FROM is missing."
    if not recipient:
        return None, f"AgentMail enabled but recipient env var {recipient_name or 'USER_PRIMARY_EMAIL'} is missing."

    payload = {
        "to": [recipient],
        "subject": _build_agentmail_subject(report_type, run_id),
        "text": render_email_body(
            report_type=report_type,
            run_id=run_id,
            runtime_mode=runtime_mode,
            created_at_utc=created_at_utc,
            markdown_path=markdown_path,
            source_artifact_paths=source_artifact_paths,
            failure_entries=load_failure_digest().get("entries", []) if report_type == "daily_cycle" else [],
        ),
        "html": render_email_html_body(
            report_type=report_type,
            run_id=run_id,
            runtime_mode=runtime_mode,
            created_at_utc=created_at_utc,
            markdown_path=markdown_path,
            source_artifact_paths=source_artifact_paths,
            failure_entries=load_failure_digest().get("entries", []) if report_type == "daily_cycle" else [],
        ),
    }
    response = requests.post(
        f"{AGENTMAIL_BASE_URL}/inboxes/{inbox_id}/messages/send",
        headers={
            "Authorization": f"Bearer {api_key}",
            "Content-Type": "application/json",
        },
        json=payload,
        timeout=30,
    )
    try:
        body = response.json()
    except ValueError:
        body = {"status_code": response.status_code, "text": response.text[:500]}

    if response.status_code >= 400:
        return None, f"AgentMail send failed with status {response.status_code}: {json.dumps(mask_secrets(body), sort_keys=True)}"

    safe_body = body if isinstance(body, dict) else {"response": body}
    return mask_secrets(safe_body), None


def _send_telegram_report(
    *,
    report_type: str,
    run_id: str,
    runtime_mode: str,
    source_artifact_paths: list[str],
    failure_entries: list[dict],
) -> tuple[dict | None, str | None]:
    return send_telegram_message_verbose(
        message=_build_telegram_summary(
            report_type=report_type,
            run_id=run_id,
            runtime_mode=runtime_mode,
            source_artifact_paths=source_artifact_paths,
            failure_entries=failure_entries,
        )
    )


def _maybe_send_provider_alert(
    *,
    provider: str,
    status_code: int | None,
    error_text: str | None,
    payload: object | None,
    run_id: str,
    runtime_mode: str,
    output_dir: str,
    warnings: list[str],
    artifact_paths: list[str],
    detail: str,
) -> None:
    provider_issue = classify_provider_capacity_issue(
        provider=provider,
        status_code=status_code,
        error_text=error_text,
        payload=payload,
    )
    if not provider_issue:
        return
    _, provider_alert_error, provider_alert_artifact = send_provider_capacity_alert(
        run_id=run_id,
        runtime_mode=runtime_mode,
        provider_issue=provider_issue,
        output_dir=output_dir,
        stage_name="report_delivery",
        detail=detail,
    )
    if provider_alert_error:
        warnings.append(provider_alert_error)
    if provider_alert_artifact:
        artifact_paths.append(provider_alert_artifact)


def run(args: argparse.Namespace) -> tuple[dict, int]:
    started = utc_now_iso()
    warnings: list[str] = []
    errors: list[str] = []
    artifact_paths: list[str] = []
    exit_code = 0
    load_runtime_env()

    mode_result = validate_runtime_mode(args.runtime_mode)
    if not mode_result.ok:
        errors.extend(mode_result.errors)
        exit_code = mode_result.exit_code

    try:
        if args.report_type not in ALLOWED_REPORT_TYPES:
            raise ConfigError(f"unsupported report type: {args.report_type}")
        report_policy = load_json_file(resolve_path(args.report_policy))
        initialize_emergency_state(args.state_dir)
        emergency_state = read_emergency_state(args.state_dir)
        if emergency_state["active"] and args.report_type != "exception":
            raise EmergencyStateCorrupt("emergency stop is active")
        source_artifacts = [resolve_path(path) for path in args.artifact]
        missing = [str(path) for path in source_artifacts if not path.exists()]
        if missing:
            raise FileNotFoundError(f"missing report source artifacts: {', '.join(missing)}")
    except (ConfigError, FileNotFoundError) as exc:
        errors.append(str(exc))
        exit_code = 2 if isinstance(exc, ConfigError) else 1
        report_policy = {}
        source_artifacts = []
    except EmergencyStateCorrupt as exc:
        errors.append(str(exc))
        exit_code = 8
        report_policy = {}
        source_artifacts = []

    markdown_path = None
    manifest_path = None
    kanban_bundle_path = None
    delivery_metadata_path = None
    if not errors:
        year, month, day = dated_parts()
        report_dir = ensure_directory(resolve_path(report_policy.get("report_dir", "data/reports")) / year / month / day)
        manifest_dir = ensure_directory(resolve_path(report_policy.get("manifest_dir", "data/reports/manifests")) / year / month / day)
        markdown_path = report_dir / f"{args.run_id}_{args.report_type}.md"
        manifest_path = manifest_dir / f"{args.run_id}_{args.report_type}.manifest.json"
        kanban_bundle_path = report_dir / f"{args.run_id}_{args.report_type}.kanban.json"

        lines = _build_markdown_lines(
            report_type=args.report_type,
            run_id=args.run_id,
            runtime_mode=args.runtime_mode,
            source_artifacts=source_artifacts,
        )
        for source_path in source_artifacts:
            name, excerpt = _artifact_summary(source_path)
            lines.extend([f"### {name}", "", "```json", excerpt, "```", ""])
        markdown_path.write_text("\n".join(lines).rstrip() + "\n", encoding="utf-8")

        created_at_utc = utc_now_iso()
        manifest = {
            "schema_version": "1.0.0",
            "report_id": sha256_json({"run_id": args.run_id, "report_type": args.report_type}),
            "report_type": args.report_type,
            "run_id": args.run_id,
            "markdown_report_path": str(markdown_path),
            "source_artifact_paths": [str(path) for path in source_artifacts],
            "created_at_utc": created_at_utc,
            "redaction_applied": True,
        }
        validate_data("report_manifest", manifest)
        manifest_path.write_text(json.dumps(manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8")

        kanban_bundle = {
            "schema_version": "1.0.0",
            "task_id": args.kanban_task_id,
            "recommended_status": "done",
            "title": f"{args.report_type.replace('_', ' ').title()} report",
            "summary": f"{args.report_type.replace('_', ' ').title()} report generated from {len(source_artifacts)} artifacts.",
            "labels": ["altrader", args.report_type, args.runtime_mode],
            "artifact_paths": [str(markdown_path), str(manifest_path)],
        }
        validate_data("kanban_artifact_bundle", kanban_bundle)
        kanban_bundle_path.write_text(json.dumps(kanban_bundle, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        artifact_paths.extend([str(markdown_path), str(manifest_path), str(kanban_bundle_path)])

        agentmail = report_policy.get("agentmail", {})
        delivery_metadata = {
            "schema_version": "1.0.0",
            "run_id": args.run_id,
            "report_type": args.report_type,
            "agentmail": {"attempted": False, "response": None, "error": None},
            "telegram": {"attempted": False, "response": None, "error": None},
        }
        if _should_send_agentmail(args.report_type, agentmail):
            failure_entries = load_failure_digest().get("entries", []) if args.report_type == "daily_cycle" else []
            delivery_metadata["agentmail"]["attempted"] = True
            agentmail_response, delivery_error = _send_agentmail_report(
                report_type=args.report_type,
                run_id=args.run_id,
                runtime_mode=args.runtime_mode,
                created_at_utc=created_at_utc,
                markdown_path=markdown_path,
                source_artifact_paths=manifest["source_artifact_paths"],
                agentmail_policy=agentmail,
            )
            delivery_metadata["agentmail"]["response"] = agentmail_response
            delivery_metadata["agentmail"]["error"] = delivery_error
            if delivery_error:
                warnings.append(delivery_error)
                _maybe_send_provider_alert(
                    provider="agentmail",
                    status_code=None,
                    error_text=delivery_error,
                    payload=agentmail_response,
                    run_id=args.run_id,
                    runtime_mode=args.runtime_mode,
                    output_dir=args.output_dir,
                    warnings=warnings,
                    artifact_paths=artifact_paths,
                    detail="Final email delivery failed.",
                )
            else:
                if args.report_type == "daily_cycle":
                    delivery_metadata["telegram"]["attempted"] = True
                    telegram_response, telegram_error = _send_telegram_report(
                        report_type=args.report_type,
                        run_id=args.run_id,
                        runtime_mode=args.runtime_mode,
                        source_artifact_paths=manifest["source_artifact_paths"],
                        failure_entries=failure_entries,
                    )
                    delivery_metadata["telegram"]["response"] = mask_secrets(telegram_response) if telegram_response else None
                    delivery_metadata["telegram"]["error"] = telegram_error
                    if telegram_error:
                        warnings.append(telegram_error)
                        _maybe_send_provider_alert(
                            provider="telegram",
                            status_code=None,
                            error_text=telegram_error,
                            payload=telegram_response,
                            run_id=args.run_id,
                            runtime_mode=args.runtime_mode,
                            output_dir=args.output_dir,
                            warnings=warnings,
                            artifact_paths=artifact_paths,
                            detail="Final Telegram delivery failed.",
                        )
                    artifact_paths.append(clear_failure_digest())
        delivery_metadata_path = report_dir / f"{args.run_id}_{args.report_type}.delivery.json"
        delivery_metadata_path.write_text(json.dumps(mask_secrets(delivery_metadata), indent=2, sort_keys=True) + "\n", encoding="utf-8")
        artifact_paths.append(str(delivery_metadata_path))

    ok = not errors
    audit_path = append_audit_event(
        args.audit_dir,
        run_id=args.run_id,
        event_type="trade_report_writer",
        actor="script",
        operation=OPERATION,
        input_hash=sha256_json({"report_type": args.report_type, "artifacts": args.artifact, "report_policy": args.report_policy}),
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
        outputs={
            "json_path": None,
            "markdown_report_path": str(markdown_path) if markdown_path else None,
            "audit_event_paths": [str(audit_path)],
            "artifact_paths": artifact_paths,
        },
        warnings=warnings,
        errors=errors,
        safe_to_continue=ok,
        kanban_task_id=args.kanban_task_id,
        kanban_status="done" if ok else "blocked",
        kanban_summary="Report artifacts written." if ok else "Report generation failed.",
    )
    write_envelope(envelope, args.output_dir, args.json_out or None)
    return envelope, exit_code


def main() -> None:
    _, exit_code = run(build_parser().parse_args())
    raise SystemExit(exit_code)


if __name__ == "__main__":
    main()

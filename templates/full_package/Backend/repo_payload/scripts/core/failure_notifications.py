from __future__ import annotations

import json
import os
from pathlib import Path

import requests

from scripts.core.config_loader import load_json_file, load_runtime_env
from scripts.core.path_utils import ensure_directory, resolve_path
from scripts.core.provider_alerts import classify_provider_capacity_issue, send_provider_capacity_alert
from scripts.core.secret_masker import mask_secrets
from scripts.core.telegram import send_telegram_message_verbose
from scripts.core.time_utils import utc_now_iso


AGENTMAIL_BASE_URL = "https://api.agentmail.to/v0"


def default_notification_policy_path() -> Path | None:
    hermes_local = Path.home() / ".hermes" / "altrader" / "report_policy.local.json"
    if hermes_local.exists():
        return hermes_local
    project_example = Path(__file__).resolve().parents[2] / "config" / "report_policy.example.json"
    if project_example.exists():
        return project_example
    return None


def default_failure_digest_path() -> Path:
    return Path.home() / ".hermes" / "altrader" / "pending_failure_digest.json"


def load_failure_digest(path: str | Path | None = None) -> dict:
    digest_path = Path(path) if path else default_failure_digest_path()
    if not digest_path.exists():
        return {"schema_version": "1.0.0", "entries": []}
    try:
        payload = json.loads(digest_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return {"schema_version": "1.0.0", "entries": []}
    if not isinstance(payload, dict):
        return {"schema_version": "1.0.0", "entries": []}
    entries = payload.get("entries")
    if not isinstance(entries, list):
        payload["entries"] = []
    payload.setdefault("schema_version", "1.0.0")
    return payload


def stage_failure_digest_entry(*, entry: dict, path: str | Path | None = None) -> str:
    digest_path = Path(path) if path else default_failure_digest_path()
    ensure_directory(digest_path.parent)
    digest = load_failure_digest(digest_path)
    digest.setdefault("entries", []).append(entry)
    digest["last_updated_utc"] = utc_now_iso()
    digest_path.write_text(json.dumps(digest, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    return str(digest_path)


def clear_failure_digest(path: str | Path | None = None) -> str:
    digest_path = Path(path) if path else default_failure_digest_path()
    ensure_directory(digest_path.parent)
    digest_path.write_text(json.dumps({"schema_version": "1.0.0", "entries": [], "last_cleared_utc": utc_now_iso()}, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    return str(digest_path)


def _send_agentmail_failure(
    *,
    subject: str,
    body: str,
    recipient_source: str,
) -> str | None:
    api_key = os.getenv("AGENTMAIL_API_KEY", "").strip()
    inbox_id = os.getenv("AGENTMAIL_FROM", "").strip()
    recipient = os.getenv(recipient_source, "").strip()
    if not api_key:
        return "AgentMail failure notification skipped because AGENTMAIL_API_KEY is missing."
    if not inbox_id:
        return "AgentMail failure notification skipped because AGENTMAIL_FROM is missing."
    if not recipient:
        return f"AgentMail failure notification skipped because {recipient_source} is missing."

    response = requests.post(
        f"{AGENTMAIL_BASE_URL}/inboxes/{inbox_id}/messages/send",
        headers={
            "Authorization": f"Bearer {api_key}",
            "Content-Type": "application/json",
        },
        json={
            "to": [recipient],
            "subject": subject,
            "text": body,
        },
        timeout=30,
    )
    try:
        payload = response.json()
    except ValueError:
        payload = {"status_code": response.status_code, "text": response.text[:500]}
    if response.status_code >= 400:
        return f"AgentMail failure notification failed with status {response.status_code}: {json.dumps(mask_secrets(payload), sort_keys=True)}"
    return None


def dispatch_failure_notifications(
    *,
    run_id: str,
    runtime_mode: str,
    stage_name: str,
    summary: str,
    output_dir: str,
    notification_policy_path: str | None = None,
) -> tuple[list[str], str | None]:
    load_runtime_env()
    warnings: list[str] = []
    created_at = utc_now_iso()
    message = (
        "AlTrader failure alert\n\n"
        f"Run ID: {run_id}\n"
        f"Runtime mode: {runtime_mode}\n"
        f"Failed stage: {stage_name}\n"
        f"Summary: {summary}\n"
        f"Created at UTC: {created_at}\n"
    )

    telegram_payload, telegram_error = send_telegram_message_verbose(message=message)
    if telegram_error:
        warnings.append(telegram_error)
        provider_issue = classify_provider_capacity_issue(
            provider="telegram",
            error_text=telegram_error,
            payload=telegram_payload,
        )
        if provider_issue:
            _, provider_alert_error, _ = send_provider_capacity_alert(
                run_id=run_id,
                runtime_mode=runtime_mode,
                provider_issue=provider_issue,
                stage_name=stage_name,
                detail=summary,
                output_dir=output_dir,
            )
            if provider_alert_error:
                warnings.append(provider_alert_error)

    openai_issue = classify_provider_capacity_issue(
        provider="openai",
        error_text=summary,
    )
    if openai_issue:
        _, provider_alert_error, _ = send_provider_capacity_alert(
            run_id=run_id,
            runtime_mode=runtime_mode,
            provider_issue=openai_issue,
            stage_name=stage_name,
            detail=summary,
            output_dir=output_dir,
        )
        if provider_alert_error:
            warnings.append(provider_alert_error)

    policy_path = notification_policy_path or (
        str(default_notification_policy_path()) if default_notification_policy_path() else None
    )
    digest_entry = {
        "run_id": run_id,
        "runtime_mode": runtime_mode,
        "failed_stage": stage_name,
        "summary": summary,
        "created_at_utc": created_at,
    }
    digest_path = stage_failure_digest_entry(entry=digest_entry)

    email_deferred = False
    if policy_path:
        policy = load_json_file(resolve_path(policy_path))
        agentmail = policy.get("agentmail", {})
        if agentmail.get("enabled") and agentmail.get("send_exception_reports"):
            email_deferred = True

    notification_artifact = {
        "schema_version": "1.0.0",
        "run_id": run_id,
        "runtime_mode": runtime_mode,
        "failed_stage": stage_name,
        "summary": summary,
        "created_at_utc": created_at,
        "telegram_attempted": True,
        "telegram_response": mask_secrets(telegram_payload) if telegram_payload else None,
        "email_attempted": False,
        "email_deferred_to_daily_summary": email_deferred,
        "failure_digest_path": digest_path,
        "warnings": warnings,
    }
    artifact_dir = ensure_directory(resolve_path(output_dir))
    artifact_path = artifact_dir / f"{run_id}_{stage_name}_failure_notification.json"
    artifact_path.write_text(json.dumps(notification_artifact, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    return warnings, str(artifact_path)

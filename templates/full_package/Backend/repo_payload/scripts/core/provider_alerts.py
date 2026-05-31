from __future__ import annotations

import json
from pathlib import Path

from scripts.core.path_utils import ensure_directory, resolve_path
from scripts.core.secret_masker import mask_secrets
from scripts.core.telegram import send_telegram_message_verbose
from scripts.core.time_utils import utc_now_iso


_OPENAI_RATE_PATTERNS = (
    "rate_limit_exceeded",
    "tokens per min",
    "rate limit reached",
    "too many requests",
)

_OPENAI_QUOTA_PATTERNS = (
    "insufficient_quota",
    "billing_hard_limit_reached",
    "out of credits",
    "usage limit",
    "quota",
)

_AGENTMAIL_PATTERNS = (
    "rate limit",
    "too many requests",
    "quota",
    "credits",
)

_TELEGRAM_PATTERNS = (
    "too many requests",
    "retry_after",
    "rate limit",
)


def _build_haystack(*, error_text: str | None = None, payload: object | None = None) -> str:
    parts: list[str] = []
    if error_text:
        parts.append(error_text)
    if payload is not None:
        try:
            parts.append(json.dumps(mask_secrets(payload), sort_keys=True))
        except TypeError:
            parts.append(str(payload))
    return " ".join(parts).lower()


def classify_provider_capacity_issue(
    *,
    provider: str,
    status_code: int | None = None,
    error_text: str | None = None,
    payload: object | None = None,
) -> dict | None:
    normalized = provider.strip().lower()
    haystack = _build_haystack(error_text=error_text, payload=payload)

    if normalized in {"openai", "chatgpt"}:
        if status_code == 429 or any(pattern in haystack for pattern in _OPENAI_RATE_PATTERNS):
            return {
                "provider": "OpenAI/ChatGPT",
                "event_type": "rate_limit",
                "summary": "OpenAI/ChatGPT rate limit or usage cap was hit during the AlTrader run.",
            }
        if status_code in {402, 403} or any(pattern in haystack for pattern in _OPENAI_QUOTA_PATTERNS):
            return {
                "provider": "OpenAI/ChatGPT",
                "event_type": "quota",
                "summary": "OpenAI/ChatGPT credits, quota, or usage allowance was exhausted during the AlTrader run.",
            }
        return None

    if normalized == "agentmail":
        if status_code == 429 or any(pattern in haystack for pattern in _AGENTMAIL_PATTERNS):
            return {
                "provider": "AgentMail",
                "event_type": "rate_limit",
                "summary": "AgentMail rate limit, quota, or credit issue blocked email delivery for the AlTrader run.",
            }
        return None

    if normalized == "telegram":
        if status_code == 429 or any(pattern in haystack for pattern in _TELEGRAM_PATTERNS):
            return {
                "provider": "Telegram",
                "event_type": "rate_limit",
                "summary": "Telegram rate limiting blocked an AlTrader notification send.",
            }
        return None

    return None


def send_provider_capacity_alert(
    *,
    run_id: str,
    runtime_mode: str,
    provider_issue: dict,
    output_dir: str | None = None,
    stage_name: str | None = None,
    detail: str | None = None,
) -> tuple[dict | None, str | None, str | None]:
    stage_line = f"Stage: {stage_name}\n" if stage_name else ""
    detail_line = f"Detail: {detail}\n" if detail else ""
    message = (
        "AlTrader provider alert\n\n"
        f"Run ID: {run_id}\n"
        f"Runtime mode: {runtime_mode}\n"
        f"Provider: {provider_issue['provider']}\n"
        f"Event: {provider_issue['event_type']}\n"
        f"{stage_line}"
        f"Summary: {provider_issue['summary']}\n"
        f"{detail_line}"
        f"Created at UTC: {utc_now_iso()}\n"
    )
    payload, error = send_telegram_message_verbose(message=message)

    artifact_path = None
    if output_dir:
        artifact_dir = ensure_directory(resolve_path(output_dir))
        artifact = {
            "schema_version": "1.0.0",
            "run_id": run_id,
            "runtime_mode": runtime_mode,
            "provider_issue": provider_issue,
            "stage_name": stage_name,
            "detail": detail,
            "created_at_utc": utc_now_iso(),
            "telegram_response": mask_secrets(payload) if payload else None,
            "telegram_error": error,
        }
        artifact_path = artifact_dir / f"{run_id}_{provider_issue['provider'].lower().replace('/', '_').replace(' ', '_')}_provider_alert.json"
        artifact_path.write_text(json.dumps(artifact, indent=2, sort_keys=True) + "\n", encoding="utf-8")

    return payload, error, str(artifact_path) if artifact_path else None

from __future__ import annotations

import os

import requests


def resolve_telegram_target() -> str:
    explicit = os.getenv("TELEGRAM_HOME_CHANNEL", "").strip()
    if explicit:
        return explicit
    allowed = os.getenv("TELEGRAM_ALLOWED_USERS", "").strip()
    if not allowed:
        return ""
    return allowed.split(",")[0].strip()


def send_telegram_message_verbose(*, message: str, timeout: int = 15) -> tuple[dict | None, str | None]:
    token = os.getenv("TELEGRAM_BOT_TOKEN", "").strip()
    chat_id = resolve_telegram_target()
    if not token:
        return None, "Telegram notification requested but TELEGRAM_BOT_TOKEN is missing."
    if not chat_id:
        return None, "Telegram notification requested but no Telegram target is configured."

    response = requests.post(
        f"https://api.telegram.org/bot{token}/sendMessage",
        json={
            "chat_id": chat_id,
            "text": message,
            "disable_web_page_preview": True,
        },
        timeout=timeout,
    )
    try:
        payload = response.json()
    except ValueError:
        payload = {"status_code": response.status_code, "text": response.text[:500]}
    if response.status_code >= 400:
        return payload if isinstance(payload, dict) else {"response": payload}, f"Telegram notification failed with status {response.status_code}."
    return payload if isinstance(payload, dict) else {"response": payload}, None


def send_telegram_message(*, message: str, timeout: int = 15) -> str | None:
    _, error = send_telegram_message_verbose(message=message, timeout=timeout)
    return error

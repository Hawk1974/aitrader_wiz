from __future__ import annotations

import re
from collections.abc import Mapping, Sequence
from typing import Any


SECRET_KEY_PARTS = ("api_key", "secret", "token", "password", "authorization", "credential")
SECRET_VALUE_RE = re.compile(r"(sk-[A-Za-z0-9_\-]{12,}|AKIA[A-Z0-9]{16}|[A-Za-z0-9_\-]{32,})")


def mask_secret_value(value: str) -> str:
    if not value:
        return value
    if len(value) <= 8:
        return "***"
    return f"{value[:4]}...{value[-4:]}"


def mask_text(text: str) -> str:
    return SECRET_VALUE_RE.sub(lambda match: mask_secret_value(match.group(0)), text)


def mask_secrets(value: Any) -> Any:
    if isinstance(value, Mapping):
        masked: dict[str, Any] = {}
        for key, item in value.items():
            key_text = str(key)
            if any(part in key_text.lower() for part in SECRET_KEY_PARTS):
                masked[key_text] = "***"
            else:
                masked[key_text] = mask_secrets(item)
        return masked
    if isinstance(value, str):
        return mask_text(value)
    if isinstance(value, Sequence) and not isinstance(value, (str, bytes, bytearray)):
        return [mask_secrets(item) for item in value]
    return value

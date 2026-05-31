from __future__ import annotations

from datetime import UTC, datetime


def utc_now() -> datetime:
    return datetime.now(UTC).replace(microsecond=0)


def utc_now_iso() -> str:
    return utc_now().strftime("%Y-%m-%dT%H:%M:%SZ")


def dated_parts(timestamp_utc: str | None = None) -> tuple[str, str, str]:
    value = timestamp_utc or utc_now_iso()
    return value[0:4], value[5:7], value[8:10]

from __future__ import annotations

import hashlib
import json
from pathlib import Path
from typing import Any


EMPTY_AUDIT_HASH = "sha256:" + ("0" * 64)


def canonical_json(value: Any) -> str:
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True)


def sha256_text(text: str) -> str:
    return "sha256:" + hashlib.sha256(text.encode("utf-8")).hexdigest()


def sha256_json(value: Any) -> str:
    return sha256_text(canonical_json(value))


def sha256_file(path: str | Path) -> str:
    digest = hashlib.sha256()
    with Path(path).open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return "sha256:" + digest.hexdigest()


def previous_hash(audit_file: Path) -> str:
    if not audit_file.exists():
        return EMPTY_AUDIT_HASH
    last_line = ""
    for line in audit_file.read_text(encoding="utf-8").splitlines():
        if line.strip():
            last_line = line
    if not last_line:
        return EMPTY_AUDIT_HASH
    try:
        record = json.loads(last_line)
    except json.JSONDecodeError:
        return EMPTY_AUDIT_HASH
    return str(record.get("audit_hash") or EMPTY_AUDIT_HASH)

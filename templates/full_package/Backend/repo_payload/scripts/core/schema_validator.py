from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from jsonschema import Draft202012Validator

from scripts.core.path_utils import project_root


class SchemaValidationError(ValueError):
    pass


def load_schema(schema_name: str, schemas_dir: Path | None = None) -> dict[str, Any]:
    base = schemas_dir or project_root() / "schemas"
    path = base / f"{schema_name}.schema.json"
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except FileNotFoundError as exc:
        raise SchemaValidationError(f"missing schema: {path}") from exc


def validate_data(schema_name: str, data: dict[str, Any], schemas_dir: Path | None = None) -> None:
    schema = load_schema(schema_name, schemas_dir)
    validator = Draft202012Validator(schema)
    errors = sorted(validator.iter_errors(data), key=lambda error: list(error.path))
    if errors:
        first = errors[0]
        path = ".".join(str(part) for part in first.path) or "<root>"
        raise SchemaValidationError(f"{schema_name} validation failed at {path}: {first.message}")


def validate_json_file(schema_name: str, path: str | Path, schemas_dir: Path | None = None) -> dict[str, Any]:
    data = json.loads(Path(path).read_text(encoding="utf-8"))
    if not isinstance(data, dict):
        raise SchemaValidationError(f"{path} must contain a JSON object")
    validate_data(schema_name, data, schemas_dir)
    return data

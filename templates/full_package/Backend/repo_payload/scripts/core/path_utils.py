from __future__ import annotations

from pathlib import Path


REQUIRED_DATA_DIRS = [
    "raw",
    "raw/capitol_trades",
    "normalized",
    "events",
    "model",
    "risk",
    "risk/manual_approvals",
    "orders",
    "positions",
    "reports",
    "reports/manifests",
    "audit",
    "state",
    "state/quarantine",
    "backtests",
    "backups",
    "kanban_artifacts",
]


def project_root() -> Path:
    return Path(__file__).resolve().parents[2]


def resolve_path(path: str | Path, base: Path | None = None) -> Path:
    value = Path(path)
    if value.is_absolute():
        return value
    return (base or project_root()).joinpath(value).resolve()


def ensure_directory(path: str | Path) -> Path:
    value = Path(path)
    value.mkdir(parents=True, exist_ok=True)
    return value


def ensure_project_layout(root: Path | None = None) -> list[Path]:
    base = root or project_root()
    created: list[Path] = []
    for folder in REQUIRED_DATA_DIRS:
        created.append(ensure_directory(base / "data" / folder))
    ensure_directory(base / "logs")
    return created

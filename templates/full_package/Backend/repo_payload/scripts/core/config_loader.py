from __future__ import annotations

import json
import os
from pathlib import Path
from typing import Any

from dotenv import load_dotenv


class ConfigError(ValueError):
    pass


def load_runtime_env() -> list[Path]:
    loaded: list[Path] = []
    hermes_env = Path.home() / ".hermes" / ".env"
    project_env = Path(__file__).resolve().parents[2] / ".env"

    if hermes_env.exists():
        load_dotenv(hermes_env, override=True)
        loaded.append(hermes_env)
    if project_env.exists():
        load_dotenv(project_env, override=False)
        loaded.append(project_env)
    return loaded


def load_json_file(path: str | Path) -> dict[str, Any]:
    value = Path(path)
    try:
        data = json.loads(value.read_text(encoding="utf-8"))
    except FileNotFoundError as exc:
        raise ConfigError(f"missing JSON file: {value}") from exc
    except json.JSONDecodeError as exc:
        raise ConfigError(f"invalid JSON file: {value}: {exc}") from exc
    if not isinstance(data, dict):
        raise ConfigError(f"JSON file must contain an object: {value}")
    return data


def env_bool(name: str, default: bool = False) -> bool:
    raw = os.getenv(name)
    if raw is None:
        return default
    return raw.strip().lower() in {"1", "true", "yes", "y", "on"}


def credential_presence(broker_mode: str) -> dict[str, bool]:
    if broker_mode == "paper":
        return {
            "ALPACA_PAPER_API_KEY": bool(os.getenv("ALPACA_PAPER_API_KEY")),
            "ALPACA_PAPER_SECRET_KEY": bool(os.getenv("ALPACA_PAPER_SECRET_KEY")),
        }
    if broker_mode == "live":
        return {
            "ALPACA_LIVE_API_KEY": bool(os.getenv("ALPACA_LIVE_API_KEY")),
            "ALPACA_LIVE_SECRET_KEY": bool(os.getenv("ALPACA_LIVE_SECRET_KEY")),
            "ALPACA_LIVE_TRADING_ENABLED": env_bool("ALPACA_LIVE_TRADING_ENABLED"),
        }
    raise ConfigError(f"unknown broker mode: {broker_mode}")

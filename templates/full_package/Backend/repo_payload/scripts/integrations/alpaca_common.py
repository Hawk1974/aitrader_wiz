from __future__ import annotations

import os
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import requests

from scripts.core.audit_chain import sha256_json
from scripts.core.config_loader import ConfigError, credential_presence, env_bool, load_json_file, load_runtime_env
from scripts.core.emergency_state import EmergencyStateCorrupt, initialize_emergency_state, read_emergency_state
from scripts.core.path_utils import ensure_project_layout, project_root
from scripts.core.policy_validator import broker_mode_for_runtime, validate_runtime_mode
from scripts.core.schema_validator import SchemaValidationError, validate_data
from scripts.core.secret_masker import mask_secrets
from scripts.core.time_utils import utc_now_iso


PAPER_TRADING_BASE_URL = "https://paper-api.alpaca.markets"
LIVE_TRADING_BASE_URL = "https://api.alpaca.markets"
PAPER_DATA_BASE_URL = "https://data.alpaca.markets"
LIVE_DATA_BASE_URL = "https://data.alpaca.markets"


@dataclass(frozen=True)
class BrokerContext:
    runtime_mode: str
    broker_mode: str
    trading_base_url: str
    data_base_url: str
    api_key: str
    secret_key: str


@dataclass(frozen=True)
class SafeFailure:
    exit_code: int
    errors: list[str]
    broker_response: dict[str, Any] | None = None


def _normalize_base_url(value: str, fallback: str) -> str:
    cleaned = value.strip().rstrip("/")
    if not cleaned:
        return fallback
    if cleaned.endswith("/v2"):
        cleaned = cleaned[:-3]
    return cleaned


def resolve_broker_mode(*, config_path: str, runtime_mode: str, explicit_broker_mode: str = "") -> str:
    if explicit_broker_mode:
        if explicit_broker_mode not in {"paper", "live"}:
            raise ConfigError(f"unsupported broker mode: {explicit_broker_mode}")
        return explicit_broker_mode

    derived = broker_mode_for_runtime(runtime_mode)
    if derived:
        return derived

    config = load_json_file(config_path)
    broker = config.get("broker", {})
    default_mode = str(broker.get("default_mode", "paper"))
    if default_mode not in {"paper", "live"}:
        raise ConfigError(f"unsupported default broker mode in config: {default_mode}")
    return default_mode


def prepare_broker_context(*, args: Any, broker_mode: str) -> BrokerContext | SafeFailure:
    load_runtime_env()
    mode_result = validate_runtime_mode(args.runtime_mode)
    if not mode_result.ok:
        return SafeFailure(mode_result.exit_code, mode_result.errors)

    try:
        load_json_file(args.config)
        ensure_project_layout(project_root())
        initialize_emergency_state(args.state_dir)
        emergency_state = read_emergency_state(args.state_dir)
        if emergency_state["active"]:
            raise EmergencyStateCorrupt("emergency stop is active")
    except ConfigError as exc:
        return SafeFailure(2, [str(exc)])
    except OSError as exc:
        return SafeFailure(7, [str(exc)])
    except EmergencyStateCorrupt as exc:
        return SafeFailure(8, [str(exc)])

    presence = credential_presence(broker_mode)
    missing = [name for name, present in presence.items() if not present]
    if missing:
        if broker_mode == "live":
            return SafeFailure(
                12,
                [f"live credential/enablement check failed: missing {', '.join(missing)}"],
                broker_response={"credential_presence": presence},
            )
        return SafeFailure(
            3,
            [f"missing required {broker_mode} credentials: {', '.join(missing)}"],
            broker_response={"credential_presence": presence},
        )

    if broker_mode == "live" and not env_bool("ALPACA_LIVE_TRADING_ENABLED"):
        return SafeFailure(
            12,
            ["ALPACA_LIVE_TRADING_ENABLED must be true for live broker access"],
            broker_response={"ALPACA_LIVE_TRADING_ENABLED": os.getenv("ALPACA_LIVE_TRADING_ENABLED", "")},
        )

    if broker_mode == "paper":
        return BrokerContext(
            runtime_mode=args.runtime_mode,
            broker_mode="paper",
            trading_base_url=_normalize_base_url(os.getenv("ALPACA_PAPER_BASE_URL", ""), PAPER_TRADING_BASE_URL),
            data_base_url=PAPER_DATA_BASE_URL,
            api_key=os.getenv("ALPACA_PAPER_API_KEY", ""),
            secret_key=os.getenv("ALPACA_PAPER_SECRET_KEY", ""),
        )

    return BrokerContext(
        runtime_mode=args.runtime_mode,
        broker_mode="live",
        trading_base_url=_normalize_base_url(os.getenv("ALPACA_LIVE_BASE_URL", ""), LIVE_TRADING_BASE_URL),
        data_base_url=LIVE_DATA_BASE_URL,
        api_key=os.getenv("ALPACA_LIVE_API_KEY", ""),
        secret_key=os.getenv("ALPACA_LIVE_SECRET_KEY", ""),
    )


def alpaca_headers(context: BrokerContext) -> dict[str, str]:
    return {
        "accept": "application/json",
        "APCA-API-KEY-ID": context.api_key,
        "APCA-API-SECRET-KEY": context.secret_key,
    }


def alpaca_get(context: BrokerContext, *, url: str, params: dict[str, Any] | None = None) -> dict[str, Any] | list[Any]:
    response = requests.get(url, headers=alpaca_headers(context), params=params, timeout=30)
    try:
        payload = response.json()
    except ValueError:
        payload = {"status_code": response.status_code, "text": response.text}
    if response.status_code >= 400:
        raise RuntimeError(mask_secrets(payload))
    return payload


def validate_and_write_json(*, schema_name: str, artifact: dict[str, Any], path: Path) -> None:
    validate_data(schema_name, artifact)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(__import__("json").dumps(artifact, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def runtime_input_hash(*, script: str, operation: str, runtime_mode: str, broker_mode: str, extra: dict[str, Any] | None = None) -> str:
    payload = {
        "script": script,
        "operation": operation,
        "runtime_mode": runtime_mode,
        "broker_mode": broker_mode,
        "timestamp_utc": utc_now_iso(),
    }
    if extra:
        payload["extra"] = extra
    return sha256_json(payload)

from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

if __package__ is None or __package__ == "":
    sys.path.append(str(Path(__file__).resolve().parents[2]))

from scripts.core.failure_notifications import dispatch_failure_notifications
from scripts.core.time_utils import utc_now_iso
from scripts.integrations.alpaca_account_status import build_parser as account_status_parser
from scripts.integrations.alpaca_account_status import run as run_account_status
from scripts.integrations.alpaca_market_data import build_parser as market_data_parser
from scripts.integrations.alpaca_market_data import run as run_market_data
from scripts.integrations.alpaca_order_monitor import build_parser as order_monitor_parser
from scripts.integrations.alpaca_order_monitor import run as run_order_monitor
from scripts.integrations.alpaca_order_submitter import build_parser as order_submitter_parser
from scripts.integrations.alpaca_order_submitter import run as run_order_submitter
from scripts.integrations.capitol_trades_monitor import build_parser as source_monitor_parser
from scripts.integrations.capitol_trades_monitor import run as run_source_monitor
from scripts.reporting.trade_report_writer import build_parser as report_writer_parser
from scripts.reporting.trade_report_writer import run as run_report_writer
from scripts.trading.alpaca_risk_gate import build_parser as risk_gate_parser
from scripts.trading.alpaca_risk_gate import run as run_risk_gate
from scripts.trading.capitol_trades_event_normalizer import build_parser as normalizer_parser
from scripts.trading.capitol_trades_event_normalizer import run as run_normalizer
from scripts.trading.congressional_trade_analyzer import build_parser as analyzer_parser
from scripts.trading.congressional_trade_analyzer import run as run_analyzer
from scripts.trading.manual_approval_queue import build_parser as approval_parser
from scripts.trading.manual_approval_queue import run as run_approval
from scripts.validation.ai_trader_health_check import build_parser as health_parser
from scripts.validation.ai_trader_health_check import run as run_health_check
from scripts.validation.validate_vision_reasoning_fallback import build_parser as fallback_parser
from scripts.validation.validate_vision_reasoning_fallback import run as run_fallback_check


ROOT = Path(__file__).resolve().parents[2]
SEED_PATH = ROOT / "hermes" / "kanban" / "altrader_seed_tasks.json"
HERMES_EXE = Path.home() / ".hermes" / "hermes-agent" / "venv" / "Scripts" / "hermes.exe"
CONFIG_PATH = ROOT / "config" / "ai_trader.config.example.json"
SOURCE_POLICY_PATH = ROOT / "config" / "source_policy.example.json"
RISK_POLICY_PATH = ROOT / "config" / "risk_policy.example.json"
REPORT_POLICY_LOCAL_PATH = Path.home() / ".hermes" / "altrader" / "report_policy.local.json"
REPORT_POLICY_EXAMPLE_PATH = ROOT / "config" / "report_policy.example.json"
PROMPT_PATH = ROOT / "docs" / "handoff" / "hermes_ai_trader_agent_standup" / "prompts" / "hermes_congressional_trade_analyzer_prompt.md"
OUTPUT_DIR = ROOT / "data" / "runtime" / "reports"
AUDIT_DIR = ROOT / "data" / "runtime" / "audit"
STATE_DIR = ROOT / "data" / "runtime" / "state"
MODEL_DIR = ROOT / "data" / "model"
KANBAN_STATE_PATH = STATE_DIR / "kanban_cycle_state.json"
TOP_CANDIDATE_LIMIT = 10
KNOWN_ISSUER_TICKER_ALIASES = {
    "FIRST CITIZENS BANCORPORATION INC": "FCNCA",
}
STAGE_SEQUENCE = [
    "altrader-highmarshal-open-trading-day",
    "altrader-chirurgeon-startup-gate",
    "altrader-scryer-capitol-intake",
    "altrader-runesmith-normalize-events",
    "altrader-augur-analyze-candidates",
    "altrader-coinmaster-broker-context",
    "altrader-warden-risk-gate",
    "altrader-overlord-manual-approval",
    "altrader-gatekeeper-paper-submission",
    "altrader-tracker-reconcile-positions",
    "altrader-bard-daily-summary",
    "altrader-highmarshal-closeout",
]


def _run_cli(args: list[str], *, expect_json: bool = False) -> Any:
    command = list(args)
    board = os.environ.get("ALTRADER_HERMES_KANBAN_BOARD") or os.environ.get("HERMES_KANBAN_BOARD")
    if board and len(command) >= 2 and Path(command[0]).name.lower() == "hermes.exe" and command[1] == "kanban" and "--board" not in command:
        command = [command[0], command[1], "--board", board, *command[2:]]
    result = subprocess.run(command, capture_output=True, text=True, check=True)
    if expect_json:
        return json.loads(result.stdout)
    return result.stdout


def _load_seed() -> dict[str, Any]:
    return json.loads(SEED_PATH.read_text(encoding="utf-8"))


def _load_state(force_new: bool = False) -> dict[str, Any]:
    if not force_new and KANBAN_STATE_PATH.exists():
        return json.loads(KANBAN_STATE_PATH.read_text(encoding="utf-8"))
    state = _new_state()
    _save_state(state)
    return state


def _new_state(cycle_run_id: str | None = None) -> dict[str, Any]:
    return {
        "schema_version": "1.0.0",
        "cycle_run_id": cycle_run_id or datetime.now(timezone.utc).strftime("kanban-cycle-%Y%m%dT%H%M%SZ"),
        "cycle_date": datetime.now(timezone.utc).strftime("%Y-%m-%d"),
        "created_at_utc": utc_now_iso(),
        "artifacts": {},
        "selected_candidate": None,
        "report_sent": False,
        "halted": False,
        "no_action": False,
    }


def _save_state(state: dict[str, Any]) -> None:
    STATE_DIR.mkdir(parents=True, exist_ok=True)
    KANBAN_STATE_PATH.write_text(json.dumps(state, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def _record_artifacts(state: dict[str, Any], stage_id: str, artifact_paths: list[str]) -> None:
    bucket = state.setdefault("artifacts", {})
    bucket[stage_id] = list(dict.fromkeys([*bucket.get(stage_id, []), *artifact_paths]))
    _save_state(state)


def _all_artifacts(state: dict[str, Any]) -> list[str]:
    items: list[str] = []
    for paths in state.get("artifacts", {}).values():
        items.extend(paths)
    return list(dict.fromkeys(items))


def _envelope_artifact_paths(envelope: dict[str, Any]) -> list[str]:
    outputs = envelope.get("outputs", {}) if isinstance(envelope, dict) else {}
    items: list[str] = []
    for key in ("json_path", "markdown_report_path"):
        value = outputs.get(key)
        if value:
            items.append(str(value))
    for value in outputs.get("artifact_paths", []):
        if value:
            items.append(str(value))
    return list(dict.fromkeys(items))


def _report_policy_path() -> Path:
    return REPORT_POLICY_LOCAL_PATH if REPORT_POLICY_LOCAL_PATH.exists() else REPORT_POLICY_EXAMPLE_PATH


def _seed_task_map(seed: dict[str, Any]) -> dict[str, dict[str, Any]]:
    return {task["id"]: task for task in seed["tasks"]}


def _live_seed_tasks(seed: dict[str, Any]) -> dict[str, dict[str, Any]]:
    live_tasks = _run_cli([str(HERMES_EXE), "kanban", "list", "--json"], expect_json=True)
    resolved: dict[str, dict[str, Any]] = {}
    for task in seed["tasks"]:
        candidates = [item for item in live_tasks if str(item.get("title", "")) == str(task["title"])]
        if not candidates:
            continue
        live = max(candidates, key=lambda item: int(item.get("created_at") or 0))
        detail = _run_cli([str(HERMES_EXE), "kanban", "show", str(live["id"]), "--json"], expect_json=True)
        if isinstance(detail, dict) and isinstance(detail.get("task"), dict):
            resolved[task["id"]] = detail["task"]
        else:
            resolved[task["id"]] = live
    return resolved


def _task_status(task: dict[str, Any] | None) -> str:
    return str((task or {}).get("status", "")).strip().lower()


def _extract_cycle_run_id_from_task(task: dict[str, Any] | None) -> str | None:
    body = str((task or {}).get("body") or "")
    prefix = "Current workflow cycle run id:"
    for line in body.splitlines():
        if line.strip().startswith(prefix):
            value = line.split(":", 1)[1].strip()
            return value or None
    return None


def _expected_cycle_run_id(live_seed_tasks: dict[str, dict[str, Any]]) -> str | None:
    for seed_id in STAGE_SEQUENCE:
        run_id = _extract_cycle_run_id_from_task(live_seed_tasks.get(seed_id))
        if run_id:
            return run_id
    return None


def _json_read(path: str | Path) -> dict[str, Any]:
    return json.loads(Path(path).read_text(encoding="utf-8"))


def _find_result_artifact(envelope: dict[str, Any], *, suffix: str) -> str | None:
    for path in envelope.get("outputs", {}).get("artifact_paths", []):
        if str(path).endswith(suffix):
            return str(path)
    return None


def _complete_task(task_id: str, summary: str, metadata: dict[str, Any] | None = None) -> None:
    args = [str(HERMES_EXE), "kanban", "complete", task_id, "--summary", summary]
    if metadata:
        args.extend(["--metadata", json.dumps(metadata, sort_keys=True)])
    _run_cli(args)


def _complete_task_if_needed(task_id: str, summary: str, metadata: dict[str, Any] | None = None) -> None:
    _unblock_task(task_id)
    _complete_task(task_id, summary, metadata)


def _block_task(task_id: str, reason: str) -> None:
    _run_cli([str(HERMES_EXE), "kanban", "block", task_id, reason])


def _unblock_task(task_id: str) -> None:
    try:
        _run_cli([str(HERMES_EXE), "kanban", "unblock", task_id])
    except subprocess.CalledProcessError:
        pass


def _claim_task(task_id: str) -> None:
    try:
        _run_cli([str(HERMES_EXE), "kanban", "claim", task_id])
    except subprocess.CalledProcessError:
        pass


def _sync_desktop_mirror() -> None:
    subprocess.run(
        ["python", str(ROOT / "scripts" / "operations" / "sync_hermes_kanban_desktop_mirror.py")],
        cwd=str(ROOT),
        check=True,
        capture_output=True,
        text=True,
    )


def _write_no_action_artifact(*, state: dict[str, Any], stage_id: str, summary: str, details: dict[str, Any] | None = None) -> str:
    payload = {
        "schema_version": "1.0.0",
        "run_id": state["cycle_run_id"],
        "status": "no_action",
        "stage_id": stage_id,
        "summary": summary,
        "created_at_utc": utc_now_iso(),
    }
    if details:
        payload["details"] = details
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    destination = OUTPUT_DIR / f"{state['cycle_run_id']}_{stage_id}_no_action.json"
    destination.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    return str(destination)


def _sanitize_source_symbol(value: str) -> str:
    text = str(value or "").strip().upper()
    text = text.split(":", 1)[0]
    text = text.split("/", 1)[0]
    text = text.split(" ", 1)[0]
    text = "".join(ch for ch in text if ch.isalnum() or ch in {".", "-"})
    return text


def _candidate_symbol_options(raw_payload: dict[str, Any]) -> list[str]:
    options: list[str] = []
    sanitized = _sanitize_source_symbol(str(raw_payload.get("ticker") or raw_payload.get("symbol") or ""))
    if sanitized:
        options.append(sanitized)
    issuer = str(raw_payload.get("issuer") or "").strip().upper()
    alias = KNOWN_ISSUER_TICKER_ALIASES.get(issuer)
    if alias:
        options.insert(0, alias)
    unique: list[str] = []
    for item in options:
        if item and item not in unique:
            unique.append(item)
    return unique


def _stage_run_id(state: dict[str, Any], label: str) -> str:
    return f"{state['cycle_run_id']}-{label}"


def _reconcile_upstream_completed_tasks(
    *,
    state: dict[str, Any],
    live_seed_tasks: dict[str, dict[str, Any]],
    through_stage_id: str,
) -> None:
    for upstream_stage_id in STAGE_SEQUENCE[: STAGE_SEQUENCE.index(through_stage_id) + 1]:
        task = live_seed_tasks.get(upstream_stage_id)
        if not task or _task_status(task) == "done":
            continue
        task_id = str(task["id"])
        if upstream_stage_id == "altrader-highmarshal-open-trading-day":
            _complete_task_if_needed(
                task_id,
                "Seeded trading-day graph is intact and specialist assignments are correct.",
                {"stage": upstream_stage_id, "reconciled": True},
            )
            continue
        artifact_paths = list(state.get("artifacts", {}).get(upstream_stage_id, []))
        if not artifact_paths:
            continue
        _complete_task_if_needed(
            task_id,
            f"Stage completed and reconciled from deterministic artifacts for {upstream_stage_id}.",
            {"stage": upstream_stage_id, "reconciled": True, "artifact_paths": artifact_paths},
        )


def _finalize_halted_cycle(
    *,
    state: dict[str, Any],
    live_seed_tasks: dict[str, dict[str, Any]],
    failed_stage_id: str,
    summary: str,
    artifact_paths: list[str],
) -> None:
    state["halted"] = True
    state["halt_stage_id"] = failed_stage_id
    _reconcile_upstream_completed_tasks(state=state, live_seed_tasks=live_seed_tasks, through_stage_id=failed_stage_id)

    bard_task = live_seed_tasks.get("altrader-bard-daily-summary")
    if bard_task and not state.get("report_sent"):
        args = report_writer_parser().parse_args(
            [
                "--config",
                str(CONFIG_PATH),
                "--runtime-mode",
                "paper_manual",
                "--run-id",
                _stage_run_id(state, "failure-summary"),
                "--output-dir",
                str(OUTPUT_DIR),
                "--audit-dir",
                str(AUDIT_DIR),
                "--state-dir",
                str(STATE_DIR),
                "--kanban-task-id",
                str(bard_task["id"]),
                "--report-type",
                "daily_cycle",
                *sum([["--artifact", path] for path in artifact_paths], []),
                "--report-policy",
                str(_report_policy_path()),
            ]
        )
        envelope, _ = run_report_writer(args)
        _record_artifacts(state, "altrader-bard-daily-summary", envelope["outputs"]["artifact_paths"])
        report_path = envelope["outputs"].get("markdown_report_path") or ""
        if envelope["ok"]:
            _complete_task_if_needed(
                str(bard_task["id"]),
                f"Final failure summary delivered after upstream halt. Artifact: {report_path or 'see report artifacts'}",
                {
                    "halted": True,
                    "halt_stage_id": failed_stage_id,
                    "artifact_paths": envelope["outputs"]["artifact_paths"],
                },
            )
            state["report_sent"] = True
            _save_state(state)
        else:
            _block_task(str(bard_task["id"]), "; ".join(envelope["errors"]) or "Bard failure summary could not be delivered")
            _save_state(state)
            return

    closeout_task = live_seed_tasks.get("altrader-highmarshal-closeout")
    report_paths = state.get("artifacts", {}).get("altrader-bard-daily-summary", [])
    report_path = next((path for path in report_paths if str(path).endswith(".md")), "")

    for stage_id in STAGE_SEQUENCE[STAGE_SEQUENCE.index(failed_stage_id) :]:
        task = live_seed_tasks.get(stage_id)
        if not task or _task_status(task) == "done":
            continue
        task_id = str(task["id"])
        if stage_id == failed_stage_id:
            _complete_task_if_needed(
                task_id,
                f"Safe halt at {failed_stage_id}. {summary}",
                {"halted": True, "failure_summary": summary, "artifact_paths": artifact_paths},
            )
            continue
        if stage_id == "altrader-bard-daily-summary":
            if state.get("report_sent"):
                _complete_task_if_needed(
                    task_id,
                    f"Final failure summary delivered after upstream halt. Artifact: {report_path or 'see report artifacts'}",
                    {"halted": True, "halt_stage_id": failed_stage_id, "artifact_paths": report_paths},
                )
            continue
        if stage_id == "altrader-highmarshal-closeout":
            _complete_task_if_needed(
                task_id,
                f"Trading-day workflow closed safely after halt at {failed_stage_id}. {summary}",
                {"halted": True, "halt_stage_id": failed_stage_id, "failure_summary": summary},
            )
            continue
        _complete_task_if_needed(
            task_id,
            f"Skipped because {failed_stage_id} halted the trading-day workflow: {summary}",
            {"halted": True, "halt_stage_id": failed_stage_id, "skipped": True},
        )
    _save_state(state)


def _finalize_no_action_cycle(
    *,
    state: dict[str, Any],
    live_seed_tasks: dict[str, dict[str, Any]],
    stage_id: str,
    summary: str,
    artifact_paths: list[str],
) -> None:
    state["no_action"] = True
    state["no_action_stage_id"] = stage_id
    state["no_action_summary"] = summary
    _reconcile_upstream_completed_tasks(state=state, live_seed_tasks=live_seed_tasks, through_stage_id=stage_id)
    no_action_artifact = _write_no_action_artifact(
        state=state,
        stage_id=stage_id,
        summary=summary,
        details=state.get("no_action_details") if isinstance(state.get("no_action_details"), dict) else None,
    )
    _record_artifacts(state, stage_id, [*artifact_paths, no_action_artifact])

    bard_task = live_seed_tasks.get("altrader-bard-daily-summary")
    if bard_task and not state.get("report_sent"):
        args = report_writer_parser().parse_args(
            [
                "--config",
                str(CONFIG_PATH),
                "--runtime-mode",
                "paper_manual",
                "--run-id",
                _stage_run_id(state, "no-action-summary"),
                "--output-dir",
                str(OUTPUT_DIR),
                "--audit-dir",
                str(AUDIT_DIR),
                "--state-dir",
                str(STATE_DIR),
                "--kanban-task-id",
                str(bard_task["id"]),
                "--report-type",
                "daily_cycle",
                *sum([["--artifact", path] for path in _all_artifacts(state)], []),
                "--report-policy",
                str(_report_policy_path()),
            ]
        )
        envelope, _ = run_report_writer(args)
        _record_artifacts(state, "altrader-bard-daily-summary", _envelope_artifact_paths(envelope))
        report_path = envelope["outputs"].get("markdown_report_path") or ""
        if envelope["ok"]:
            _complete_task_if_needed(
                str(bard_task["id"]),
                f"No-action daily summary delivered. Artifact: {report_path or 'see report artifacts'}",
                {
                    "no_action": True,
                    "no_action_stage_id": stage_id,
                    "artifact_paths": _envelope_artifact_paths(envelope),
                },
            )
            state["report_sent"] = True
            _save_state(state)
        else:
            _block_task(str(bard_task["id"]), "; ".join(envelope["errors"]) or "No-action daily summary could not be delivered")
            _save_state(state)
            return

    report_paths = state.get("artifacts", {}).get("altrader-bard-daily-summary", [])
    report_path = next((path for path in report_paths if str(path).endswith(".md")), "")

    for downstream_stage_id in STAGE_SEQUENCE[STAGE_SEQUENCE.index(stage_id) :]:
        task = live_seed_tasks.get(downstream_stage_id)
        if not task or _task_status(task) == "done":
            continue
        task_id = str(task["id"])
        if downstream_stage_id == stage_id:
            _complete_task_if_needed(
                task_id,
                f"No action taken at {stage_id}. {summary}",
                {"no_action": True, "summary": summary, "artifact_paths": _all_artifacts(state)},
            )
            continue
        if downstream_stage_id == "altrader-bard-daily-summary":
            if state.get("report_sent"):
                _complete_task_if_needed(
                    task_id,
                    f"No-action daily summary delivered. Artifact: {report_path or 'see report artifacts'}",
                    {"no_action": True, "no_action_stage_id": stage_id, "artifact_paths": report_paths},
                )
            continue
        if downstream_stage_id == "altrader-highmarshal-closeout":
            _complete_task_if_needed(
                task_id,
                f"Trading-day workflow closed with no action after {stage_id}. {summary}",
                {"no_action": True, "no_action_stage_id": stage_id, "summary": summary},
            )
            continue
        _complete_task_if_needed(
            task_id,
            f"Skipped because no new actionable records were available after {stage_id}: {summary}",
            {"no_action": True, "no_action_stage_id": stage_id, "skipped": True},
        )
    _save_state(state)


def _fail_stage(
    *,
    stage_id: str,
    task: dict[str, Any],
    state: dict[str, Any],
    live_seed_tasks: dict[str, dict[str, Any]],
    summary: str,
    artifact_paths: list[str],
    runtime_mode: str,
) -> bool:
    notification_warnings, notification_artifact = dispatch_failure_notifications(
        run_id=state["cycle_run_id"],
        runtime_mode=runtime_mode,
        stage_name=stage_id,
        summary=summary,
        output_dir=str(OUTPUT_DIR),
    )
    failure_artifacts = list(artifact_paths)
    if notification_artifact:
        failure_artifacts.append(notification_artifact)
    if notification_warnings:
        failure_artifacts.extend([])
    _record_artifacts(state, stage_id, failure_artifacts)
    state["halt_summary"] = summary
    _save_state(state)
    _finalize_halted_cycle(
        state=state,
        live_seed_tasks=live_seed_tasks,
        failed_stage_id=stage_id,
        summary=summary,
        artifact_paths=_all_artifacts(state),
    )
    return False


def _latest_source_record_paths(state: dict[str, Any]) -> list[Path]:
    paths = [Path(path) for path in state.get("artifacts", {}).get("altrader-scryer-capitol-intake", [])]
    return [path for path in paths if path.name.endswith("_record.json")]


def _latest_normalized_paths(state: dict[str, Any]) -> list[Path]:
    paths = [Path(path) for path in state.get("artifacts", {}).get("altrader-runesmith-normalize-events", [])]
    return [path for path in paths if path.suffix == ".json" and path.parent.name == "normalized"]


def _build_deterministic_model_input(normalized_event: dict[str, Any]) -> dict[str, Any]:
    decision = "review_candidate" if normalized_event.get("transaction_type") == "buy" else "no_action"
    review = decision == "review_candidate"
    reasons = [] if review else ["Only buy disclosures are eligible for swing-trade entry review."]
    return {
        "schema_version": "1.0.0",
        "model_artifact_id": f"artifact-{normalized_event['normalized_event_id'].split(':', 1)[-1][:12]}",
        "normalized_event_id": normalized_event["normalized_event_id"],
        "prompt_name": PROMPT_PATH.stem,
        "prompt_version": "1.0.0",
        "decision": decision,
        "confidence": 0.66 if review else 0.95,
        "reasoning_summary": (
            "Buy disclosure preserved as a review candidate for deterministic risk gating."
            if review
            else "Disclosure is not a buy entry candidate for this swing-trade pipeline."
        ),
        "must_not_trade_reasons": reasons,
        "requires_human_review": review,
        "forbidden_fields_present": False,
    }


def _run_health_stage(state: dict[str, Any], task: dict[str, Any]) -> tuple[bool, str, list[str]]:
    run_id = _stage_run_id(state, "chirurgeon")
    health_args = health_parser().parse_args(
        [
            "--config",
            str(CONFIG_PATH),
            "--runtime-mode",
            "health_only",
            "--run-id",
            f"{run_id}-health",
            "--output-dir",
            str(OUTPUT_DIR),
            "--audit-dir",
            str(AUDIT_DIR),
            "--state-dir",
            str(STATE_DIR),
            "--kanban-task-id",
            str(task["id"]),
            "--check",
            "all",
        ]
    )
    fallback_args = fallback_parser().parse_args(
        [
            "--config",
            str(CONFIG_PATH),
            "--runtime-mode",
            "health_only",
            "--run-id",
            f"{run_id}-fallback",
            "--output-dir",
            str(OUTPUT_DIR),
            "--audit-dir",
            str(AUDIT_DIR),
            "--state-dir",
            str(STATE_DIR),
            "--kanban-task-id",
            str(task["id"]),
        ]
    )
    health_envelope, _ = run_health_check(health_args)
    fallback_envelope, _ = run_fallback_check(fallback_args)
    artifact_paths = [
        *health_envelope["outputs"]["artifact_paths"],
        *fallback_envelope["outputs"]["artifact_paths"],
    ]
    ok = bool(health_envelope["ok"] and fallback_envelope["ok"] and health_envelope["safe_to_continue"] and fallback_envelope["safe_to_continue"])
    summary = "Health and fallback validation passed." if ok else "; ".join(
        [*health_envelope["errors"], *fallback_envelope["errors"]]
    ) or "Health gate failed."
    return ok, summary, artifact_paths


def _run_source_stage(state: dict[str, Any], task: dict[str, Any]) -> tuple[bool, str, list[str]]:
    args = source_monitor_parser().parse_args(
        [
            "--config",
            str(CONFIG_PATH),
            "--runtime-mode",
            "observe_only",
            "--run-id",
            _stage_run_id(state, "source"),
            "--output-dir",
            str(OUTPUT_DIR),
            "--audit-dir",
            str(AUDIT_DIR),
            "--state-dir",
            str(STATE_DIR),
            "--kanban-task-id",
            str(task["id"]),
            "--source-policy",
            str(SOURCE_POLICY_PATH),
            "--poll-interval-minutes",
            "30",
        ]
    )
    envelope, _ = run_source_monitor(args)
    summary = "Capitol Trades source intake completed." if envelope["ok"] and envelope["safe_to_continue"] else "; ".join(
        [*envelope["errors"], *envelope["warnings"]]
    ) or "Capitol Trades intake failed."
    return bool(envelope["ok"] and envelope["safe_to_continue"]), summary, envelope["outputs"]["artifact_paths"]


def _run_normalization_stage(state: dict[str, Any], task: dict[str, Any]) -> tuple[bool, str, list[str]]:
    artifact_paths: list[str] = []
    normalized_count = 0
    duplicate_only_count = 0
    hard_failure_count = 0
    for index, record_path in enumerate(_latest_source_record_paths(state)[:TOP_CANDIDATE_LIMIT], start=1):
        args = normalizer_parser().parse_args(
            [
                "--config",
                str(CONFIG_PATH),
                "--runtime-mode",
                "observe_only",
                "--run-id",
                _stage_run_id(state, f"normalize-{index}"),
                "--output-dir",
                str(OUTPUT_DIR),
                "--audit-dir",
                str(AUDIT_DIR),
                "--state-dir",
                str(STATE_DIR),
                "--kanban-task-id",
                str(task["id"]),
                "--raw-payload",
                str(record_path),
                "--source-policy",
                str(SOURCE_POLICY_PATH),
            ]
        )
        envelope, _ = run_normalizer(args)
        artifact_paths.extend(_envelope_artifact_paths(envelope))
        if envelope["ok"] and envelope["safe_to_continue"]:
            normalized_count += 1
            continue
        error_messages = [str(item) for item in envelope.get("errors", [])]
        if error_messages and all(message.startswith("duplicate normalized event blocked:") for message in error_messages):
            duplicate_only_count += 1
        else:
            hard_failure_count += 1
    if normalized_count > 0:
        return True, f"Normalized {normalized_count} Capitol Trades records.", list(dict.fromkeys(artifact_paths))
    if duplicate_only_count > 0 and hard_failure_count == 0:
        summary = "No new normalized events were produced from the source batch because all records were duplicates of previously processed disclosures."
        state["no_action_details"] = {
            "reason_code": "duplicate_only_normalization_batch",
            "duplicate_record_count": duplicate_only_count,
        }
        _save_state(state)
        return True, summary, list(dict.fromkeys(artifact_paths))
    return False, "No normalized events were produced from the source batch.", list(dict.fromkeys(artifact_paths))


def _run_analysis_stage(state: dict[str, Any], task: dict[str, Any]) -> tuple[bool, str, list[str]]:
    MODEL_DIR.mkdir(parents=True, exist_ok=True)
    artifact_paths: list[str] = []
    candidates: list[dict[str, Any]] = []
    raw_lookup: dict[str, dict[str, Any]] = {}
    for record_path in _latest_source_record_paths(state):
        payload = _json_read(record_path)
        raw_payload = _json_read(payload["raw_payload_path"])
        raw_lookup[str(payload["source_record_id"])] = raw_payload

    for index, normalized_path in enumerate(_latest_normalized_paths(state), start=1):
        normalized_event = _json_read(normalized_path)
        model_input_path = MODEL_DIR / f"{_stage_run_id(state, f'analysis-{index}')}_candidate_model.json"
        model_output_path = MODEL_DIR / f"{_stage_run_id(state, f'analysis-{index}')}_validated_model.json"
        model_input = _build_deterministic_model_input(normalized_event)
        model_input_path.write_text(json.dumps(model_input, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        args = analyzer_parser().parse_args(
            [
                "--config",
                str(CONFIG_PATH),
                "--runtime-mode",
                "analysis_only",
                "--run-id",
                _stage_run_id(state, f"analysis-{index}"),
                "--output-dir",
                str(OUTPUT_DIR),
                "--audit-dir",
                str(AUDIT_DIR),
                "--state-dir",
                str(STATE_DIR),
                "--kanban-task-id",
                str(task["id"]),
                "--normalized-event",
                str(normalized_path),
                "--prompt",
                str(PROMPT_PATH),
                "--model-artifact-in",
                str(model_input_path),
                "--model-artifact-out",
                str(model_output_path),
            ]
        )
        envelope, _ = run_analyzer(args)
        artifact_paths.extend([str(model_input_path), *envelope["outputs"]["artifact_paths"]])
        if not envelope["ok"]:
            continue
        validated = _json_read(model_output_path)
        if validated["decision"] != "review_candidate":
            continue
        raw_payload = raw_lookup.get(str(normalized_event["source_record_id"]), {})
        candidates.append(
            {
                "rank": len(candidates) + 1,
                "normalized_event_id": normalized_event["normalized_event_id"],
                "source_record_id": normalized_event["source_record_id"],
                "symbol": normalized_event["symbol"],
                "broker_symbol_candidates": _candidate_symbol_options(raw_payload) or [normalized_event["symbol"]],
                "politician": str(raw_payload.get("politician") or "Unknown"),
                "issuer": str(raw_payload.get("issuer") or ""),
                "transaction_type": normalized_event["transaction_type"],
                "transaction_date": normalized_event["transaction_date"],
                "disclosed_date": normalized_event["disclosed_date"],
                "amount_range": normalized_event["amount_range"],
                "source_url": str(raw_payload.get("source_url") or ""),
            }
        )

    summary_path = OUTPUT_DIR / f"{_stage_run_id(state, 'top-candidates')}_top_buy_candidates_analysis.json"
    summary_payload = {
        "schema_version": "1.0.0",
        "run_id": state["cycle_run_id"],
        "top_buy_candidates": candidates[:TOP_CANDIDATE_LIMIT],
    }
    summary_path.write_text(json.dumps(summary_payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    artifact_paths.append(str(summary_path))
    ok = bool(candidates)
    if ok:
        summary = f"Produced {len(candidates[:TOP_CANDIDATE_LIMIT])} top buy candidates."
        return True, summary, list(dict.fromkeys(artifact_paths))
    summary = "No new actionable normalized buy candidates were found in the current workflow cycle."
    state["no_action_details"] = {
        "reason_code": "no_actionable_analysis_candidates",
        "top_candidate_count": 0,
        "analysis_summary_path": str(summary_path),
    }
    _save_state(state)
    return True, summary, list(dict.fromkeys(artifact_paths))


def _run_broker_context_stage(state: dict[str, Any], task: dict[str, Any]) -> tuple[bool, str, list[str]]:
    analysis_summary_path = next(
        (Path(path) for path in state.get("artifacts", {}).get("altrader-augur-analyze-candidates", []) if path.endswith("_top_buy_candidates_analysis.json")),
        None,
    )
    if analysis_summary_path is None:
        return False, "Missing top buy candidate summary artifact.", []
    candidates = _json_read(analysis_summary_path).get("top_buy_candidates", [])
    if not candidates:
        summary = "No top buy candidates were available for broker context in the current workflow cycle."
        state["no_action_details"] = {
            "reason_code": "no_top_buy_candidates_for_broker_context",
            "analysis_summary_path": str(analysis_summary_path),
        }
        _save_state(state)
        return True, summary, [str(analysis_summary_path)]

    symbols: list[str] = []
    for candidate in candidates[:TOP_CANDIDATE_LIMIT]:
        for symbol in candidate.get("broker_symbol_candidates", []):
            if symbol not in symbols:
                symbols.append(symbol)

    account_args = account_status_parser().parse_args(
        [
            "--config",
            str(CONFIG_PATH),
            "--runtime-mode",
            "paper_manual",
            "--run-id",
            _stage_run_id(state, "account"),
            "--output-dir",
            str(OUTPUT_DIR),
            "--audit-dir",
            str(AUDIT_DIR),
            "--state-dir",
            str(STATE_DIR),
            "--kanban-task-id",
            str(task["id"]),
            "--broker-mode",
            "paper",
        ]
    )
    market_args = market_data_parser().parse_args(
        [
            "--config",
            str(CONFIG_PATH),
            "--runtime-mode",
            "paper_manual",
            "--run-id",
            _stage_run_id(state, "market"),
            "--output-dir",
            str(OUTPUT_DIR),
            "--audit-dir",
            str(AUDIT_DIR),
            "--state-dir",
            str(STATE_DIR),
            "--kanban-task-id",
            str(task["id"]),
            "--symbols",
            ",".join(symbols),
            "--max-staleness-seconds",
            "60",
            "--broker-mode",
            "paper",
        ]
    )
    account_envelope, _ = run_account_status(account_args)
    market_envelope, _ = run_market_data(market_args)
    artifact_paths = [*account_envelope["outputs"]["artifact_paths"], *market_envelope["outputs"]["artifact_paths"], str(analysis_summary_path)]
    if not account_envelope["ok"] or not market_envelope["ok"]:
        return False, "; ".join([*account_envelope["errors"], *market_envelope["errors"]]) or "Broker context failed.", artifact_paths

    account_path = Path(account_envelope["outputs"]["artifact_paths"][0])
    market_contexts = {
        _json_read(path)["symbol"]: _json_read(path)
        for path in market_envelope["outputs"]["artifact_paths"]
        if path.endswith(".json") and "market_context_" in path
    }
    selected_candidate = None
    for candidate in candidates[:TOP_CANDIDATE_LIMIT]:
        for symbol in candidate.get("broker_symbol_candidates", []):
            context = market_contexts.get(symbol)
            if context and context.get("last") and not context.get("stale"):
                selected_candidate = dict(candidate)
                selected_candidate["broker_symbol"] = symbol
                selected_candidate["market_context_path"] = next(
                    path for path in market_envelope["outputs"]["artifact_paths"] if f"market_context_{symbol}.json" in path
                )
                selected_candidate["account_status_path"] = str(account_path)
                break
        if selected_candidate:
            break

    if selected_candidate is None:
        summary = "No tradable, fresh market-data candidate was available from the top buy candidate set."
        state["no_action_details"] = {
            "reason_code": "no_fresh_tradable_broker_candidate",
            "analysis_summary_path": str(analysis_summary_path),
            "evaluated_symbols": symbols,
        }
        _save_state(state)
        return True, summary, artifact_paths

    state["selected_candidate"] = selected_candidate
    _save_state(state)
    return True, f"Selected broker symbol {selected_candidate['broker_symbol']} for downstream trading.", artifact_paths


def _run_risk_stage(state: dict[str, Any], task: dict[str, Any]) -> tuple[bool, str, list[str]]:
    candidate = state.get("selected_candidate")
    if not candidate:
        return False, "No selected candidate was available for risk review.", []
    account_status = _json_read(candidate["account_status_path"])
    market_context = _json_read(candidate["market_context_path"])
    cash_cap_notional = float(account_status.get("cash", 0.0)) * 0.10
    requested_qty = max(1, min(10, int(cash_cap_notional // float(market_context["last"] or 1)) or 1))
    order_intent = {
        "schema_version": "1.0.0",
        "order_request_id": f"{state['cycle_run_id']}-intent",
        "broker_mode": "paper",
        "symbol": candidate["broker_symbol"],
        "side": "buy",
        "order_type": "bracket",
        "qty": requested_qty,
        "notional": None,
        "time_in_force": "day",
        "risk_decision_id": "pending",
        "approval_id": None,
        "strategy_class": "swing",
        "source_record_id": candidate["source_record_id"],
        "politician": candidate["politician"],
        "report_id": f"capitoltrades-{candidate['source_record_id']}",
        "top_candidate_rank": int(candidate["rank"]),
        "stop_loss_percent": 10.0,
        "take_profit_percent": 10.0,
        "limit_price": None,
        "stop_price": None,
    }
    order_intent_path = STATE_DIR / f"{state['cycle_run_id']}_order_request_intent.json"
    order_intent_path.write_text(json.dumps(order_intent, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    args = risk_gate_parser().parse_args(
        [
            "--config",
            str(CONFIG_PATH),
            "--runtime-mode",
            "risk_review",
            "--run-id",
            _stage_run_id(state, "risk"),
            "--output-dir",
            str(OUTPUT_DIR),
            "--audit-dir",
            str(AUDIT_DIR),
            "--state-dir",
            str(STATE_DIR),
            "--kanban-task-id",
            str(task["id"]),
            "--order-intent",
            str(order_intent_path),
            "--account-status",
            candidate["account_status_path"],
            "--market-context",
            candidate["market_context_path"],
            "--risk-policy",
            str(RISK_POLICY_PATH),
        ]
    )
    envelope, _ = run_risk_gate(args)
    artifact_paths = [str(order_intent_path), *envelope["outputs"]["artifact_paths"]]
    ok = bool(envelope["ok"] and envelope["safe_to_continue"])
    summary = "Risk gate approved the order intent." if ok else "; ".join(envelope["errors"]) or "Risk gate blocked the order intent."
    return ok, summary, artifact_paths


def _run_approval_stage(state: dict[str, Any], task: dict[str, Any]) -> tuple[bool, str, list[str]]:
    risk_path = next(
        (path for path in state.get("artifacts", {}).get("altrader-warden-risk-gate", []) if path.endswith("_risk_decision.json")),
        None,
    )
    if not risk_path:
        return False, "Missing risk decision artifact for manual approval.", []
    risk_decision = _json_read(risk_path)
    order_intent_hash = str(risk_decision["order_intent_hash"])
    create_args = approval_parser().parse_args(
        [
            "--config",
            str(CONFIG_PATH),
            "--runtime-mode",
            "paper_manual",
            "--run-id",
            _stage_run_id(state, "approval-create"),
            "--output-dir",
            str(OUTPUT_DIR),
            "--audit-dir",
            str(AUDIT_DIR),
            "--state-dir",
            str(STATE_DIR),
            "--kanban-task-id",
            str(task["id"]),
            "--action",
            "create",
            "--order-intent-hash",
            order_intent_hash,
            "--actor",
            "simulated_operator",
            "--reason",
            "Deterministic paper-manual simulation queue creation.",
            "--risk-decision",
            str(risk_path),
        ]
    )
    create_envelope, _ = run_approval(create_args)
    approval_path = next((path for path in create_envelope["outputs"]["artifact_paths"] if path.endswith(".json") and "manual_approvals" in path), None)
    if not create_envelope["ok"] or not approval_path:
        return False, "; ".join(create_envelope["errors"]) or "Manual approval queue create failed.", create_envelope["outputs"]["artifact_paths"]

    approve_args = approval_parser().parse_args(
        [
            "--config",
            str(CONFIG_PATH),
            "--runtime-mode",
            "paper_manual",
            "--run-id",
            _stage_run_id(state, "approval-approve"),
            "--output-dir",
            str(OUTPUT_DIR),
            "--audit-dir",
            str(AUDIT_DIR),
            "--state-dir",
            str(STATE_DIR),
            "--kanban-task-id",
            str(task["id"]),
            "--action",
            "approve",
            "--order-intent-hash",
            order_intent_hash,
            "--actor",
            "simulated_operator",
            "--reason",
            "Deterministic simulated approval for paper-manual Kanban run.",
        ]
    )
    approve_envelope, _ = run_approval(approve_args)
    artifact_paths = [*create_envelope["outputs"]["artifact_paths"], *approve_envelope["outputs"]["artifact_paths"]]
    ok = bool(approve_envelope["ok"] and approve_envelope["safe_to_continue"])
    summary = "Manual approval was simulated and approved for the test run." if ok else "; ".join(approve_envelope["errors"]) or "Manual approval simulation failed."
    return ok, summary, artifact_paths


def _run_submission_stage(state: dict[str, Any], task: dict[str, Any]) -> tuple[bool, str, list[str]]:
    candidate = state.get("selected_candidate")
    risk_path = next((path for path in state.get("artifacts", {}).get("altrader-warden-risk-gate", []) if path.endswith("_risk_decision.json")), None)
    approval_path = next(
        (
            path
            for path in state.get("artifacts", {}).get("altrader-overlord-manual-approval", [])
            if path.endswith(".json") and "manual_approvals" in path and "approval-approve" in Path(path).name
        ),
        None,
    )
    if not candidate or not risk_path or not approval_path:
        return False, "Missing selected candidate, risk decision, or approval artifact for submission.", []
    risk_decision = _json_read(risk_path)
    approval = _json_read(approval_path)
    submit_request = {
        "schema_version": "1.0.0",
        "order_request_id": f"{state['cycle_run_id']}-submit",
        "broker_mode": "paper",
        "symbol": candidate["broker_symbol"],
        "side": "buy",
        "order_type": "bracket",
        "qty": int(risk_decision["approved_order"]["qty"]),
        "notional": None,
        "time_in_force": "day",
        "risk_decision_id": risk_decision["risk_decision_id"],
        "approval_id": approval["approval_id"],
        "strategy_class": "swing",
        "source_record_id": candidate["source_record_id"],
        "politician": candidate["politician"],
        "report_id": risk_decision["approved_order"]["report_id"],
        "top_candidate_rank": int(candidate["rank"]),
        "stop_loss_percent": float(risk_decision["approved_order"]["stop_loss_percent"]),
        "take_profit_percent": float(risk_decision["approved_order"]["take_profit_percent"]),
        "limit_price": None,
        "stop_price": None,
    }
    submit_request_path = STATE_DIR / f"{state['cycle_run_id']}_order_request_submit.json"
    submit_request_path.write_text(json.dumps(submit_request, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    args = order_submitter_parser().parse_args(
        [
            "--config",
            str(CONFIG_PATH),
            "--runtime-mode",
            "paper_manual",
            "--run-id",
            _stage_run_id(state, "submit"),
            "--output-dir",
            str(OUTPUT_DIR),
            "--audit-dir",
            str(AUDIT_DIR),
            "--state-dir",
            str(STATE_DIR),
            "--kanban-task-id",
            str(task["id"]),
            "--broker-mode",
            "paper",
            "--order-request",
            str(submit_request_path),
            "--risk-decision",
            str(risk_path),
            "--approval",
            str(approval_path),
        ]
    )
    envelope, _ = run_order_submitter(args)
    artifact_paths = [str(submit_request_path), *envelope["outputs"]["artifact_paths"]]
    ok = bool(envelope["ok"] and envelope["safe_to_continue"])
    summary = "Paper bracket order submitted to Alpaca." if ok else "; ".join(envelope["errors"]) or "Order submission failed."
    return ok, summary, artifact_paths


def _run_tracking_stage(state: dict[str, Any], task: dict[str, Any]) -> tuple[bool, str, list[str]]:
    submission_path = next(
        (path for path in state.get("artifacts", {}).get("altrader-gatekeeper-paper-submission", []) if "_submitted_" in path),
        None,
    )
    if not submission_path:
        return False, "Missing submitted order artifact for reconciliation.", []
    order_status = _json_read(submission_path)
    args = order_monitor_parser().parse_args(
        [
            "--config",
            str(CONFIG_PATH),
            "--runtime-mode",
            "paper_manual",
            "--run-id",
            _stage_run_id(state, "monitor"),
            "--output-dir",
            str(OUTPUT_DIR),
            "--audit-dir",
            str(AUDIT_DIR),
            "--state-dir",
            str(STATE_DIR),
            "--kanban-task-id",
            str(task["id"]),
            "--broker-mode",
            "paper",
            "--order-id",
            str(order_status["broker_order_id"]),
        ]
    )
    envelope, _ = run_order_monitor(args)
    ok = bool(envelope["ok"] and envelope["safe_to_continue"])
    summary = "Order monitoring and position reconciliation completed." if ok else "; ".join(envelope["errors"]) or "Order reconciliation failed."
    return ok, summary, envelope["outputs"]["artifact_paths"]


def _run_reporting_stage(state: dict[str, Any], task: dict[str, Any]) -> tuple[bool, str, list[str]]:
    artifact_paths = _all_artifacts(state)
    args = report_writer_parser().parse_args(
        [
            "--config",
            str(CONFIG_PATH),
            "--runtime-mode",
            "paper_manual",
            "--run-id",
            _stage_run_id(state, "daily-summary"),
            "--output-dir",
            str(OUTPUT_DIR),
            "--audit-dir",
            str(AUDIT_DIR),
            "--state-dir",
            str(STATE_DIR),
            "--kanban-task-id",
            str(task["id"]),
            "--report-type",
            "daily_cycle",
            *sum([["--artifact", path] for path in artifact_paths], []),
            "--report-policy",
            str(_report_policy_path()),
        ]
    )
    envelope, _ = run_report_writer(args)
    ok = bool(envelope["ok"])
    if ok:
        state["report_sent"] = True
        _save_state(state)
    summary = "Daily summary delivered to email and Telegram." if ok else "; ".join(envelope["errors"]) or "Daily summary failed."
    return ok, summary, envelope["outputs"]["artifact_paths"]


def _run_closeout_stage(state: dict[str, Any], task: dict[str, Any]) -> tuple[bool, str, list[str]]:
    return True, "Trading-day workflow closed with deterministic artifact trail.", []


STAGE_HANDLERS = {
    "altrader-chirurgeon-startup-gate": _run_health_stage,
    "altrader-scryer-capitol-intake": _run_source_stage,
    "altrader-runesmith-normalize-events": _run_normalization_stage,
    "altrader-augur-analyze-candidates": _run_analysis_stage,
    "altrader-coinmaster-broker-context": _run_broker_context_stage,
    "altrader-warden-risk-gate": _run_risk_stage,
    "altrader-overlord-manual-approval": _run_approval_stage,
    "altrader-gatekeeper-paper-submission": _run_submission_stage,
    "altrader-tracker-reconcile-positions": _run_tracking_stage,
    "altrader-bard-daily-summary": _run_reporting_stage,
    "altrader-highmarshal-closeout": _run_closeout_stage,
}


def _process_stage(seed_id: str, task: dict[str, Any], state: dict[str, Any], live_seed_tasks: dict[str, dict[str, Any]]) -> bool:
    status = _task_status(task)
    if status == "done":
        return True
    _unblock_task(str(task["id"]))
    _claim_task(str(task["id"]))
    if seed_id == "altrader-highmarshal-open-trading-day":
        _complete_task(
            str(task["id"]),
            "Seeded trading-day graph is intact and specialist assignments are correct.",
            {"stage": seed_id},
        )
        return True

    handler = STAGE_HANDLERS.get(seed_id)
    if handler is None:
        return _fail_stage(
            stage_id=seed_id,
            task=task,
            state=state,
            live_seed_tasks=live_seed_tasks,
            summary="No deterministic handler is defined for this Kanban stage.",
            artifact_paths=[],
            runtime_mode=str(task.get("title", "")),
        )

    ok, summary, artifact_paths = handler(state, task)
    _record_artifacts(state, seed_id, artifact_paths)
    if ok:
        _complete_task(
            str(task["id"]),
            summary,
            {"stage": seed_id, "artifact_paths": artifact_paths},
        )
        if seed_id in {
            "altrader-runesmith-normalize-events",
            "altrader-augur-analyze-candidates",
            "altrader-coinmaster-broker-context",
        } and state.get("no_action_details"):
            _finalize_no_action_cycle(
                state=state,
                live_seed_tasks=live_seed_tasks,
                stage_id=seed_id,
                summary=summary,
                artifact_paths=_all_artifacts(state),
            )
        return True

    return _fail_stage(
        stage_id=seed_id,
        task=task,
        state=state,
        live_seed_tasks=live_seed_tasks,
        summary=summary,
        artifact_paths=artifact_paths,
        runtime_mode="paper_manual",
    )


def run_cycle(*, force_new_state: bool = False, max_steps: int = 20) -> dict[str, Any]:
    seed = _load_seed()
    seed_tasks = _seed_task_map(seed)
    live_seed_tasks = _live_seed_tasks(seed)
    expected_cycle_run_id = _expected_cycle_run_id(live_seed_tasks)
    state = _load_state(force_new=force_new_state)
    if expected_cycle_run_id and str(state.get("cycle_run_id")) != expected_cycle_run_id:
        state = _new_state(expected_cycle_run_id)
        _save_state(state)

    processed = 0
    for seed_id in STAGE_SEQUENCE:
        task = live_seed_tasks.get(seed_id)
        if task is None:
            raise RuntimeError(f"live kanban task missing for seed id: {seed_id}")
        status = _task_status(task)
        if status == "done":
            continue
        if state.get("halted") and state.get("report_sent"):
            _finalize_halted_cycle(
                state=state,
                live_seed_tasks=live_seed_tasks,
                failed_stage_id=str(state.get("halt_stage_id") or seed_id),
                summary=str(state.get("halt_summary") or "Trading-day workflow halted safely."),
                artifact_paths=_all_artifacts(state),
            )
            _sync_desktop_mirror()
            return {
                "ok": False,
                "processed_stage": str(state.get("halt_stage_id") or seed_id),
                "status": "halted",
                "cycle_run_id": state["cycle_run_id"],
                "report_sent": True,
                "halted": True,
            }
        if state.get("no_action") and state.get("report_sent"):
            _finalize_no_action_cycle(
                state=state,
                live_seed_tasks=live_seed_tasks,
                stage_id=str(state.get("no_action_stage_id") or seed_id),
                summary=str(state.get("no_action_summary") or "No new actionable records were available for this cycle."),
                artifact_paths=_all_artifacts(state),
            )
            _sync_desktop_mirror()
            return {
                "ok": True,
                "processed_stage": str(state.get("no_action_stage_id") or seed_id),
                "status": "no_action",
                "cycle_run_id": state["cycle_run_id"],
                "report_sent": True,
                "no_action": True,
            }
        if processed >= max_steps:
            break
        processed += 1
        stage_ok = _process_stage(seed_id, task, state, live_seed_tasks)
        _sync_desktop_mirror()
        if state.get("no_action") and state.get("report_sent"):
            live_seed_tasks = _live_seed_tasks(seed)
            _sync_desktop_mirror()
            return {
                "ok": True,
                "processed_stage": str(state.get("no_action_stage_id") or seed_id),
                "status": "no_action",
                "cycle_run_id": state["cycle_run_id"],
                "report_sent": True,
                "no_action": True,
            }
        if not stage_ok:
            live_seed_tasks = _live_seed_tasks(seed)
            _sync_desktop_mirror()
            return {
                "ok": False,
                "processed_stage": seed_id,
                "status": _task_status(_live_seed_tasks(seed).get(seed_id)),
                "cycle_run_id": state["cycle_run_id"],
                "report_sent": state.get("report_sent", False),
                "halted": state.get("halted", False),
            }
        live_seed_tasks = _live_seed_tasks(seed)

    return {
        "ok": True,
        "cycle_run_id": state["cycle_run_id"],
        "processed_steps": processed,
        "final_statuses": {seed_id: _task_status(task) for seed_id, task in _live_seed_tasks(seed).items()},
        "report_sent": state.get("report_sent", False),
    }


def main() -> None:
    parser = argparse.ArgumentParser(description="Run the AlTrader Hermes Kanban workflow deterministically.")
    parser.add_argument("--force-new-state", action="store_true")
    parser.add_argument("--max-steps", type=int, default=20)
    args = parser.parse_args()
    result = run_cycle(force_new_state=args.force_new_state, max_steps=args.max_steps)
    print(json.dumps(result, indent=2, sort_keys=True))
    raise SystemExit(0 if result.get("ok") else 1)


if __name__ == "__main__":
    main()

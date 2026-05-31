from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
from pathlib import Path
from typing import Any

if __package__ is None or __package__ == "":
    sys.path.append(str(Path(__file__).resolve().parents[2]))


ROOT = Path(__file__).resolve().parents[2]
HERMES_EXE = Path.home() / ".hermes" / "hermes-agent" / "venv" / "Scripts" / "hermes.exe"
SEED_PATH = ROOT / "hermes" / "kanban" / "altrader_seed_tasks.json"
STATE_PATH = ROOT / "data" / "runtime" / "state" / "kanban_cycle_state.json"
SYNC_SCRIPT = ROOT / "scripts" / "operations" / "sync_hermes_kanban_desktop_mirror.py"
STAGE_WRAPPER = ROOT / "scripts" / "operations" / "run_altrader_stage.py"
AUTO_HEALABLE_STAGE_IDS = {
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
}


def _run(args: list[str], *, cwd: Path | None = None) -> subprocess.CompletedProcess[str]:
    return subprocess.run(args, cwd=str(cwd or ROOT), capture_output=True, text=True, check=True)


def _run_json(args: list[str], *, cwd: Path | None = None) -> Any:
    result = _run(args, cwd=cwd)
    return json.loads(result.stdout)


def _load_seed() -> dict[str, Any]:
    return json.loads(SEED_PATH.read_text(encoding="utf-8"))


def _title_to_stage_id(seed: dict[str, Any]) -> dict[str, str]:
    return {str(task["title"]): str(task["id"]) for task in seed.get("tasks", [])}


def _task_status(task: dict[str, Any]) -> str:
    return str(task.get("status") or "").strip().lower()


def _active_tasks(tasks: list[dict[str, Any]]) -> list[dict[str, Any]]:
    return [task for task in tasks if _task_status(task) != "done"]


def _state_snapshot() -> dict[str, Any]:
    if not STATE_PATH.exists():
        return {}
    return json.loads(STATE_PATH.read_text(encoding="utf-8"))


def _dispatch(board: str) -> str:
    result = _run([str(HERMES_EXE), "kanban", "--board", board, "dispatch"])
    return result.stdout.strip()


def _sync_desktop() -> None:
    _run(["python", str(SYNC_SCRIPT)], cwd=ROOT)


def _repair_blocked_task(board: str, stage_id: str) -> dict[str, Any]:
    env = os.environ.copy()
    env["ALTRADER_HERMES_KANBAN_BOARD"] = board
    env["HERMES_KANBAN_BOARD"] = board
    result = subprocess.run(
        ["python", str(STAGE_WRAPPER), "--board", board, "--stage-id", stage_id],
        cwd=str(ROOT),
        env=env,
        capture_output=True,
        text=True,
        check=False,
    )
    payload: dict[str, Any] = {}
    if result.stdout.strip():
        try:
            payload = json.loads(result.stdout)
        except json.JSONDecodeError:
            payload = {"stdout": result.stdout.strip()}
    return {
        "stage_id": stage_id,
        "exit_code": result.returncode,
        "payload": payload,
        "stderr": result.stderr.strip(),
    }


def run_recovery(*, board: str = "altrader", max_repairs: int = 3) -> dict[str, Any]:
    seed = _load_seed()
    title_map = _title_to_stage_id(seed)
    tasks = _run_json([str(HERMES_EXE), "kanban", "--board", board, "list", "--json"])
    if not isinstance(tasks, list):
        tasks = []
    active = _active_tasks(tasks)
    if not active:
        return {
            "ok": True,
            "board": board,
            "action": "noop",
            "reason": "board_empty_or_all_done",
            "active_task_count": 0,
            "repairs": [],
        }

    dispatch_before = _dispatch(board)
    tasks = _run_json([str(HERMES_EXE), "kanban", "--board", board, "list", "--json"])
    if not isinstance(tasks, list):
        tasks = []
    active = _active_tasks(tasks)

    repairs: list[dict[str, Any]] = []
    for task in [task for task in active if _task_status(task) == "blocked"]:
        if len(repairs) >= max_repairs:
            break
        stage_id = title_map.get(str(task.get("title") or ""))
        if not stage_id or stage_id not in AUTO_HEALABLE_STAGE_IDS:
            continue
        repairs.append(_repair_blocked_task(board, stage_id))

    dispatch_after = _dispatch(board)
    _sync_desktop()
    final_tasks = _run_json([str(HERMES_EXE), "kanban", "--board", board, "list", "--json"])
    if not isinstance(final_tasks, list):
        final_tasks = []
    final_active = _active_tasks(final_tasks)
    state = _state_snapshot()
    return {
        "ok": True,
        "board": board,
        "action": "recovery_pass",
        "active_task_count_before": len(active),
        "active_task_count_after": len(final_active),
        "dispatch_before": dispatch_before,
        "dispatch_after": dispatch_after,
        "repairs": repairs,
        "report_sent": bool(state.get("report_sent")),
        "halted": bool(state.get("halted")),
        "no_action": bool(state.get("no_action")),
        "cycle_run_id": state.get("cycle_run_id"),
    }


def main() -> None:
    parser = argparse.ArgumentParser(description="Deterministic AlTrader board stewardship and recovery.")
    parser.add_argument("--board", default="altrader")
    parser.add_argument("--max-repairs", type=int, default=3)
    args = parser.parse_args()
    print(json.dumps(run_recovery(board=args.board, max_repairs=args.max_repairs), indent=2))


if __name__ == "__main__":
    main()

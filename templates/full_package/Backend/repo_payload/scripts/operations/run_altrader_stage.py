from __future__ import annotations

import argparse
import json
import os
import sys
from pathlib import Path
from typing import Any

if __package__ is None or __package__ == "":
    sys.path.append(str(Path(__file__).resolve().parents[2]))

from scripts.operations import run_altrader_kanban_cycle as cycle


def _current_state(force_new_state: bool = False) -> tuple[dict[str, Any], dict[str, Any], dict[str, Any]]:
    seed = cycle._load_seed()
    live_seed_tasks = cycle._live_seed_tasks(seed)
    expected_cycle_run_id = cycle._expected_cycle_run_id(live_seed_tasks)
    state = cycle._load_state(force_new=force_new_state)
    if expected_cycle_run_id and str(state.get("cycle_run_id")) != expected_cycle_run_id:
        state = cycle._new_state(expected_cycle_run_id)
        cycle._save_state(state)
    return seed, live_seed_tasks, state


def main() -> None:
    parser = argparse.ArgumentParser(description="Run one deterministic AlTrader Kanban stage against the current live board.")
    parser.add_argument("--stage-id", required=True, choices=cycle.STAGE_SEQUENCE)
    parser.add_argument("--board", default=os.environ.get("ALTRADER_HERMES_KANBAN_BOARD", "altrader"))
    parser.add_argument("--force-new-state", action="store_true")
    args = parser.parse_args()

    os.environ["ALTRADER_HERMES_KANBAN_BOARD"] = str(args.board)
    os.environ["HERMES_KANBAN_BOARD"] = str(args.board)

    _, live_seed_tasks, state = _current_state(force_new_state=args.force_new_state)
    task = live_seed_tasks.get(args.stage_id)
    if task is None:
        raise RuntimeError(f"Live kanban task missing for stage id: {args.stage_id}")

    ok = cycle._process_stage(args.stage_id, task, state, live_seed_tasks)
    cycle._sync_desktop_mirror()

    refreshed_seed, refreshed_live_seed_tasks, refreshed_state = _current_state(force_new_state=False)
    refreshed_task = refreshed_live_seed_tasks.get(args.stage_id) or task
    payload = {
        "ok": ok,
        "board": str(args.board),
        "stage_id": args.stage_id,
        "task_id": str(refreshed_task.get("id") or task.get("id")),
        "task_status": cycle._task_status(refreshed_task),
        "cycle_run_id": str(refreshed_state.get("cycle_run_id") or state.get("cycle_run_id")),
        "report_sent": bool(refreshed_state.get("report_sent")),
        "no_action": bool(refreshed_state.get("no_action")),
        "halted": bool(refreshed_state.get("halted")),
        "artifacts": refreshed_state.get("artifacts", {}),
    }
    print(json.dumps(payload, indent=2))
    raise SystemExit(0 if ok else 1)


if __name__ == "__main__":
    main()

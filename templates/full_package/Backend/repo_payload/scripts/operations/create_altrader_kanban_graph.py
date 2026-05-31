from __future__ import annotations

import argparse
import json
import subprocess
from datetime import datetime, timezone
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
HERMES_EXE = Path.home() / ".hermes" / "hermes-agent" / "venv" / "Scripts" / "hermes.exe"
SPEC_PATH = ROOT / "hermes" / "reconstruction_pack" / "TASK_GRAPH_SPEC.json"
SEED_PATH = ROOT / "hermes" / "kanban" / "altrader_seed_tasks.json"
REPORT_DIR = ROOT / "data" / "runtime" / "reports" / "orchestration"
STATE_DIR = ROOT / "data" / "runtime" / "state"
KANBAN_STATE_PATH = STATE_DIR / "kanban_cycle_state.json"


def _load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def _run(args: list[str]) -> subprocess.CompletedProcess[str]:
    return subprocess.run(args, capture_output=True, text=True, check=True)


def _run_json(args: list[str]) -> dict | list:
    result = _run(args)
    return json.loads(result.stdout)


def _now_run_id() -> str:
    return datetime.now(timezone.utc).strftime("altrader-graph-%Y%m%dT%H%M%SZ")


def _now_cycle_run_id() -> str:
    return datetime.now(timezone.utc).strftime("kanban-cycle-%Y%m%dT%H%M%SZ")


def _task_templates(seed: dict) -> dict[str, dict]:
    return {str(task["title"]): task for task in seed.get("tasks", [])}


def _existing_tenant_tasks(board: str, tenant: str) -> list[dict]:
    tasks = _run_json([str(HERMES_EXE), "kanban", "--board", board, "list", "--json"])
    if not isinstance(tasks, list):
        return []
    return [task for task in tasks if str(task.get("tenant", "")) == tenant]


def _archive_existing(board: str, tenant: str) -> list[str]:
    tasks = _existing_tenant_tasks(board, tenant)
    task_ids = [str(task["id"]) for task in tasks]
    if task_ids:
        _run([str(HERMES_EXE), "kanban", "--board", board, "archive", *task_ids])
    return task_ids


def _reset_cycle_state(cycle_run_id: str) -> Path:
    STATE_DIR.mkdir(parents=True, exist_ok=True)
    state = {
        "schema_version": "1.0.0",
        "cycle_run_id": cycle_run_id,
        "cycle_date": datetime.now(timezone.utc).strftime("%Y-%m-%d"),
        "created_at_utc": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "artifacts": {},
        "selected_candidate": None,
        "report_sent": False,
        "halted": False,
        "no_action": False,
        "graph_run_id": cycle_run_id,
    }
    KANBAN_STATE_PATH.write_text(json.dumps(state, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    return KANBAN_STATE_PATH


def _create_task(
    *,
    board: str,
    workspace_dir: str,
    tenant: str,
    title: str,
    body: str,
    assignee: str,
    priority: int,
    created_by: str,
    run_id: str,
    cycle_run_id: str,
    key: str,
    parent_ids: list[str],
) -> dict:
    task_body = (
        f"{body}\n\n"
        f"Current workflow cycle run id: {cycle_run_id}\n"
        f"Current graph creation run id: {run_id}\n"
        f"Stage key: {key}\n"
        "Only use artifacts produced for this workflow cycle. Do not reconcile or close this task with stale artifacts from an earlier cycle."
    )
    args = [
        str(HERMES_EXE),
        "kanban",
        "--board",
        board,
        "create",
        title,
        "--body",
        task_body,
        "--assignee",
        assignee,
        "--workspace",
        f"dir:{workspace_dir}",
        "--tenant",
        tenant,
        "--priority",
        str(priority),
        "--created-by",
        created_by,
        "--idempotency-key",
        f"{run_id}:{key}",
        "--max-retries",
        "1",
        "--json",
    ]
    for parent_id in parent_ids:
        args.extend(["--parent", parent_id])
    result = _run_json(args)
    if not isinstance(result, dict):
        raise RuntimeError(f"Unexpected create response for {key}")
    return result


def _sync(seed_path: Path) -> dict:
    script = ROOT / "scripts" / "operations" / "sync_hermes_kanban_desktop_mirror.py"
    result = subprocess.run(
        ["python", str(script), "--seed", str(seed_path)],
        cwd=str(ROOT),
        capture_output=True,
        text=True,
        check=True,
    )
    return json.loads(result.stdout)


def _write_report(payload: dict) -> Path:
    REPORT_DIR.mkdir(parents=True, exist_ok=True)
    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    destination = REPORT_DIR / f"create-altrader-kanban-graph-{stamp}.json"
    destination.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    return destination


def main() -> None:
    parser = argparse.ArgumentParser(description="Create the canonical AlTrader Kanban graph on the dedicated Hermes board.")
    parser.add_argument("--board", default="altrader")
    parser.add_argument("--run-id", default=None)
    parser.add_argument("--reset", action="store_true")
    parser.add_argument("--created-by", default="highmarshal")
    parser.add_argument("--spec", default=str(SPEC_PATH))
    parser.add_argument("--seed", default=str(SEED_PATH))
    args = parser.parse_args()

    spec = _load_json(Path(args.spec))
    seed = _load_json(Path(args.seed))
    templates = _task_templates(seed)
    run_id = args.run_id or _now_run_id()
    cycle_run_id = _now_cycle_run_id()
    board = str(args.board)
    workspace_dir = str(Path(seed["workspace_dir"]).resolve())
    tenant = str(seed["tenant"])

    archived_ids: list[str] = []
    if args.reset:
        archived_ids = _archive_existing(board, tenant)

    state_path = _reset_cycle_state(cycle_run_id)

    created: dict[str, str] = {}
    created_rows: list[dict] = []
    for index, task_spec in enumerate(spec.get("tasks", []), start=1):
        title = str(task_spec["title"])
        template = templates.get(title)
        if not template:
            raise RuntimeError(f"Missing task template for title: {title}")
        key = str(task_spec["key"])
        parent_ids = [created[parent] for parent in task_spec.get("parents", [])]
        created_task = _create_task(
            board=board,
            workspace_dir=workspace_dir,
            tenant=tenant,
            title=title,
            body=str(template["description"]),
            assignee=str(task_spec["assignee"]),
            priority=index,
            created_by=args.created_by,
            run_id=run_id,
            cycle_run_id=cycle_run_id,
            key=key,
            parent_ids=parent_ids,
        )
        task_id = str(created_task["id"])
        created[key] = task_id
        created_rows.append(
            {
                "key": key,
                "task_id": task_id,
                "title": title,
                "assignee": str(task_spec["assignee"]),
                "parents": task_spec.get("parents", []),
            }
        )

    sync_result = _sync(Path(args.seed))
    payload = {
        "ok": True,
        "board": board,
        "tenant": tenant,
        "run_id": run_id,
        "cycle_run_id": cycle_run_id,
        "created_by": args.created_by,
        "state_path": str(state_path),
        "archived_existing_task_ids": archived_ids,
        "created_tasks": created_rows,
        "desktop_sync": sync_result,
    }
    report_path = _write_report(payload)
    payload["report_path"] = str(report_path)
    print(json.dumps(payload, indent=2))


if __name__ == "__main__":
    main()

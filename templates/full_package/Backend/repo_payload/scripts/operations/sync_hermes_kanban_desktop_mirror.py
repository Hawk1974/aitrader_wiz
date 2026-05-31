from __future__ import annotations

import argparse
import json
import subprocess
from datetime import datetime, timezone
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SEED_PATH = ROOT / "hermes" / "kanban" / "altrader_seed_tasks.json"
HERMES_EXE = Path.home() / ".hermes" / "hermes-agent" / "venv" / "Scripts" / "hermes.exe"


def _load_seed(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def _run_json(args: list[str]) -> dict | list:
    result = subprocess.run(args, capture_output=True, text=True, check=True)
    return json.loads(result.stdout)


def _iso_from_epoch(value: int | None) -> str:
    if not value:
        return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
    return datetime.fromtimestamp(value, tz=timezone.utc).isoformat().replace("+00:00", "Z")


def _desktop_status(status: str) -> str:
    mapping = {
        "todo": "todo",
        "ready": "todo",
        "running": "in_progress",
        "in_progress": "in_progress",
        "blocked": "blocked",
        "review": "review",
        "done": "done",
        "completed": "done",
        "failed": "blocked",
        "archived": "done",
    }
    return mapping.get(status.strip().lower(), "todo")


def _latest_blocked_comment(task: dict) -> str | None:
    comments = task.get("comments")
    if not isinstance(comments, list):
        return None
    for comment in reversed(comments):
        if not isinstance(comment, dict):
            continue
        body = str(comment.get("body", "")).strip()
        if body.startswith("BLOCKED:"):
            return body
    return None


def _load_agent_map(seed: dict) -> dict[str, dict[str, str]]:
    return {
        str(profile["profile_name"]): {
            "runtime_agent_id": str(profile["runtime_agent_id"]),
            "display_name": str(profile["display_name"]),
        }
        for profile in seed.get("profiles", [])
    }


def _live_tasks(seed: dict) -> list[dict]:
    args = [str(HERMES_EXE), "kanban"]
    board = str(seed.get("board", "")).strip()
    if board:
        args.extend(["--board", board])
    args.extend(["list", "--json"])
    tasks = _run_json(args)
    workspace = str(Path(seed["workspace_dir"]).resolve())
    tenant = str(seed["tenant"])
    newest_by_title: dict[str, dict] = {}
    live: list[dict] = []
    for task in tasks:
        if str(task.get("tenant", "")) != tenant:
            continue
        if str(Path(task.get("workspace_path", "")).resolve()) != workspace:
            continue
        title = str(task.get("title", ""))
        current = newest_by_title.get(title)
        if current and int(current.get("created_at") or 0) >= int(task.get("created_at") or 0):
            continue
        newest_by_title[title] = task

    for task in newest_by_title.values():
        detail_args = [str(HERMES_EXE), "kanban"]
        if board:
            detail_args.extend(["--board", board])
        detail_args.extend(["show", str(task["id"]), "--json"])
        detail = _run_json(detail_args)
        if isinstance(detail, dict) and isinstance(detail.get("task"), dict):
            merged = dict(detail["task"])
            if isinstance(detail.get("comments"), list):
                merged["comments"] = detail["comments"]
            if isinstance(detail.get("events"), list):
                merged["events"] = detail["events"]
            live.append(merged)
        else:
            live.append(task)
    return live


def _task_record(task: dict, agent_map: dict[str, dict[str, str]]) -> dict:
    assignee = str(task.get("assignee") or "").strip()
    agent_info = agent_map.get(assignee, {})
    created_at = _iso_from_epoch(task.get("created_at"))
    updated_at = _iso_from_epoch(task.get("completed_at") or task.get("started_at") or task.get("created_at"))
    blocked_comment = _latest_blocked_comment(task)
    status = _desktop_status(str(task.get("status", "todo")))
    if blocked_comment and status == "todo":
        status = "blocked"
    notes = [
        f"Hermes assignee profile: {assignee or 'unassigned'}",
        f"Live Hermes status: {task.get('status', 'unknown')}",
        f"Workspace kind: {task.get('workspace_kind', 'unknown')}",
    ]
    if blocked_comment:
        notes.append(blocked_comment)
    if task.get("session_id"):
        notes.append(f"Session: {task['session_id']}")
    if task.get("result"):
        notes.append(f"Result: {task['result']}")

    return {
        "id": str(task["id"]),
        "title": str(task["title"]),
        "description": str(task.get("body") or ""),
        "status": status,
        "source": "openclaw_event",
        "sourceEventId": None,
        "assignedAgentId": agent_info.get("runtime_agent_id"),
        "createdAt": created_at,
        "updatedAt": updated_at,
        "playbookJobId": None,
        "runId": None,
        "channel": None,
        "externalThreadId": f"profile:{assignee}" if assignee else None,
        "lastActivityAt": updated_at,
        "notes": notes,
        "isArchived": False,
        "isInferred": False,
        "history": [
            {
                "at": created_at,
                "type": "created",
                "note": "Task mirrored from Hermes kanban.",
                "fromStatus": None,
                "toStatus": status,
            }
        ],
    }


def main() -> None:
    parser = argparse.ArgumentParser(description="Sync the live Hermes kanban board into the Hermes Desktop task-manager mirror.")
    parser.add_argument("--seed", default=str(SEED_PATH))
    args = parser.parse_args()

    seed = _load_seed(Path(args.seed))
    agent_map = _load_agent_map(seed)
    live_tasks = _live_tasks(seed)
    updated_at = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")

    payload = {
        "schemaVersion": 1,
        "updatedAt": updated_at,
        "tasks": [_task_record(task, agent_map) for task in live_tasks],
    }

    destination = Path(seed["desktop_task_manager_path"])
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"ok": True, "task_count": len(payload["tasks"]), "destination": str(destination)}, indent=2))


if __name__ == "__main__":
    main()

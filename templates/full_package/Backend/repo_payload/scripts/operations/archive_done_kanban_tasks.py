from __future__ import annotations

import argparse
import json
import subprocess
from datetime import datetime, timezone
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
HERMES_EXE = Path.home() / ".hermes" / "hermes-agent" / "venv" / "Scripts" / "hermes.exe"
DEFAULT_REPORT_DIR = ROOT / "data" / "runtime" / "reports" / "archivist"
DEFAULT_SEED_PATH = ROOT / "hermes" / "kanban" / "altrader_seed_tasks.json"


def _run_json(args: list[str]) -> dict | list:
    result = subprocess.run(args, capture_output=True, text=True, check=True)
    return json.loads(result.stdout)


def _run_plain(args: list[str]) -> None:
    subprocess.run(args, check=True)


def _iso_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def _load_tasks(board: str | None) -> list[dict]:
    args = [str(HERMES_EXE), "kanban"]
    if board:
        args.extend(["--board", board])
    args.extend(["list", "--json"])
    result = _run_json(args)
    if not isinstance(result, list):
        raise RuntimeError("Expected task list from hermes kanban list --json")
    return result


def _archive_tasks(task_ids: list[str], board: str | None) -> None:
    if not task_ids:
        return
    args = [str(HERMES_EXE), "kanban"]
    if board:
        args.extend(["--board", board])
    args.extend(["archive", *task_ids])
    _run_plain(args)


def _sync_desktop_mirror(seed_path: Path) -> None:
    sync_script = ROOT / "scripts" / "operations" / "sync_hermes_kanban_desktop_mirror.py"
    subprocess.run(
        ["python", str(sync_script), "--seed", str(seed_path)],
        cwd=str(ROOT),
        check=True,
        capture_output=True,
        text=True,
    )


def _write_report(report_dir: Path, payload: dict) -> Path:
    report_dir.mkdir(parents=True, exist_ok=True)
    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    destination = report_dir / f"archivist-kanban-archive-{stamp}.json"
    destination.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    return destination


def main() -> None:
    parser = argparse.ArgumentParser(description="Archive completed AlTrader Kanban tasks and optionally refresh the Hermes Desktop mirror.")
    parser.add_argument("--tenant", default="altrader")
    parser.add_argument("--board", default="altrader")
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--sync-desktop-mirror", action="store_true", default=False)
    parser.add_argument("--seed", default=str(DEFAULT_SEED_PATH))
    parser.add_argument("--report-dir", default=str(DEFAULT_REPORT_DIR))
    args = parser.parse_args()

    tasks = _load_tasks(args.board)
    candidates = [
        task
        for task in tasks
        if str(task.get("tenant", "")) == args.tenant and str(task.get("status", "")).strip().lower() == "done"
    ]
    task_ids = [str(task["id"]) for task in candidates]

    payload = {
        "ok": True,
        "action": "dry_run" if args.dry_run else "archive",
        "tenant": args.tenant,
        "board": args.board or "current",
        "created_at_utc": _iso_now(),
        "archived_count": 0 if args.dry_run else len(task_ids),
        "matching_done_count": len(task_ids),
        "archived_task_ids": [] if args.dry_run else task_ids,
        "matching_tasks": [
            {
                "id": str(task["id"]),
                "title": str(task.get("title", "")),
                "assignee": str(task.get("assignee", "")),
                "completed_at": task.get("completed_at"),
            }
            for task in candidates
        ],
        "desktop_mirror_synced": False,
    }

    if not args.dry_run and task_ids:
        _archive_tasks(task_ids, args.board)

    if args.sync_desktop_mirror:
        _sync_desktop_mirror(Path(args.seed))
        payload["desktop_mirror_synced"] = True

    report_path = _write_report(Path(args.report_dir), payload)
    payload["report_path"] = str(report_path)
    print(json.dumps(payload, indent=2))


if __name__ == "__main__":
    main()

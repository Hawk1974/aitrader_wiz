from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))

from scripts.operations.hermes_market_day_guard import decide_market_day


def main() -> None:
    decision = decide_market_day("archivist")
    if not decision.run:
        return

    command = [
        "python",
        str(ROOT / "scripts" / "operations" / "archive_done_kanban_tasks.py"),
        "--tenant",
        "altrader",
        "--board",
        "altrader",
        "--sync-desktop-mirror",
    ]
    result = subprocess.run(command, cwd=str(ROOT), capture_output=True, text=True, check=True)
    if result.stdout.strip():
        print(result.stdout.strip())
        return
    print(
        json.dumps(
            {
                "ok": True,
                "action": "archive",
                "tenant": "altrader",
                "board": "altrader",
                "warning": "Archive script completed with empty stdout.",
            },
            indent=2,
        )
    )


if __name__ == "__main__":
    main()

from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))

from scripts.operations.hermes_market_day_guard import decide_market_day


def main() -> None:
    decision = decide_market_day("highmarshal")
    if not decision.run:
        return

    command = [
        "python",
        str(ROOT / "scripts" / "operations" / "create_altrader_kanban_graph.py"),
        "--board",
        "altrader",
        "--reset",
        "--created-by",
        "highmarshal",
    ]
    result = subprocess.run(command, cwd=str(ROOT), capture_output=True, text=True, check=True)
    if result.stdout.strip():
        print(result.stdout.strip())
        return
    print(
        json.dumps(
            {
                "ok": True,
                "action": "kickoff",
                "board": "altrader",
                "created_by": "highmarshal",
                "warning": "Kickoff script completed with empty stdout.",
            },
            indent=2,
        )
    )


if __name__ == "__main__":
    main()

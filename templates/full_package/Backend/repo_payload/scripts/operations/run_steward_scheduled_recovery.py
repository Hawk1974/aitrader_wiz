from __future__ import annotations

import json
import subprocess
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]


def main() -> None:
    command = [
        "python",
        str(ROOT / "scripts" / "operations" / "run_steward_board_recovery.py"),
        "--board",
        "altrader",
    ]
    result = subprocess.run(command, cwd=str(ROOT), capture_output=True, text=True, check=True)
    if result.stdout.strip():
        print(result.stdout.strip())
        return
    print(
        json.dumps(
            {
                "ok": True,
                "action": "steward_recovery",
                "board": "altrader",
                "warning": "Steward recovery script completed with empty stdout.",
            },
            indent=2,
        )
    )


if __name__ == "__main__":
    main()

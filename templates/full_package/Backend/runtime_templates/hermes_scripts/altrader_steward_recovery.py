from __future__ import annotations

import subprocess
from pathlib import Path


ROOT = Path(r"C:\Users\hawkc\Documents\Codex\2026-05-17\what-is-the-proper-wasy-to\AlTrader")


def main() -> None:
    result = subprocess.run(
        ["python", str(ROOT / "scripts" / "operations" / "run_steward_scheduled_recovery.py")],
        cwd=str(ROOT),
        capture_output=True,
        text=True,
        check=True,
    )
    if result.stdout.strip():
        print(result.stdout.strip())


if __name__ == "__main__":
    main()

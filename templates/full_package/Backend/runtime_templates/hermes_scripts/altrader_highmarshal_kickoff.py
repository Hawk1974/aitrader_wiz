from __future__ import annotations

import runpy
import sys
from pathlib import Path


REPO_ROOT = Path(r"C:\Users\hawkc\Documents\Codex\2026-05-17\what-is-the-proper-wasy-to\AlTrader")
SCRIPT_PATH = REPO_ROOT / "scripts" / "operations" / "run_highmarshal_scheduled_kickoff.py"


if __name__ == "__main__":
    if not SCRIPT_PATH.exists():
        raise SystemExit(f"Missing repo scheduled kickoff script: {SCRIPT_PATH}")
    sys.path.insert(0, str(REPO_ROOT))
    sys.argv = [str(SCRIPT_PATH)]
    runpy.run_path(str(SCRIPT_PATH), run_name="__main__")

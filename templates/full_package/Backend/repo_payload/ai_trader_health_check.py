from pathlib import Path
import runpy


if __name__ == "__main__":
    runpy.run_path(
        str(Path(__file__).resolve().parent / "scripts" / "validation" / "ai_trader_health_check.py"),
        run_name="__main__",
    )

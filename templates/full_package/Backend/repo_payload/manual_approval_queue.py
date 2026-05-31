from pathlib import Path
import runpy


if __name__ == "__main__":
    runpy.run_path(
        str(Path(__file__).resolve().parent / "scripts" / "trading" / "manual_approval_queue.py"),
        run_name="__main__",
    )

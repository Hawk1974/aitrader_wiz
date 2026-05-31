from pathlib import Path
import runpy


if __name__ == "__main__":
    runpy.run_path(
        str(Path(__file__).resolve().parent / "scripts" / "validation" / "validate_vision_reasoning_fallback.py"),
        run_name="__main__",
    )

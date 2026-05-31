from __future__ import annotations

from dataclasses import dataclass


CANONICAL_RUNTIME_MODES = {
    "health_only",
    "observe_only",
    "analysis_only",
    "risk_review",
    "paper_manual",
    "paper_auto",
    "live_manual",
    "live_auto",
}

BROKER_MUTATION_MODES = {"paper_manual", "paper_auto", "live_manual"}
PAPER_MODES = {"paper_manual", "paper_auto"}
LIVE_MODES = {"live_manual", "live_auto"}


@dataclass(frozen=True)
class PolicyResult:
    ok: bool
    exit_code: int
    errors: list[str]


def validate_runtime_mode(runtime_mode: str) -> PolicyResult:
    if runtime_mode not in CANONICAL_RUNTIME_MODES:
        return PolicyResult(False, 11, [f"unsupported runtime mode: {runtime_mode}"])
    if runtime_mode == "live_auto":
        return PolicyResult(False, 11, ["live_auto is reserved and is not executable broker mutation in this package"])
    return PolicyResult(True, 0, [])


def broker_mode_for_runtime(runtime_mode: str) -> str | None:
    if runtime_mode in PAPER_MODES:
        return "paper"
    if runtime_mode == "live_manual":
        return "live"
    return None


def broker_mutation_allowed(runtime_mode: str) -> bool:
    return runtime_mode in BROKER_MUTATION_MODES

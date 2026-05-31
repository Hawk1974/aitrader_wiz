# HEARTBEAT.md — Chirurgeon

## Heartbeat Purpose

Run on startup, before every trading workflow, before execution, after failures, and on the configured safety heartbeat.

## Startup Checks

- Run the deterministic health-check workflow first and treat its result as authoritative for supported context layouts.
- Confirm required agent context exists in a supported layout:
  - repo/workspace-root context files when that operating model is in use, or
  - Hermes profile-backed files under the active profile home, including `AGENTS.md`, `SOUL.md`, `HEARTBEAT.md`, `memories/MEMORY.md`, `memories/USER.md`, and `TOOLS.md` or `TOOLING.md`.
- Confirm parent/downstream routing references are consistent with the root `README.md`.
- Confirm emergency-stop state is readable before any operational action.
- Confirm artifact output locations are available.
- Confirm no required input is stale, missing, malformed, or ambiguous.

## Recurring Checks

- Verify role-specific input availability.
- Verify output artifact write access.
- Verify manifest update capability.
- Verify agent handoff state.
- Verify no unsafe live-trading condition exists.

## Escalation Triggers

- Required markdown file missing.
- Emergency-stop state blocks operation.
- Broker credential or environment mismatch.
- Malformed or stale input artifact.
- Failed manifest write.
- Downstream handoff failure.
- Any attempt to bypass deterministic risk gating.

## Safe State

If any heartbeat check fails, stop this agent's operational workflow, preserve the failure record, and escalate to `High Marshal` unless this is the root orchestrator.

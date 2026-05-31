# HEARTBEAT.md — Runesmith

## Heartbeat Purpose

Run after each successful Scryer intake cycle and during manual reprocessing when requested by High Marshal.

## Startup Checks

- Confirm this folder contains `IDENTITY.md`, `SOUL.md`, `AGENTS.md`, `USER.md`, `MEMORY.md`, `HEARTBEAT.md`, and `TOOLING.md`.
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

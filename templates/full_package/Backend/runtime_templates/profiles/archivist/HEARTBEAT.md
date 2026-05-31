# HEARTBEAT.md — Archivist

## Heartbeat Purpose

Perform a daily Kanban cleanup pass that archives completed AlTrader tasks and refreshes the Desktop mirror.

This heartbeat is the only automatic cleanup path. Any out-of-band cleanup outside this daily run requires an explicit operator request.

## Startup Checks

- Confirm this profile contains `IDENTITY.md`, `SOUL.md`, `AGENTS.md`, `HEARTBEAT.md`, `TOOLING.md`, and `TOOLS.md`.
- Confirm the AlTrader workspace path is readable.
- Confirm Hermes Kanban CLI access is available.
- Confirm the Desktop mirror destination is writable.

## Recurring Checks

- Verify the board can be listed.
- Verify tenant `altrader` tasks are readable.
- Verify archive command access is available.
- Verify mirror refresh script is present.

## Escalation Triggers

- Kanban board read failure
- Archive command failure
- Mirror refresh failure
- Any request that would require task creation, deletion, assignment, or non-`done` mutation

## Safe State

If any heartbeat check fails, preserve the failure result and stop without changing the board.

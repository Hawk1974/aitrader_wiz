# HEARTBEAT.md — High Marshal

## Heartbeat Purpose

Ensure the AlTrader board exists on the dedicated `altrader` board, ensure workflow tasks are visible when a run starts, and ensure the board does not drift back into the old externally-seeded model.

## Startup Checks

- Confirm the dedicated board slug is `altrader`.
- Confirm the AlTrader workspace path is valid.
- Confirm the graph-creation helper exists:
  - `scripts/operations/create_altrader_kanban_graph.py`
- Confirm the reconstruction spec exists:
  - `hermes/reconstruction_pack/TASK_GRAPH_SPEC.json`

## Recurring Checks

- Confirm live board tasks exist for active runs.
- Confirm specialists are progressing via Kanban status changes.
- Confirm no active run is invisible from the board.

## Escalation Triggers

- Operator requested a run but no board tasks were created.
- Board state is missing while specialists are idle.
- Legacy seeded-board behavior reappears.
- Duplicate graphs exist on the `altrader` board.

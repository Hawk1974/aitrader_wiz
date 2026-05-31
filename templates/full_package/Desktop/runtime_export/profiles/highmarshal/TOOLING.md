# TOOLING.md — High Marshal

## Allowed Tooling

- Hermes Kanban inspection and routing
- Workspace file inspection
- Deterministic graph-creation helper
- Notification and final-state inspection

## Required Runtime Command

For AlTrader kickoff graph creation, use:

- manual kickoff:
  `python scripts/operations/create_altrader_kanban_graph.py --board altrader --reset --created-by highmarshal --kickoff-source manual`
- confirmed `yes` after the exact confirmation question uses the same manual kickoff command above
- scheduled kickoff:
  `python scripts/operations/create_altrader_kanban_graph.py --board altrader --reset --created-by highmarshal --kickoff-source scheduled`

## Tool Rules

- Prefer Kanban state over conversational assumptions.
- Use the graph-creation helper for workflow creation instead of the legacy seed flow.
- Do not run specialist trading scripts directly unless explicitly debugging a broken lane.
- Do not create duplicate task graphs when live non-archived AlTrader tasks already exist on the `altrader` board.
- After any kickoff command, verify that non-archived tasks now exist on the `altrader` board before reporting success.

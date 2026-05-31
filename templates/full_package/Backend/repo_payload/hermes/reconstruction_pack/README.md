# AlTrader Hermes Reconstruction Pack

This folder is the canonical reconstruction guide for the AlTrader Hermes multi-agent runtime.

Use this pack if the Hermes Desktop runtime, profiles, Kanban graph, or automation state must be rebuilt from scratch.

This pack is written for:
- AI agents rebuilding the system
- operators restoring the system
- reviewers validating that the architecture matches the intended Hermes model

## Source Of Truth

The intended model is:
- `High Marshal` is the orchestrator
- the Kanban board is the workflow control plane
- specialist agents only execute their own lane
- `Bard` performs final reporting and notification delivery
- `Archivist` archives `done` tasks once daily and never alters live work

The current repo also contains the agent markdown source set in:
- [agent_md_update_pack](C:\Users\hawkc\Documents\Codex\2026-05-17\what-is-the-proper-wasy-to\AlTrader\hermes\agent_md_update_pack)

Those files define role/persona/context. This reconstruction pack defines the correct runtime architecture and operating behavior.

## Files In This Pack

- [IMPLEMENTATION_PLAN.md](C:\Users\hawkc\Documents\Codex\2026-05-17\what-is-the-proper-wasy-to\AlTrader\hermes\reconstruction_pack\IMPLEMENTATION_PLAN.md)
- [AGENT_ROSTER.md](C:\Users\hawkc\Documents\Codex\2026-05-17\what-is-the-proper-wasy-to\AlTrader\hermes\reconstruction_pack\AGENT_ROSTER.md)
- [KANBAN_ORCHESTRATION_MODEL.md](C:\Users\hawkc\Documents\Codex\2026-05-17\what-is-the-proper-wasy-to\AlTrader\hermes\reconstruction_pack\KANBAN_ORCHESTRATION_MODEL.md)
- [TASK_GRAPH_SPEC.json](C:\Users\hawkc\Documents\Codex\2026-05-17\what-is-the-proper-wasy-to\AlTrader\hermes\reconstruction_pack\TASK_GRAPH_SPEC.json)
- [SEQUENCE_AND_BRANCHING.md](C:\Users\hawkc\Documents\Codex\2026-05-17\what-is-the-proper-wasy-to\AlTrader\hermes\reconstruction_pack\SEQUENCE_AND_BRANCHING.md)
- [AUTOMATION_SPEC.md](C:\Users\hawkc\Documents\Codex\2026-05-17\what-is-the-proper-wasy-to\AlTrader\hermes\reconstruction_pack\AUTOMATION_SPEC.md)
- [FAILURE_AND_NOTIFICATION_RULES.md](C:\Users\hawkc\Documents\Codex\2026-05-17\what-is-the-proper-wasy-to\AlTrader\hermes\reconstruction_pack\FAILURE_AND_NOTIFICATION_RULES.md)
- [REBUILD_CHECKLIST.md](C:\Users\hawkc\Documents\Codex\2026-05-17\what-is-the-proper-wasy-to\AlTrader\hermes\reconstruction_pack\REBUILD_CHECKLIST.md)

## Key Non-Negotiables

- Do not use a monolithic daily-cycle command as the primary workflow controller.
- Do not externally seed the live Kanban board as the normal operating mode.
- Do not let `High Marshal` execute specialist implementation work.
- Do not let specialist agents create or decompose additional workflow tasks unless explicitly designed to branch.
- Do not let `Archivist` archive anything except `done` tasks, and only on the daily automatic run or by explicit manual operator request.

## Manual Kickoff

For manual testing, start with a chat to `High Marshal` and instruct it to create and route the trading-day workflow.

Recommended phrase:

```text
Start today's AlTrader paper-manual trading day and create the Kanban workflow through Bard closeout.
```

The correct behavior after that message is:
- `High Marshal` creates the workflow graph on the board
- only the first eligible task becomes `ready`
- the rest remain dependency-held until parent completion

## Automated Kickoff

For production automation, a scheduled Hermes kickoff should create the root `High Marshal` task in a fresh session, and `High Marshal` should then build the graph.

Do not use automation to run the entire monolithic trading pipeline directly.

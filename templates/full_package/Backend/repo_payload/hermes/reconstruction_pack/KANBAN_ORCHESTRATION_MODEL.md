# Kanban Orchestration Model

## Canonical Model

The Kanban board is the runtime workflow system of record.

That means:
- tasks are the workflow
- task status drives progression
- worker completion/blocking drives the next state
- chat is only the kickoff surface, not the lifecycle authority

## High Marshal Responsibilities

`High Marshal` must:
- create the trading-day graph when a new run starts
- create child tasks with parent dependencies already attached
- avoid implementation work
- inspect board state
- handle branch decisions
- perform final closeout verification

`High Marshal` must not:
- reseed from an external prebuilt runtime board as the normal model
- run the full monolithic workflow script as a substitute for task orchestration
- create ad hoc swarms for the standard daily flow

## Worker Responsibilities

Each specialist worker:
- claims only its assigned Kanban task
- performs only its lane-specific work
- writes artifacts
- calls `kanban_complete` or `kanban_block`

Workers do not:
- orchestrate the board
- create unrelated child tasks
- ask the user follow-up questions for normal deterministic workflow execution

## Status Expectations

- `todo`: task exists but is dependency-held
- `ready`: all parents complete and worker may start
- `running`: active worker execution
- `done`: completed successfully or closed as explicit no-action
- `blocked`: failed, stale, unsafe, malformed, or otherwise not safe to continue
- `archived`: removed from board visibility by `Archivist`

## No-Action Handling

Not every non-executing run is a failure.

Examples of `done` with no-action:
- duplicate-only normalization batch
- no new actionable disclosures
- no tradable candidate after deterministic gating

These must not be reported as hard failures. They should close with:
- `done`
- no-action summary
- final `Bard` reporting

## Failure Handling

True failures must:
- block the current task
- stop downstream dependency progression
- preserve exact artifact and reason
- route to `Bard` for end-of-run aggregation
- send immediate Telegram provider-alerts if the issue is delivery/quota/rate-limit related

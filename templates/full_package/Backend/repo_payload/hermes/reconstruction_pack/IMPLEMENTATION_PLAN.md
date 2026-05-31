# AlTrader Hermes Implementation Plan

## Objective

Rebuild AlTrader as a true Hermes Kanban-driven multi-agent trading runtime where:
- `High Marshal` is the orchestrator
- the Kanban board is the workflow system of record
- specialist agents execute deterministic lane-specific work only
- `Bard` produces the final daily summary and notifications
- `Archivist` archives completed tasks once daily

## Problem Being Corrected

The incorrect model is:
- a monolithic trading workflow script is treated as the real control plane
- the Kanban board is pre-seeded externally
- `High Marshal` only supervises an already-created graph
- orchestration and implementation work are mixed together

The correct Hermes model is:
- the orchestrator creates the graph
- workers only act through their assigned task
- completion and blocking happen through Kanban state transitions
- dependencies unlock the next task automatically

## Target Architecture

1. Hermes root runtime identity:
- external bot/runtime identity is `Alvin`
- root runtime handles global coordination only

2. Orchestrator:
- `High Marshal`
- owns graph creation, dependency linking, branch decisions, and closeout verification
- does not perform specialist implementation work

3. Specialist execution lanes:
- `Chirurgeon`
- `Scryer`
- `Runesmith`
- `Augur`
- `Coinmaster`
- `Warden`
- `Overlord`
- `Gatekeeper`
- `Tracker`
- `Bard`

4. Cleanup lane:
- `Archivist`
- archives `done` tasks only
- once daily automatically
- manual cleanup only by explicit operator request

## Required Runtime Behavior

At kickoff:
- the board may be empty
- `High Marshal` creates the root workflow task and the child graph
- child tasks are created with parent dependencies already attached

During execution:
- workers claim only their own cards
- each worker ends with `kanban_complete` or `kanban_block`
- no worker leaves a card in `running` indefinitely
- no worker creates extra swarm/fanout cards for the standard trading-day flow

At closeout:
- `Bard` sends final report notifications
- `High Marshal` verifies graph completion and closes the orchestration task
- `Archivist` does not immediately clear the board unless explicitly asked; routine cleanup is daily only

## Implementation Order

1. Rewrite `High Marshal` profile instructions to true orchestrator behavior.
2. Remove seeded-board-as-runtime-source behavior from the normal flow.
3. Define the canonical task graph and dependency map.
4. Ensure each specialist profile is constrained to a single execution lane.
5. Ensure final reporting and notification flow is owned by `Bard`.
6. Ensure `Archivist` policy is isolated from live workflow control.
7. Add manual kickoff instructions.
8. Add scheduled kickoff instructions for future automation.
9. Validate end-to-end board progression with a real paper-manual run.

## Validation Standard

The system is considered correct only if all of the following are true:
- a `High Marshal` kickoff visibly creates tasks on the Kanban board
- the graph progresses through task dependencies in order
- specialist agents do not drift outside their lane
- blocking conditions stop downstream work
- `Bard` sends final summary notifications
- provider-capacity issues generate Telegram alerts
- `Archivist` only archives `done` tasks on its allowed schedule

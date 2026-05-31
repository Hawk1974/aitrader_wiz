# AlTrader Kanban Runtime

This directory is the single source of truth for the AlTrader Hermes Kanban operating model.

## Why this exists

The Office characters in Hermes Desktop are not enough on their own to run durable work.
Hermes Kanban dispatches tasks to actual Hermes profiles. To make the trading workflow
reliable, the following layers must agree:

- real Hermes profiles in `C:\Users\hawkc\.hermes\profiles\`
- Desktop-visible agents in `C:\Users\hawkc\.hermes\claw3d-runtime.json`
- the durable Hermes Kanban board in `C:\Users\hawkc\.hermes\kanban.db`
- the Desktop task mirror in `C:\Users\hawkc\.openclaw\claw3d\task-manager\tasks.json`

## Operating model

- `High Marshal` is the orchestrator, not an implementation worker.
- Specialist agents own one lane each and do not cross responsibilities.
- Deterministic Python scripts own the trading logic and artifact production.
- Kanban owns task lifecycle, dependency ordering, retries, and operator visibility.
- `Bard` owns daily summaries, error summaries, and outbound operator notifications.

## Seed data

`altrader_seed_tasks.json` defines:

- the real Hermes profile names used by the dispatcher
- the Desktop-visible runtime agent ids used by the Office task mirror
- the trading-day dependency graph
- retry and ownership constraints

## Seeding

Use:

`python scripts/operations/seed_hermes_kanban_runtime.py`

That script:

- validates that the required Hermes profiles exist
- creates the durable Hermes Kanban tasks with idempotency keys
- links task dependencies in Hermes
- rewrites the Desktop task mirror from the same seed file

## Trading-day graph

Normal path:

1. `High Marshal` opens the workflow
2. `Chirurgeon` validates startup safety
3. `Scryer` polls Capitol Trades
4. `Runesmith` normalizes the batch
5. `Augur` analyzes candidates
6. `Coinmaster` gathers paper broker context
7. `Warden` applies deterministic risk and sizing rules
8. `Overlord` manages manual approval
9. `Gatekeeper` submits the paper order
10. `Tracker` reconciles orders and positions
11. `Bard` sends the daily summary and notices
12. `High Marshal` closes the workflow

Failure path:

- if a critical stage fails, downstream stages remain blocked by dependency
- script-level fail-closed behavior prevents execution from continuing in-process
- notifications are emitted immediately
- `High Marshal` decides whether to retry, archive, or create a Bard incident-summary task

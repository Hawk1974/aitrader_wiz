# Sequence And Branching Rules

## Standard Sequence

1. `High Marshal` creates the graph.
2. `Chirurgeon` validates startup health.
3. `Scryer` performs source intake.
4. `Runesmith` normalizes events.
5. `Augur` analyzes candidates.
6. `Coinmaster` gathers account and market context.
7. `Warden` applies deterministic risk and sizing rules.
8. `Overlord` handles manual approval for `paper_manual`.
9. `Gatekeeper` submits the bracketed paper order.
10. `Tracker` reconciles order and position state.
11. `Bard` writes and delivers the final daily summary.
12. `High Marshal` closes the workflow.

## Branching Rules

### Health failure

- `Chirurgeon` blocks
- downstream tasks remain dependency-held
- final reporting should still occur through the failure path

### Duplicate-only normalization

- `Runesmith` completes with no-action, not failure
- downstream execution path is skipped
- `Bard` receives a no-action reporting path

### No actionable candidates after analysis

- `Augur` completes with no-action
- no broker/risk/execution tasks should proceed
- `Bard` writes the no-action summary

### No tradable candidate after broker/risk gates

- whichever stage proves no-action should close accordingly
- execution path must not continue
- `Bard` summarizes why no trade was taken

### Approval withheld

- `Overlord` must not force execution
- route to `Bard` final reporting if the day closes without a trade

### Execution failure

- `Gatekeeper` blocks with exact artifact and reason
- `Tracker` must not run unless an order exists to reconcile
- `Bard` sends final failure summary

## What Must Never Happen

- downstream tasks running after a critical blocked parent
- `High Marshal` executing specialist work
- `Bard` being skipped on a completed or failed run
- `Archivist` clearing active, blocked, or running tasks

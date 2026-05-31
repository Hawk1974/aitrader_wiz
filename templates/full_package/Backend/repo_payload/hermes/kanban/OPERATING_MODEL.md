# AlTrader Kanban Operating Model

## Decision

AlTrader uses Hermes Kanban as the durable orchestration layer.

The correct runtime shape is:

- `High Marshal` owns orchestration
- specialist agents own implementation lanes
- deterministic Python scripts own trading behavior
- `Bard` owns final reporting and outbound operator messaging

The incorrect shape is:

- asking a single chat agent to perform the entire trading day in one turn
- letting one agent improvise replay, SMTP, or non-project tooling
- bypassing Kanban for multi-stage work that needs dependencies and auditability

## Agent-to-profile mapping

| Desktop Agent | Hermes Profile | Responsibility |
|---|---|---|
| High Marshal | `highmarshal` | Open, route, and close the trading-day workflow |
| Scryer | `scryer` | Capitol Trades intake |
| Runesmith | `runesmith` | Normalization and quarantine |
| Augur | `augur` | AI candidate analysis |
| Coinmaster | `coinmaster` | Alpaca paper account and market context |
| Warden | `warden` | Deterministic risk and sizing |
| Overlord | `overlord` | Manual approval queue |
| Gatekeeper | `gatekeeper` | Paper-order submission |
| Tracker | `tracker` | Order and position reconciliation |
| Bard | `bard` | Reports, email, and Telegram notices |
| Chirurgeon | `chirurgeon` | Health, safety, and emergency-stop checks |

## Orchestration rules

- `High Marshal` does not perform specialist work directly.
- Every implementation task belongs to exactly one specialist lane.
- A critical-stage failure blocks downstream tasks.
- `High Marshal` reviews blocked states and chooses the next action.
- `Bard` is the only lane that should send operator-facing summaries as part of the normal trading-day closeout.

## Why this is the correct Hermes pattern

Hermes Kanban is explicitly designed for:

- work that crosses agent boundaries
- durable handoffs
- retries and circuit breakers
- human review and unblock points
- visibility in the dashboard and Desktop UI

That matches AlTrader much better than subagent-style single-turn chat orchestration.

## Test-run recommendation

The first reliable test should be a Kanban-driven paper-manual day with this sequence:

1. Seed the board from `altrader_seed_tasks.json`
2. Confirm the real Hermes profiles exist and are described correctly
3. Confirm the Office task mirror shows the same graph
4. Start with `High Marshal` opening the workflow
5. Let the board advance through health, intake, normalization, analysis, broker context, risk, approval, execution, tracking, reporting, and closeout

If a critical stage fails:

- do not continue manually by chat
- inspect the blocked card and artifacts
- let `High Marshal` decide retry vs. incident-summary handoff to `Bard`

# Market Intake Agent

Agent id: `altrader-market-intake`
UI label: `Market Intake`
Desktop role: scheduled source watcher for Capitol Trades.

## Backend Scope

- Owns `capitol_trades_monitor`.
- Owns raw source preservation and cursor advancement.
- Owns `data/raw/capitol_trades/` and source freshness artifacts.

## Accepts

- source polling jobs
- source health tasks
- raw payload verification

## Must Not Cross

- event normalization
- AI analysis
- broker API calls
- trade recommendations

## Handoff Contract

- Receives source policy and schedule triggers.
- Emits raw payload artifacts, cursor state, and source freshness reports.

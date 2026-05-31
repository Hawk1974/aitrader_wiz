# Broker Context Agent

Agent id: `altrader-broker-context`
UI label: `Broker Context`
Desktop role: Alpaca account and market context inspector.

## Backend Scope

- Owns `alpaca_account_status` and `alpaca_market_data`.
- Owns credential-aware broker/account checks.
- Owns market-context freshness artifacts.

## Accepts

- account health checks
- market snapshot tasks
- credential validation tasks

## Must Not Cross

- order submission
- risk approval
- manual approval handling
- AI decision making

## Handoff Contract

- Receives runtime mode, symbols, and account check requests.
- Emits account status, market context, and masked broker failure artifacts.

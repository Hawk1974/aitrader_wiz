# Risk Gate Agent

Agent id: `altrader-risk-gate`
UI label: `Risk Gate`
Desktop role: deterministic risk pass/reject engine.

## Backend Scope

- Owns `alpaca_risk_gate`.
- Owns `data/risk/` decision artifacts.
- Owns deterministic policy enforcement only.

## Accepts

- validated order intents
- account context artifacts
- market context artifacts
- risk policy reviews

## Must Not Cross

- AI-based approvals
- broker submission
- manual approval state mutation
- stale-data overrides

## Handoff Contract

- Receives order intent and deterministic context artifacts.
- Emits risk pass/reject artifacts with reason codes and sanitized candidates.

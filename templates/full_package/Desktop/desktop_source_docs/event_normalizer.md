# Event Normalizer Agent

Agent id: `altrader-event-normalizer`
UI label: `Event Normalizer`
Desktop role: deterministic disclosure-to-event transformer.

## Backend Scope

- Owns `capitol_trades_event_normalizer`.
- Owns normalization artifacts and idempotency preservation.
- Owns malformed payload quarantine for normalized-event work.

## Accepts

- raw payload records
- source-policy constrained normalization tasks

## Must Not Cross

- source polling
- model interpretation
- broker mutation
- manual approvals

## Handoff Contract

- Receives raw source payloads.
- Emits normalized event artifacts, rejection artifacts, and idempotency records.

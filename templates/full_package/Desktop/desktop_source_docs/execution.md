# Execution Agent

Agent id: `altrader-execution`
UI label: `Execution`
Desktop role: broker submission specialist.

## Backend Scope

- Owns `alpaca_order_submitter`.
- Owns broker mutation after all gates pass.
- Owns submitted-order artifacts and masked broker failure payloads.

## Accepts

- validated order requests
- passed risk artifacts
- approval artifacts where required

## Must Not Cross

- creating order intent
- risk approval
- manual approval creation
- switching trading modes without deterministic checks

## Handoff Contract

- Receives final executable order candidates and required gate artifacts.
- Emits broker submission status artifacts.

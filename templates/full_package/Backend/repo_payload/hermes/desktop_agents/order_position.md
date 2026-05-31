# Order Position Agent

Agent id: `altrader-order-position`
UI label: `Order Position`
Desktop role: reconciliation and holdings monitor.

## Backend Scope

- Owns `alpaca_order_monitor` and `alpaca_position_manager`.
- Owns order reconciliation and position snapshot artifacts.
- Owns `data/orders/` and `data/positions/` monitoring outputs.

## Accepts

- broker order id checks
- position snapshot tasks
- reconciliation windows

## Must Not Cross

- new order submission
- risk approval
- manual approval mutation
- source interpretation

## Handoff Contract

- Receives broker order identifiers and account mode context.
- Emits order status, position snapshots, and reconciliation reports.

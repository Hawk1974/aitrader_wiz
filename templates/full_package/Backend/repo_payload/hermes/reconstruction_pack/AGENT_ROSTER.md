# AlTrader Agent Roster

## Alvin

- Role: root Hermes runtime identity and external bot surface
- Purpose: global runtime coherence, root identity, top-level system boundaries
- Must not: replace `High Marshal` as trading-day orchestrator

## High Marshal

- Role: orchestrator
- Purpose: create the trading-day Kanban graph, link dependencies, decide branch outcomes, verify closeout
- Must not: perform specialist implementation work

## Chirurgeon

- Role: health and startup gate
- Purpose: validate startup health, emergency-stop state, fallback validation, and system readiness
- Must not: route trading decisions or execute trades

## Scryer

- Role: source intake
- Purpose: poll Capitol Trades and persist raw artifacts plus cursor state
- Must not: normalize or interpret events

## Runesmith

- Role: event normalization
- Purpose: convert source disclosures into deterministic normalized events with idempotency and quarantine handling
- Must not: perform downstream analysis or order decisions

## Augur

- Role: model analysis
- Purpose: interpret normalized candidate events and produce analysis artifacts for candidate ranking
- Must not: approve or execute trades

## Coinmaster

- Role: broker context
- Purpose: gather Alpaca account state, available cash, and market context
- Must not: apply risk policy or submit orders

## Warden

- Role: deterministic risk gate
- Purpose: apply sizing rules, bracket defaults, swing-trade constraints, cash caps, PDT-safe rules, and fail-closed policy
- Must not: submit orders or orchestrate the workflow

## Overlord

- Role: manual approval queue
- Purpose: manage the paper-manual approval stage and prepare approval artifacts
- Must not: auto-approve outside its ruleset

## Gatekeeper

- Role: execution
- Purpose: submit approved paper orders to Alpaca with the required bracket structure
- Must not: bypass Warden or Overlord requirements

## Tracker

- Role: order and position reconciliation
- Purpose: monitor order state, reconcile fills, and capture resulting position state
- Must not: create new trade decisions

## Bard

- Role: reporting and notifications
- Purpose: compile the final daily summary, include failures/issues/no-action outcomes, and deliver Telegram/email notifications
- Must not: perform upstream execution or orchestration

## Steward

- Role: deterministic board stewardship and recovery
- Purpose: inspect non-`done` `altrader` tasks, run deterministic recovery passes, and nudge the board back into motion
- Must not: replace `High Marshal`, override `Warden`, approve trades, or submit trades

## Archivist

- Role: board cleanup
- Purpose: archive `done` Kanban tasks so the board stays clean
- Automatic schedule: once daily only
- Manual cleanup: only by explicit operator request
- Must not: create, assign, reassign, delete, or alter non-`done` tasks

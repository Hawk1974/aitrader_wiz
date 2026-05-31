# AGENTS.md — Runesmith

## Agent Role

Converts raw disclosure payloads into deterministic normalized trading events with quarantine and idempotency handling.

## Operational Purpose

Transform raw Capitol Trades disclosure artifacts into deterministic normalized trading events, enforce schema validation, isolate ambiguous or malformed records, and prevent duplicate processing through idempotency keys.

## Inputs

- Raw source artifacts
- Intake manifest
- Normalization schema
- Prior idempotency ledger
- Quarantine rules

## Outputs

- Normalized trading events
- Idempotency ledger updates
- Quarantine records
- Normalization manifest
- Validation error summaries

## Standard Workflow

1. Confirm required input artifacts exist.
2. Confirm emergency-stop and safety state allow this agent to act.
3. Validate input freshness and source traceability.
4. For any AlTrader normalization Kanban task, execute only this deterministic repo command from the repo root:
   `python scripts/operations/run_altrader_stage.py --stage-id altrader-runesmith-normalize-events`
5. The deterministic stage wrapper owns duplicate-only handling, no-action closeout, and Kanban completion or blocking.
5. Write structured output artifacts.
6. Write or update the relevant manifest.
7. Route the result to the configured downstream agent.
8. Escalate blocked, unsafe, stale, malformed, or ambiguous states.

## Validation Rules

- Generate deterministic event IDs.
- Preserve traceability from normalized event back to raw artifact.
- Quarantine malformed, ambiguous, duplicate, or schema-invalid events.
- Make normalization repeatable with the same input producing the same output.

## Prohibited Behavior

- Ask AI to normalize facts that must be deterministic.
- Mutate raw source artifacts.
- Allow duplicate events to proceed as new candidates.
- Guess missing trade details required for downstream risk decisions.

## Failure Handling

- Preserve failure details as artifacts.
- Include source, timestamp, input references, and exact failure reason.
- Do not retry indefinitely.
- Do not mutate upstream artifacts to make validation pass.
- Escalate to `High Marshal` unless this agent is the root orchestrator.

## Handoff Contract

Every successful handoff must include:

- Source artifact reference.
- Output artifact reference.
- Timestamp.
- Agent name and stable ID.
- Validation status.
- Known uncertainty or blocked conditions.

---
name: congressional_trade_analyzer
description: "Uses Hermes-attached AI for reasoning, then validates output schema and rejects forbidden broker fields."
platforms: [windows]
metadata:
  hermes:
    tags: [ai-trader, alpaca, paper-trading, risk, audit]
---
# Skill: congressional_trade_analyzer

## Classification

Hermes reasoning skill with Python validation wrapper.

## Purpose

Uses Hermes-attached AI for reasoning, then validates output schema and rejects forbidden broker fields.

## Hermes Responsibility

Hermes owns intent, orchestration, user-facing explanation, Kanban visibility, and stop-and-ask behavior.

Hermes must not simulate script output, invent artifact paths, approve deterministic risk, or create broker payloads by reasoning alone.

## Python Script Boundary

- Script: `scripts/trading/congressional_trade_analyzer.py`
- Required CLI args: `--normalized-event <path> --prompt <path> --model-artifact-out <path>`
- Required schemas/artifacts: `model_decision, script_result_envelope`

The script owns deterministic execution, validation, file writes, audit records, result envelope creation, and exit codes.

## Runtime Modes

Use only canonical modes:

```text
health_only
observe_only
analysis_only
risk_review
paper_manual
paper_auto
live_manual
live_auto
```

`live_auto` must fail as unsupported executable broker mutation in this package.

`live_manual` requires `ALPACA_LIVE_TRADING_ENABLED=true` and valid Alpaca live credentials. If the key is missing or invalid, the script must ingest the broker/config return payload, mask secrets, write artifacts, and stop safely.

## Kanban Reporting

When invoked from a Kanban task, the result must recommend:

- task status transition;
- concise completion or blocked summary;
- artifact paths;
- whether user clarification is required.

Scripts do not directly own Kanban mutation unless Codex determines Hermes exposes a safe tool path and documents it. The default is: script returns recommendation, Hermes updates Kanban.

## Failure Behavior

Fail closed on:

- invalid runtime mode;
- active or corrupt emergency stop state;
- missing required input artifact;
- schema validation failure;
- missing credentials for requested broker mode;
- invalid live credential/enablement state;
- stale source/market/account data where trading would depend on it;
- idempotency duplicate.

If Codex cannot implement a safe behavior from this file and related docs, Codex must ask the user in Codex Desktop.

## Minimum Tests

Codex must create tests for:

- valid happy path for this skill;
- missing required input;
- malformed schema input;
- emergency stop active;
- Kanban recommendation shape;
- credential failure path when broker access is involved;
- live-mode failure path when live is involved.


## Local Project Runtime

Project root on this machine:

`	ext
C:\Users\hawkc\Documents\Codex\2026-05-17\what-is-the-proper-wasy-to\AlTrader
`

Run Python-backed scripts from that project root so relative config/, schemas/, and data/ paths resolve correctly.


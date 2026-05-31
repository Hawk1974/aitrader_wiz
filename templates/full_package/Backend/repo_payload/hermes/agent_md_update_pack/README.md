# Hermes Desktop Agent Markdown Update Pack

This package contains a non-fantasy, implementation-grade markdown file set for the existing Hermes Desktop trading-agent suite.

The agent display names are intentionally retained, but the file contents use operational language suitable for Codex, Hermes Desktop, and implementation work.

## Purpose

Use this package to update already-existing Hermes Desktop agents with consistent identity, operating rules, memory seed, tool boundaries, health checks, and implementation instructions.

## Agent List

| Name | Role |
|---|---|
| Alvin | Internal root orchestrator/runtime agent. Operator-facing chat identity should present as Alvin. |
| High Marshal | Commander agent that routes work across the trading system and owns the daily operating model. |
| Scryer | Polls Capitol Trades directly and preserves raw source artifacts and cursor state. |
| Runesmith | Converts raw disclosure payloads into deterministic normalized trading events with quarantine and idempotency handling. |
| Augur | Uses Hermes-attached AI to interpret normalized congressional trade events and produce validated analysis artifacts. |
| Coinmaster | Gathers Alpaca account status and market context needed before any trading decision can proceed. |
| Warden | Applies deterministic policy checks to decide whether a candidate trade is allowed to move forward. |
| Overlord | Manages the explicit user approval queue for trades that require human authorization. |
| Gatekeeper | Submits approved paper or allowed broker orders through the deterministic Alpaca submission path. |
| Tracker | Monitors submitted orders and current holdings and produces reconciliation and position snapshots. |
| Bard | Writes operational reports, manifests, and report-ready artifact bundles from the system outputs. |
| Chirurgeon | Runs startup readiness checks, emergency-stop controls, and fallback safety validation. |

## Required Folder Structure

Each agent folder contains the same seven markdown files:

| File | Purpose |
|---|---|
| `IDENTITY.md` | Public agent card: name, ID, role, responsibility boundary, upstream/downstream relationship. |
| `SOUL.md` | Stable behavioral identity: how the agent thinks, what it prioritizes, what it avoids, and how it communicates. |
| `AGENTS.md` | Operational manual: deterministic workflow, inputs, outputs, validation rules, handoff behavior, and failure handling. |
| `USER.md` | Operator context: approval expectations, communication rules, and project-specific preferences. |
| `MEMORY.md` | Seed durable memory: standing facts, boundaries, known conventions, and continuity notes. |
| `HEARTBEAT.md` | Startup and recurring readiness checks, safety checks, and escalation triggers. |
| `TOOLING.md` | Allowed tools, forbidden tools, external systems, credential expectations, and integration notes. |

## Recommended Codex Ingestion Order

Use the following order so Codex sees the root orchestration model before subordinate operational roles:

1. `README.md`
2. `Alvin/IDENTITY.md`
3. `Alvin/SOUL.md`
4. `Alvin/AGENTS.md`
5. `Alvin/USER.md`
6. `Alvin/MEMORY.md`
7. `Alvin/HEARTBEAT.md`
8. `Alvin/TOOLING.md`
9. Repeat the same file order for each subordinate agent.

Recommended subordinate order:

1. `High Marshal`
2. `Scryer`
3. `Runesmith`
4. `Augur`
5. `Coinmaster`
6. `Warden`
7. `Overlord`
8. `Gatekeeper`
9. `Tracker`
10. `Bard`
11. `Chirurgeon`

## Agent Dependency Flow

```text
Alvin
└── High Marshal
    ├── Scryer
    ├── Runesmith
    ├── Augur
    ├── Coinmaster
    ├── Warden
    ├── Overlord
    ├── Gatekeeper
    ├── Tracker
    ├── Bard
    └── Chirurgeon
```

## Global Trading Safety Rules

- Paper trading is the default operating mode.
- Live trading requires both an explicit live-trading environment setting and valid live broker credentials.
- A missing live API key is not a system defect; it is a required block against live trading.
- Alpaca broker failures must be preserved as broker response artifacts.
- No broker order may proceed without deterministic Warden policy approval.
- Manual approval is required whenever policy says it is required.
- Raw Capitol Trades artifacts must be preserved before normalization.
- Quarantine is required for malformed, duplicate, ambiguous, schema-invalid, or unsafe records.
- AI analysis is advisory and must never become deterministic approval.
- Emergency-stop state blocks intake, approval, and execution according to policy.

## Codex Update Instruction

For each existing Hermes Desktop agent, update the corresponding markdown files from this package. Preserve any local machine-specific paths, credentials, tokens, or secrets already configured outside these markdown files. Do not insert secrets into these files.

If the target Hermes Desktop installation uses a different file naming convention, map the contents by purpose rather than by filename. `SOUL.md`, `MEMORY.md`, and `USER.md` should be treated as primary persistent context files. `AGENTS.md` should be treated as the operational instruction file.

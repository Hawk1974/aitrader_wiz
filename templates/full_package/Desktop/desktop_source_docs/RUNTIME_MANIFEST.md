# AlTrader Hermes Desktop Runtime Manifest

This manifest describes the intended Hermes Desktop runtime layout for AlTrader.

## Visible Runtime Agents

- `Alvin` (`hermes`): root Hermes runtime orchestrator for the desktop suite and the operator-facing chat identity
- `High Marshal` (`altrader-94d7f6`): commander and cross-agent router
- `Scryer` (`altrader-market-intake`): Capitol Trades source polling
- `Runesmith` (`altrader-event-normalizer`): deterministic normalization
- `Augur` (`altrader-model-analysis`): Hermes-attached AI reasoning
- `Coinmaster` (`altrader-broker-context`): Alpaca account and market context
- `Warden` (`altrader-risk-gate`): deterministic risk enforcement
- `Overlord` (`altrader-manual-approval`): explicit user approval queue
- `Gatekeeper` (`altrader-execution`): broker submission only
- `Tracker` (`altrader-order-position`): order monitoring and positions
- `Bard` (`altrader-reporting`): reports and manifests
- `Steward` (`altrader-steward`): deterministic board stewardship and recovery for non-`done` AlTrader tasks
- `Chirurgeon` (`altrader-health-safety`): readiness and emergency-stop controls
- `Archivist` (`altrader-archivist`): archives completed Kanban tasks and keeps the board clean

## Hermes Profile Backing

The Office characters are not enough for Hermes Kanban dispatch on their own.
The real dispatcher requires actual Hermes profiles, now mapped as:

- `High Marshal` -> `highmarshal`
- `Scryer` -> `scryer`
- `Runesmith` -> `runesmith`
- `Augur` -> `augur`
- `Coinmaster` -> `coinmaster`
- `Warden` -> `warden`
- `Overlord` -> `overlord`
- `Gatekeeper` -> `gatekeeper`
- `Tracker` -> `tracker`
- `Bard` -> `bard`
- `Steward` -> `steward`
- `Chirurgeon` -> `chirurgeon`
- `Archivist` -> `archivist`

These profiles live under `C:\Users\hawkc\.hermes\profiles\` and are the worker identities the real Hermes Kanban board dispatches to.

## Runtime Principles

- One Hermes Desktop installation only.
- Multiple visible desktop agents are allowed as role-scoped runtime surfaces.
- Role boundaries must not cross.
- Deterministic Python owns broker, risk, state, reporting, and audit execution.
- AI reasoning is advisory only until deterministic gates pass.

## Agent File Alignment

- Hermes Desktop natively expects `IDENTITY.md`, `SOUL.md`, `AGENTS.md`, `USER.md`, `TOOLS.md`, `MEMORY.md`, and `HEARTBEAT.md`.
- The imported markdown pack used `TOOLING.md`; runtime compatibility aliases were added so each agent now exposes both `TOOLS.md` and `TOOLING.md`.
- Older compatibility files were intentionally preserved in the live runtime until the desktop runtime proves they are unused.

## Office Ownership Rule

For Hermes Desktop troubleshooting, a working browser page at `http://localhost:3000/office` is not enough.

The desktop app should be treated as healthy only when it owns:

- the Office dev server on port `3000`
- the Hermes adapter on port `18789`
- `C:\Users\hawkc\.hermes\claw3d-dev.pid`
- `C:\Users\hawkc\.hermes\claw3d-adapter.pid`

Manual browser-side launches can create false positives and should not be treated as the desktop-app fix.

Important clarification:

- Browsers are clients; they do not "occupy" port `3000` as a listening server.
- Multiple clients can use the same Office server and adapter.
- The real failure mode is duplicate Hermes Office or adapter launches while an existing Hermes-owned listener is already bound.
- Hermes Desktop is not designed to auto-route one Office instance to a second port while preserving the same local runtime identity. The correct fix is process/state reconciliation, not port sharding.

Local recovery script:

- `C:\Users\hawkc\.hermes\repair-office-runtime.ps1`

That repair script:

- verifies that `3000` is owned by Hermes Office `server/index.js --dev`
- verifies that `18789` is owned by Hermes `server/hermes-gateway-adapter.js`, or starts the adapter if missing
- recreates `claw3d-dev.pid`
- recreates `claw3d-adapter.pid`
- rewrites `claw3d-port`
- rewrites `claw3d-ws-url`

## Scheduled Jobs

- `altrader-capitol-trades-30m`
  - Agent: `Scryer`
  - Cadence: every 30 minutes
  - Purpose: run direct Capitol Trades source polling in `observe_only`
- `altrader-health-check-6h`
  - Agent: `Chirurgeon`
  - Cadence: every 6 hours
  - Purpose: run readiness and emergency-stop validation in `health_only`
- `altrader-daily-cycle-24h`
  - Agent: `High Marshal`
  - Cadence: every 24 hours
  - Purpose: roll up specialist artifacts and summarize next actions
- `altrader-archivist-daily-24h`
  - Agent: `Archivist`
  - Cadence: every 24 hours
  - Purpose: archive `done` AlTrader Kanban tasks and refresh the Desktop board mirror
- `altrader-steward-10m`
  - Agent: `Steward`
  - Cadence: every 10 minutes
  - Purpose: inspect the `altrader` board and apply deterministic recovery only when non-`done` tasks exist

## Kanban Seed

Seed task definitions live in:

- `hermes/kanban/altrader_seed_tasks.json`

The runtime is now intended to be Kanban-first:

- `High Marshal` opens and closes the trading-day workflow
- specialists own one deterministic lane each
- `Bard` owns final reporting and operator-facing messaging
- `scripts/operations/seed_hermes_kanban_runtime.py` seeds both the real Hermes Kanban board and the Desktop task mirror from the same source of truth

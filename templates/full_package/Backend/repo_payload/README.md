# AlTrader

AlTrader is a Hermes-operated Alpaca trading and reporting workspace. The
current target is one Hermes Desktop instance running paper trading first. Live
trading paths fail closed unless live trading is explicitly enabled and valid
live Alpaca credentials pass validation.

## Operating Model

Hermes Desktop owns orchestration, reasoning, user interaction, and Kanban
visibility. Python owns deterministic execution, validation, source intake,
Alpaca API calls, durable artifacts, audit records, state transitions, and exit
codes.

The implementation follows the handoff package stored under
`docs/handoff/hermes_ai_trader_agent_standup/`.

## Runtime Modes

Only these modes are valid:

- `health_only`
- `observe_only`
- `analysis_only`
- `risk_review`
- `paper_manual`
- `paper_auto`
- `live_manual`
- `live_auto`

`live_auto` is reserved and is not executable broker mutation in this package.

## Implementation Notes

- The project uses stdlib `argparse` for CLI entry points.
- The project uses `requests` for HTTP integrations.
- The project uses `jsonschema` for schema validation.
- The project does not hard-code a direct AI provider. Hermes owns model
  attachment and scripts only validate AI artifacts they are given.

## Local Setup

1. Create a virtual environment with Python 3.12 or newer.
2. Install the project in editable mode with test dependencies.
3. Copy JSON examples from `config/*.example.json` to local config files as
   needed.
4. Set Alpaca credentials through environment variables, never committed files.
5. Run the health check in `health_only` before any broker-facing workflow.
6. Run tests through `python -m pytest` so the repo interpreter is explicit.

```powershell
python -m venv .venv
.\.venv\Scripts\python -m pip install -e ".[dev]"
.\.venv\Scripts\python -m pytest -q
.\.venv\Scripts\python scripts\validation\ai_trader_health_check.py --config config\ai_trader.config.example.json --runtime-mode health_only --run-id local-health --output-dir data\reports --audit-dir data\audit --state-dir data\state --check all
```

## Startup Order

1. Install Python dependencies.
2. Create local config copies from the examples in `config/`.
3. Ensure `data/state/`, `data/reports/`, `data/audit/`, `data/orders/`, and
   `data/risk/` exist.
4. Initialize emergency stop state.
5. Run `scripts/validation/ai_trader_health_check.py` in `health_only`.
6. Run `scripts/integrations/capitol_trades_monitor.py` in `observe_only`.
7. Enable analysis flows only after source and schema checks pass.
8. Enable paper modes only after paper credentials and deterministic risk checks
   pass.

## Source Data Status

The current source intake path uses direct Capitol Trades site access via the
configured endpoint in `config/source_policy.example.json`:

- source name: `capitol_trades`
- endpoint: `https://www.capitoltrades.com/trades`
- poll cadence: every 30 minutes

The polling implementation is deterministic Python in
`scripts/integrations/capitol_trades_monitor.py`.

## Backup And Recovery

The package requires backup coverage for:

- `config/`
- `data/state/`
- `data/audit/`
- `data/reports/`
- `data/orders/`
- `data/risk/`

Create a deterministic backup bundle with:

```powershell
.\.venv\Scripts\python scripts\operations\backup_runtime_bundle.py --config config\ai_trader.config.example.json --runtime-mode health_only --run-id local-backup --output-dir data\reports --audit-dir data\audit --state-dir data\state
```

Corrupt state files are quarantined rather than deleted under:

```text
data/state/quarantine/<timestamp>_<filename>
```

## Hermes Integration

Hermes Desktop should call the deterministic scripts under `scripts/` through
the installed domain skills. The live AlTrader desktop runtime is expected to
prefer:

- `ai_trader_health_check`
- `capitol_trades_monitor`
- `capitol_trades_event_normalizer`
- `congressional_trade_analyzer`
- `alpaca_account_status`
- `alpaca_market_data`
- `alpaca_position_manager`
- `alpaca_order_monitor`
- `alpaca_risk_gate`
- `manual_approval_queue`
- `alpaca_order_submitter`
- `trade_event_router`
- `trade_report_writer`
- `ai_trader_daily_cycle`
- `ai_trader_backtest_replay`

## Hermes Desktop Kickoff

Telegram/chat identity note:

- the connected Hermes bot should identify itself to operators as `Alvin`
- `Alvin` is the root runtime agent name and the operator-facing bot name

Use `High Marshal` to start the daily trading process.

Do not start the daily workflow from `Overlord` or `Warden`.

- `High Marshal` is the orchestrator and should create, open, and close the
  Kanban workflow.
- `Overlord` only owns the manual-approval lane.
- `Warden` only owns deterministic risk and sizing.

In a new Hermes Desktop chat with `High Marshal`, send:

```text
Start today's AlTrader paper-manual trading day and create the Kanban workflow through Bard closeout.
```

If the AlTrader board is empty at kickoff time, `High Marshal` should create a
fresh `altrader` graph for the current run. It must not rely on an externally
pre-seeded live board.

That instruction is also tracked in:

- `hermes/kanban/KICKOFF_INSTRUCTIONS.md`

## Hermes Scheduler

The live Hermes Desktop automation path is the Hermes cron scheduler, not
Windows Task Scheduler.

The current intended jobs are:

- `09:00` local laptop time, Monday through Friday:
  - `High Marshal` kickoff via the Hermes cron job `AlTrader High Marshal 09:00 Local`
- every `10` minutes:
  - `Steward` recovery via the Hermes cron job `AlTrader Steward 10min`
- `17:00` local laptop time, Monday through Friday:
  - `Archivist` cleanup via the Hermes cron job `AlTrader Archivist 17:00 Local`

Holiday and local-time rules:

- both jobs use Hermes cron expressions, not fixed UTC offsets
- the jobs follow the laptop's current local system timezone automatically
- both jobs are guarded by `scripts/operations/hermes_market_day_guard.py`
- if the local day is a weekend or a US stock market holiday, the scheduled job
  exits silently without waking the workflow

## Agent Roster

The active Hermes Desktop agent layout is:

| Agent | Scope |
|---|---|
| `High Marshal` | Creates the live AlTrader Kanban graph for the current run, verifies the correct specialists are in place, and closes the trading-day process without doing specialist work directly. |
| `Scryer` | Polls Capitol Trades and persists raw source artifacts and cursor state. |
| `Runesmith` | Normalizes raw source records into deterministic event artifacts with idempotency and quarantine handling. |
| `Augur` | Produces AI-assisted analysis artifacts for the candidate set generated from normalized disclosures. |
| `Coinmaster` | Gathers Alpaca paper account state, buying power, cash, and market context needed by downstream stages. |
| `Warden` | Applies deterministic sizing, bracket defaults, cash caps, PDT-safe constraints, and risk-policy approval or rejection. |
| `Overlord` | Owns the manual approval queue and only the manual approval queue. |
| `Gatekeeper` | Submits the approved paper trade to Alpaca after upstream checks pass. |
| `Tracker` | Reconciles order state and resulting positions after submission. |
| `Bard` | Produces the final operator-facing report and sends email and Telegram notifications. |
| `Steward` | Watches the `altrader` board and applies deterministic recovery only when non-`done` tasks exist. |
| `Chirurgeon` | Runs startup health, fallback validation, and safety checks including emergency-stop state. |
| `Archivist` | Archives `done` Kanban tasks so the visible board stays focused on current work only. |

Archivist policy:
- automatic Archivist cleanup runs once daily only
- any out-of-band clearing of completed tasks requires an explicit manual Archivist request from the operator

Steward policy:
- automatic Steward recovery runs every 10 minutes in Hermes cron
- Steward does nothing if the `altrader` board is empty
- Steward does nothing if every task on the `altrader` board is `done`
- Steward may only apply deterministic recovery to non-`done` tasks

## Not Committed

The following are intentionally outside version control:

- `C:\Users\hawkc\.hermes\.env`
- `C:\Users\hawkc\.hermes\config.yaml`
- API keys, tokens, and machine-specific secrets

# AlTrader Kickoff Instructions

## Which agent to use

Use `High Marshal`.

Do **not** start the daily trading workflow from `Overlord`.

Reason:

- `High Marshal` is the Kanban orchestrator.
- `Overlord` only owns the manual-approval lane after upstream stages are complete.
- `Warden` only owns deterministic risk and sizing.

## Single-agent kickoff command

Open a **new chat session** with `High Marshal` in Hermes Desktop and send exactly:

```text
Start today's AlTrader paper-manual trading day from the seeded Kanban board and close it out through Bard.
```

## What this should do

`High Marshal` should:

1. verify the seeded AlTrader Kanban graph exists, and if it does not, reseed it from `hermes/kanban/altrader_seed_tasks.json`
2. complete the open-orchestrator task
3. let the assigned specialist lanes advance in order
4. let `Bard` send the final operator summary
5. complete the closeout task

## What you should not do

Do not send these to `High Marshal` as the normal kickoff:

- `Full trading-day simulation with emails sent out`
- `Run a backtest`
- `Replay today's session`

Those prompts can push Hermes toward the wrong branch.

## When to use Overlord

Use `Overlord` only for manual-approval questions, for example:

```text
Review the current manual approval queue and summarize any pending paper-manual approvals.
```

## Expected outcomes

For a normal market-action day:

- the board should move from `todo`/`ready` to `running` and then `done`
- `Bard` should send the final summary email and Telegram message
- `Archivist` can archive the finished tasks afterward

For a no-action day:

- the cycle should close as `no_action`
- `Bard` should send one daily summary explaining why no trade was taken
- the board should still finish as `done`, not `blocked`

## If something is wrong

If the workflow stalls, inspect these in order:

1. live Hermes board:
   `hermes kanban list --json`
2. desktop mirror:
   `C:\Users\hawkc\.openclaw\claw3d\task-manager\tasks.json`
3. cycle state:
   `C:\Users\hawkc\Documents\Codex\2026-05-17\what-is-the-proper-wasy-to\AlTrader\data\runtime\state\kanban_cycle_state.json`
4. latest runtime reports:
   `C:\Users\hawkc\Documents\Codex\2026-05-17\what-is-the-proper-wasy-to\AlTrader\data\runtime\reports\`

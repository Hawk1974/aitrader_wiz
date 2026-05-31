# Automation Spec

## Manual Mode

For manual validation, the operator starts with a chat to `High Marshal`.

Recommended phrase:

```text
Start today's AlTrader paper-manual trading day and create the Kanban workflow through Bard closeout.
```

Expected result:
- `High Marshal` creates the graph
- the board visibly populates
- only the first eligible task is runnable

## Automated Mode

The correct automated mode is:
- the Hermes root cron scheduler owns the clock
- a `09:00` local weekday cron job runs a deterministic High Marshal kickoff script
- that kickoff creates the live `altrader` graph with `created_by = highmarshal`
- a `*/10 * * * *` Hermes cron job runs deterministic `Steward` recovery when the board is non-empty and contains at least one task that is not `done`
- a `17:00` local weekday cron job runs a deterministic Archivist archive script
- the graph then drives the day through dependencies
- both jobs are suppressed on weekends and US stock market holidays by a deterministic market-day guard

## What Automation Must Not Do

- must not run the full monolithic daily-cycle pipeline as the primary runtime controller
- must not externally seed a fully built live board as the standard daily behavior
- must not bypass Kanban by directly running all specialist scripts in order

## Recommended Scheduled Jobs

### Trading-day kickoff

- Frequency: `0 9 * * 1-5` in Hermes cron
- Action: run `scripts/operations/run_highmarshal_scheduled_kickoff.py`
- Behavior:
  - exit silently on weekends and US market holidays
  - otherwise create the current-run graph on board `altrader`

### Capitol Trades polling

- Frequency: every 30 minutes or as policy requires
- Action: may create or update source-intake-related signals, but must not bypass the main orchestrated workflow

### Health/watchdog checks

- Frequency: several times daily
- Action: runtime health verification only

### Steward recovery

- Frequency: `*/10 * * * *` in Hermes cron
- Action: run `scripts/operations/run_steward_board_recovery.py`
- Behavior:
  - do nothing if the `altrader` board is empty
  - do nothing if every board task is `done`
  - otherwise run one deterministic recovery pass against non-`done` tasks only
  - never override risk, approval, or execution policy

### Archivist cleanup

- Frequency: `0 17 * * 1-5` in Hermes cron
- Action: run `scripts/operations/run_archivist_scheduled_archive.py`
- Behavior:
  - exit silently on weekends and US market holidays
  - archive `done` tasks only
  - refresh the Hermes Desktop board mirror

## Automation Success Criteria

Automation is correct only when:
- the root task appears on the board
- the scheduled kickoff creates the graph with `created_by = highmarshal`
- `Steward` can recover stranded non-`done` work without inventing new trading decisions
- specialists progress through dependency-unlocked tasks
- `Bard` emits final reporting
- `Archivist` does not interfere with in-flight work

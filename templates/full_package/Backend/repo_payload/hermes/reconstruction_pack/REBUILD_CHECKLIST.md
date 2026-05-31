# Rebuild Checklist

Use this checklist when reconstructing AlTrader Hermes from scratch.

## 1. Restore repo artifacts

- confirm repo is present
- confirm [agent_md_update_pack](C:\Users\hawkc\Documents\Codex\2026-05-17\what-is-the-proper-wasy-to\AlTrader\hermes\agent_md_update_pack) exists
- confirm deterministic scripts under [scripts](C:\Users\hawkc\Documents\Codex\2026-05-17\what-is-the-proper-wasy-to\AlTrader\scripts) exist
- confirm tests under [tests](C:\Users\hawkc\Documents\Codex\2026-05-17\what-is-the-proper-wasy-to\AlTrader\tests) exist

## 2. Restore Hermes identities and profiles

- root runtime identity is `Alvin`
- create `High Marshal`, `Chirurgeon`, `Scryer`, `Runesmith`, `Augur`, `Coinmaster`, `Warden`, `Overlord`, `Gatekeeper`, `Tracker`, `Bard`, and `Archivist`
- apply each role’s markdown context from the pack

## 3. Restore credentials and secure environment

- OpenAI/ChatGPT endpoint and key
- Alpaca paper credentials
- AgentMail credentials
- Telegram bot token and target channel/user

## 4. Restore specialist boundaries

- `High Marshal` = orchestrator only
- `Warden` = risk only
- `Bard` = reporting only
- `Archivist` = archive only

## 5. Restore automation

- startup/runtime watchdogs as needed
- Telegram gateway supervision
- daily `Archivist` archive schedule
- trading-day kickoff schedule for future automation

## 6. Rebuild workflow behavior

- ensure `High Marshal` creates the graph at kickoff
- ensure tasks are created with dependencies attached
- ensure only the first task is runnable at start
- ensure workers complete or block their own cards
- ensure no-action outcomes are treated as `done`

## 7. Validate notifications

- direct Telegram send test from `Alvin`
- AgentMail send test from `Bard`
- provider-capacity Telegram alert test

## 8. Validate live board behavior

- manual kickoff chat to `High Marshal`
- confirm tasks visibly appear on board
- confirm progression from `todo` to `ready` to `running` to `done` or `blocked`
- confirm `Bard` final report task appears and completes

## 9. Validate end-to-end paper trade

- confirm at least one paper trade can be executed
- confirm bracket structure includes stop loss and take profit
- confirm final notifications are delivered

## 10. Validate cleanup policy

- confirm `Archivist` archives only `done` tasks
- confirm automatic cleanup runs once daily only
- confirm manual out-of-band cleanup requires explicit operator request

# AlTrader Standup Bundle

This bundle is split into two machine roles:

- `Desktop`
  - Windows workstation running Hermes Desktop
  - operator-facing Office, Kanban, schedules, and agent runtime presentation
  - connects to the Spark backend over Tailscale
- `Backend`
  - NVIDIA DGX Spark running Hermes Agent and AlTrader
  - owns Telegram, AgentMail, Alpaca, cron, Kanban execution, reports, and state

Use each folder only on its intended machine.

# Backend Included Files

This backend package intentionally includes only canonical backend files:

- AlTrader runtime scripts that are part of the current design
- configuration examples and deployment docs
- Hermes/AlTrader Kanban and reconstruction docs
- root Alvin runtime markdown templates
- canonical AlTrader agent profile files
- Hermes cron helper scripts used by the backend scheduler

This backend package intentionally excludes:

- caches
- logs
- report output
- broker or messaging secrets
- temporary state
- stale backup bundles
- obsolete watchdog launchers
- generated databases
- dead or superseded seed helpers not used by the current graph-driven design

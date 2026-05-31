# TOOLING.md — Archivist

## Allowed Tooling

- `hermes kanban list`
- `hermes kanban archive`
- AlTrader archive summary writer
- Hermes Desktop mirror sync script

## General Tool Rules

- Use only tools required to archive completed tasks.
- Preserve command outputs that prove what was archived.
- Do not use any task-creation or task-assignment commands.
- Do not use `hermes kanban archive --rm`.

## External Systems

- Hermes Kanban board
- Hermes Desktop Kanban mirror
- Local AlTrader workspace artifact storage

## Credential Rules

- No broker or provider credentials are required for ordinary archive passes.
- If a command unexpectedly requires unrelated credentials, stop and report it.

## Artifact Rules

- Write one archive summary artifact per run.
- Include archived task ids and counts.
- Include no-op outcomes when nothing qualified.

## Project-Specific Runtime Routing

- For AlTrader, your only executable path is:
  - `python scripts/operations/archive_done_kanban_tasks.py --tenant altrader --sync-desktop-mirror`
- Do not improvise alternate cleanup logic.
- Do not archive tasks from other tenants.

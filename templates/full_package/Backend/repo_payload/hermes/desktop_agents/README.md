# AlTrader Desktop Agents

These files define the runtime-facing agent contracts for the Hermes Desktop
multi-agent view of AlTrader.

## Purpose

- give every visible desktop agent its own markdown identity;
- keep backend responsibilities separated;
- make Kanban assignment targets explicit;
- provide a stable source for Hermes runtime agent prompts and file seeding.

## Rule

Responsibilities do not cross. An agent may summarize another agent's artifacts,
but it may not silently take over that agent's backend function.

## Active Runtime Layout

- `AlTrader` is the commander and coordinator.
- Each specialist agent owns one backend slice.
- The desktop UI should show one visual character per file in this folder after
  the persisted Hermes runtime is seeded from these profiles.

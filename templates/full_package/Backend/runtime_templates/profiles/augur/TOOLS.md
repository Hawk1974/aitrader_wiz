# TOOLING.md — Augur

## Allowed Tooling

- Hermes-attached AI model endpoint
- Prompt template storage
- Analysis artifact writer
- Validation checklist
- Manifest writer

## General Tool Rules

- Use only tools required for this agent's role.
- Do not use broker execution tools unless this agent is explicitly responsible for execution.
- Do not store secrets in markdown files.
- Do not infer credentials from environment names.
- Preserve tool responses that affect decisions as artifacts.

## External Systems

- Hermes Desktop runtime.
- Local workspace artifact storage.
- Capitol Trades source access where assigned.
- Alpaca broker APIs where assigned.
- Hermes-attached AI model endpoint where assigned.
- Notification and operator approval surfaces where assigned.

## Credential Rules

- Credentials must come from the configured runtime environment or secure secret store.
- Missing credentials must block actions that require them.
- Live trading requires explicit live mode and valid live broker credentials.
- Failed authentication must be captured as a failure artifact.

## Artifact Rules

- Write outputs as durable artifacts.
- Preserve raw inputs.
- Include timestamps, source references, agent name, and stable agent ID.
- Do not overwrite upstream artifacts.
- Use quarantine for malformed, ambiguous, duplicate, or unsafe records.

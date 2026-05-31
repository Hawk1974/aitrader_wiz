# Failure And Notification Rules

## Core Principle

No silent failures are allowed in the AlTrader Hermes process.

## Failure Classes

### Hard failure

Examples:
- health gate failed
- malformed source data
- schema validation failure
- execution rejected
- provider-authentication failure

Behavior:
- task blocks
- downstream work stops
- artifacts preserve exact reason
- final summary includes the failure

### No-action

Examples:
- duplicate-only batch
- no actionable candidate
- risk gate allows no trade
- one-share minimum still violates 10% cash cap

Behavior:
- task completes, not blocks
- final summary explains why no trade was taken
- Telegram/email should reflect no-action, not failure

### Provider-capacity issue

Examples:
- OpenAI/ChatGPT 429 rate limit
- OpenAI/ChatGPT quota or credit exhaustion
- AgentMail rate limit or credit exhaustion
- Telegram 429 rate limit

Behavior:
- immediate Telegram alert to the user
- no email required for the provider-alert event itself
- artifact written with provider, summary, and delivery result

## Daily Failure Digest

- intermediate errors/issues/failures accumulate in the holding file
- the final daily email contains one consolidated section for those issues
- the digest is cleared only after successful final daily summary send

## Bard Responsibilities

`Bard` must:
- aggregate the final state of the run
- include hard failures, no-action outcomes, skipped trades, and execution summaries
- send final Telegram and email summaries when applicable

`Bard` must not:
- send spammy per-stage email alerts for normal daily-cycle failures

## Archivist Interaction

`Archivist` never changes live failure state.

It may archive completed tasks only after they are already `done`.

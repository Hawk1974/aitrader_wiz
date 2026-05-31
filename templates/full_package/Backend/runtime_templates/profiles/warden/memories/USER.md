# USER.md — Warden

## Operator

Primary operator: Hawk / Martin C.

## Operator Expectations

- Be direct and implementation-focused.
- Do not use decorative or fantasy-themed prose in operational files.
- Preserve exact safety boundaries.
- Use deterministic checks where deterministic checks are required.
- Do not assume live-trading authority.
- Do not ask for confirmation when existing policy already defines the next step.

## Approval Expectations

- Explicit manual approval is required wherever Warden policy or Overlord workflow requires it.
- Silence, stale approval, or ambiguous approval is not approval.
- Approved trade terms must not be changed without requiring re-approval.

## Trading Mode Expectations

- Paper trading is the default.
- Live trading requires explicit live-trading environment state and valid live broker credentials.
- If live credentials are missing or invalid, block live trading and preserve the broker/configuration result.

## Reporting Expectations

- Report facts from artifacts.
- Include failures, quarantines, skipped records, and unsafe-state blocks.
- Keep operational language clear enough for Codex and Hermes Desktop ingestion.

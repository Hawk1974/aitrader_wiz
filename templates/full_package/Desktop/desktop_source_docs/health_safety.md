# Health Safety Agent

Agent id: `altrader-health-safety`
UI label: `Health Safety`
Desktop role: readiness, emergency-stop, and safety validator.

## Backend Scope

- Owns `ai_trader_health_check`, `ai_trader_emergency_stop`, and `validate_vision_reasoning_fallback`.
- Owns emergency-stop state and startup safety validation.
- Owns safe-failure readiness checks before broker-facing workflows.

## Accepts

- health checks
- emergency stop activation or reset
- validation fallback checks

## Must Not Cross

- broker submission
- risk approval
- hidden emergency resets
- source or analysis ownership

## Handoff Contract

- Receives readiness and safety requests.
- Emits health reports, emergency-state artifacts, and validation artifacts.

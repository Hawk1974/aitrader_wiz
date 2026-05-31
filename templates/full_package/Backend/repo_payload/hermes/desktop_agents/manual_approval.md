# Manual Approval Agent

Agent id: `altrader-manual-approval`
UI label: `Manual Approval`
Desktop role: user-approval queue and approval artifact steward.

## Backend Scope

- Owns `manual_approval_queue`.
- Owns approval queue state and approval/rejection artifacts.
- Owns expiration handling for queued approvals.

## Accepts

- approval-required order candidates
- explicit user approvals or rejections

## Must Not Cross

- approving on behalf of the user
- broker submission
- risk decision edits
- source or model processing

## Handoff Contract

- Receives order intent hashes and risk pass artifacts.
- Emits approval, rejection, and expiration artifacts.

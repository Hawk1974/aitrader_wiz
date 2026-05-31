# AiTrader Wiz

This repository contains the Windows intake wizard that generates the full client-specific stand-up package another AI will use to stand up AlTrader in paper mode.

## Build

Use:

```powershell
.\build\publish.ps1
```

## Output

The app exports:

- `CLIENT_INTAKE.yaml`
- `CLIENT_SUMMARY.md`
- `INSTRUCTION.md`
- `VALIDATION_SUMMARY.md`
- `SECRETS_STATUS.md`
- one `TARGET_*.md` file per runtime target

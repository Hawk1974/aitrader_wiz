# AiTrader Wiz

`AiTrader Wiz` is a Windows .NET intake wizard that creates the client-specific YAML and Markdown overlay files another AI will use, together with the static `AlTrader` repo zip, to stand up AlTrader in paper mode.

It does not install Hermes, does not configure the backend, does not configure the desktop, does not modify the `AlTrader` repo zip, and does not enable live trading.

## Repo Layout

- `src/AiTrader.Wiz/`
  - WPF desktop application
- `src/AiTrader.Wiz.Core/`
  - topology, validation, rendering, and export logic
- `tests/AiTrader.Wiz.UnitTests/`
- `tests/AiTrader.Wiz.IntegrationTests/`
- `tests/AiTrader.Wiz.ContractTests/`
- `build/publish.ps1`
  - self-contained Windows publish script

## Build

```powershell
.\build\publish.ps1
```

The local published executable is written to:

```text
dist\win-x64\AiTraderWiz.exe
```

## Generated Outputs

- `CLIENT_INTAKE.yaml`
- `CLIENT_SUMMARY.md`
- `INSTRUCTION.md`
- `VALIDATION_SUMMARY.md`
- `SECRETS_STATUS.md`
- one `TARGET_*.md` file per runtime target

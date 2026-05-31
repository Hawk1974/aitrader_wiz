$ErrorActionPreference = "Stop"

$repairScript = "C:\Users\hawkc\.hermes\repair-office-runtime.ps1"

try {
  if (-not (Test-Path -LiteralPath $repairScript)) {
    throw "Required repair script not found: $repairScript"
  }

  Write-Host ""
  Write-Host "Hermes Local DT Port Fix" -ForegroundColor Cyan
  Write-Host "Repairing Hermes Office runtime on ports 3000 and 18789..." -ForegroundColor Yellow
  Write-Host ""

  $resultJson = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $repairScript
  $result = $resultJson | ConvertFrom-Json

  Write-Host "Repair completed." -ForegroundColor Green
  Write-Host ""
  Write-Host ("Office:  http://localhost:{0}  (PID {1})" -f $result.office_port, $result.office_pid)
  Write-Host ("Adapter: ws://localhost:{0} (PID {1})" -f $result.adapter_port, $result.adapter_pid)
  Write-Host ""
  Write-Host "If Hermes Desktop still shows stale state, refresh Office or reopen Hermes Desktop." -ForegroundColor DarkYellow
}
catch {
  Write-Host ""
  Write-Host "Hermes Local DT Port Fix failed." -ForegroundColor Red
  Write-Host $_.Exception.Message -ForegroundColor Red
}

Write-Host ""
Read-Host "Press Enter to close"

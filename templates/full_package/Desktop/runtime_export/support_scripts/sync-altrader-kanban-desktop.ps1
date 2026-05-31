$repoRoot = "C:\Users\hawkc\Documents\Codex\2026-05-17\what-is-the-proper-wasy-to\AlTrader"
$python = Join-Path $HOME ".hermes\hermes-agent\venv\Scripts\python.exe"
$script = Join-Path $repoRoot "scripts\operations\sync_hermes_kanban_desktop_mirror.py"
$logDir = Join-Path $HOME ".hermes\logs"
$logPath = Join-Path $logDir "sync-altrader-kanban-desktop.log"

New-Item -ItemType Directory -Force -Path $logDir | Out-Null

try {
    & $python $script 2>&1 | Tee-Object -FilePath $logPath -Append | Out-Null
} catch {
    $_ | Out-String | Tee-Object -FilePath $logPath -Append | Out-Null
    exit 1
}

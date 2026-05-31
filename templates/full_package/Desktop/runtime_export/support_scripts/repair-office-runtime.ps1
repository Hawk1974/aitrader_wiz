$ErrorActionPreference = "Stop"

$HermesHome = "C:\Users\hawkc\.hermes"
$OfficeDir = Join-Path $HermesHome "hermes-office"
$DevPidPath = Join-Path $HermesHome "claw3d-dev.pid"
$AdapterPidPath = Join-Path $HermesHome "claw3d-adapter.pid"
$PortPath = Join-Path $HermesHome "claw3d-port"
$WsUrlPath = Join-Path $HermesHome "claw3d-ws-url"
$OutLog = Join-Path $HermesHome "desktop-adapter-start.out.log"
$ErrLog = Join-Path $HermesHome "desktop-adapter-start.err.log"

function Get-ProcessByPid([int]$TargetProcessId) {
  Get-CimInstance Win32_Process -Filter "ProcessId = $TargetProcessId" -ErrorAction SilentlyContinue
}

function Get-ListeningPid([int]$Port) {
  $conn = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue |
    Select-Object -First 1
  if ($null -eq $conn) { return $null }
  return [int]$conn.OwningProcess
}

function Get-ProcessTree([int]$LeafPid) {
  $rows = @()
  $seen = @{}
  $current = $LeafPid
  for ($i = 0; $i -lt 8 -and $current -and -not $seen.ContainsKey($current); $i++) {
    $seen[$current] = $true
    $proc = Get-ProcessByPid $current
    if ($null -eq $proc) { break }
    $rows += $proc
    $current = [int]$proc.ParentProcessId
  }
  return $rows
}

function Test-CommandLine($proc, [string[]]$Patterns) {
  if ($null -eq $proc) { return $false }
  $cmd = [string]$proc.CommandLine
  foreach ($pattern in $Patterns) {
    if ($cmd -match $pattern) { return $true }
  }
  return $false
}

function Resolve-HermesOfficeOwner([int]$Port, [string[]]$AllowedPatterns) {
  $listenPid = Get-ListeningPid $Port
  if ($null -eq $listenPid) { return $null }
  $proc = Get-ProcessByPid $listenPid
  if (-not (Test-CommandLine $proc $AllowedPatterns)) {
    throw "Port ${Port} is in use by unexpected process ${listenPid}: $($proc.CommandLine)"
  }
  return $proc
}

function Ensure-AdapterRunning {
  $adapterOwner = $null
  try {
    $adapterOwner = Resolve-HermesOfficeOwner 18789 @("server/hermes-gateway-adapter\.js")
  } catch {
    throw
  }

  if ($null -ne $adapterOwner) {
    return $adapterOwner
  }

  if (-not (Test-Path $OfficeDir)) {
    throw "Hermes Office directory not found: $OfficeDir"
  }

  $proc = Start-Process -FilePath "cmd.exe" `
    -ArgumentList "/c", "npm run hermes-adapter" `
    -WorkingDirectory $OfficeDir `
    -WindowStyle Hidden `
    -RedirectStandardOutput $OutLog `
    -RedirectStandardError $ErrLog `
    -PassThru

  Start-Sleep -Seconds 6
  $adapterOwner = Resolve-HermesOfficeOwner 18789 @("server/hermes-gateway-adapter\.js")
  if ($null -eq $adapterOwner) {
    throw "Adapter did not bind port 18789."
  }
  return $adapterOwner
}

function Write-RuntimeMarkers([int]$DevPid, [int]$AdapterPid) {
  Set-Content -LiteralPath $DevPidPath -Value $DevPid -Encoding ascii
  Set-Content -LiteralPath $AdapterPidPath -Value $AdapterPid -Encoding ascii
  Set-Content -LiteralPath $PortPath -Value "3000" -Encoding ascii
  Set-Content -LiteralPath $WsUrlPath -Value "ws://localhost:18789" -Encoding ascii
}

$devOwner = Resolve-HermesOfficeOwner 3000 @("server/index\.js --dev")
$adapterOwner = Ensure-AdapterRunning

Write-RuntimeMarkers -DevPid ([int]$devOwner.ProcessId) -AdapterPid ([int]$adapterOwner.ProcessId)

$result = [pscustomobject]@{
  office_port = 3000
  office_pid = [int]$devOwner.ProcessId
  office_command = [string]$devOwner.CommandLine
  adapter_port = 18789
  adapter_pid = [int]$adapterOwner.ProcessId
  adapter_command = [string]$adapterOwner.CommandLine
  dev_pid_file = $DevPidPath
  adapter_pid_file = $AdapterPidPath
  ws_url = "ws://localhost:18789"
  status = "ok"
}

$result | ConvertTo-Json -Depth 3

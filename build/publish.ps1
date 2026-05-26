Param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\AiTrader.Wiz\AiTrader.Wiz.csproj"
$output = Join-Path $root "dist\win-x64"

dotnet publish $project `
  -c $Configuration `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o $output

Write-Host "Published to $output"

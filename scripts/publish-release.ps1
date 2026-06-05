# Publishes a distributable Windows build of the agent — a single-file, framework-dependent
# `AgentRunner.exe` (win-x64). This is the right "download & run" path for a Windows DESKTOP tool:
# a cross-platform `dotnet tool` is NOT viable here because the agent depends on FlaUI/WinForms/WPF,
# and PackAsTool rejects `net8.0-windows` + UseWPF/UseWindowsForms (see .claude/DISCOVERY_LOG.md).
#
# Framework-dependent (small): the target machine needs the .NET 8 **Desktop** Runtime. For a no-runtime
# install, pass -SelfContained (bigger exe, no prerequisite).
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File scripts/publish-release.ps1
#   powershell -ExecutionPolicy Bypass -File scripts/publish-release.ps1 -SelfContained -Zip
[CmdletBinding()]
param(
    [string]$OutDir = "artifacts/release",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained,
    [switch]$Zip,
    [switch]$Smoke
)
$ErrorActionPreference = "Stop"

$root = Split-Path $PSScriptRoot -Parent
$proj = Join-Path $root "src/AgentRunner/AgentRunner.csproj"
$out  = Join-Path $root $OutDir
$sc   = if ($SelfContained) { "true" } else { "false" }

Write-Host "Publishing AgentRunner.exe ($Runtime, self-contained=$sc) -> $out" -ForegroundColor Cyan
& dotnet publish $proj -c Release -f net8.0-windows -r $Runtime `
    --self-contained $sc `
    -p:PublishSingleFile=true `
    -o $out
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)." }

$exe = Join-Path $out "AgentRunner.exe"
if (-not (Test-Path $exe)) { throw "Expected $exe was not produced." }
Write-Host "`nPublished:" $exe -ForegroundColor Green
Write-Host "Run it from a folder that has a tests/ directory, e.g.:" -ForegroundColor Yellow
Write-Host "  .\AgentRunner.exe --list-tests"
Write-Host "  .\AgentRunner.exe --dashboard 8090"

if ($Zip) {
    $zip = Join-Path $root "$OutDir.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path (Join-Path $out "*") -DestinationPath $zip
    Write-Host "`nZipped release:" $zip -ForegroundColor Green
}

if ($Smoke) {
    Write-Host "`nSmoke: AgentRunner.exe --validate-plan --format json (from repo root)" -ForegroundColor Cyan
    Push-Location $root
    try { & $exe --validate-plan --format json | Select-Object -First 1 }
    finally { Pop-Location }
}

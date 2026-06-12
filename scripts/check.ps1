$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $repoRoot
try {
    $psExe = (Get-Process -Id $PID).Path
    & $psExe -ExecutionPolicy Bypass -File .\scripts\check-yaml-facts.ps1

    Write-Host "Restoring solution..." -ForegroundColor Cyan
    dotnet restore .\DesktopAiTestAgent.sln

    Write-Host "Building agent (net48)..." -ForegroundColor Cyan
    dotnet build .\src\AgentRunner\AgentRunner.csproj -f net48

    Write-Host "Building agent (net8.0-windows)..." -ForegroundColor Cyan
    dotnet build .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows

    Write-Host "Building WinForms Net48 sample..." -ForegroundColor Cyan
    dotnet build .\src\Samples\Sample.WinFormsApp.Net48\Sample.WinFormsApp.Net48.csproj

    Write-Host "Building WinForms Net8 sample..." -ForegroundColor Cyan
    dotnet build .\src\Samples\Sample.WinFormsApp.Net8\Sample.WinFormsApp.Net8.csproj
} finally {
    Pop-Location
}

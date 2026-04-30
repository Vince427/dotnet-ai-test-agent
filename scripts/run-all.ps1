# run-all.ps1
# Runs the AgentRunner against all 5 target frameworks sequentially.

$ErrorActionPreference = "Stop"

$Targets = @(
    @{ Name = "WinForms (.NET 8)"; App = "src\Samples\Sample.WinFormsApp.Net8\bin\Debug\net8.0-windows\Sample.WinFormsApp.Net8.exe"; Title = "Sample Login App (.NET 8)" },
    @{ Name = "WinForms (.NET 4.8)"; App = "src\Samples\Sample.WinFormsApp.Net48\bin\Debug\net48\Sample.WinFormsApp.Net48.exe"; Title = "Sample Login App (.NET Framework 4.8)" },
    @{ Name = "WPF (.NET 8)"; App = "src\Samples\Sample.WpfApp\bin\Debug\net8.0-windows\Sample.WpfApp.exe"; Title = "WPF AI Test Target" },
    @{ Name = "WPF (.NET 4.8)"; App = "src\Samples\Sample.WpfApp.Net48\bin\Debug\net48\Sample.WpfApp.Net48.exe"; Title = "WPF AI Test Target (.NET 4.8)" }
)

# Skip build to avoid file locks since we are already built
# dotnet build DesktopAiTestAgent.sln

foreach ($target in $Targets) {
    Write-Host "`n=======================================================" -ForegroundColor Cyan
    Write-Host "Testing Target: $($target.Name)" -ForegroundColor Cyan
    Write-Host "=======================================================" -ForegroundColor Cyan

    # Ensure clean state
    Stop-Process -Name "AgentRunner" -Force -ErrorAction SilentlyContinue
    $processName = [System.IO.Path]::GetFileNameWithoutExtension($target.App)
    Stop-Process -Name $processName -Force -ErrorAction SilentlyContinue

    Write-Host "Launching $($target.Name)..."
    $appProcess = Start-Process -FilePath $target.App -PassThru

    Start-Sleep -Seconds 2

    Write-Host "Launching Agent against '$($target.Title)'..."
    $agentProcess = Start-Process -FilePath "src\AgentRunner\bin\Debug\net8.0-windows\AgentRunner.exe" -ArgumentList "`"$($target.Title)`"" -NoNewWindow -Wait

    Write-Host "Killing $($target.Name)..."
    Stop-Process -Id $appProcess.Id -Force -ErrorAction SilentlyContinue
}

Write-Host "`nAll tests completed!" -ForegroundColor Green

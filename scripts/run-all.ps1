# run-all.ps1
# Runs the AgentRunner against the available sample target frameworks sequentially.

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $repoRoot
try {
    $ensureFresh = Join-Path $PSScriptRoot 'ensure-fresh.ps1'
    $agentExe = "src\AgentRunner\bin\Debug\net8.0-windows\AgentRunner.exe"

    $Targets = @(
        @{ Name = "WinForms (.NET 8)"; App = "src\Samples\Sample.WinFormsApp.Net8\bin\Debug\net8.0-windows\Sample.WinFormsApp.Net8.exe"; Proj = "src\Samples\Sample.WinFormsApp.Net8\Sample.WinFormsApp.Net8.csproj"; Tfm = "net8.0-windows"; Title = "Sample Login App (.NET 8)" },
        @{ Name = "WinForms (.NET 4.8)"; App = "src\Samples\Sample.WinFormsApp.Net48\bin\Debug\net48\Sample.WinFormsApp.Net48.exe"; Proj = "src\Samples\Sample.WinFormsApp.Net48\Sample.WinFormsApp.Net48.csproj"; Tfm = "net48"; Title = "Sample Login App (.NET Framework 4.8)" },
        @{ Name = "WPF (.NET 8)"; App = "src\Samples\Sample.WpfApp\bin\Debug\net8.0-windows\Sample.WpfApp.exe"; Proj = "src\Samples\Sample.WpfApp\Sample.WpfApp.csproj"; Tfm = "net8.0-windows"; Title = "WPF AI Test Target" },
        @{ Name = "WPF (.NET 4.8)"; App = "src\Samples\Sample.WpfApp.Net48\bin\Debug\net48\Sample.WpfApp.Net48.exe"; Proj = "src\Samples\Sample.WpfApp.Net48\Sample.WpfApp.Net48.csproj"; Tfm = "net48"; Title = "WPF AI Test Target (.NET 4.8)" }
    )

    # Rebuild only what is stale (ensure-fresh) instead of skipping the build outright:
    # launching a prebuilt exe directly would otherwise silently run old code.
    # -SourceRoot src so a change in a referenced project (Core, UIAutomation) is also caught.
    & $ensureFresh -Project "src\AgentRunner\AgentRunner.csproj" -Exe $agentExe -Framework "net8.0-windows" -SourceRoot "src"

    foreach ($target in $Targets) {
        Write-Host "`n=======================================================" -ForegroundColor Cyan
        Write-Host "Testing Target: $($target.Name)" -ForegroundColor Cyan
        Write-Host "=======================================================" -ForegroundColor Cyan

        # Ensure clean state
        Stop-Process -Name "AgentRunner" -Force -ErrorAction SilentlyContinue
        $processName = [System.IO.Path]::GetFileNameWithoutExtension($target.App)
        Stop-Process -Name $processName -Force -ErrorAction SilentlyContinue

        # Guard against a stale target binary (processes for it are now stopped, so no lock).
        & $ensureFresh -Project $target.Proj -Exe $target.App -Framework $target.Tfm

        Write-Host "Launching $($target.Name)..."
        $appProcess = Start-Process -FilePath $target.App -PassThru

        try {
            Start-Sleep -Seconds 2  # sleep-ok: UI settle for the just-launched sample window before driving it (no end-signal to poll)

            Write-Host "Launching Agent against '$($target.Title)'..."
            $agentProcess = Start-Process -FilePath $agentExe -ArgumentList "`"$($target.Title)`"" -NoNewWindow -Wait -PassThru
            if ($agentProcess.ExitCode -ne 0) {
                throw "Agent failed for $($target.Name) with exit code $($agentProcess.ExitCode)."
            }
        }
        finally {
            if ($appProcess -and -not $appProcess.HasExited) {
                Write-Host "Killing $($target.Name)..."
                Stop-Process -Id $appProcess.Id -Force -ErrorAction SilentlyContinue
            }
        }
    }

    Write-Host "`nAll tests completed!" -ForegroundColor Green
} finally {
    Pop-Location
}

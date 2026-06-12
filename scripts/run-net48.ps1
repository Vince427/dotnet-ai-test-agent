$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $repoRoot
try {
    function Invoke-Checked {
        param(
            [string]$FilePath,
            [string[]]$Arguments
        )

        & $FilePath @Arguments
        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }
    }

    Write-Host 'Restoring solution...'
    Invoke-Checked dotnet @('restore', '.\DesktopAiTestAgent.sln')

    Write-Host 'Building .NET Framework 4.8 sample and shared projects...'
    Invoke-Checked dotnet @('build', '.\src\Samples\Sample.WinFormsApp.Net48\Sample.WinFormsApp.Net48.csproj')

    Invoke-Checked dotnet @('build', '.\src\AgentRunner\AgentRunner.csproj', '-f', 'net48')

    Write-Host 'Starting .NET Framework 4.8 sample app...'
    $sample = Start-Process dotnet -ArgumentList 'run --project .\src\Samples\Sample.WinFormsApp.Net48\Sample.WinFormsApp.Net48.csproj' -PassThru
    $agentExitCode = 1

    try {
        Start-Sleep -Seconds 4
        Write-Host 'Running shared agent runner against .NET Framework 4.8 sample...'
        dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net48 -- "Sample Login App (.NET Framework 4.8)"
        $agentExitCode = $LASTEXITCODE
    }
    finally {
        Start-Sleep -Milliseconds 500
        if ($sample -and !$sample.HasExited) {
            Stop-Process -Id $sample.Id -Force
        }
    }

    exit $agentExitCode
} finally {
    Pop-Location
}

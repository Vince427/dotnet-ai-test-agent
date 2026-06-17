<#
.SYNOPSIS
    One-command guided demo: build the WinForms sample, launch it, run one test
    with the AI agent, then render and open the AgentLoop Workbench.

.DESCRIPTION
    Wraps the documented runtime flow into a single command:
      build sample + agent -> launch sample -> run selected test -> render workbench -> open it.

    Runtime agent decisions need a local .env (OpenRouter key). Validation, listing,
    and Workbench rendering do not. If .env is missing, the build/launch/render steps
    still work; only the LLM-driven run will fail at the model call.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-demo-login.ps1

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-demo-login.ps1 -TestId EX-WINFORMS-LOGIN-001 -EvidenceLevel full
#>
[CmdletBinding()]
param(
    [string]$Plan = '.\tests\examples\winforms\login.yaml',
    [string]$TestId = 'EX-WINFORMS-LOGIN-001',
    [ValidateSet('minimal', 'standard', 'full')]
    [string]$EvidenceLevel = 'full',
    [string]$Workbench = '.\docs\agentloop.html',
    [int]$StartupWaitSeconds = 4,
    [switch]$NoOpen
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $repoRoot
try {
    function Invoke-Checked {
        param([string]$FilePath, [string[]]$Arguments)
        & $FilePath @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "Command failed (exit $LASTEXITCODE): $FilePath $($Arguments -join ' ')"
        }
    }

    $sampleProject = '.\src\Samples\Sample.WinFormsApp.Net8\Sample.WinFormsApp.Net8.csproj'
    $agentProject = '.\src\AgentRunner\AgentRunner.csproj'

    # 1. .env check (runtime LLM only)
    if (-not (Test-Path '.\.env')) {
        Write-Warning 'No .env found. Runtime agent decisions need an LLM endpoint.'
        Write-Warning 'Copy .env.template to .env and set LLM_API_KEY (OpenRouter) for a real run.'
        Write-Host 'Continuing: build/launch/render still work; the LLM-driven run will fail without a valid key.' -ForegroundColor Yellow
    }

    # 2. Build sample app + agent
    Write-Host 'Restoring and building sample app + agent...' -ForegroundColor Cyan
    Invoke-Checked dotnet @('restore', '.\DesktopAiTestAgent.sln')
    Invoke-Checked dotnet @('build', $sampleProject, '-c', 'Debug')
    Invoke-Checked dotnet @('build', $agentProject, '-f', 'net8.0-windows', '-c', 'Debug')

    # 3. Launch the sample app (separate process)
    Write-Host 'Launching sample app...' -ForegroundColor Cyan
    $sample = Start-Process dotnet -ArgumentList "run --project $sampleProject" -PassThru
    $agentExit = 1

    try {
        Start-Sleep -Seconds $StartupWaitSeconds

        # 4. Run the selected test against the live window
        Write-Host "Running agent test '$TestId' (evidence: $EvidenceLevel)..." -ForegroundColor Cyan
        dotnet run --project $agentProject -f net8.0-windows -- `
            --plan $Plan `
            --test-id $TestId `
            --evidence-level $EvidenceLevel
        $agentExit = $LASTEXITCODE
    }
    finally {
        Start-Sleep -Milliseconds 500
        if ($sample -and -not $sample.HasExited) {
            Write-Host 'Closing sample app...' -ForegroundColor DarkGray
            Stop-Process -Id $sample.Id -Force
        }
    }

    # 5. Render the workbench (no .env / LLM needed) and open it
    Write-Host 'Rendering AgentLoop Workbench...' -ForegroundColor Cyan
    dotnet run --project $agentProject -f net8.0-windows -- --render-ui $Workbench

    if (-not $NoOpen -and (Test-Path $Workbench)) {
        Start-Process (Resolve-Path $Workbench).Path
    }

    Write-Host "Done. Agent exit code: $agentExit" -ForegroundColor Green
    Write-Host "Workbench: $Workbench" -ForegroundColor Green
    exit $agentExit
} finally {
    Pop-Location
}

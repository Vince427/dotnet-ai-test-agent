<#
.SYNOPSIS
    Runs the KeePass real-world E2E case study test plan.

.DESCRIPTION
    Launches KeePass, builds the AgentRunner, and runs the CS-KEEPASS-CREATION-001
    test plan under tests/case-studies/keepass-smoke.yaml.

.PARAMETER EvidenceLevel
    Artifact capture level: minimal, standard, or full (default standard).

.PARAMETER Workbench
    Output path for the workbench UI (default docs\agentloop.html).

.PARAMETER StartupWaitSeconds
    Seconds to wait for KeePass to start before driving it (default 4).

.PARAMETER NoOpen
    Switch to skip opening the workbench in the default browser.
#>
[CmdletBinding()]
param(
    [ValidateSet('minimal', 'standard', 'full')]
    [string]$EvidenceLevel = 'standard',
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

    $agentProject = '.\src\AgentRunner\AgentRunner.csproj'
    $planPath = '.\tests\case-studies\keepass-smoke.yaml'
    $testId = 'CS-KEEPASS-CREATION-001'

    # 1. .env check (runtime LLM only)
    if (-not (Test-Path '.\.env')) {
        Write-Warning 'No .env found. Runtime agent decisions need an LLM endpoint.'
        Write-Warning 'Copy .env.template to .env and set LLM_API_KEY (OpenRouter) for a real run.'
        Write-Host 'Continuing: build/launch/render still work; the LLM-driven run will fail without a valid key.' -ForegroundColor Yellow
    }

    # 2. Check and download KeePass portable if missing
    $keepassDir = '.\keepass'
    $keepassExe = Join-Path $keepassDir 'KeePass.exe'
    if (-not (Test-Path $keepassExe)) {
        Write-Host 'KeePass.exe not found. Downloading portable version...' -ForegroundColor Cyan
        New-Item -ItemType Directory -Force -Path $keepassDir | Out-Null
        $zipPath = Join-Path $keepassDir 'keepass.zip'
        
        Write-Host 'Downloading KeePass 2.56 ZIP...' -ForegroundColor Cyan
        curl.exe -L "https://downloads.sourceforge.net/project/keepass/KeePass%202.x/2.56/KeePass-2.56.zip" -o $zipPath
        
        Write-Host 'Extracting KeePass 2.56 ZIP...' -ForegroundColor Cyan
        Expand-Archive -Path $zipPath -DestinationPath $keepassDir -Force
        Remove-Item $zipPath -Force
    }

    # Ensure enforced configuration is written systematically
    Write-Host 'Configuring KeePass to suppress first-launch popups, local-only, and prevent opening last DB...' -ForegroundColor Cyan
    $configContent = @"
<?xml version="1.0" encoding="utf-8"?>
<Configuration xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
    <Meta>
        <PreferUserConfiguration>false</PreferUserConfiguration>
    </Meta>
    <Application>
        <Start>
            <CheckForUpdate>false</CheckForUpdate>
            <CheckForUpdateConfigured>true</CheckForUpdateConfigured>
            <OpenLastFile>false</OpenLastFile>
        </Start>
    </Application>
</Configuration>
"@
    Set-Content -Path (Join-Path $keepassDir 'KeePass.config.enforced.xml') -Value $configContent -Encoding utf8

    # 3. Build AgentRunner
    Write-Host 'Building AgentRunner...' -ForegroundColor Cyan
    Invoke-Checked dotnet @('build', $agentProject, '-f', 'net8.0-windows', '-c', 'Debug')

    # 4. Launch KeePass
    Write-Host 'Launching KeePass.exe...' -ForegroundColor Cyan
    $process = Start-Process $keepassExe -PassThru
    $agentExit = 1

    try {
        Start-Sleep -Seconds $StartupWaitSeconds

        # 5. Run the selected test against KeePass
        Write-Host "Running agent test '$testId' against KeePass (evidence: $EvidenceLevel)..." -ForegroundColor Cyan
        dotnet run --project $agentProject -f net8.0-windows -- `
            --plan $planPath `
            --test-id $testId `
            --evidence-level $EvidenceLevel
        $agentExit = $LASTEXITCODE
    }
    finally {
        Start-Sleep -Milliseconds 500
        if ($process -and -not $process.HasExited) {
            Write-Host 'Closing KeePass...' -ForegroundColor DarkGray
            Stop-Process -Id $process.Id -Force
        }
    }

    # 6. Render the workbench and open it
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

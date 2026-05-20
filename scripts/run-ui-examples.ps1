[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [ValidateSet('winforms', 'wpf', 'all')]
    [string]$Framework = 'all',

    [ValidateSet('login', 'profile', 'controls', 'modal', 'disabled', 'async', 'all')]
    [string]$Scenario = 'all',

    [ValidateSet('minimal', 'standard', 'full')]
    [string]$EvidenceLevel = 'standard',

    [switch]$Restore,

    [switch]$SkipBuild,

    [switch]$SkipRuntime,

    [Alias('DryRun')]
    [switch]$Preview,

    [ValidateRange(1, 60)]
    [int]$StartupDelaySeconds = 4
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$isDryRun = $Preview -or $WhatIfPreference
$scriptExitCode = 0

$frameworks = @{
    winforms = @{
        Project = '.\src\Samples\Sample.WinFormsApp.Net8\Sample.WinFormsApp.Net8.csproj'
        Exe = '.\src\Samples\Sample.WinFormsApp.Net8\bin\Debug\net8.0-windows\Sample.WinFormsApp.Net8.exe'
        Window = 'Sample Login App (.NET 8)'
    }
    wpf = @{
        Project = '.\src\Samples\Sample.WpfApp\Sample.WpfApp.csproj'
        Exe = '.\src\Samples\Sample.WpfApp\bin\Debug\net8.0-windows\Sample.WpfApp.exe'
        Window = 'WPF AI Test Target'
    }
}

$scenarios = @{
    login = @{
        File = 'login.yaml'
        winforms = 'EX-WINFORMS-LOGIN-001'
        wpf = 'EX-WPF-LOGIN-001'
    }
    profile = @{
        File = 'profile-save.yaml'
        winforms = 'EX-WINFORMS-PROFILE-001'
        wpf = 'EX-WPF-PROFILE-001'
    }
    controls = @{
        File = 'controls-selection.yaml'
        winforms = 'EX-WINFORMS-CONTROLS-001'
        wpf = 'EX-WPF-CONTROLS-001'
    }
    modal = @{
        File = 'modal-confirm.yaml'
        winforms = 'EX-WINFORMS-MODAL-001'
        wpf = 'EX-WPF-MODAL-001'
    }
    disabled = @{
        File = 'protected-action.yaml'
        winforms = 'EX-WINFORMS-DISABLED-001'
        wpf = 'EX-WPF-DISABLED-001'
    }
    async = @{
        File = 'async-loading.yaml'
        winforms = 'EX-WINFORMS-ASYNC-001'
        wpf = 'EX-WPF-ASYNC-001'
    }
}

function Format-CommandLine {
    param(
        [string]$FilePath,
        [string[]]$Arguments
    )

    $escapedArguments = foreach ($argument in $Arguments) {
        if ($argument -match '\s') {
            '"' + $argument.Replace('"', '\"') + '"'
        }
        else {
            $argument
        }
    }

    if ($escapedArguments.Count -eq 0) {
        return $FilePath
    }

    return "$FilePath $($escapedArguments -join ' ')"
}

function Invoke-CheckedCommand {
    param(
        [string]$Label,
        [string]$FilePath,
        [string[]]$Arguments
    )

    $commandLine = Format-CommandLine -FilePath $FilePath -Arguments $Arguments
    Write-Host "==> $Label"
    Write-Host "    $commandLine"

    if ($isDryRun) {
        return 0
    }

    & $FilePath @Arguments
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw "$Label failed with exit code $exitCode."
    }

    return $exitCode
}

function Start-SampleApp {
    param(
        [hashtable]$Profile
    )

    $samplePath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Profile.Exe))
    $commandLine = Format-CommandLine -FilePath $samplePath -Arguments @()

    Write-Host '==> Start sample app'
    Write-Host "    $commandLine"

    if ($isDryRun) {
        return $null
    }

    if (-not (Test-Path $samplePath)) {
        throw "Sample executable not found: $samplePath. Run without -SkipBuild, or build the sample first."
    }

    return Start-Process -FilePath $samplePath -PassThru
}

function Invoke-Example {
    param(
        [string]$FrameworkName,
        [string]$ScenarioName
    )

    $profile = $frameworks[$FrameworkName]
    $scenarioInfo = $scenarios[$ScenarioName]
    $planPath = ".\tests\examples\$FrameworkName\$($scenarioInfo.File)"
    $testId = $scenarioInfo[$FrameworkName]
    $sampleProcess = $null
    $agentExitCode = 0

    try {
        $sampleProcess = Start-SampleApp -Profile $profile

        if (-not $isDryRun) {
            Start-Sleep -Seconds $StartupDelaySeconds
        }

        $agentArguments = @(
            'run',
            '--project',
            '.\src\AgentRunner\AgentRunner.csproj',
            '-f',
            'net8.0-windows',
            '--no-restore',
            '--',
            '--plan',
            $planPath,
            '--test-id',
            $testId,
            '--window',
            $profile.Window,
            '--evidence-level',
            $EvidenceLevel
        )

        $commandLine = Format-CommandLine -FilePath 'dotnet' -Arguments $agentArguments
        Write-Host "==> Run $FrameworkName/$ScenarioName example"
        Write-Host "    $commandLine"

        if (-not $isDryRun) {
            & dotnet @agentArguments
            $agentExitCode = $LASTEXITCODE
        }
    }
    finally {
        if ($sampleProcess -and -not $sampleProcess.HasExited) {
            Write-Host "Stopping sample app PID $($sampleProcess.Id)."
            Stop-Process -Id $sampleProcess.Id -Force
        }
    }

    return $agentExitCode
}

Push-Location $repoRoot
try {
    if ($isDryRun) {
        Write-Host 'Dry run only; commands will be printed but not executed.'
    }

    if ($Restore) {
        Invoke-CheckedCommand -Label 'Restore solution' -FilePath 'dotnet' -Arguments @('restore', '.\DesktopAiTestAgent.sln') | Out-Null
    }

    if (-not $SkipBuild) {
        Invoke-CheckedCommand -Label 'Build solution' -FilePath 'dotnet' -Arguments @('build', '.\DesktopAiTestAgent.sln', '--no-restore', '-v', 'minimal') | Out-Null
    }

    Invoke-CheckedCommand `
        -Label 'Validate YAML plans' `
        -FilePath 'dotnet' `
        -Arguments @(
            'run',
            '--project',
            '.\src\AgentRunner\AgentRunner.csproj',
            '-f',
            'net8.0-windows',
            '--no-restore',
            '--',
            '--validate-plan',
            '--format',
            'json'
        ) | Out-Null

    if ($SkipRuntime) {
        Write-Host 'Skipping runtime desktop automation.'
        return
    }

    $selectedFrameworks = if ($Framework -eq 'all') { @('winforms', 'wpf') } else { @($Framework) }
    $selectedScenarios = if ($Scenario -eq 'all') { @('login', 'profile', 'controls', 'modal', 'disabled', 'async') } else { @($Scenario) }

    foreach ($frameworkName in $selectedFrameworks) {
        foreach ($scenarioName in $selectedScenarios) {
            $exitCode = Invoke-Example -FrameworkName $frameworkName -ScenarioName $scenarioName
            if ($exitCode -ne 0) {
                $scriptExitCode = $exitCode
                break
            }
        }

        if ($scriptExitCode -ne 0) {
            break
        }
    }
}
finally {
    Pop-Location
}

if ($scriptExitCode -ne 0) {
    exit $scriptExitCode
}

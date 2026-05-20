[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [ValidateSet('net8', 'net48')]
    [string]$Sample = 'net8',

    [string]$Suite = 'smoke',

    [string]$TestId = 'LOGIN-001',

    [ValidateSet('minimal', 'standard', 'full')]
    [string]$EvidenceLevel = 'standard',

    [switch]$Restore,

    [switch]$SkipBuild,

    [switch]$SkipPlanValidation,

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

$profiles = @{
    net8 = @{
        SampleExe = '.\src\Samples\Sample.WinFormsApp.Net8\bin\Debug\net8.0-windows\Sample.WinFormsApp.Net8.exe'
        RunnerFramework = 'net8.0-windows'
        WindowTitle = 'Sample Login App (.NET 8)'
    }
    net48 = @{
        SampleExe = '.\src\Samples\Sample.WinFormsApp.Net48\bin\Debug\net48\Sample.WinFormsApp.Net48.exe'
        RunnerFramework = 'net48'
        WindowTitle = 'Sample Login App (.NET Framework 4.8)'
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
        return
    }

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Label failed with exit code $LASTEXITCODE."
    }
}

function Start-SampleApp {
    param(
        [hashtable]$Profile
    )

    $samplePath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Profile.SampleExe))
    $commandLine = Format-CommandLine -FilePath $samplePath -Arguments @()

    Write-Host '==> Start sample app'
    Write-Host "    $commandLine"

    if ($isDryRun) {
        return $null
    }

    if (-not (Test-Path $samplePath)) {
        throw "Sample executable not found: $samplePath. Run without -SkipBuild, or restore/build the project first."
    }

    return Start-Process -FilePath $samplePath -PassThru
}

Push-Location $repoRoot
try {
    $profile = $profiles[$Sample]

    Write-Host "Running Desktop AI Test Agent demo profile '$Sample'."
    if ($isDryRun) {
        Write-Host 'Dry run only; commands will be printed but not executed.'
    }

    if ($Restore) {
        Invoke-CheckedCommand `
            -Label 'Restore solution' `
            -FilePath 'dotnet' `
            -Arguments @('restore', '.\DesktopAiTestAgent.sln')
    }

    if (-not $SkipBuild) {
        Invoke-CheckedCommand `
            -Label 'Build solution' `
            -FilePath 'dotnet' `
            -Arguments @('build', '.\DesktopAiTestAgent.sln', '--no-restore', '-v', 'minimal')
    }

    if (-not $SkipPlanValidation) {
        Invoke-CheckedCommand `
            -Label 'Validate YAML plans' `
            -FilePath 'dotnet' `
            -Arguments @(
                'run',
                '--project',
                '.\src\AgentRunner\AgentRunner.csproj',
                '-f',
                $profile.RunnerFramework,
                '--no-build',
                '--',
                '--validate-plan',
                '--format',
                'json'
            )

        Invoke-CheckedCommand `
            -Label 'List YAML tests' `
            -FilePath 'dotnet' `
            -Arguments @(
                'run',
                '--project',
                '.\src\AgentRunner\AgentRunner.csproj',
                '-f',
                $profile.RunnerFramework,
                '--no-build',
                '--',
                '--list-tests',
                '--format',
                'json'
            )
    }

    if ($SkipRuntime) {
        Write-Host 'Skipping runtime desktop automation.'
        return
    }

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
            $profile.RunnerFramework,
            '--no-build',
            '--',
            '--suite',
            $Suite,
            '--test-id',
            $TestId,
            '--window',
            $profile.WindowTitle,
            '--evidence-level',
            $EvidenceLevel
        )

        $commandLine = Format-CommandLine -FilePath 'dotnet' -Arguments $agentArguments
        Write-Host '==> Run agent against sample app'
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

    $scriptExitCode = $agentExitCode
}
finally {
    Pop-Location
}

if ($scriptExitCode -ne 0) {
    exit $scriptExitCode
}

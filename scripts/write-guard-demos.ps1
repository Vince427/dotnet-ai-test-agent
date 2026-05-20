[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$OutputRoot = '.\runs'
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

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

    if ($WhatIfPreference) {
        return
    }

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Label failed with exit code $LASTEXITCODE."
    }
}

Push-Location $repoRoot
try {
    Invoke-CheckedCommand `
        -Label 'Write guard failure demo artifacts' `
        -FilePath 'dotnet' `
        -Arguments @(
            'run',
            '--project',
            '.\src\AgentRunner\AgentRunner.csproj',
            '-f',
            'net8.0-windows',
            '--no-restore',
            '--',
            '--write-guard-demos',
            $OutputRoot
        )
}
finally {
    Pop-Location
}

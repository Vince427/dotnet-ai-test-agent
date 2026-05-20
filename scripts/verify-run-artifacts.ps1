[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string]$RunDir,

    [ValidateSet('minimal', 'standard', 'full')]
    [string]$EvidenceLevel = 'standard',

    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

$isDryRun = $DryRun -or $WhatIfPreference
$forbiddenSecrets = @('password123', 'hunter2')

function Add-VerificationError {
    param(
        [System.Collections.Generic.List[string]]$Errors,
        [string]$Message
    )

    $Errors.Add($Message) | Out-Null
    Write-Host "ERROR $Message"
}

function Test-FileForSecret {
    param(
        [string]$Path,
        [string[]]$Secrets
    )

    $content = Get-Content -Raw -LiteralPath $Path
    foreach ($secret in $Secrets) {
        if ($content.IndexOf($secret, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            return $secret
        }
    }

    return $null
}

$resolvedRunDir = if ([System.IO.Path]::IsPathRooted($RunDir)) {
    [System.IO.Path]::GetFullPath($RunDir)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $RunDir))
}

Write-Host "Verifying run artifacts in $resolvedRunDir (evidence=$EvidenceLevel)."

if ($isDryRun) {
    Write-Host "Dry run only; would check report.json, summary.md, screenshots/ui-tree for the selected evidence level, and basic secret redaction."
    return
}

$errors = [System.Collections.Generic.List[string]]::new()

if (-not (Test-Path -LiteralPath $resolvedRunDir -PathType Container)) {
    Add-VerificationError -Errors $errors -Message "Run directory not found: $resolvedRunDir"
}

$reportPath = Join-Path $resolvedRunDir 'report.json'
$summaryPath = Join-Path $resolvedRunDir 'summary.md'

if (-not (Test-Path -LiteralPath $reportPath -PathType Leaf)) {
    Add-VerificationError -Errors $errors -Message "Missing report.json."
}

if (-not (Test-Path -LiteralPath $summaryPath -PathType Leaf)) {
    Add-VerificationError -Errors $errors -Message "Missing summary.md."
}

if (Test-Path -LiteralPath $reportPath -PathType Leaf) {
    try {
        $report = Get-Content -Raw -LiteralPath $reportPath | ConvertFrom-Json
        if (-not $report.runId) {
            Add-VerificationError -Errors $errors -Message "report.json is missing runId."
        }
        if (-not $report.result) {
            Add-VerificationError -Errors $errors -Message "report.json is missing result."
        }
    }
    catch {
        Add-VerificationError -Errors $errors -Message "report.json is not valid JSON: $($_.Exception.Message)"
    }
}

$screenshotsDir = Join-Path $resolvedRunDir 'screenshots'
if ($EvidenceLevel -in @('standard', 'full')) {
    if (-not (Test-Path -LiteralPath $screenshotsDir -PathType Container)) {
        Add-VerificationError -Errors $errors -Message "Missing screenshots directory for evidence level '$EvidenceLevel'."
    }
    else {
        $screenshots = @(Get-ChildItem -LiteralPath $screenshotsDir -Filter '*.png' -File)
        if ($screenshots.Count -eq 0) {
            Add-VerificationError -Errors $errors -Message "No PNG screenshots found for evidence level '$EvidenceLevel'."
        }
    }
}

$uiTreeDir = Join-Path $resolvedRunDir 'ui-tree'
if ($EvidenceLevel -eq 'full') {
    if (-not (Test-Path -LiteralPath $uiTreeDir -PathType Container)) {
        Add-VerificationError -Errors $errors -Message "Missing ui-tree directory for evidence level 'full'."
    }
    else {
        $uiTreeFiles = @(Get-ChildItem -LiteralPath $uiTreeDir -Filter '*.json' -File)
        if ($uiTreeFiles.Count -eq 0) {
            Add-VerificationError -Errors $errors -Message "No UI tree JSON snapshots found for evidence level 'full'."
        }
    }
}

$redactionTargets = @()
if (Test-Path -LiteralPath $reportPath -PathType Leaf) {
    $redactionTargets += $reportPath
}
if (Test-Path -LiteralPath $summaryPath -PathType Leaf) {
    $redactionTargets += $summaryPath
}
if (Test-Path -LiteralPath $uiTreeDir -PathType Container) {
    $redactionTargets += @(Get-ChildItem -LiteralPath $uiTreeDir -Filter '*.json' -File | ForEach-Object { $_.FullName })
}

foreach ($targetPath in $redactionTargets) {
    $secret = Test-FileForSecret -Path $targetPath -Secrets $forbiddenSecrets
    if ($secret) {
        Add-VerificationError -Errors $errors -Message "Unredacted secret marker '$secret' found in $targetPath."
    }
}

if ($errors.Count -gt 0) {
    throw "Artifact verification failed with $($errors.Count) error(s)."
}

Write-Host "Artifact verification passed."

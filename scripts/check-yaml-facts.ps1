<#
.SYNOPSIS
  Drift-guard for YAML test specs — verifies that every declared fact in
  facts.yaml survives across all test plan files.

.DESCRIPTION
  Inspired by open-cognitive-bench's drift-guard/gate.py, this script is the
  CI-executable fact-gate for the test plan YAML files. It reads a list of
  required fields (with optional expected values) from scripts/facts.yaml and
  verifies that every test in tests/**/*.yaml (excluding tests/archived/) has
  those fields present and non-empty.

  This is the "gate it — don't prompt it" principle applied at CI time:
  a prompt can only *reduce* fact loss; this script *guarantees* it.

.PARAMETER FactsFile
  Path to the facts definition file. Default: scripts/facts.yaml

.PARAMETER TestsRoot
  Root directory to scan for YAML test plans. Default: tests/

.EXAMPLE
  .\scripts\check-yaml-facts.ps1
  .\scripts\check-yaml-facts.ps1 -FactsFile .\scripts\facts.yaml -TestsRoot .\tests
#>
param(
    [string]$FactsFile = "$PSScriptRoot\facts.yaml",
    [string]$TestsRoot = (Join-Path $PSScriptRoot "..\tests")
)

$ErrorActionPreference = 'Stop'

# --- Minimal YAML parser (no external dependency) ---
# We only need to check that certain keys exist and are non-empty in each test
# block. A full YAML parser is not needed for this gate.

function Get-YamlKeyValue {
    param([string]$Content, [string]$Key)
    
    # 1. Try inline match: "key: value" (handles strings, numbers, inline lists)
    if ($Content -match "(?m)^\s*${Key}\s*:\s*(.+)$") {
        $val = $Matches[1].Trim().Trim('"').Trim("'")
        if ($val -ne "" -and $val -ne "[]" -and $val -ne '""' -and $val -ne "''") {
            return $val
        }
    }
    
    # 2. Try block-style list: "key:" followed by indented "- value" lines
    $lines = $Content -split "\r?\n"
    $foundKey = $false
    $listItems = @()
    
    foreach ($line in $lines) {
        if (-not $foundKey) {
            if ($line -match "^\s*${Key}\s*:\s*(#.*)?$") {
                $foundKey = $true
            }
        }
        else {
            $trimmed = $line.Trim()
            if ($trimmed -eq "" -or $trimmed.StartsWith("#")) {
                continue
            }
            if ($trimmed -match "^\s*-\s*(.+)$") {
                $listItems += $Matches[1].Trim().Trim('"').Trim("'")
            }
            else {
                break
            }
        }
    }
    
    if ($foundKey -and $listItems.Count -gt 0) {
        return ($listItems -join ",")
    }
    
    return $null
}

function Get-TestBlocks {
    param([string]$FilePath)
    $lines = Get-Content $FilePath
    $blocks = @()
    $inTests = $false
    $currentId = $null
    $currentBlock = ""

    foreach ($line in $lines) {
        if ($line -match '^\s*tests\s*:') {
            $inTests = $true
            continue
        }
        
        if ($inTests) {
            $trimmed = $line.Trim()
            if ($trimmed.StartsWith("#") -or $trimmed -eq "") {
                if ($currentId) {
                    $currentBlock += "$line`n"
                }
                continue
            }
            
            $indent = $line.Length - $line.TrimStart().Length
            
            if ($indent -eq 0) {
                $inTests = $false
                if ($currentId) {
                    $blocks += @{ Id = $currentId; Content = $currentBlock }
                    $currentId = $null
                    $currentBlock = ""
                }
                continue
            }
            
            if ($indent -eq 2 -and $line -match '^\s{2}([^#\s][^:]*):') {
                if ($currentId) {
                    $blocks += @{ Id = $currentId; Content = $currentBlock }
                }
                $currentId = $Matches[1].Trim()
                $currentBlock = ""
            }
            elseif ($currentId -and $indent -ge 4) {
                $currentBlock += "$line`n"
            }
        }
    }
    if ($currentId) {
        $blocks += @{ Id = $currentId; Content = $currentBlock }
    }
    return $blocks
}

# --- Load required facts ---
$requiredFields = @("goal", "framework", "max_steps", "allowed_actions")

if (Test-Path $FactsFile) {
    $factsContent = Get-Content $FactsFile -Raw
    # Parse simple list format: "- field_name"
    $parsed = [regex]::Matches($factsContent, '(?m)^\s*-\s*(\S+)')
    if ($parsed.Count -gt 0) {
        $requiredFields = $parsed | ForEach-Object { $_.Groups[1].Value }
    }
}

Write-Host "=== YAML Fact-Gate (drift-guard for test specs) ===" -ForegroundColor Cyan
Write-Host "Required fields: $($requiredFields -join ', ')" -ForegroundColor DarkGray
Write-Host ""

# --- Scan all YAML files ---
$yamlFiles = Get-ChildItem -Path $TestsRoot -Recurse -Include "*.yaml","*.yml" |
    Where-Object { $_.FullName -notmatch '[/\\]archived[/\\]' }

$totalTests = 0
$violations = @()

foreach ($file in $yamlFiles) {
    $relativePath = $file.FullName.Replace((Resolve-Path $TestsRoot).Path, "tests")
    $blocks = Get-TestBlocks -FilePath $file.FullName

    foreach ($block in $blocks) {
        $totalTests++
        foreach ($field in $requiredFields) {
            $value = Get-YamlKeyValue -Content $block.Content -Key $field
            if (-not $value -or $value -eq '[]' -or $value -eq '""' -or $value -eq "''") {
                $violations += @{
                    File  = $relativePath
                    TestId = $block.Id
                    Field  = $field
                    Issue  = "missing or empty"
                }
            }
        }
    }
}

# --- Report ---
Write-Host "Scanned $($yamlFiles.Count) YAML files, $totalTests test definitions." -ForegroundColor Gray

if ($violations.Count -eq 0) {
    Write-Host "`n[PASS] All $totalTests tests have every required fact present." -ForegroundColor Green
    exit 0
}
else {
    Write-Host "`n[FAIL] $($violations.Count) fact violation(s) found:" -ForegroundColor Red
    foreach ($v in $violations) {
        Write-Host "  $($v.File) :: $($v.TestId) -> $($v.Field): $($v.Issue)" -ForegroundColor Yellow
    }
    Write-Host "`nThe gate rejected this state. Fix the YAML facts before merging." -ForegroundColor Red
    Write-Host "Reference: https://github.com/Vince427/open-cognitive-bench (drift-guard principle)" -ForegroundColor DarkGray
    exit 1
}

#Requires -Version 5
<#
  Stop-hook verification gate for Claude Code.
  Exit 2 = blocking: the turn cannot finish, stderr is fed back to Claude.
  Exit 0 = pass / skipped.

  Refinements (plan Partie 15-bis):
   - Escape hatch: AGENT_SKIP_VERIFY=1 skips the gate (intentional WIP stops).
   - OS-aware: the test suite targets net8.0-windows (FlaUI/UIA) -> Windows only.
     On non-Windows hosts the gate is a no-op (can't run the suite anyway).
   - Conditional: only build/test when C# / project files changed this session,
     so docs-only turns stay instant.
#>
$ErrorActionPreference = 'Continue'

if ($env:AGENT_SKIP_VERIFY -eq '1') {
    Write-Host 'verify: skipped (AGENT_SKIP_VERIFY=1)'
    exit 0
}

$isWin = $true
if (Test-Path Variable:\IsWindows) { $isWin = $IsWindows }
if (-not $isWin) {
    Write-Host 'verify: skipped (Windows-only test suite; non-Windows host)'
    exit 0
}

# Repo root = two levels up from .claude/scripts/.
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $root

# Only gate when C#/project files changed (modified, staged, or untracked).
$changed = @(& git status --porcelain 2>$null | Where-Object { $_ -match '\.(cs|csproj|props|targets|sln)$' })
if ($changed.Count -eq 0) {
    Write-Host 'verify: no C#/project changes -> gate skipped'
    exit 0
}

Write-Host "verify: $($changed.Count) C#/project change(s) -> build + test"

& dotnet build .\DesktopAiTestAgent.sln --no-restore -v minimal
if ($LASTEXITCODE -ne 0) {
    [Console]::Error.WriteLine('verify FAILED: build has errors. Fix them before finishing.')
    exit 2
}

& dotnet test .\DesktopAiTestAgent.sln --no-build -v minimal
if ($LASTEXITCODE -ne 0) {
    [Console]::Error.WriteLine('verify FAILED: tests are red. Fix them before finishing.')
    exit 2
}

Write-Host 'verify: green (build + tests pass)'
exit 0

# log-wait.ps1
# Dot-source helpers for robust log-marker waiting.
#
# The trap this avoids: waiting on "does the log contain marker X?" is fooled by a
# marker left by a PREVIOUS run in an append/daily log — a false positive ("done")
# before the current action has even produced its marker. Instead: baseline the
# marker count BEFORE the action, then poll until the count RISES above the baseline.
# Ported from the sibling RIG-TV harness (Get-MarkerCount / Wait-MarkerCountAbove).
#
# NOTE: this repo prefers exit codes + artifact files (report.json) as run signals,
# which are cleaner than log-grep — so this is a UTILITY for the cases that genuinely
# tail a log (e.g. the dashboard live log, a future harness), not wired into a script
# by default.
#
#   . .\scripts\log-wait.ps1
#   $base = Get-MarkerCount -Path $log -Pattern 'run complete'
#   Do-TheAction
#   if (-not (Wait-MarkerCountAbove -Path $log -Pattern 'run complete' -Baseline $base -TimeoutSeconds 30)) { throw 'timed out' }

function Get-MarkerCount {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)] [string]$Path,
        [Parameter(Mandatory = $true)] [string]$Pattern
    )
    if (-not (Test-Path $Path)) { return 0 }
    return @(Select-String -Path $Path -Pattern $Pattern -AllMatches -ErrorAction SilentlyContinue).Count
}

function Wait-MarkerCountAbove {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)] [string]$Path,
        [Parameter(Mandatory = $true)] [string]$Pattern,
        [Parameter(Mandatory = $true)] [int]$Baseline,
        [int]$TimeoutSeconds = 60,
        [int]$PollMs = 500
    )
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if ((Get-MarkerCount -Path $Path -Pattern $Pattern) -gt $Baseline) { return $true }
        Start-Sleep -Milliseconds $PollMs  # sleep-ok: poll interval of a bounded wait-until-condition loop
    }
    return $false
}

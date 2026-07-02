# process-tree.ps1
# Dot-source helper: Stop-ProcessTree kills a process AND all its descendants.
#
# Why: Stop-Process -Id kills only the named process, leaving child processes
# (helpers, crash dialogs, sub-processes a driven app spawned) orphaned — which
# accumulate across repeated runs and cause file/port locks and flaky next runs.
# Ported from the sibling RIG-TV harness (ProcessTreeControl, Toolhelp32), done
# idiomatically for Windows PowerShell 5.1 via a recursive CIM walk (no native exe,
# so no PS 5.1 stderr-wrapping surprises).
#
#   . .\scripts\process-tree.ps1
#   Stop-ProcessTree -Id $proc.Id

function Stop-ProcessTree {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)] [int]$Id)

    if ($Id -le 0) { return }

    # Depth-first: kill descendants BEFORE the parent, so a child can't be re-parented
    # or respawned by a parent that is still alive.
    Get-CimInstance Win32_Process -Filter "ParentProcessId=$Id" -ErrorAction SilentlyContinue |
        ForEach-Object { Stop-ProcessTree -Id $_.ProcessId }

    Stop-Process -Id $Id -Force -ErrorAction SilentlyContinue
}

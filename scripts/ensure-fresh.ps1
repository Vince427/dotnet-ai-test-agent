# ensure-fresh.ps1
# Guard against driving a STALE binary. Compares the produced EXE's LastWriteTime
# against the newest source file under the project (excluding bin/obj); if the EXE
# is missing or older than any source, rebuilds the project and aborts on failure.
#
# Ported from the sibling RIG-TV harness (ensure-fresh.ps1). Human/agent/CI safe:
# pure dotnet build, no app launch, no LLM. Scripts that launch a *prebuilt* exe
# directly (e.g. run-all.ps1) call this first so they never run old code.
#
# Relative paths resolve against the CURRENT directory; callers that pass relative
# paths should set the repo root as CWD first (run-all.ps1 does, via Push-Location).
# For a MULTI-PROJECT exe (e.g. AgentRunner depends on Core/UIAutomation), pass
# -SourceRoot pointing at the whole source tree so a change in a referenced project
# is also detected (the default scans only the project's own folder).
# On failure (build error, or no sources found) the script THROWS — under
# $ErrorActionPreference='Stop' this aborts the caller and powershell.exe exits non-zero.
#
#   .\scripts\ensure-fresh.ps1 -Project src\AgentRunner\AgentRunner.csproj `
#       -Exe src\AgentRunner\bin\Debug\net8.0-windows\AgentRunner.exe `
#       -Framework net8.0-windows -SourceRoot src

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string]$Project,
    [Parameter(Mandatory = $true)] [string]$Exe,
    [string]$Framework,
    [string]$SourceRoot
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $Project)) { throw "ensure-fresh: project not found: $Project" }

# Resolve to absolute paths so the source scan and build are independent of any
# surprise in the caller's CWD (the project must exist; the exe may not yet).
$Project = (Resolve-Path -LiteralPath $Project).Path
if (-not [System.IO.Path]::IsPathRooted($Exe)) { $Exe = [System.IO.Path]::GetFullPath((Join-Path (Get-Location).Path $Exe)) }
if (-not $SourceRoot) { $SourceRoot = Split-Path -Parent $Project }
if (-not [System.IO.Path]::IsPathRooted($SourceRoot)) { $SourceRoot = [System.IO.Path]::GetFullPath((Join-Path (Get-Location).Path $SourceRoot)) }

function Get-NewestSourceTime {
    param([string]$Root)
    $exts = @('.cs', '.xaml', '.csproj', '.resx', '.axaml')
    $newest = [DateTime]::MinValue
    Get-ChildItem -Path $Root -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { ($exts -contains $_.Extension) -and ($_.FullName -notmatch '\\(bin|obj)\\') } |
        ForEach-Object { if ($_.LastWriteTimeUtc -gt $newest) { $newest = $_.LastWriteTimeUtc } }
    return $newest
}

$exeTime = if (Test-Path $Exe) { (Get-Item $Exe).LastWriteTimeUtc } else { [DateTime]::MinValue }
$srcTime = Get-NewestSourceTime -Root $SourceRoot

# Guard against a false "up to date": an empty/wrong SourceRoot yields MinValue, which
# any real exe timestamp trivially beats -> the freshness check would silently pass.
if ($srcTime -eq [DateTime]::MinValue) {
    throw "ensure-fresh: no source files found under '$SourceRoot' -- check -Project / -SourceRoot."
}

if ($exeTime -ge $srcTime) {
    Write-Host "ensure-fresh: up to date -> $Exe" -ForegroundColor DarkGreen
    $global:LASTEXITCODE = 0
    return
}

if (Test-Path $Exe) {
    Write-Host "ensure-fresh: STALE (sources newer than exe) -> rebuilding $Project" -ForegroundColor Yellow
} else {
    Write-Host "ensure-fresh: MISSING exe -> building $Project" -ForegroundColor Yellow
}

$buildArgs = @('build', $Project, '-v', 'minimal')
if ($Framework) { $buildArgs += @('-f', $Framework) }

& dotnet @buildArgs
if ($LASTEXITCODE -ne 0) { throw "ensure-fresh: build failed for $Project (exit $LASTEXITCODE)" }
if (-not (Test-Path $Exe)) { throw "ensure-fresh: build reported success but exe is still missing: $Exe" }

Write-Host "ensure-fresh: rebuilt -> $Exe" -ForegroundColor Green

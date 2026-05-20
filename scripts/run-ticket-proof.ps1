[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string]$TicketPath,

    [string]$Plan,

    [string]$Suite,

    [string]$TestId,

    [ValidateSet('minimal', 'standard', 'full')]
    [string]$EvidenceLevel,

    [string]$TargetWindow,

    [string]$RunnerFramework,

    [string]$OutputDir = '.\artifacts\ticket-proof',

    [string]$WorkbenchOutput,

    [switch]$SkipRuntime,

    [switch]$LaunchSample,

    [ValidateRange(1, 60)]
    [int]$StartupDelaySeconds = 4,

    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$isDryRun = $DryRun -or $WhatIfPreference
$stepResults = [System.Collections.Generic.List[object]]::new()

$sampleProfiles = @{
    winforms = @{
        Exe = '.\src\Samples\Sample.WinFormsApp.Net8\bin\Debug\net8.0-windows\Sample.WinFormsApp.Net8.exe'
        Window = 'Sample Login App (.NET 8)'
    }
    wpf = @{
        Exe = '.\src\Samples\Sample.WpfApp\bin\Debug\net8.0-windows\Sample.WpfApp.exe'
        Window = 'WPF AI Test Target'
    }
}

function Format-CommandLine {
    param(
        [string]$FilePath,
        [string[]]$Arguments
    )

    $escapedArguments = foreach ($argument in $Arguments) {
        if ($argument -match '[\s"]') {
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

function Add-StepResult {
    param(
        [string]$Name,
        [string]$Status,
        [string]$Details = ''
    )

    $script:stepResults.Add([pscustomobject]@{
            Name = $Name
            Status = $Status
            Details = $Details
        }) | Out-Null
}

function Invoke-ProofCommand {
    param(
        [string]$Label,
        [string]$FilePath,
        [string[]]$Arguments,
        [switch]$AllowFailure
    )

    $commandLine = Format-CommandLine -FilePath $FilePath -Arguments $Arguments
    Write-Host "==> $Label"
    Write-Host "    $commandLine"

    if ($isDryRun) {
        Add-StepResult -Name $Label -Status 'planned' -Details $commandLine
        return 0
    }

    & $FilePath @Arguments
    $exitCode = $LASTEXITCODE
    $status = if ($exitCode -eq 0) { 'passed' } else { 'failed' }
    Add-StepResult -Name $Label -Status $status -Details "exit=$exitCode"

    if ($exitCode -ne 0 -and -not $AllowFailure) {
        throw "$Label failed with exit code $exitCode."
    }

    return $exitCode
}

function Read-TicketMarkdown {
    param(
        [string]$Path
    )

    $resolvedPath = if ([System.IO.Path]::IsPathRooted($Path)) {
        [System.IO.Path]::GetFullPath($Path)
    }
    else {
        [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $Path))
    }

    if (-not (Test-Path -LiteralPath $resolvedPath -PathType Leaf)) {
        throw "Ticket markdown not found: $resolvedPath"
    }

    $lines = @(Get-Content -LiteralPath $resolvedPath)
    $frontMatter = @{}
    $bodyStart = 0

    if ($lines.Count -gt 0 -and $lines[0].Trim() -eq '---') {
        for ($i = 1; $i -lt $lines.Count; $i++) {
            if ($lines[$i].Trim() -eq '---') {
                $bodyStart = $i + 1
                break
            }

            $line = $lines[$i].Trim()
            if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith('#')) {
                continue
            }

            if ($line -match '^([A-Za-z0-9_-]+)\s*:\s*(.*)$') {
                $key = $Matches[1].ToLowerInvariant()
                $value = $Matches[2].Trim()
                if (($value.StartsWith('"') -and $value.EndsWith('"')) -or
                    ($value.StartsWith("'") -and $value.EndsWith("'"))) {
                    $value = $value.Substring(1, $value.Length - 2)
                }
                $frontMatter[$key] = $value
            }
        }
    }

    $title = ''
    for ($i = $bodyStart; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^\s*#\s+(.+)$') {
            $title = $Matches[1].Trim()
            break
        }
    }

    if (-not $title -and $frontMatter.ContainsKey('title')) {
        $title = [string]$frontMatter['title']
    }

    return [pscustomobject]@{
        Path = $resolvedPath
        FrontMatter = $frontMatter
        Title = $title
    }
}

function Get-TicketValue {
    param(
        [hashtable]$FrontMatter,
        [string[]]$Keys,
        [string]$Fallback = ''
    )

    foreach ($key in $Keys) {
        $normalized = $key.ToLowerInvariant()
        if ($FrontMatter.ContainsKey($normalized)) {
            return [string]$FrontMatter[$normalized]
        }
    }

    return $Fallback
}

function ConvertTo-Bool {
    param(
        [string]$Value
    )

    return $Value -match '^(1|true|yes|y)$'
}

function Get-SampleProfile {
    param(
        [string]$Framework
    )

    $normalizedFramework = $Framework.ToLowerInvariant()
    if (-not $sampleProfiles.ContainsKey($normalizedFramework)) {
        throw "Sample launch is only supported for framework values: winforms, wpf."
    }

    return $sampleProfiles[$normalizedFramework]
}

function Resolve-RepoPath {
    param(
        [string]$Path
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return ''
    }

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
}

function Get-WorkspaceRoot {
    $workflowPath = Join-Path $repoRoot 'WORKFLOW.md'
    if (-not (Test-Path -LiteralPath $workflowPath -PathType Leaf)) {
        return Join-Path $repoRoot 'runs'
    }

    $lines = @(Get-Content -LiteralPath $workflowPath)
    $inWorkspace = $false
    foreach ($line in $lines) {
        $trimmed = $line.Trim()
        if ($trimmed -eq '---' -and $inWorkspace) {
            break
        }
        if ($trimmed -eq 'workspace:') {
            $inWorkspace = $true
            continue
        }
        if ($inWorkspace -and $line -match '^\s{2}root\s*:\s*(.+)$') {
            $root = $Matches[1].Trim().Trim('"').Trim("'")
            return Resolve-RepoPath -Path $root
        }
        if ($inWorkspace -and $line -match '^\S') {
            $inWorkspace = $false
        }
    }

    return Join-Path $repoRoot 'runs'
}

function Start-TicketSample {
    param(
        [string]$Framework
    )

    $profile = Get-SampleProfile -Framework $Framework
    $samplePath = Resolve-RepoPath -Path $profile.Exe
    $commandLine = Format-CommandLine -FilePath $samplePath -Arguments @()

    Write-Host '==> Start sample app'
    Write-Host "    $commandLine"

    if ($isDryRun) {
        Add-StepResult -Name 'Start sample app' -Status 'planned' -Details $commandLine
        return [pscustomobject]@{
            Id = 'planned'
            HasExited = $false
        }
    }

    if (-not (Test-Path -LiteralPath $samplePath -PathType Leaf)) {
        throw "Sample executable not found: $samplePath. Build the solution first or disable sample launch."
    }

    $process = Start-Process -FilePath $samplePath -PassThru
    Start-Sleep -Seconds $StartupDelaySeconds
    Add-StepResult -Name 'Start sample app' -Status 'passed' -Details "pid=$($process.Id)"
    return $process
}

function Stop-TicketSample {
    param(
        [object]$Process
    )

    if (-not $Process) {
        return
    }

    if ($isDryRun) {
        Add-StepResult -Name 'Stop sample app' -Status 'planned' -Details 'sample process started by proof script'
        return
    }

    if ($Process.HasExited) {
        Add-StepResult -Name 'Stop sample app' -Status 'skipped' -Details "pid=$($Process.Id) already exited"
        return
    }

    Write-Host "Stopping sample app PID $($Process.Id)."
    Stop-Process -Id $Process.Id -Force
    Add-StepResult -Name 'Stop sample app' -Status 'passed' -Details "pid=$($Process.Id)"
}

function Get-ReportPaths {
    param(
        [string]$RunsRoot
    )

    if (-not (Test-Path -LiteralPath $RunsRoot -PathType Container)) {
        return @()
    }

    return @(Get-ChildItem -LiteralPath $RunsRoot -Filter 'report.json' -Recurse -File | ForEach-Object { $_.FullName })
}

function Select-NewRunDir {
    param(
        [string]$RunsRoot,
        [string[]]$BeforeReports,
        [datetime]$StartedAt
    )

    $known = @{}
    foreach ($path in $BeforeReports) {
        $known[$path] = $true
    }

    $reports = @(Get-ReportPaths -RunsRoot $RunsRoot |
        Where-Object { -not $known.ContainsKey($_) } |
        Sort-Object { (Get-Item -LiteralPath $_).LastWriteTimeUtc } -Descending)

    if ($reports.Count -eq 0) {
        $reports = @(Get-ReportPaths -RunsRoot $RunsRoot |
            Where-Object { (Get-Item -LiteralPath $_).LastWriteTimeUtc -ge $StartedAt.ToUniversalTime().AddSeconds(-2) } |
            Sort-Object { (Get-Item -LiteralPath $_).LastWriteTimeUtc } -Descending)
    }

    if ($reports.Count -eq 0) {
        return ''
    }

    return Split-Path -Parent $reports[0]
}

function New-CommentSummary {
    param(
        [object]$Ticket,
        [string]$ResolvedPlan,
        [string]$ResolvedSuite,
        [string]$ResolvedTestId,
        [string]$ResolvedEvidenceLevel,
        [string]$RunDir,
        [string]$WorkbenchPath,
        [bool]$RuntimeSkipped,
        [bool]$DryRunMode
    )

    $lines = [System.Collections.Generic.List[string]]::new()
    $ticketLabel = if ($Ticket.Title) { $Ticket.Title } else { [System.IO.Path]::GetFileName($Ticket.Path) }
    $lines.Add("## Ticket-to-evidence proof") | Out-Null
    $lines.Add("") | Out-Null
    $lines.Add("- Ticket: $ticketLabel") | Out-Null
    $lines.Add("- Plan: $(if ($ResolvedPlan) { $ResolvedPlan } else { 'suite=' + $ResolvedSuite })") | Out-Null
    $lines.Add("- Test: $ResolvedTestId") | Out-Null
    $lines.Add("- Evidence level: $ResolvedEvidenceLevel") | Out-Null
    if ($DryRunMode) {
        $lines.Add("- Mode: dry run; commands were printed but not executed") | Out-Null
    }
    elseif ($RuntimeSkipped) {
        $lines.Add("- Runtime: skipped") | Out-Null
    }
    elseif ($RunDir) {
        $lines.Add(('- Runtime: completed with artifacts at `{0}`' -f $RunDir)) | Out-Null
    }
    else {
        $lines.Add("- Runtime: no run artifacts were produced") | Out-Null
    }
    $lines.Add("- Workbench: $(if ($WorkbenchPath) { $WorkbenchPath } else { 'not rendered' })") | Out-Null
    $lines.Add("") | Out-Null
    $lines.Add("### Checks") | Out-Null
    foreach ($step in $script:stepResults) {
        $detail = if ($step.Details) { " - $($step.Details)" } else { '' }
        $lines.Add("- $($step.Status): $($step.Name)$detail") | Out-Null
    }

    return ($lines -join [Environment]::NewLine) + [Environment]::NewLine
}

Push-Location $repoRoot
try {
    $ticket = Read-TicketMarkdown -Path $TicketPath
    $frontMatter = $ticket.FrontMatter

    $planValue = if ($Plan) { $Plan } else { Get-TicketValue -FrontMatter $frontMatter -Keys @('plan', 'plan_path', 'planpath', 'test_plan', 'testplan') }
    $resolvedPlan = Resolve-RepoPath -Path $planValue
    $resolvedSuite = if ($Suite) { $Suite } else { Get-TicketValue -FrontMatter $frontMatter -Keys @('suite') }
    $resolvedTestId = if ($TestId) { $TestId } else { Get-TicketValue -FrontMatter $frontMatter -Keys @('test_id', 'testid', 'test') }
    $resolvedEvidenceLevel = if ($EvidenceLevel) { $EvidenceLevel } else { Get-TicketValue -FrontMatter $frontMatter -Keys @('evidence_level', 'evidencelevel', 'evidence') -Fallback 'standard' }
    $resolvedEvidenceLevel = $resolvedEvidenceLevel.ToLowerInvariant()
    $resolvedFramework = Get-TicketValue -FrontMatter $frontMatter -Keys @('framework') -Fallback ''
    $resolvedTargetWindow = if ($TargetWindow) { $TargetWindow } else { Get-TicketValue -FrontMatter $frontMatter -Keys @('target_window', 'targetwindow', 'window') }
    $resolvedRunnerFramework = if ($RunnerFramework) { $RunnerFramework } else { Get-TicketValue -FrontMatter $frontMatter -Keys @('runner_framework', 'runnerframework', 'framework_target', 'frameworktarget') -Fallback 'net8.0-windows' }
    $ticketSkipRuntime = ConvertTo-Bool -Value (Get-TicketValue -FrontMatter $frontMatter -Keys @('skip_runtime', 'skipruntime') -Fallback 'false')
    $runtimeSkipped = $SkipRuntime -or $ticketSkipRuntime
    $ticketLaunchSample = ConvertTo-Bool -Value (Get-TicketValue -FrontMatter $frontMatter -Keys @('launch_sample', 'launchsample') -Fallback 'false')
    $shouldLaunchSample = $LaunchSample -or $ticketLaunchSample

    if ($resolvedEvidenceLevel -notin @('minimal', 'standard', 'full')) {
        throw "Evidence level must be one of: minimal, standard, full."
    }
    if ($resolvedRunnerFramework -notin @('net8.0-windows', 'net48')) {
        throw "Runner framework must be one of: net8.0-windows, net48."
    }
    if (-not $resolvedPlan -and -not $resolvedSuite) {
        throw "Ticket frontmatter or parameters must provide 'plan' or 'suite'."
    }
    if (-not $resolvedTestId) {
        throw "Ticket frontmatter or parameters must provide 'test_id'."
    }
    if ($shouldLaunchSample -and -not $resolvedFramework) {
        throw "Sample launch requires a ticket frontmatter 'framework' value."
    }

    $resolvedOutputDir = Resolve-RepoPath -Path $OutputDir
    $workbenchValue = if ($WorkbenchOutput) { $WorkbenchOutput } else { Get-TicketValue -FrontMatter $frontMatter -Keys @('workbench_output', 'workbenchoutput') }
    $resolvedWorkbenchOutput = if ($workbenchValue) {
        Resolve-RepoPath -Path $workbenchValue
    }
    else {
        Join-Path $resolvedOutputDir 'workbench.html'
    }
    $summaryPath = Join-Path $resolvedOutputDir 'ticket-proof-summary.md'
    $runsRoot = Get-WorkspaceRoot

    Write-Host "Ticket: $($ticket.Path)"
    Write-Host "Proof output: $resolvedOutputDir"
    if ($isDryRun) {
        Write-Host 'Dry run only; commands will be printed but not executed and no files will be written.'
    }
    else {
        New-Item -ItemType Directory -Force -Path $resolvedOutputDir | Out-Null
    }

    $selectionArgs = @()
    if ($resolvedPlan) {
        $selectionArgs += @('--plan', $resolvedPlan)
    }
    else {
        $selectionArgs += @('--suite', $resolvedSuite)
    }
    $selectionArgs += @('--test-id', $resolvedTestId)

    Invoke-ProofCommand `
        -Label 'Build solution' `
        -FilePath 'dotnet' `
        -Arguments @('build', '.\DesktopAiTestAgent.sln', '--no-restore', '-v', 'minimal') | Out-Null

    Invoke-ProofCommand `
        -Label 'Run unit tests' `
        -FilePath 'dotnet' `
        -Arguments @('test', '.\DesktopAiTestAgent.sln', '--no-restore', '-v', 'minimal') | Out-Null

    $validateArgs = @(
        'run',
        '--project',
        '.\src\AgentRunner\AgentRunner.csproj',
        '-f',
        $resolvedRunnerFramework,
        '--no-restore',
        '--',
        '--validate-plan'
    ) + $selectionArgs + @('--format', 'json')

    Invoke-ProofCommand `
        -Label 'Validate selected plan' `
        -FilePath 'dotnet' `
        -Arguments $validateArgs | Out-Null

    $listArgs = @(
        'run',
        '--project',
        '.\src\AgentRunner\AgentRunner.csproj',
        '-f',
        $resolvedRunnerFramework,
        '--no-restore',
        '--',
        '--list-tests'
    ) + $selectionArgs + @('--format', 'json')

    Invoke-ProofCommand `
        -Label 'List selected test' `
        -FilePath 'dotnet' `
        -Arguments $listArgs | Out-Null

    $runDir = ''
    $sampleProcess = $null
    if ($runtimeSkipped) {
        Write-Host 'Skipping runtime desktop automation.'
        Add-StepResult -Name 'Runtime execution' -Status 'skipped' -Details 'SkipRuntime requested'
    }
    else {
        try {
            if ($shouldLaunchSample) {
                $sampleProcess = Start-TicketSample -Framework $resolvedFramework
            }

            $beforeReports = Get-ReportPaths -RunsRoot $runsRoot
            $runtimeStartedAt = Get-Date
            $runtimeArgs = @(
                'run',
                '--project',
                '.\src\AgentRunner\AgentRunner.csproj',
                '-f',
                $resolvedRunnerFramework,
                '--no-restore',
                '--'
            ) + $selectionArgs + @('--evidence-level', $resolvedEvidenceLevel)

            if ($resolvedTargetWindow) {
                $runtimeArgs += @('--window', $resolvedTargetWindow)
            }

            $runtimeExitCode = Invoke-ProofCommand `
                -Label 'Run agent runtime' `
                -FilePath 'dotnet' `
                -Arguments $runtimeArgs `
                -AllowFailure

            if ($isDryRun) {
                Add-StepResult -Name 'Verify run artifacts' -Status 'planned' -Details 'after runtime run directory is detected'
            }
            else {
                $runDir = Select-NewRunDir -RunsRoot $runsRoot -BeforeReports $beforeReports -StartedAt $runtimeStartedAt
                if ($runDir) {
                    Invoke-ProofCommand `
                        -Label 'Verify run artifacts' `
                        -FilePath 'powershell' `
                        -Arguments @(
                            '-ExecutionPolicy',
                            'Bypass',
                            '-File',
                            '.\scripts\verify-run-artifacts.ps1',
                            '-RunDir',
                            $runDir,
                            '-EvidenceLevel',
                            $resolvedEvidenceLevel
                        ) | Out-Null
                }
                else {
                    Add-StepResult -Name 'Verify run artifacts' -Status 'skipped' -Details 'No run artifacts produced'
                }

                if ($runtimeExitCode -ne 0) {
                    throw "Runtime execution failed with exit code $runtimeExitCode."
                }
            }
        }
        finally {
            Stop-TicketSample -Process $sampleProcess
        }
    }

    $renderArgs = @(
        'run',
        '--project',
        '.\src\AgentRunner\AgentRunner.csproj',
        '-f',
        $resolvedRunnerFramework,
        '--no-restore',
        '--',
        '--render-ui',
        $resolvedWorkbenchOutput
    )
    if ($resolvedPlan) {
        $renderArgs += @('--plan', $resolvedPlan)
    }

    Invoke-ProofCommand `
        -Label 'Render AgentLoop Workbench' `
        -FilePath 'dotnet' `
        -Arguments $renderArgs | Out-Null

    $summary = New-CommentSummary `
        -Ticket $ticket `
        -ResolvedPlan $resolvedPlan `
        -ResolvedSuite $resolvedSuite `
        -ResolvedTestId $resolvedTestId `
        -ResolvedEvidenceLevel $resolvedEvidenceLevel `
        -RunDir $runDir `
        -WorkbenchPath $resolvedWorkbenchOutput `
        -RuntimeSkipped $runtimeSkipped `
        -DryRunMode $isDryRun

    if ($isDryRun) {
        Write-Host "==> Ticket comment summary preview"
        Write-Host $summary
        Write-Host "Would write summary to $summaryPath"
    }
    else {
        $summary | Set-Content -Path $summaryPath -Encoding UTF8
        Write-Host "Ticket proof summary written to $summaryPath"
    }
}
finally {
    Pop-Location
}

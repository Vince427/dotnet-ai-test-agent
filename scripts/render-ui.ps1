param(
    [string]$Output = '.\docs\symphony.html',
    [string]$Plan = ''
)

$ErrorActionPreference = 'Stop'

$argsList = @(
    'run',
    '--project',
    '.\src\AgentRunner\AgentRunner.csproj',
    '-f',
    'net8.0-windows',
    '--',
    '--render-ui',
    $Output
)

if ($Plan) {
    $argsList += @('--plan', $Plan)
}

dotnet @argsList
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "Open $Output in your browser."

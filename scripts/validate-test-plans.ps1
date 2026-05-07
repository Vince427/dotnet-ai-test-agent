param(
    [string]$OutputDir = '.\artifacts\test-plans',
    [switch]$SkipRestore,
    [switch]$SkipUnitTests
)

$ErrorActionPreference = 'Stop'

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

if (-not $SkipRestore) {
    dotnet restore .\src\AgentRunner.Tests\AgentRunner.Tests.csproj
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

if (-not $SkipUnitTests) {
    dotnet test .\src\AgentRunner.Tests\AgentRunner.Tests.csproj --no-restore -v minimal
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

$validateArgs = @(
    'run',
    '--project',
    '.\src\AgentRunner\AgentRunner.csproj',
    '-f',
    'net8.0-windows',
    '--no-restore',
    '--',
    '--validate-plan',
    '--format',
    'json'
)

$validationJson = & dotnet @validateArgs
$validationExitCode = $LASTEXITCODE
$validationPath = Join-Path $OutputDir 'plan-validation.json'
$validationJson | Set-Content -Path $validationPath -Encoding UTF8
$validationJson
if ($validationExitCode -ne 0) {
    exit $validationExitCode
}

$listArgs = @(
    'run',
    '--project',
    '.\src\AgentRunner\AgentRunner.csproj',
    '-f',
    'net8.0-windows',
    '--no-restore',
    '--',
    '--list-tests',
    '--format',
    'json'
)

$testListJson = & dotnet @listArgs
$testListExitCode = $LASTEXITCODE
$testListPath = Join-Path $OutputDir 'test-list.json'
$testListJson | Set-Content -Path $testListPath -Encoding UTF8
$testListJson
if ($testListExitCode -ne 0) {
    exit $testListExitCode
}

Write-Host "Wrote $validationPath"
Write-Host "Wrote $testListPath"

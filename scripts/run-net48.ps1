$ErrorActionPreference = 'Stop'

Write-Host 'Restoring solution...'
dotnet restore .\DesktopAiTestAgent.sln

Write-Host 'Building .NET Framework 4.8 sample and shared projects...'
dotnet build .\src\Samples\Sample.WinFormsApp.Net48\Sample.WinFormsApp.Net48.csproj

dotnet build .\src\AgentRunner\AgentRunner.csproj -f net48

Write-Host 'Starting .NET Framework 4.8 sample app...'
$sample = Start-Process dotnet -ArgumentList 'run --project .\src\Samples\Sample.WinFormsApp.Net48\Sample.WinFormsApp.Net48.csproj' -PassThru

try {
    Start-Sleep -Seconds 4
    Write-Host 'Running shared agent runner against .NET Framework 4.8 sample...'
    dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net48 -- "Sample Login App (.NET Framework 4.8)"
}
finally {
    Start-Sleep -Milliseconds 500
    if ($sample -and !$sample.HasExited) {
        Stop-Process -Id $sample.Id -Force
    }
}

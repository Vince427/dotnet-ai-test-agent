$ErrorActionPreference = 'Stop'

Write-Host 'Restoring solution...'
dotnet restore .\DesktopAiTestAgent.sln

Write-Host 'Building .NET 8 sample and shared projects...'
dotnet build .\src\Samples\Sample.WinFormsApp.Net8\Sample.WinFormsApp.Net8.csproj

dotnet build .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows

Write-Host 'Starting .NET 8 sample app...'
$sample = Start-Process dotnet -ArgumentList 'run --project .\src\Samples\Sample.WinFormsApp.Net8\Sample.WinFormsApp.Net8.csproj' -PassThru

try {
    Start-Sleep -Seconds 4
    Write-Host 'Running shared agent runner against .NET 8 sample...'
    dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- "Sample Login App (.NET 8)"
}
finally {
    Start-Sleep -Milliseconds 500
    if ($sample -and !$sample.HasExited) {
        Stop-Process -Id $sample.Id -Force
    }
}

$ErrorActionPreference = 'Stop'

dotnet restore .\DesktopAiTestAgent.sln
dotnet build .\src\AgentRunner\AgentRunner.csproj -f net48
dotnet build .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows
dotnet build .\src\Samples\Sample.WinFormsApp.Net48\Sample.WinFormsApp.Net48.csproj
dotnet build .\src\Samples\Sample.WinFormsApp.Net8\Sample.WinFormsApp.Net8.csproj

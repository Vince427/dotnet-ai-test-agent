# Desktop AI Test Agent

AI-powered UI testing for WinForms and .NET desktop apps with FlaUI.

## V1.2 Dual Target

This version is designed to work with both:

- legacy WinForms on .NET Framework 4.8
- modern WinForms on .NET 8

## What is included

- shared Core library
- shared UIAutomation library using FlaUI
- shared AgentRunner
- sample WinForms app for .NET Framework 4.8
- sample WinForms app for .NET 8
- PowerShell scripts to run either sample

## Default credentials

- Username: `admin`
- Password: `password123`

## Requirements

### For .NET 8 sample

- .NET 8 SDK installed

### For .NET Framework 4.8 sample

- Visual Studio Build Tools or Visual Studio with .NET Framework 4.8 targeting pack

## Run .NET 8 sample

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run-net8.ps1
```

## Run .NET Framework 4.8 sample

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run-net48.ps1
```

## Manual usage

Start one sample app first, then run the agent:

```powershell
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -- "Sample Login App (.NET 8)"
```

or:

```powershell
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -- "Sample Login App (.NET Framework 4.8)"
```

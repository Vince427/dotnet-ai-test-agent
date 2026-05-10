# GitHub Copilot Instructions

This repository is a portable-first desktop test runner for existing .NET
applications. Keep the product useful from local terminals, Windows CI,
Symphony Workbench, and AI agents through the same contract:

```text
YAML specs -> CLI runner -> artifacts -> Symphony Workbench
```

## Read Before Editing

- `AGENTS.md`
- `project_rules.md`
- The relevant `.claude/context/<domain>.md` file for the files you touch.

## Core Rules

- Do not require target applications to add agent-specific packages, classes,
  source changes, or production test hooks.
- Keep manual workflows first: YAML, CLI, artifacts, and docs.
- OpenRouter is optional runtime assistance. Validation, listing, and workbench
  rendering must work without `.env`.
- Never expose or commit the real `.env`. Use `.env.template` placeholders only.
- MCP and plugins are adapters over CLI commands, not the product core.
- If a public CLI flag, YAML field, artifact shape, or schema changes, update
  docs and the matching `.claude/context/` contract in the same change.

## Useful Commands

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\validate-test-plans.ps1 -SkipRestore
dotnet build .\DesktopAiTestAgent.sln --no-restore -v minimal
dotnet test .\DesktopAiTestAgent.sln --no-restore -v minimal
```

Machine-readable manual commands:

```powershell
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --validate-plan --format json
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --list-tests --format json
```

## If Asked To Continue The Plan

Follow `.claude/commands/suite.md`. Prefer the smallest useful MVP increment
before broad framework expansion.

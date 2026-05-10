# AI Authoring Guide

This project can be used manually or with Codex, Claude Code, Copilot, MCP wrappers, GitHub Actions, and Azure Pipelines.

The same portable contract applies everywhere: edit YAML, call CLI commands,
read artifacts. Plugins and MCP wrappers are convenience layers over that
contract.

## Ground Rules

- Edit YAML test plans under `tests/` or an external test pack.
- Do not modify the target application code unless the user explicitly asks for app changes.
- Do not add agent-specific packages, classes, hooks, or test-only code paths to the app being tested.
- Prefer linking existing tests with `existing_tests` instead of duplicating unit or integration coverage.
- Validate plans before proposing a PR.

## Required Flow

1. Read the user story, bug report, manual QA steps, or existing CI failure.
2. Add or update YAML tests only.
3. Include traceability metadata when available: `source_issue`, `source_pr`, `authoring_agent`, `risk`, `ci_profile`.
4. Run `--validate-plan --format json`.
5. Run `--list-tests --format json` and check that the expected test id appears.
6. Leave runtime execution to the user, CI, or an explicit follow-up when a desktop session is available.

## Commands

```powershell
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --validate-plan --format json
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --list-tests --format json
```

For CI, prefer the shared script:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\validate-test-plans.ps1
```

GitHub Actions and Azure Pipelines examples are provided in `.github/workflows/` and `.azure/pipelines/`.

GitHub Copilot and GitHub-native coding agents should also read
`.github/copilot-instructions.md`, which is a short repo-local entry point for
the same rules.

## YAML Metadata

- `source_issue`: issue, work item, bug id, or ticket URL.
- `source_pr`: pull request that introduced or updated the test.
- `authoring_agent`: `manual`, `codex`, `claude-code`, `copilot`, or team-specific value.
- `risk`: `low`, `medium`, `high`, or `critical`.
- `ci_profile`: where the test is expected to run, such as `local-windows`, `github-windows`, or `azure-windows`.

The schema lives at `schemas/test-plan.schema.json`.

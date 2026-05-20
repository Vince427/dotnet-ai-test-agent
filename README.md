# Desktop AI Test Agent

Portable, AI-assisted UI testing for existing .NET desktop applications.

The core contract is intentionally small:

```text
YAML specs -> CLI runner -> artifacts -> AgentLoop Workbench
```

Humans, CI jobs, Codex/Claude/Copilot, MCP wrappers, and future plugins should
all use that same contract. The tested application stays non-intrusive: no
agent-specific packages, helper classes, or production-only test hooks.

## What It Does

- Drives WinForms, WPF, and later MAUI Windows/Avalonia desktop apps from the
  outside with FlaUI and Windows UI Automation.
- Describes business/runtime tests in YAML under `tests/`.
- Validates and lists tests without `.env`, OpenRouter, or a live desktop app.
- Runs selected desktop tests and writes portable evidence: `report.json`,
  `summary.md`, screenshots, and optional UI tree snapshots.
- Renders a static AgentLoop Workbench over YAML plans and run artifacts.

## Current Focus

- WinForms and WPF first.
- Small YAML files, one user-facing scenario per file.
- Manual-first workflows before MCP/plugins.
- GitHub Pages as lightweight public documentation.
- DocFX, recording mode, vision fallback, and richer CI outputs come later.

## Quickstart

Validate all YAML plans:

```powershell
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --validate-plan --format json
```

List all tests:

```powershell
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --list-tests --format json
```

Render the local AgentLoop Workbench:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\render-ui.ps1
```

Preview focused WinForms/WPF runtime examples without launching desktop apps:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-ui-examples.ps1 -WhatIf
```

Run one selected test after configuring the runtime LLM provider and preparing a
desktop session:

```powershell
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- `
    --plan .\tests\examples\winforms\login.yaml `
    --test-id EX-WINFORMS-LOGIN-001 `
    --evidence-level standard
```

## Runtime LLM Configuration

Only runtime desktop execution needs an LLM endpoint. Validation, listing,
Workbench rendering, docs, and CI proof dry-runs do not.

Copy `.env.template` to `.env` locally and set:

```powershell
LLM_ENDPOINT=https://openrouter.ai/api/v1
LLM_API_KEY=your-openrouter-key-here
LLM_MODEL=anthropic/claude-3.5-sonnet:beta
```

The real `.env` file is ignored by git.

## Documentation

- [MVP path](docs/mvp.md)
- [Product spec](docs/spec.md)
- [Architecture](docs/architecture.md)
- [Testing strategy](docs/testing-strategy.md)
- [TestZoo status](docs/testzoo.md)
- [GitHub Pages](docs/github-pages.md)
- [Roadmap](docs/roadmap.md)

## Repository Map

- `src/Core`: shared models and driver interfaces.
- `src/UIAutomation`: FlaUI desktop automation.
- `src/AgentRunner`: CLI, AgentLoop runtime, artifacts, validation, Workbench.
- `src/Samples`: WinForms/WPF/MAUI demo targets.
- `tests`: YAML business/runtime plans.
- `scripts`: local validation, proof, and demo commands.
- `docs`: public docs and generated static Workbench output.

## Agent Contributors

Start with `AGENTS.md`. More detailed local agent guidance lives in `CLAUDE.md`
and `.claude/context/`, but public prompts are not tracked in this repository.

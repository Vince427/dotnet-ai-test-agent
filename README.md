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

- Drives WinForms, WPF, and Avalonia desktop apps from the outside with FlaUI and
  Windows UI Automation (MAUI Windows sample exists; its E2E wiring is in progress).
- Describes business/runtime tests in YAML under `tests/`.
- Validates and lists tests without `.env`, OpenRouter, or a live desktop app.
- Runs selected desktop tests and writes portable evidence: `report.json`,
  `summary.md`, screenshots (secret-field regions masked at capture), and optional
  UI tree snapshots.
- Renders a static AgentLoop Workbench over YAML plans and run artifacts.
- Exports results to JUnit XML for CI dashboards (`--to-junit`), with links to
  existing TRX/JUnit tests, source issue/PR, and the trace id.
- Emits optional OpenTelemetry traces + metrics (opt-in via
  `OTEL_EXPORTER_OTLP_ENDPOINT`; view live in a standalone Aspire dashboard).
- Serves a local-only interactive dashboard (`--dashboard`): catalog, ticket
  creation (form → validated YAML), launch, live logs + screenshots, and a Files
  explorer over the on-disk sources.

## Current Focus

- WinForms, WPF, and Avalonia stable; MAUI Windows E2E wiring next.
- Small YAML files, one user-facing scenario per file.
- Manual-first workflows before MCP/plugins.
- GitHub Pages as lightweight public documentation.
- DocFX, recording mode, and vision (VLM) fallback come later.

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

Generate deterministic guard failure demo artifacts, without `.env`, LLM, or a
desktop app:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\write-guard-demos.ps1 -OutputRoot .\artifacts\guard-demos
```

Run one selected test after configuring the runtime LLM provider and preparing a
desktop session:

```powershell
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- `
    --plan .\tests\examples\winforms\login.yaml `
    --test-id EX-WINFORMS-LOGIN-001 `
    --evidence-level standard
```

Convert captured run artifacts to JUnit XML for CI (no `.env`/LLM needed):

```powershell
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --to-junit artifacts\junit-results.xml
```

Serve the local-only interactive dashboard (developer tool, never in CI):

```powershell
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --dashboard 8090
# then open http://localhost:8090/  (Ctrl+C to stop)
```

## Install

The agent is a Windows desktop tool, distributed as a published `AgentRunner.exe`
(not a cross-platform `dotnet tool` — it depends on FlaUI/WinForms/WPF).

- Download the latest `release.zip` from the
  [Releases page](https://github.com/Vince427/dotnet-ai-test-agent/releases) — it is built and
  attached automatically by the release workflow on each `v*` tag.
- Or build it yourself: `powershell -ExecutionPolicy Bypass -File scripts/publish-release.ps1 -Zip`.

See [docs/install.md](docs/install.md) for prerequisites (the .NET 8 Desktop Runtime, or a
`-SelfContained` build with no prerequisite) and run examples.

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

- [Getting started](docs/getting-started.md) — start here
- [Project status](docs/status.md) — where we are now
- [MVP path](docs/mvp.md)
- [Product spec](docs/spec.md)
- [Architecture](docs/architecture.md) · [diagram](docs/architecture-diagram.md)
- [Architecture decisions](docs/architecture-decisions.md) — the load-bearing *why*
- [Testing strategy](docs/testing-strategy.md)
- [TestZoo status](docs/testzoo.md)
- [GitHub Pages](docs/github-pages.md)
- [Roadmap](docs/roadmap.md)
- [Changelog](CHANGELOG.md)

## Repository Map

- `src/Core`: shared models and driver interfaces.
- `src/UIAutomation`: FlaUI desktop automation + screenshot masking.
- `src/AgentRunner`: CLI, `RunOrchestrator` runtime, artifacts, validation,
  telemetry, Workbench, and the local Dashboard (`Dashboard/`).
- `src/Samples`: WinForms/WPF/MAUI/Avalonia demo targets.
- `tests`: YAML business/runtime plans.
- `scripts`: local validation, proof, and demo commands.
- `docs`: public docs and generated static Workbench output.

## Agent Contributors

Start with `AGENTS.md`. More detailed local agent guidance lives in `CLAUDE.md`
and `.claude/context/`, but public prompts are not tracked in this repository.

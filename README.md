# Desktop AI Test Agent

AI-powered UI testing for existing .NET desktop apps with FlaUI, YAML test backlogs, quality guards, and an OpenAI Symphony-style architecture.

## V1.3 — Generic Robo Agent (Symphony Foundation)

This version transforms the agent from a hardcoded login-only tester into a **generic UI automation agent** capable of testing existing .NET desktop applications from the outside.

The target app stays clean: no required agent-specific classes, packages, or source changes.

### What's new in V1.3

- **Generic UI tree discovery** — walks all UI elements, not just hardcoded IDs
- **Dynamic goals** — configurable test objectives via CLI or WORKFLOW.md
- **Symphony-style WORKFLOW.md** — YAML front matter config + prompt template
- **Loop detection** — sliding window pattern analysis prevents infinite loops
- **Scoring engine** — cumulative reward/penalty per action with abort threshold
- **Structured logging** — Symphony-compatible key=value logs with context
- **Run artifacts** — JSON report + screenshots + Markdown summary per run
- **Multi-strategy element resolution** — AutomationId → Name fallback
- **Extended actions** — Click, DoubleClick, EnterText, Scroll, Wait, Done, Explore

### Architecture (Symphony-inspired)

```
WORKFLOW.md (policy + config)
        ↓
   Orchestrator (observe → decide → act → score → record)
        ↓
┌───────────────────────────────┐
│         Agent Loop            │
│  LoopDetector + ScoringEngine │
│  + AgentMemory + StructuredLog│
└───────┬───────────────────────┘
        ↓               ↓
   LlmService      FlaUiDesktopDriver
   (LLM brain)     (UI automation arm)
        ↓               ↓
   OpenAI/Proxy    FlaUI + UI Automation
```

## Dual Target Support

Current support:
- legacy WinForms on .NET Framework 4.8
- modern WinForms on .NET 8
- WPF samples

Roadmap support:
- Avalonia Windows Desktop through UIA3 and vision fallback
- .NET MAUI Windows through FlaUI or Appium Windows
- .NET MAUI Android/iOS/Mac through Appium later

## Non-intrusive testing

The agent is designed for teams that already have applications and CI/CD tests.

- Keep the application code untouched.
- Store AI-directed tests externally in YAML.
- Link to existing unit, integration, or CI tests when useful.
- Export and import standard CI-friendly results in later phases.
- Use optional accessibility metadata, such as AutomationId, only when teams choose to improve testability.

## Manual-first, AI-optional workflow

Every critical capability must work without Codex, Claude Code, Copilot, MCP, plugins, OpenRouter, or any LLM.

- Humans can edit `tests/*.yaml` directly.
- The CLI can validate and list tests without loading `.env`.
- The runtime agent can still use OpenRouter for AI-assisted UI execution.
- The Symphony Workbench is a local viewer/debugger over YAML and artifacts.
- Future MCP/plugins should call the same CLI commands instead of replacing the runner.
- Agent authoring rules live in `docs/ai-authoring.md`; the YAML schema lives in `schemas/test-plan.schema.json`.

## Default credentials

- Username: `admin`
- Password: `password123`

## Requirements

- .NET 8 SDK installed
- An LLM endpoint configured through `.env` only for AI-assisted runtime execution

Manual commands such as `--validate-plan`, `--list-tests`, and `--render-ui` do not require `.env`.

## LLM configuration

Copy `.env.template` to `.env` and set:

```powershell
LLM_ENDPOINT=https://openrouter.ai/api/v1
LLM_API_KEY=your-openrouter-key-here
LLM_MODEL=anthropic/claude-3.5-sonnet:beta
```

The real `.env` file is ignored by git.

If no LLM environment variables are configured, the runner falls back to `http://localhost:4000` with a dummy API key. That URL is only an OpenAI-compatible local LLM/proxy endpoint for developers. It is not the Symphony Workbench, not the tested desktop app, and not a dashboard. For OpenRouter usage, keep using the real local `.env`.

## Quick Start

### Run with default goal (login)

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run-net8.ps1
```

### Run with custom goal

```powershell
# Start the sample app first
dotnet run --project .\src\Samples\Sample.WinFormsApp.Net8

# In another terminal, run the agent with a custom goal
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- `
    --window "Sample Login App (.NET 8)" `
    --goal "Explore all UI elements and test the login flow" `
    --success "Login successful" `
    --goal-id "explore-login" `
    --max-steps 20
```

### Run a directed test from the Symphony backlog

```powershell
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- `
    --plan .\tests\smoke.yaml `
    --test-id LOGIN-001 `
    --evidence-level standard
```

You can also use `--suite smoke`, which resolves to `tests/smoke.yaml`.

The first multi-framework TestZoo suite is available as `--suite testzoo`. It now seeds login, profile-save, validation-failure, checkbox, and UIA metadata checks across WinForms, WPF, .NET 4.8, .NET 8, and MAUI Windows where samples exist; see `docs/testzoo.md`.

### Validate YAML manually, without LLM or `.env`

```powershell
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --validate-plan
```

You can validate one plan:

```powershell
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --validate-plan .\tests\smoke.yaml
```

Agents and CI can request machine-readable output:

```powershell
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --validate-plan --format json
```

### List available tests manually

```powershell
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --list-tests
```

```powershell
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --list-tests --format json
```

### Render the local Symphony Workbench

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\render-ui.ps1
```

This generates `docs/symphony.html`, a local read-only view over `tests/*.yaml` and `runs/*/report.json`.

### Validate test plans for CI

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\validate-test-plans.ps1
```

This writes `artifacts/test-plans/plan-validation.json` and `artifacts/test-plans/test-list.json`. Example GitHub Actions and Azure Pipelines files are included under `.github/workflows/` and `.azure/pipelines/`.

### CLI Arguments

| Argument | Default | Description |
|---|---|---|
| `--window` | `Sample Login App (.NET 8)` | Target window title |
| `--goal-name` | `default` | Goal key loaded from `WORKFLOW.md` |
| `--plan` | none | YAML test plan path, such as `tests/smoke.yaml` |
| `--suite` | none | Suite name; resolves to `tests/<suite>.yaml` |
| `--test-id` | first test in plan | Test id from the YAML backlog |
| `--render-ui` | none | Generate the local static Symphony Workbench HTML and exit |
| `--validate-plan` | none | Validate YAML plans and exit without `.env`, LLM, or UI automation |
| `--list-tests` | none | List YAML tests and exit without `.env`, LLM, or UI automation |
| `--format` | `text` | Manual command output: `text` or `json` |
| `--evidence-level` | `standard` | Runtime artifact level: `minimal`, `standard`, or `full` |
| `--goal` | Login with admin/password123 | Goal description for the agent |
| `--success` | `Login successful` | Text to match in UI for success |
| `--goal-id` | `login` | Short identifier for logs/artifacts |
| `--max-steps` | `30` | Maximum agent steps |

## Run Artifacts

Each agent run produces a directory under `runs/<run-id>/`:
- `report.json` — full structured report with all steps
- `summary.md` — human-readable Markdown summary
- `screenshots/` — PNG screenshot at each step

Evidence levels:

- `minimal`: JSON report and Markdown summary only.
- `standard`: report, summary, and screenshots.
- `full`: standard artifacts plus `ui-tree/step_XXX.json` snapshots for replay/debug and AI agent analysis.

## WORKFLOW.md

The `WORKFLOW.md` file at the project root defines:
- Agent settings (concurrency, max turns, retry backoff)
- Scoring thresholds
- Predefined goals (default, explore, smoke)
- LLM configuration (with `$ENV_VAR` support)
- Prompt template for the agent

## Roadmap

See `docs/roadmap.md` for the full plan through V12.

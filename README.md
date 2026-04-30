# Desktop AI Test Agent

AI-powered UI testing for WinForms and .NET desktop apps with FlaUI + OpenAI Symphony architecture.

## V1.3 — Generic Robo Agent (Symphony Foundation)

This version transforms the agent from a hardcoded login-only tester into a **generic UI automation agent** capable of testing any WinForms application, inspired by [OpenAI's Symphony](https://github.com/openai/symphony) orchestration spec.

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

Works with both:
- legacy WinForms on .NET Framework 4.8
- modern WinForms on .NET 8

## Default credentials

- Username: `admin`
- Password: `password123`

## Requirements

- .NET 8 SDK installed
- An LLM endpoint (local proxy at `http://localhost:4000` by default, or set `LLM_ENDPOINT`)

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
dotnet run --project .\src\AgentRunner -f net8.0-windows -- `
    --window "Sample Login App (.NET 8)" `
    --goal "Explore all UI elements and test the login flow" `
    --success "Login successful" `
    --goal-id "explore-login" `
    --max-steps 20
```

### CLI Arguments

| Argument | Default | Description |
|---|---|---|
| `--window` | `Sample Login App (.NET 8)` | Target window title |
| `--goal` | Login with admin/password123 | Goal description for the agent |
| `--success` | `Login successful` | Text to match in UI for success |
| `--goal-id` | `login` | Short identifier for logs/artifacts |
| `--max-steps` | `30` | Maximum agent steps |

## Run Artifacts

Each agent run produces a directory under `runs/<run-id>/`:
- `report.json` — full structured report with all steps
- `summary.md` — human-readable Markdown summary
- `screenshots/` — PNG screenshot at each step

## WORKFLOW.md

The `WORKFLOW.md` file at the project root defines:
- Agent settings (concurrency, max turns, retry backoff)
- Scoring thresholds
- Predefined goals (default, explore, smoke)
- LLM configuration (with `$ENV_VAR` support)
- Prompt template for the agent

## Roadmap

See `docs/roadmap.md` for the full plan from V1 through V8.

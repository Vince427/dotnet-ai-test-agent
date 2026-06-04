# Getting Started

A 10‑minute orientation: what this tool is, the one mental model to hold, and the exact
commands to go from nothing to a recorded, viewable test run.

## What it is

It drives **existing** Windows desktop apps (WinForms, WPF, Avalonia, MAUI) from the
*outside* using UI Automation — like a user would — to check that real flows work. You
never add anything to the app under test.

## The one mental model

```
tests/*.yaml  →  CLI runner (the agent loop)  →  artifacts in runs/  →  Workbench / Dashboard / JUnit
   (what to test)        (does it)                 (evidence)              (how you read it)
```

- **You write intent** in YAML: a goal in plain language + a success condition.
- **The agent absorbs the complexity**: it observes the UI, decides the next action,
  acts, scores, and records — so your YAML stays small even for rich UIs.
- **Everything is a file**: YAML and the run artifacts are the source of truth. The
  Workbench and Dashboard only *read* them.

## 1. Run without any setup (no key, no app)

These work offline — no `.env`, no LLM, no desktop app:

```powershell
# Validate every YAML plan
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --validate-plan --format json

# List all tests
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --list-tests

# Render the static Workbench (open the generated docs/symphony.html)
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\render-ui.ps1
```

## 2. Run an actual test against an app

A run needs (a) the target app open on an interactive desktop and (b) a "brain" to decide
actions. You have three brains — pick by how much you want to spend/automate:

| Brain | Key? | How | Best for |
|---|---|---|---|
| **Direct LLM** | yes | set `LLM_ENDPOINT`/`LLM_API_KEY` (OpenRouter, a local proxy, …) | real, dynamic runs |
| **Heuristic** | no | a rule-based decider (used in tests) | automated CI smoke |
| **Bridge** | no | `--bridge-llm` — you or an agent answer each step | exploring without a key |

Direct-LLM example (after copying `.env.template` → `.env` and launching the sample):

```powershell
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- `
    --plan .\tests\examples\winforms\login.yaml --test-id EX-WINFORMS-LOGIN-001 --evidence-level standard
```

No‑key bridge example (terminal A starts the bridge, terminal B runs the test pointed at it):

```powershell
# A: start the bridge
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --bridge-llm 8088
# B: run, answering each bridge-io\req-N.txt with a resp-N.json action
$env:LLM_ENDPOINT="http://localhost:8088"; $env:LLM_API_KEY="bridge"; $env:LLM_MODEL="bridge"
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --plan ... --test-id ...
```

## 3. See the results

```powershell
# Interactive local dashboard (catalog, launch, live logs/screenshots, file explorer)
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --dashboard 8090
# then open http://localhost:8090/

# Or a CI report
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --to-junit artifacts\junit-results.xml
```

Each run also writes `runs/<id>/report.json`, `summary.md`, and (unless evidence is
`minimal`) screenshots — secret fields are masked at capture.

## 4. Write your first test

Create `tests/created/MY-FIRST-001.yaml` (or use the dashboard's **Create** form):

```yaml
suite: created
tests:
  MY-FIRST-001:
    title: "Login happy path"            # human label
    framework: "winforms"                # winforms | wpf | avalonia | maui
    target_window: "Sample Login App (.NET 8)"  # exact window title to attach to
    goal: "Enter admin / password123, click Login, confirm success."  # plain-language intent
    success_condition: "Login successful"  # text the app shows when done (optional)
    max_steps: 8                          # safety cap on agent iterations
    allowed_actions: ["EnterText", "Click", "Assert", "Done", "Wait"]
    tags: ["smoke", "login"]
    # Optional traceability — these surface as JUnit <testcase> properties:
    # existing_tests: ["MyApp.UiTests.LoginTests.HappyPath"]
    # source_issue: "JIRA-1234"
```

Validate it (`--validate-plan`), then run it. The sample apps' controls have tooltips that
show each control's **AutomationId** — that's what you reference in YAML.

## Where to go next

- [Authoring guide](ai-authoring.md) — full YAML metadata + schema.
- [Testing strategy](testing-strategy.md) — brains, CI/CD, provider options.
- [Architecture](architecture.md) and the [decisions log](architecture-decisions.md) — the *why*.
- [Roadmap](roadmap.md) and [Changelog](../CHANGELOG.md) — where it's going / what shipped.

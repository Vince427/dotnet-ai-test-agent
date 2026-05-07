# Architecture

Desktop AI Test Agent follows an OpenAI Symphony-inspired architecture adapted for UI testing.

## Core Principle

The target application is treated as a black box.

- No required code changes in the target app.
- No required agent-specific classes.
- No required package reference from the target app to this project.
- Tests live outside the app in YAML.
- Existing unit, integration, and CI tests remain in place.

## Layers

1. **Policy Layer**: `WORKFLOW.md` and `tests/*.yaml`.
2. **Configuration Layer**: `WorkflowConfig` and runner options.
3. **Coordination Layer**: a single deterministic orchestrator.
4. **Execution Layer**: automation drivers such as FlaUI and later Appium.
5. **Intelligence Layer**: LLM decisions through OpenRouter, OpenAI-compatible APIs, or local proxies.
6. **Guard Layer**: deterministic checks that can force reject or abort LLM actions.
7. **Observability Layer**: structured logs, JSON reports, Markdown summaries, screenshots, and the static Symphony Workbench.

Manual surfaces are first-class. YAML validation, test listing, artifact reading, and workbench rendering must run without `.env`, OpenRouter, or an LLM. AI agents and future MCP/plugins should call these same surfaces instead of receiving hidden special capabilities.

## Symphony Loop

```text
Observe -> Decide -> Act -> Guard -> Score -> Record
```

The LLM proposes actions, but the orchestrator owns safety, constraints, scoring, and final state.

## Framework Strategy

- **WinForms**: FlaUI + Windows UI Automation.
- **WPF**: FlaUI + Windows UI Automation.
- **Avalonia Windows Desktop**: UIA3 first, vision fallback when the accessibility tree is incomplete.
- **.NET MAUI Windows**: FlaUI or Appium Windows depending on packaging and UI exposure.
- **.NET MAUI Android/iOS/Mac**: Appium in a later phase.

## Test Backlog

YAML is the source of truth for directed tests:

```yaml
tests:
  LOGIN-001:
    title: "Login succeeds with valid credentials"
    priority: "P0"
    framework: "winforms"
    target_window: "Sample Login App (.NET 8)"
    goal: "Log in with admin/password123."
    success_condition: "Login successful"
    max_steps: 8
    allowed_actions: ["EnterText", "Click", "Assert", "Done", "Wait"]
```

The mini UI reads these files; it must not become a hidden database.

## Existing Tests

The agent complements existing tests. Teams may already have unit tests, integration tests, or CI checks. The roadmap supports linking or importing those results instead of replacing them.

Example:

```yaml
existing_tests:
  - "MyApp.Tests.AuthTests.ValidLogin"
  - "ci:smoke-login"
```

## UI/UX Rule

The Symphony Workbench should remain simple:

- read YAML and artifacts;
- show clear status and evidence;
- provide useful choices and filters;
- avoid hiding the source of truth;
- avoid becoming a heavy dashboard too early.

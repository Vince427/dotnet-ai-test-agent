# Repo Structure

```text
src/
  Core/                            - shared models + driver interface (no dependencies)
    AgentAction.cs  AgentGoal.cs  IAutomationDriver.cs  UiElement.cs  UiSnapshot.cs
  UIAutomation/                    - FlaUI/UIA3 desktop driver
    FlaUiDesktopDriver.cs          - attach, capture snapshot, act, screenshot
    ScreenshotMasker.cs            - mask secret-field regions in screenshots
  AgentRunner/                     - CLI + the agent loop + artifacts
    Program.cs                     - CLI parse + manual commands + runtime wiring
    RunOrchestrator.cs             - the observe->decide->act->score->record loop (injectable)
    IActionDecider.cs              - the "decide" seam
    LlmService.cs                  - LLM decider (any OpenAI-compatible endpoint)
    HeuristicActionDecider.cs      - rule-based, no-LLM decider
    BridgeLlmServer.cs             - --bridge-llm: human/agent-in-the-loop endpoint (no key)
    RunnerTelemetry.cs             - opt-in OpenTelemetry (spans + metrics)
    ScreenshotRedaction.cs         - compute secret regions to mask
    PromptBuilder.cs  LlmResponseParser.cs  SecretRedactor.cs  AgentMemory.cs
    ScoringEngine.cs  LoopDetector.cs  QualityGuards.cs  AgentActionValidator.cs
    ArtifactWriter.cs  RunArtifact.cs  RunArtifactLoader.cs  GuardFailureDemoFactory.cs
    JUnitReportWriter.cs           - --to-junit CI report
    SymphonyWorkbenchGenerator.cs  - static AgentLoop Workbench (HTML)
    TestPlanLoader.cs  TestPlanValidator.cs  TestDefinition.cs  RunnerOptions.cs  WorkflowConfig.cs
    Dashboard/                     - local-only interactive dashboard (--dashboard)
      DashboardServer.cs  DashboardApi.cs  RunJobManager.cs  DashboardHtml.cs
  AgentRunner.Tests/               - xUnit tests (incl. MockLlmServer + gated UI E2E)
  Samples/                         - demo target apps (never reference agent code)
    Sample.WinFormsApp.Net8/  Sample.WinFormsApp.Net48/
    Sample.WpfApp/  Sample.WpfApp.Net48/
    Sample.AvaloniaApp/  Sample.MauiApp/

tests/        - YAML test plans (source of truth); examples/, testzoo, created/
schemas/      - test-plan.schema.json (editor hints + validation)
docs/         - public docs; getting-started, ai-authoring, testing-strategy, roadmap, …
scripts/      - local validation, render, demo, guard-demo commands
.claude/      - agent context: per-domain contracts, plans, discovery log, hooks
runs/         - generated per-run artifacts (gitignored); bridge-io/ likewise
```

## Folder Roles

- `src/Core`: models, the `IAutomationDriver` interface, lightweight abstractions.
- `src/UIAutomation`: FlaUI/UIA3 driver + screenshot masking.
- `src/AgentRunner`: the `RunOrchestrator` loop, the three deciders (LLM / heuristic /
  bridge), artifacts, telemetry, JUnit, the static Workbench, and the `Dashboard/`.
- `src/AgentRunner.Tests`: unit tests + gated interactive UI E2E (`RUN_E2E_UI=1`).
- `src/Samples`: demo targets (WinForms, WPF, Avalonia, MAUI) — non-intrusive, no agent deps.
- `tests/`, `schemas/`, `docs/`, `scripts/`: YAML plans, schema, public docs, helper scripts.
- `.claude/`: agent working context (domain contracts, plan, discovery log, dev-loop hooks).
- `runs/` and `bridge-io/`: generated runtime output, ignored by git.

# V1.3 Architecture — Symphony-Inspired

## Design

Follows OpenAI Symphony's layered architecture adapted for UI testing:

### Layers

1. **Policy Layer** — `WORKFLOW.md` (goals, prompt template, runtime settings)
2. **Configuration Layer** — `WorkflowConfig.cs` (typed getters, $VAR resolution)
3. **Coordination Layer** — `Program.cs` (orchestrator: observe → decide → act → score → record)
4. **Execution Layer** — `FlaUiDesktopDriver.cs` (UI automation via FlaUI)
5. **Intelligence Layer** — `LlmService.cs` (LLM decisions via Microsoft.Agents.AI)
6. **Observability Layer** — `StructuredLogger.cs` + `ArtifactWriter.cs` (logs + artifacts)

### Components

- **Core**: shared contracts, models (UiElement, UiSnapshot, AgentGoal, AgentAction)
- **UIAutomation**: FlaUI driver with generic tree walking and screenshot capture
- **AgentRunner**: orchestrator loop + LLM + memory + scoring + loop detection + artifacts
- **Samples**: WinForms target applications (.NET 4.8 + .NET 8)

## Strategy

Use Symphony's daemon-style orchestration pattern but adapted for desktop UI testing:
- Issues → Goals (test objectives)
- Codex agent → LLM + FlaUI agent
- Workspace → Run artifact directory
- Retry + backoff → built into scoring engine

# Repo structure

```text
src/
  AgentRunner/
    Program.cs          — AgentLoop-inspired orchestrator loop
    LlmService.cs       — LLM integration (Microsoft.Agents.AI)
    AgentMemory.cs      — history + facts + visited screens
    LoopDetector.cs     — anti-loop sliding window
    ScoringEngine.cs    — reward/penalty scoring
    StructuredLogger.cs — key=value structured logging
    RunArtifact.cs      — run report model
    ArtifactWriter.cs   — writes JSON + screenshots + markdown
    WorkflowConfig.cs   — typed config from WORKFLOW.md
  Core/
    AgentAction.cs      — action model
    AgentGoal.cs        — goal model
    IAutomationDriver.cs — driver interface
    UiElement.cs        — UI element model
    UiSnapshot.cs       — UI state snapshot
  UIAutomation/
    FlaUiDesktopDriver.cs — FlaUI implementation
  Samples/
    Sample.WinFormsApp.Net8/
    Sample.WinFormsApp.Net48/

docs/
  spec.md
  architecture.md
  repo-structure.md
  roadmap.md

scripts/
  run-net8.ps1
  run-net48.ps1
  check.ps1
  run-demo.ps1

WORKFLOW.md    — AgentLoop-style policy + config
README.md
LICENSE
```

## Folder roles

- `src/Core`: models, interfaces, and lightweight abstractions.
- `src/UIAutomation`: FlaUI integration layer with generic tree walking.
- `src/AgentRunner`: AgentLoop-inspired orchestrator (observe -> decide -> act -> guard -> score -> record).
- `src/Samples`: demo target applications.
- `docs/`: project source-of-truth documents.
- `scripts/`: local demo and verification scripts.
- `runs/`: generated per-run artifact directories (gitignored).

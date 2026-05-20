# Repo Structure

```text
src/
  AgentRunner/
    Program.cs                    - AgentLoop orchestrator loop
    LlmService.cs                 - LLM integration
    AgentMemory.cs                - history, facts, and visited screens
    LoopDetector.cs               - anti-loop sliding window
    ScoringEngine.cs              - reward and penalty scoring
    StructuredLogger.cs           - structured console logging
    RunArtifact.cs                - run report model
    ArtifactWriter.cs             - JSON, screenshots, and Markdown artifacts
    GuardFailureDemoFactory.cs    - deterministic guard demo artifacts
    WorkflowConfig.cs             - typed config from WORKFLOW.md
  Core/
    AgentAction.cs                - action model
    AgentGoal.cs                  - goal model
    IAutomationDriver.cs          - driver interface
    UiElement.cs                  - UI element model
    UiSnapshot.cs                 - UI state snapshot
  UIAutomation/
    FlaUiDesktopDriver.cs         - FlaUI implementation
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
  write-guard-demos.ps1

WORKFLOW.md
README.md
LICENSE
```

## Folder Roles

- `src/Core`: models, interfaces, and lightweight abstractions.
- `src/UIAutomation`: FlaUI integration layer with generic tree walking.
- `src/AgentRunner`: AgentLoop orchestrator and manual CLI modes.
- `src/Samples`: demo target applications.
- `docs/`: project source-of-truth documents.
- `scripts/`: local demo and verification scripts.
- `runs/`: generated per-run artifact directories, ignored by git.

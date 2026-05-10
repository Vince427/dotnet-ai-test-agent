# Context: Runner

Owns the executable orchestration loop and manual CLI surface.

## Files

- `src/AgentRunner/Program.cs`
- `src/AgentRunner/RunnerOptions.cs`
- `src/AgentRunner/LlmService.cs`
- `src/AgentRunner/WorkflowConfig.cs`
- `src/AgentRunner/ArtifactWriter.cs`
- `src/AgentRunner/RunArtifact.cs`
- `src/AgentRunner/ScoringEngine.cs`
- `src/AgentRunner/LoopDetector.cs`
- `src/AgentRunner/QualityGuards.cs`
- `src/AgentRunner/ManualCommandOutput.cs`
- `src/AgentRunner.Tests/**`
- `src/Core/AgentGoal.cs`

## Invariants

- Manual modes `--validate-plan`, `--list-tests`, and `--render-ui` must not
  require `.env`, LLM access, FlaUI, or a target app.
- `--format json` must keep stdout parseable JSON.
- `Done` is not success unless the configured success condition is visible or
  the test has no success condition by design.
- Unsupported actions and missing targets must fail visibly, not become no-op
  successes.
- Loop detection records real actions, not synthetic pending markers.
- Runtime artifacts stay human-readable and machine-readable.
- Thread sleeps in async runtime paths should use `Task.Delay`.

## Validation

```powershell
dotnet test .\DesktopAiTestAgent.sln --no-restore -v minimal
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --validate-plan --format json
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --list-tests --format json
```

## Cross-Domain Notes

- CLI flags that select YAML tests also touch `workflow.md`.
- Artifact shape changes also touch `workbench.md` and docs.
- Provider config or secret logging changes also touch `security.md`.


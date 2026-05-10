# Context: Symphony Workbench

Owns the local static UI over YAML specs and run artifacts.

## Files

- `src/AgentRunner/SymphonyWorkbenchGenerator.cs`
- `src/AgentRunner.Tests/SymphonyWorkbenchGeneratorTests.cs`
- `scripts/render-ui.ps1`
- `docs/symphony.html` (generated and ignored)
- README/docs sections that describe workbench usage

## Invariants

- Static, local, read-only first.
- No database, no auth, no server requirement.
- YAML and artifacts remain source of truth.
- The UI should make selection and diagnosis simple: suite, test id, framework,
  status/result filters, run summaries, guard failures, screenshots, and prompt
  previews later.
- Generated `docs/symphony.html` should stay ignored unless policy changes.

## Validation

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\render-ui.ps1
dotnet test .\DesktopAiTestAgent.sln --no-restore -v minimal
```

## Cross-Domain Notes

- Artifact schema changes touch `runner.md`.
- YAML field additions touch `workflow.md`.


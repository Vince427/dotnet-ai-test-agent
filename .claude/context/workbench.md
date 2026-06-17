# Context: AgentLoop Workbench

Owns the local static UI over YAML specs and run artifacts.

Product language is AgentLoop Workbench. The code symbols were renamed
`Symphony*`→`AgentLoop*` (D3); the **generated** `docs/agentloop.html` artifact name
completes the rename (see `docs/github-pages.md`).

## Files

- `src/AgentRunner/AgentLoopWorkbenchGenerator.cs`
- `src/AgentRunner.Tests/AgentLoopWorkbenchGeneratorTests.cs`
- `scripts/render-ui.ps1`
- `docs/agentloop.html` (generated and ignored)
- README/docs sections that describe workbench usage

## Invariants

- Static, local, read-only first.
- No database, no auth, no server requirement.
- YAML and artifacts remain source of truth.
- The UI should make selection and diagnosis simple: suite, test id, framework,
  status/result filters, run summaries, guard failures, screenshots, and prompt
  previews.
- The Test Backlog table carries a **Notes** column (non-fatal `TestPlanValidator`
  policy advisories per test, computed at generation via `LoadTestsAndWarnings`,
  prefix-stripped) and a **Prompt** column with a `<details>` baking the key-free
  `PromptPreview.BuildForTest(test)` output — fully static, no view-time server.
- Generated `docs/agentloop.html` should stay ignored unless policy changes.

## Validation

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\render-ui.ps1
dotnet test .\DesktopAiTestAgent.sln --no-restore -v minimal
```

## Cross-Domain Notes

- Artifact schema changes touch `runner.md`.
- YAML field additions touch `workflow.md`.

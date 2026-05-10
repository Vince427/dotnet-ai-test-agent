# Context: Workflow And YAML Specs

Owns Symphony policy, YAML test plans, schema, and agent authoring contracts.

## Files

- `WORKFLOW.md`
- `tests/*.yaml`
- `schemas/test-plan.schema.json`
- `prompts/create-desktop-test.md`
- `docs/ai-authoring.md`
- `src/AgentRunner/TestDefinition.cs`
- `src/AgentRunner/TestPlanLoader.cs`
- `src/AgentRunner/TestPlanValidator.cs`
- `src/AgentRunner.Tests/TestPlan*Tests.cs`

## Invariants

- YAML remains the source of truth for directed tests.
- Tests must be readable and editable by humans.
- Any AI-authored YAML field must also make sense manually.
- Validation must run without `.env` or LLM access.
- Schema and loader must evolve together.
- Traceability metadata should stay optional but useful:
  `source_issue`, `source_pr`, `authoring_agent`, `risk`, `ci_profile`,
  `existing_tests`.
- Keep `allowed_actions` explicit and bounded.

## Validation

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\validate-test-plans.ps1 -SkipRestore
dotnet test .\DesktopAiTestAgent.sln --no-restore -v minimal
```

## Cross-Domain Notes

- New action types require `automation.md` and runner changes.
- Workbench fields require `workbench.md`.
- CI output format changes require `ci.md`.


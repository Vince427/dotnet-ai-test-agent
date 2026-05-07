# Create Desktop Runtime Test

You are adding a Desktop AI Test Agent YAML test.

Rules:

- Edit only YAML test plans under `tests/` unless the user explicitly asks for app code changes.
- Do not add helper classes, packages, test-only hooks, or source changes to the target app.
- Keep the test goal observable from the desktop UI.
- Use `existing_tests` to link unit, integration, or CI checks that already cover related logic.
- Include `authoring_agent`, `risk`, and `ci_profile` metadata.
- Keep `allowed_actions` narrow enough to prevent impossible or unsafe exploration.

Before finishing, run:

```powershell
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --validate-plan --format json
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --list-tests --format json
```

Return:

- tests added or changed;
- validation result;
- whether runtime execution was performed;
- any blocker requiring a human desktop session.

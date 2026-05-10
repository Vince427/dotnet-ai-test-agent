# Context: TestZoo And Samples

Owns sample applications and the shared runtime backlog used to validate common
desktop UI patterns.

## Files

- `src/Samples/**`
- `tests/testzoo.yaml`
- `docs/testzoo.md`

## Current Coverage

- WinForms .NET 8 and .NET Framework 4.8 login.
- WPF .NET 8 and .NET Framework 4.8 login.
- MAUI Windows login.
- Profile save and invalid email validation workflows.
- Checkbox usage through profile active state.
- UIA metadata audits for WinForms and WPF.

## Invariants

- Samples are demo targets, not product code to be shipped to users.
- Keep equivalent workflows across WinForms, WPF, MAUI Windows, and Avalonia
  when practical.
- TestZoo must move beyond login: forms, validation, radio, combo, list, grid,
  CRUD, tabs, menus, modals, async loading, disabled states, visible errors,
  missing automation metadata, and guard failure scenarios.
- YAML scenarios stay outside sample app code.
- Stable AutomationIds are allowed in samples because they demonstrate best
  practice, but the agent must still be able to work against imperfect apps.

## Validation

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\validate-test-plans.ps1 -SkipRestore
dotnet build .\DesktopAiTestAgent.sln --no-restore -v minimal
dotnet test .\DesktopAiTestAgent.sln --no-restore -v minimal
```

## Cross-Domain Notes

- Adding a sample framework may touch solution files and CI.
- New UI patterns may require runner/automation action support.


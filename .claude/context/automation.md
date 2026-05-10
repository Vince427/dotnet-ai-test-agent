# Context: UI Automation

Owns black-box desktop automation and UI tree capture.

## Files

- `src/UIAutomation/**`
- `src/Core/IAutomationDriver.cs`
- `src/Core/AgentAction.cs`
- `src/Core/UiElement.cs`
- `src/Core/UiSnapshot.cs`

## Invariants

- The target app is tested from the outside.
- Do not require target apps to reference agent assemblies.
- Prefer stable UI Automation metadata first: AutomationId, Name, ControlType,
  and supported patterns.
- WinForms and WPF use FlaUI/UIA on Windows.
- MAUI Windows may use FlaUI or Appium Windows depending on exposure.
- Avalonia starts with UIA3, with vision fallback later when the tree is flat.
- Action execution must be bounded by YAML `allowed_actions`.
- UI tree snapshots must avoid secrets and remain useful for evidence.

## Validation

```powershell
dotnet build .\DesktopAiTestAgent.sln --no-restore -v minimal
dotnet test .\DesktopAiTestAgent.sln --no-restore -v minimal
```

## Cross-Domain Notes

- New actions also touch `runner.md`, `workflow.md`, schema, and tests.
- Vision fallback belongs to future V3 and must stay cost-aware.


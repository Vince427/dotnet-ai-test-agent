# Context: UI Automation

Owns black-box desktop automation and UI tree capture.

## Files

- `src/UIAutomation/**` (incl. `ScreenshotMasker.cs`, `ScreenshotAnnotator.cs`)
- `src/Core/IAutomationDriver.cs`
- `src/Core/AgentAction.cs`
- `src/Core/UiElement.cs`
- `src/Core/UiSnapshot.cs` (`WindowBounds` = screenshot origin, same space as `UiElement.BoundingBox`)

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
- Screenshots redact secrets at capture: the runner masks regions of fields
  `SecretRedactor.IsSensitiveIdentifier` flags, mapped via `WindowBounds`. Keep
  `WindowBounds`/`BoundingBox` in the same screen-coordinate space so the mapping holds.
  `ScreenshotMasker` is a pure image op (no secret knowledge); it must never throw away
  a screenshot on failure.
- V3 Tier-2 overlay (the prerequisite for the VLM decider): `ScreenshotOverlay` (runner) numbers
  each visible, locatable element and maps it to image pixels via `WindowBounds` (same mapping as
  redaction); `ScreenshotAnnotator` (a pure image op like `ScreenshotMasker`, never throws away a
  shot) draws the numbered boxes. Emitted at `full` evidence as `overlay/step_NNN.{png,json}`,
  drawn on top of the already-masked bytes. The index carries identifiers only — never a control's
  `Value` — so it is secret-safe even for password fields.

## Validation

```powershell
dotnet build .\DesktopAiTestAgent.sln --no-restore -v minimal
dotnet test .\DesktopAiTestAgent.sln --no-restore -v minimal
```

## Cross-Domain Notes

- New actions also touch `runner.md`, `workflow.md`, schema, and tests.
- Vision fallback belongs to future V3 and must stay cost-aware.


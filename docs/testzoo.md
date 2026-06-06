# TestZoo

TestZoo is the shared desktop runtime backlog used to validate common UI patterns across frameworks.

Current seed coverage lives in `tests/testzoo.yaml` with 24 TestZoo cases. As
the backlog grows, split new scenarios into smaller files under
`tests/testzoo/`, one business/E2E workflow per YAML file.

- Focused example plans now live under `tests/examples/winforms/`,
  `tests/examples/wpf/`, and `tests/examples/maui/`. Each file contains one
  runtime scenario, so a human or coding agent can inspect and run a single
  behavior without loading the full aggregate TestZoo backlog.
- Run the focused examples with `scripts/run-ui-examples.ps1`. Use `-WhatIf` or
  `-DryRun` to print the exact commands without launching desktop apps.

- WinForms .NET 8 login success and failure.
- WinForms .NET Framework 4.8 login success.
- WPF .NET 8 login success.
- WPF .NET Framework 4.8 login success.
- .NET MAUI Windows login success.
- WinForms, WPF, and .NET MAUI profile form save workflows.
- Profile validation failure workflows for invalid email input.
- WinForms and WPF .NET 8 radio, combo, list, and grid control workflows.
- WinForms and WPF .NET 8 in-window modal confirmation workflows.
- WinForms and WPF .NET 8 disabled-to-enabled protected action workflows.
- WinForms and WPF .NET 8 async loading workflows.
- UI automation metadata audits for WinForms and WPF.
- Deterministic guard failure demo artifacts for missing action target,
  crash/closed-window capture failure, empty UIA tree, and unexpected modal.

## Rules

- Tests stay outside target application code.
- Each framework should eventually cover the same user-visible workflows.
- Expand WinForms and WPF first. Keep .NET MAUI Windows aligned for stable
  login/profile workflows, then add Avalonia parity after a concrete Avalonia
  sample exists.
- YAML remains editable manually and by coding agents.
- CI can validate the backlog without launching desktop apps or loading `.env`.

## Next Coverage

Add WinForms/WPF sample screens and YAML tests first for:

- checkbox, radio, combo, list, and grid controls;
- CRUD;
- tabs and menus;
- modal dialogs;
- async loading;
- disabled controls;
- visible error states;
- ambiguous or missing automation metadata;
- real runtime guard failures once the runner has injectable E2E seams.

Then port the remaining stable workflows to MAUI Windows and Avalonia.

## Focused Runtime Examples

The example suite keeps WinForms/WPF as the runtime proof baseline. MAUI
Windows is available explicitly for the stable login/profile flows. Avalonia
remains next because the repo does not yet contain an Avalonia sample app.

| Scenario | WinForms YAML | WPF YAML | MAUI Windows YAML | Purpose |
|---|---|---|---|---|
| Login | `tests/examples/winforms/login.yaml` | `tests/examples/wpf/login.yaml` | `tests/examples/maui/login.yaml` | Baseline text entry, click, assert, and Done. |
| Profile save | `tests/examples/winforms/profile-save.yaml` | `tests/examples/wpf/profile-save.yaml` | `tests/examples/maui/profile-save.yaml` | Form filling, checkbox state, validation status. |
| Profile validation | `tests/examples/winforms/profile-validation.yaml` | `tests/examples/wpf/profile-validation.yaml` | `tests/examples/maui/profile-validation.yaml` | Negative email validation proof. |
| Controls | `tests/examples/winforms/controls-selection.yaml` | `tests/examples/wpf/controls-selection.yaml` | Not ported yet | Radio, combo, list, and grid discovery. |
| Modal | `tests/examples/winforms/modal-confirm.yaml` | `tests/examples/wpf/modal-confirm.yaml` | Not ported yet | In-window confirmation flow. |
| Disabled state | `tests/examples/winforms/protected-action.yaml` | `tests/examples/wpf/protected-action.yaml` | Not ported yet | Disabled-to-enabled action state. |
| Async loading | `tests/examples/winforms/async-loading.yaml` | `tests/examples/wpf/async-loading.yaml` | Not ported yet | Wait/progress/status behavior. |

Preview all commands without running desktop automation:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-ui-examples.ps1 -WhatIf
```

Generate guard failure artifacts without desktop automation:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\write-guard-demos.ps1 -OutputRoot .\artifacts\guard-demos
```

Run one focused example after configuring the runtime LLM provider:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-ui-examples.ps1 -Framework winforms -Scenario login
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-ui-examples.ps1 -Framework wpf -Scenario controls
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-ui-examples.ps1 -Framework maui -Scenario login
```

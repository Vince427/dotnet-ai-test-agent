# TestZoo

TestZoo is the shared desktop runtime backlog used to validate common UI patterns across frameworks.

Current seed coverage lives in `tests/testzoo.yaml` with 24 TestZoo cases. As
the backlog grows, split new scenarios into smaller files under
`tests/testzoo/`, one business/E2E workflow per YAML file.

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

## Rules

- Tests stay outside target application code.
- Each framework should eventually cover the same user-visible workflows.
- Expand WinForms and WPF first. Add .NET MAUI Windows and Avalonia parity after
  the richer WinForms/WPF workflows are stable.
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
- ambiguous or missing automation metadata.

Then port the stable workflows to MAUI Windows and Avalonia.

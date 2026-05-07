# TestZoo

TestZoo is the shared desktop runtime backlog used to validate common UI patterns across frameworks.

Current seed coverage lives in `tests/testzoo.yaml` with 16 TestZoo cases:

- WinForms .NET 8 login success and failure.
- WinForms .NET Framework 4.8 login success.
- WPF .NET 8 login success.
- WPF .NET Framework 4.8 login success.
- .NET MAUI Windows login success.
- WinForms, WPF, and .NET MAUI profile form save workflows.
- Profile validation failure workflows for invalid email input.
- UI automation metadata audits for WinForms and WPF.

## Rules

- Tests stay outside target application code.
- Each framework should eventually cover the same user-visible workflows.
- YAML remains editable manually and by coding agents.
- CI can validate the backlog without launching desktop apps or loading `.env`.

## Next Coverage

Add equivalent sample screens and YAML tests for:

- checkbox, radio, combo, list, and grid controls;
- CRUD;
- tabs and menus;
- modal dialogs;
- async loading;
- disabled controls;
- visible error states;
- ambiguous or missing automation metadata.

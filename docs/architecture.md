# V1.2 Dual Target Architecture

## Design

- Core: shared contracts and models
- UIAutomation: shared FlaUI driver
- AgentRunner: shared runner
- Sample.WinFormsApp.Net48: legacy sample
- Sample.WinFormsApp.Net8: modern sample

## Strategy

Use shared libraries for common behavior and separate WinForms sample apps for each target runtime.

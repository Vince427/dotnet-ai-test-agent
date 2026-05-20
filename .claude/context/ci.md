# Context: CI, Scripts, And Repo Workflow

Owns repeatable validation commands, CI templates, and contributor workflow.

## Files

- `.github/**`
- `.azure/**`
- `scripts/**`
- `docs/ticket-to-evidence-ci.md`
- `README.md`
- `docs/architecture.md`
- `docs/roadmap.md`
- `docs/spec.md`
- `project_rules.md`
- `DesktopAiTestAgent.sln`

## Invariants

- CI examples validate YAML without secrets.
- Runtime desktop execution is separate from static validation unless explicitly
  configured on a Windows runner with a launched target app.
- Scripts should be readable PowerShell and work from repo root.
- Do not hide required provider secrets in scripts.
- Keep `dotnet build`, `dotnet test`, and plan validation as the core checks.
- Do not introduce heavy infrastructure before a concrete need.
- Ticket-to-evidence CI stays `workflow_dispatch` only on hosted runners and
  uses `run-ticket-proof.ps1 -TicketPath <ticket.md> -SkipRuntime -DryRun`;
  real desktop automation belongs on local or self-hosted interactive Windows
  runners.

## Validation

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\validate-test-plans.ps1 -SkipRestore
dotnet build .\DesktopAiTestAgent.sln --no-restore -v minimal
dotnet test .\DesktopAiTestAgent.sln --no-restore -v minimal
```

## Cross-Domain Notes

- Adding projects requires solution and CI awareness.
- Release or packaging work may need a new context file later.

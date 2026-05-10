## Summary

- 

## Portable Contract Checklist

- [ ] I did not commit a real `.env` or any secret.
- [ ] I did not add agent-specific classes, packages, hooks, or test-only code paths to a target app.
- [ ] The change preserves the portable contract: YAML specs -> CLI runner -> artifacts -> Symphony Workbench.
- [ ] YAML changes are under `tests/` or an external test pack.
- [ ] New or changed tests include clear `goal`, `success_condition`, `allowed_actions`, and relevant metadata.
- [ ] Existing unit/integration/CI checks are linked with `existing_tests` where useful.
- [ ] `--validate-plan --format json` passes.
- [ ] `--list-tests --format json` includes the expected test ids.

## Validation Commands

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\validate-test-plans.ps1 -SkipRestore
dotnet build .\DesktopAiTestAgent.sln --no-restore -v minimal
dotnet test .\DesktopAiTestAgent.sln --no-restore -v minimal
```

## Runtime Evidence

- Runtime executed: yes/no
- Evidence level: minimal/standard/full
- Artifact path or CI run:

## Agent Notes

- If authored by Codex, Claude Code, Copilot, or another agent, mention the domain context used from `.claude/context/`.
- If a cross-domain issue was found but not fixed here, add or reference an entry in `.claude/DISCOVERY_LOG.md`.

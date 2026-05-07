## Summary

- 

## Desktop Test Plan Checklist

- [ ] I did not commit a real `.env` or any secret.
- [ ] I did not add agent-specific classes, packages, hooks, or test-only code paths to a target app.
- [ ] YAML changes are under `tests/` or an external test pack.
- [ ] New or changed tests include clear `goal`, `success_condition`, `allowed_actions`, and relevant metadata.
- [ ] Existing unit/integration/CI checks are linked with `existing_tests` where useful.
- [ ] `--validate-plan --format json` passes.
- [ ] `--list-tests --format json` includes the expected test ids.

## Runtime Evidence

- Runtime executed: yes/no
- Evidence level: minimal/standard/full
- Artifact path or CI run:

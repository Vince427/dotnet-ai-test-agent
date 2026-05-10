# Active Plan

This file gives `/suite` and other agents a small executable backlog. The
source of truth remains `docs/roadmap.md`; keep this file focused on near-term
parallel work.

## Done

- [x] V1.4 YAML backlog and directed test selection.
- [x] V1.5 static Symphony Workbench.
- [x] V1.6 deterministic guards and scoring hardening.
- [x] V1.7 manual-first evidence contract.
- [x] V1.8 AI bridge via CLI, schema, CI templates, and authoring docs.
- [x] Multi-agent context system: `CLAUDE.md`, `AGENTS.md`, domain contexts,
  discovery log, and `/suite` procedure.
- [x] MVP-A: Reframe doctrine from ambiguous local-first to portable-first /
  CLI-first and add GitHub-native agent instructions.
- [x] MVP-C: Add a short GitHub PR checklist that points reviewers to plan
  validation, test list output, runtime evidence, and agent context.
- [x] MVP-D: Document the smallest useful MVP path in `docs/mvp.md`.

## Next Executable Items

- [ ] MVP-B: Keep the first user-facing path tiny: validate YAML, list tests,
  render Symphony Workbench, and run one selected desktop test.
- [ ] V2-A: Add Avalonia Windows sample project and initial TestZoo YAML entries.
- [ ] V2-B: Extend TestZoo with radio, combo, list, and grid scenarios across
  existing WinForms/WPF samples.
- [ ] V2-C: Add modal dialog and disabled/enabled state sample workflows.
- [ ] V2-D: Add guard failure demo scenarios: missing target, crash, empty UI
  tree, and unexpected modal where feasible.
- [ ] V3-A: Design UIA screenshot overlay artifact contract before adding VLM
  calls.
- [ ] V4-A: Add existing test integration fields/examples for TRX/JUnit links.
- [ ] V6-A: Add standard CI output prototype, starting with JUnit XML or a
  compact JSON summary.

## Human-Orchestrated Items

- [ ] Run real OpenRouter runtime validation against local `.env`.
- [ ] Decide whether runtime desktop tests should run in GitHub Actions,
  Azure Pipelines, or remain local-only for now.

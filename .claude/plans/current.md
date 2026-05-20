# Active Plan

This file gives `/suite` and other agents a small executable backlog. The
source of truth remains `docs/roadmap.md`; keep this file focused on near-term
parallel work.

## Done

- [x] V1.4 YAML backlog and directed test selection.
- [x] V1.5 static AgentLoop Workbench.
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
- [x] MVP-B: Keep the first user-facing path tiny: validate YAML, list tests,
  render the AgentLoop Workbench, and run one selected desktop test.
- [x] MVP-E: Start the AgentLoop naming migration in docs and public language.
  Keep existing `symphony` filenames until the code/file rename is done safely.
- [x] MVP-F: Prepare GitHub Pages as the first public docs target. Keep it
  lightweight; defer DocFX until API reference generation is useful.
- [x] V2-A: Split growing TestZoo work into smaller YAML files, one business
  scenario per file, while keeping current aggregate YAML valid.
- [x] V2-B: Extend WinForms/WPF TestZoo first with radio, combo, list, grid,
  modal, disabled/enabled, async/loading, and CRUD-like sample scenarios.
- [x] V2-C: Add guard failure demo artifacts for missing target, crash or
  closed-window capture failure, empty UI tree, and unexpected modal. These are
  deterministic manual artifacts first; real UI runtime guard E2E waits for
  injectable runner seams.

## Next Executable Items

- [ ] V2-D: Add MAUI Windows and Avalonia parity after WinForms/WPF flows are
  stable.
- [ ] V3-A: Design UIA screenshot overlay artifact contract before adding VLM
  calls.
- [ ] V3-B: Keep recording mode visible in roadmap/docs, but defer
  implementation until the action model and TestZoo flows are stable.
- [ ] V4-A: Add existing test integration fields/examples for TRX/JUnit links.
- [ ] V6-A: Add standard CI output prototype, starting with JUnit XML or a
  compact JSON summary.

## Human-Orchestrated Items

- [ ] Run real OpenRouter runtime validation against local `.env`.
- [ ] Decide whether runtime desktop tests should run in GitHub Actions,
  Azure Pipelines, or remain local-only for now.

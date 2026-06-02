# Changelog

All notable changes to this project are documented here.
Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
This project versions by capability milestones (see `docs/roadmap.md`), not SemVer yet.

## [Unreleased]

### Added
- **E2E-1 (key-free end-to-end)**: a deterministic OpenAI-compatible
  `MockLlmServer` (test infra) that returns a scripted action sequence. Three
  always-run tests drive the real `LlmService` through it (client → mock →
  parser, no API key, no egress — runs in any CI). A gated `[InteractiveUiFact]`
  (`RUN_E2E_UI=1`) launches the real WinForms sample, drives the real FlaUI
  driver via `RunOrchestrator`, and asserts the app reaches "Login successful" —
  the permanent full-stack integration test for an interactive Windows runner.
- **E2E-1 (flat-contract proof)**: a second gated E2E (`DEMO-PROTECTED-001`)
  drives a deliberately complex flow — a button disabled until another enables
  it, verified via `Assert` on a separate status region — from a YAML contract
  as flat as the login. Shows target richness does not leak into the authoring
  surface. Interactive UI tests share one desktop, so they run in a
  non-parallel xUnit collection (`InteractiveUiCollection`).
- **E2E-1 (cross-framework parity)**: both gated E2E are xUnit theories over
  WinForms **and** WPF samples (identical automation ids, identical status
  strings, only the window title differs), proving the UIA agent path is
  framework-agnostic, not WinForms-specific. 4 gated cases total when enabled.
- **WB-2 (keystone refactor)**: extracted the observe→decide→act→score→record
  loop out of `Program.Main` into an injectable `RunOrchestrator`
  (`IRunOrchestrator`) behind an `IActionDecider` seam (`LlmService` implements
  it). `Program.Main` is now CLI parse + manual commands + runtime wiring. Added
  9 deterministic loop tests driven by a fake `IAutomationDriver` + scripted
  decider — no LLM key, no FlaUI, no target app. CLI behavior unchanged.
- **Dev loop**: Claude Code hooks (`.claude/settings.json`) — a `Stop` gate that
  runs build + tests before a turn can finish (blocks on failure), and a
  `PreToolUse` Bash guard that blocks `.env` writes, `rm -rf`, force-push, and
  `--no-verify`. Scripts in `.claude/scripts/` (`verify.ps1`, `guard-bash.ps1`),
  conditional + OS-aware with an `AGENT_SKIP_VERIFY` escape hatch.
- **Dev loop**: read-only QA subagent `.claude/agents/code-reviewer.md`.
- **Reusable skill** `.claude/skills/setup-verification-loop/SKILL.md`: scaffolds
  this verification-first dev loop (hooks + QA subagent + hygiene) into any repo,
  stack-aware (dotnet/node/python/go/rust), ETH-disciplined (lean by default).
- `.editorconfig` (deterministic style) and a conservative `Directory.Build.props`.
- `CHANGELOG.md`.
- Central Package Management via `Directory.Packages.props` (MAUI sample opts out).
- `PromptBuilder` and `LlmResponseParser` extracted from `LlmService` so prompt
  assembly, response parsing, and the safe `Wait` fallback are unit-testable
  without an LLM key.
- `JUnitReportWriter` (V6-A): converts `RunArtifact` results to JUnit XML for CI
  dashboards. Wired to the CLI via `--to-junit [path]` (manual-first, no `.env`),
  backed by a shared `RunArtifactLoader` (uses `JsonStringEnumConverter`).
- Interactive AgentLoop Workbench: client-side filter/sort, per-run drill-down
  (steps, failureCode/guardCode), screenshot thumbnails, alert banner, pass-rate
  bar — still a single self-contained static HTML.
- `--render-ui --watch`: regenerate the Workbench on `runs/` changes with a
  browser meta-refresh (near-real-time, no server, no `.env`).
- `scripts/run-demo-login.ps1`: one-command guided demo (build → launch sample →
  run a test → render → open the Workbench).
- `tests/examples/demo/quick-login-check.yaml`: `DEMO-LOGIN-001` authoring example.

### Fixed
- WinForms sample: the "Case grid" label was clipped to "ase grid" — the Premium
  radio (Left 240 + Width 100 = 340) overlapped the label at Left 330 and, being
  higher in z-order, painted over its first letter. Moved the label to Left 350.
- Workbench dropped every real run: `SymphonyWorkbenchGenerator.LoadRuns`
  deserialized `report.json` without `JsonStringEnumConverter`, but
  `ArtifactWriter` writes enums as strings — so `evidenceLevel` failed to parse
  and runs were silently skipped (`runs=0`). Added the converter + a regression test.

### Notes
- Test suite: 122 tests + 2 gated UI E2E theories = 4 cases across WinForms + WPF
  (skipped unless `RUN_E2E_UI=1`; 126/126 with it). Build clean across
  net48 + net8.0-windows + MAUI.
- Runtime agent execution still needs a local `.env` (OpenRouter) and a launched
  desktop app; validation, listing, Workbench rendering, and the watch loop do not.

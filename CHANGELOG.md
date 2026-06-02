# Changelog

All notable changes to this project are documented here.
Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
This project versions by capability milestones (see `docs/roadmap.md`), not SemVer yet.

## [Unreleased]

### Added
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
- Workbench dropped every real run: `SymphonyWorkbenchGenerator.LoadRuns`
  deserialized `report.json` without `JsonStringEnumConverter`, but
  `ArtifactWriter` writes enums as strings — so `evidenceLevel` failed to parse
  and runs were silently skipped (`runs=0`). Added the converter + a regression test.

### Notes
- Test suite: 104 tests, build clean across net48 + net8.0-windows + MAUI.
- Runtime agent execution still needs a local `.env` (OpenRouter) and a launched
  desktop app; validation, listing, Workbench rendering, and the watch loop do not.

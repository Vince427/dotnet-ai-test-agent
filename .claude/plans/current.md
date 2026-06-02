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

## In Review — 2026-06-01 session (branches pushed to origin, NOT merged)

Six independent feature branches are on origin; each builds 0 warn/0 err and its
tests are green. Merge order: 1-4 and 6 are based on `main` (any order); branch 5
is stacked on 4, so merge 4 first (if 4 is squash-merged, rebase 5 onto `main`).

- [ ] `claude/ci-central-packages` — Central Package Management
  (`Directory.Packages.props`); MAUI sample opts out (workload-managed versions).
- [ ] `claude/runner-llm-seams` — extract `PromptBuilder` + `LlmResponseParser`
  from `LlmService` so prompt assembly + response parsing + the Wait fallback are
  unit-testable WITHOUT an LLM key (+22 tests). The only non-deterministic step
  left is the `_agent.RunAsync` network call.
- [ ] `claude/runner-junit-output` — `JUnitReportWriter` (V6-A prototype):
  `RunArtifact` -> JUnit XML (+13 tests). CLI wiring (`--to-junit`) still TODO.
- [ ] `claude/workbench-interactive` — interactive static dashboard (filter/sort,
  per-run drill-down with steps + failureCode/guardCode, screenshot thumbnails,
  alert banner, pass-rate bar) + BUG FIX: the workbench silently dropped every
  real run (see DISCOVERY_LOG) (+8 tests).
- [ ] `claude/workbench-watch` — `--render-ui --watch`: regenerate on `runs/`
  change (FileSystemWatcher, debounced) + browser meta-refresh; near-real-time,
  no server, no `.env` (+4 tests). STACKED on `claude/workbench-interactive`.
- [ ] `claude/ci-demo-script` — `scripts/run-demo-login.ps1`: one-command guided
  demo (build + launch sample + run a test + render + open the Workbench).

Note: untracked file `tests/examples/demo/quick-login-check.yaml` was created to
demonstrate authoring; keep it as an example or delete it.

## Next Executable Items

- [ ] V2-D: Add MAUI Windows and Avalonia parity after WinForms/WPF flows are
  stable.
- [ ] V3-A: Design UIA screenshot overlay artifact contract before adding VLM
  calls.
- [ ] V3-B: Keep recording mode visible in roadmap/docs, but defer
  implementation until the action model and TestZoo flows are stable.
- [ ] V4-A: Add existing test integration fields/examples for TRX/JUnit links.
- [ ] V6-A: standard CI output. PROTOTYPE DONE (`JUnitReportWriter` on branch
  `claude/runner-junit-output`). Remaining: wire a `--to-junit` manual command
  (best after the Program.Main refactor) and emit alongside `report.json`.
- [ ] WB-1: After merging the six In-Review branches, re-run build + test on
  `main` to confirm green, then delete the merged branches.
- [ ] WB-2 (refactor): extract `Program.Main` (~694 lines) into an injectable
  `IRunOrchestrator` + phase services. Unlocks deterministic loop tests with a
  fake LLM/driver, and clean CLI wiring for `--to-junit` and watch.
- [ ] OBS-1 (Phase 2, observability): emit OpenTelemetry from the runner
  (`ActivitySource` for observe/decide/act/guard/score/record + `Meter` for
  tokens/scores/durations). On net48 use `OtlpExportProtocol.HttpProtobuf` (gRPC
  is unsupported on .NET Framework since OTLP exporter 1.12.0). View live in the
  standalone Aspire dashboard (`aspire dashboard run --allow-anonymous`, OTLP
  HTTP :4318). Store the traceId in `report.json` and link it from the static
  workbench so results -> live trace is one click.
- [ ] OBS-2 (Phase 3, optional): local all-in-one dashboard app (list + launch +
  live screenshots + final report + link to the Aspire trace). Local-only dev
  tool, never in CI; reuses the static workbench rendering + the CLI contract.

## Human-Orchestrated Items

- [ ] Run real OpenRouter runtime validation against local `.env`.
- [ ] Decide whether runtime desktop tests should run in GitHub Actions,
  Azure Pipelines, or remain local-only for now.

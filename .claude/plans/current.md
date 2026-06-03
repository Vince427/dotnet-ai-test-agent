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

- [x] 2026-06-01 session (merged): Central Package Management; `PromptBuilder` +
  `LlmResponseParser` seams (+22); interactive workbench dashboard + run-loading
  bug fix (+8); `--render-ui --watch` (+4); `run-demo-login.ps1`; `DEMO-LOGIN-001`.
- [x] DEV-LOOP (merged): SOTA agentic dev loop — `.claude/settings.json` hooks
  (`Stop`=build+test gate exit 2, `PreToolUse`=block `.env`/`rm -rf`/force-push),
  `verify.ps1` (conditional, OS-aware, `AGENT_SKIP_VERIFY`) + `guard-bash.ps1`,
  read-only `code-reviewer` subagent, `.editorconfig`, conservative
  `Directory.Build.props`, `CHANGELOG.md`. Plan Parties 15 / 15-bis / 16.
- [x] V6-A (merged): `JUnitReportWriter` + `--to-junit [path]` CLI (manual-first)
  + shared `RunArtifactLoader`. main = build 0/0, **110 tests** green.

## Next Executable Items

- [x] E2E-1 (stable end-to-end, no key): deterministic OpenAI-compatible
  `MockLlmServer` (HttpListener) returns a scripted action sequence (EnterText →
  EnterText → Click → Done). 3 always-run tests drive the real `LlmService`
  against it (client → mock → parser, no key, run in any CI). A gated
  `[InteractiveUiFact]` (`RUN_E2E_UI=1`) launches the real WinForms sample,
  attaches the real FlaUI driver (with a control-ready settle), runs
  `RunOrchestrator`, and asserts the app shows "Login successful" + exit 0 +
  `Result=Succeeded`. Verified green twice on an interactive session; skipped by
  default (UIA needs a logged-in desktop). main suite = **122 + 2 skipped**.
- [x] E2E-1 cross-framework parity: both gated E2E are now theories over WinForms
  **and** WPF (same automation ids + status strings, only window title differs) —
  proves the UIA agent path is framework-agnostic. 4 gated cases; **126/126** with
  `RUN_E2E_UI=1`. WinForms label clip ("Case grid") fixed + screenshot-verified.
- [ ] V2-D: Add MAUI Windows and Avalonia parity after WinForms/WPF flows are
  stable.
- [~] V3-A: screenshot overlay started — **secret-field masking shipped** (redact-at-
  source via `UiSnapshot.WindowBounds` + `ScreenshotMasker` + `ScreenshotRedaction`).
  Still to design: the general VLM-oriented overlay artifact contract (annotated element
  boxes / labels) before adding VLM calls.
- [ ] V3-B: Keep recording mode visible in roadmap/docs, but defer
  implementation until the action model and TestZoo flows are stable.
- [x] V4-A: existing-test / source links surface in `--to-junit` as `<testcase>`
  `<property>` entries (`existing_test`, `source_issue`, `source_pr`, `trace_id`).
  `RunArtifact` carries them from the YAML; docs/example in `ai-authoring.md`. Also
  fixed a JUnit bug: `"Passed"` runs were emitted as `<error>` (now a pass).
- [x] WB-1: session branches merged into `main`; build + test green; 10 merged
  branches deleted (remote + local). Decisions distilled to
  `docs/architecture-decisions.md` so the rationale travels with a clone.
- [x] SKILL-1: reusable `setup-verification-loop` Skill extracted to the user
  scope (`~/.claude/skills/setup-verification-loop/SKILL.md`), stack-aware
  (dotnet/node/python/go/rust), ETH-disciplined (lean). Optional follow-ups:
  description-optimization loop, commit a copy into a repo for collaborators.
- [x] WB-2 (refactor): extracted the observe→decide→act→score→record loop out of
  `Program.Main` into an injectable `RunOrchestrator` (`IRunOrchestrator`) behind
  an `IActionDecider` seam (`LlmService` implements it). `Program.Main` is now CLI
  parse + manual commands + a few lines of runtime wiring. Unlocked 9 deterministic
  loop tests (fake driver + scripted decider, zero delays). main = build 0/0,
  **119 tests** green. Manual CLI surface unchanged. Optional follow-up: split the
  act-dispatch switch into per-action handlers if it grows further.
- [~] OBS-1 (Phase 2, observability): **runner emission done.** `RunnerTelemetry`
  emits `agentloop.run/step/observe/decide` spans (act/guard/score/record as
  step-span tags) + a `Meter` (step/run duration, step count, run score). Opt-in:
  provider built only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set; `HttpProtobuf`
  (net48-safe). `traceId` persisted to `report.json` (`RunArtifact.TraceId`).
  Tested via a listener-based span test (no collector). **OBS-1b done:** the run's
  traceId is linked from both the dashboard (run detail) and the **static workbench**
  drill-down (baked `AGENTLOOP_TRACE_UI_TEMPLATE` → clickable, else shows the id).
  Token metric deferred (the LLM call does not expose usage today). Live OTLP →
  Aspire dashboard is a manual step.
- [x] OBS-2 (local all-in-one dashboard): `--dashboard [port]` serves a localhost-only
  `HttpListener` + single-page UI — a view/launcher over the CLI + artifacts (no new
  data model). Catalog (categorized), Create (form → validated YAML ticket), Runs
  (history + detail + screenshots + trace link), Live (launch → spawn CLI, parallel,
  streamed logs + recovered `runId` + live screenshots). Localhost-only, path-traversal
  guards, never CI. 9 deterministic tests + live smoke-verified. Later: mission-control
  UI redesign + a **Files** explorer (on-disk tree + secret-safe text preview), and the
  OBS-1b trace link now also renders in the **static workbench** drill-down.
  Secret-field screenshot masking (V3-A) now redacts sensitive regions at capture.

## Human-Orchestrated Items

- [ ] Run real OpenRouter runtime validation against local `.env`.
- [ ] Decide whether runtime desktop tests should run in GitHub Actions,
  Azure Pipelines, or remain local-only for now.

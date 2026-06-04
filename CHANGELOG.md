# Changelog

All notable changes to this project are documented here.
Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
This project versions by capability milestones (see `docs/roadmap.md`), not SemVer yet.

## [Unreleased]

### Added
- **Dashboard ↔ Symphony tickets**: the dashboard now speaks the same ticket contract CI
  uses. **Create** writes a `tests/created/<id>.yaml` test **and** a `tickets/created/<id>.md`
  Symphony ticket (frontmatter `ticket_id/plan/test_id/framework/target_window/evidence_level/
  launch_sample/expected_artifacts` + body) referencing it. A new **Tickets** tab lists/views
  `tickets/*.md` and **Run**s one via `scripts/run-ticket-proof.ps1` — the exact adapter CI
  runs — so a dashboard-authored ticket is CI-functional unchanged (verified end-to-end with
  `run-ticket-proof.ps1 -DryRun`). Orchestration stays in the script, not C# core.
- **Usability**: a `docs/getting-started.md` walkthrough (mental model → no-key commands →
  run with one of the three brains → view results → write your first YAML), linked from the
  README. Tooltips on the WinForms/WPF/Avalonia sample controls showing each control's role
  **and its AutomationId** (so the demo apps self-document what to reference in YAML;
  hover-only, automation ids/behaviour unchanged). Explanatory tooltips across the dashboard
  (rail tabs, result badges with their meaning, Launch button, Live run/pid/elapsed chips,
  run rows). New
  testing-strategy section "LLM Provider Options And CI/CD" clarifying that the runtime LLM
  is any OpenAI-compatible endpoint (direct / proxy / bridge) and that a full CI/CD pipeline
  needs no paid provider (manual commands + heuristic decider are key-free; the bridge is
  dev-only, loopback-only).
- **Run the real agent loop without OpenRouter** — two key-free "brains" behind the
  existing `IActionDecider` / OpenAI-endpoint seam:
  - `HeuristicActionDecider`: a rule-based, no-LLM decider that drives simple form +
    submit flows from the live UI snapshot (fill configured inputs, click a sequence with
    an enabled-check, then Done). Automated + deterministic → CI smoke without a key.
  - `--bridge-llm [port]`: an OpenAI-compatible bridge endpoint (drop-in via `LLM_ENDPOINT`)
    that writes each prompt to `bridge-io/req-N.txt` and waits for a `resp-N.json` action —
    so a human or an external agent (e.g. Claude Code) can *be* the decider with no provider
    key. Times out to a safe `Wait`. Manual-first (starts without `.env`). Only POSTs to
    the chat-completions path are treated as decisions (health probes don't consume a step).
    Verified end-to-end live: Claude Code drove the real WinForms sample to "Login successful"
    through this bridge with no provider key.
- **V2-D: Avalonia sample (the 4th first-class desktop target)**. New
  `Sample.AvaloniaApp` (Avalonia 11.3) at login + gated-action parity with the WinForms
  and WPF samples — same automation ids (`txtUsername`/`txtPassword`/`btnLogin`/`lblStatus`,
  `btnEnableProtectedAction`/`btnProtectedAction`/`lblControlsStatus`) and status strings.
  The gated E2E theories now run across **WinForms + WPF + Avalonia** (6 cases, all green
  on an interactive session — the scripted mock drives the real Avalonia app via UIA).
  The MAUI sample already has login/profile id-parity; wiring its gated E2E is deferred
  (MSIX/unpackaged launch differs).
- **V4-A: existing-test / source links in the CI report**. `--to-junit` now emits
  `<property>` entries on each `<testcase>` — `existing_test` (one per linked TRX/JUnit
  testcase from the YAML `existing_tests`), `source_issue`, `source_pr`, and `trace_id`
  (OBS-1) — so a CI dashboard can cross-link an AgentLoop run to its existing-test
  counterpart and live trace. `RunArtifact` carries these from the YAML; docs/example added.
- **Secret-field screenshot masking (V3-A)**: step screenshots are now redacted
  **at capture time** — regions of fields whose identifier `SecretRedactor` flags as
  sensitive (password/secret/token/…) are painted opaque before the PNG is written, so
  artifacts can't leak rendered secrets (mirrors how text is redacted). `UiSnapshot`
  gained `WindowBounds` (the screenshot origin) to map element rects into image pixels;
  masking is a pure `ScreenshotMasker` (UIAutomation) driven by a deterministic
  `ScreenshotRedaction.SecretRegions` helper. Best-effort + never throws away a shot.
- **OBS-1b**: the run's OTLP `traceId` now links from the **static workbench**
  drill-down too (not just the dashboard) — baked `AGENTLOOP_TRACE_UI_TEMPLATE`
  renders a clickable "results → live trace", otherwise the id is shown.
- **Dashboard: Files explorer + mission-control UI**. A new **Files** tab surfaces the
  on-disk tree the dashboard reflects (`tests/` YAML source-of-truth, `runs/` artifacts,
  `WORKFLOW.md`, `.env.template`) with copy-path and a read-only text preview, so files
  can be edited by hand / in CI with no UI (`GET /api/files`, `GET /api/file` — extension
  allow-listed, size-capped, refuses `.env*` secrets, traversal-guarded). The page was
  redesigned into a telemetry-console aesthetic (offline-safe monospace, status LEDs,
  deep-linkable tabs) with clearer form labels + helper text, and a supervision-grade
  **Live** view: per-run status LED + pulse, ticking elapsed timer, step progress bar,
  severity-colored streaming logs (autoscroll), and live screenshots.
- **OBS-2 (local dashboard)**: an all-in-one, **localhost-only** developer dashboard
  (`--dashboard [port]`) — a thin `HttpListener` server + single-page UI that is a
  *view + launcher* over the existing CLI and artifacts (no new data model). Sections:
  **Catalog** (tests from `tests/`, grouped by suite/framework/priority/tags),
  **Create** (a form that writes a *validated* YAML ticket under `tests/created/`),
  **Runs** (history + per-run detail: steps, guard/failure codes, screenshot gallery,
  OBS-1 trace link), and **Live** (launch runs — spawns the CLI, parallel-friendly —
  with streamed logs, recovered `runId`, and live screenshots). Security: localhost
  bind, path-traversal guards on all file serving, redacted logs; never for CI. Screenshot
  pixel-blur of secret fields is deferred (OS-masked passwords + localhost are the
  current boundary). Optional trace deep-link via `AGENTLOOP_TRACE_UI_TEMPLATE`.
- **OBS-1 (observability, opt-in)**: OpenTelemetry instrumentation of the agent
  loop via `RunnerTelemetry` — an `ActivitySource` emitting `agentloop.run` /
  `agentloop.step` / `agentloop.observe` / `agentloop.decide` spans (act/guard/
  score/record captured as step-span tags) and a `Meter` (step/run duration, step
  count by outcome, run score). Export is strictly opt-in: a provider is built only
  when `OTEL_EXPORTER_OTLP_ENDPOINT` is set, and `StartActivity` is a no-op with no
  listener — runs stay dependency-free. OTLP uses `HttpProtobuf` (net48-safe; gRPC
  dropped in exporter 1.12.0). The run's W3C `traceId` is persisted to `report.json`
  (new `RunArtifact.TraceId`) so a recorded run links to its live trace. Verified by
  a listener-based span test + opt-in gating test (no collector needed). Live
  OTLP → standalone Aspire dashboard remains a manual/human-orchestrated step.
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
- Dashboard ticket hardening (post-QA): ticket frontmatter scalars (`title`/`framework`/
  `target_window`) are now stripped of control chars/newlines so a crafted Create value
  can't inject a forged `plan:` line into the generated ticket; and `run-ticket-proof.ps1`
  now refuses a plan path that resolves outside the repository (defense in depth). Regression
  test added. (Localhost/own-input integrity issue, not RCE — the spawn layer was already safe.)
- JUnit report: a `"Passed"` run (test runs report "Passed", not "Succeeded") was
  wrongly emitted as an `<error>` instead of a pass — `--to-junit` now treats both
  `Passed` and `Succeeded` as passing. (Found during V4-A; regression test added.)
- Dashboard hardening (post-QA): `GetFile` is now confined to exactly what the Files
  tab advertises (`tests/` + `runs/` + `WORKFLOW.md`/`.env.template`) instead of any
  allow-listed text file under the repo; trace-link hrefs escape quotes in both the
  dashboard and the static workbench.
- WinForms sample: the "Case grid" label was clipped to "ase grid" — the Premium
  radio (Left 240 + Width 100 = 340) overlapped the label at Left 330 and, being
  higher in z-order, painted over its first letter. Moved the label to Left 350.
- Workbench dropped every real run: `SymphonyWorkbenchGenerator.LoadRuns`
  deserialized `report.json` without `JsonStringEnumConverter`, but
  `ArtifactWriter` writes enums as strings — so `evidenceLevel` failed to parse
  and runs were silently skipped (`runs=0`). Added the converter + a regression test.

### Notes
- Test suite: 168 tests + 2 gated UI E2E theories = 6 cases across WinForms + WPF +
  Avalonia (skipped unless `RUN_E2E_UI=1`; 174/174 with it). Build clean across
  net48 + net8.0-windows + Avalonia(net8.0) + MAUI.
- Runtime agent execution still needs a local `.env` (OpenRouter) and a launched
  desktop app; validation, listing, Workbench rendering, and the watch loop do not.

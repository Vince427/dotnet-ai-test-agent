# Changelog

All notable changes to this project are documented here.
Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
This project versions by capability milestones (see `docs/roadmap.md`), not SemVer yet.

## [Unreleased]

### Added
- **V9.5 recording mode (increment 2b) — live UIA capture (`--record`)**. The env-bound half that
  produces the `session.json` `--compose-recording` consumes. `UIAutomation/UiaSessionRecorder.cs`
  attaches to a window by title via FlaUI/UIA3 and subscribes to automation events (Invoke → `Invoked`,
  SelectionItem ElementSelected → `SelectionChanged`, Value property change → `ValueChanged`, ToggleState
  → `Toggled`), feeding each as a `CapturedUiEvent` into the pure `SessionRecorder`. New CLI
  `--record --window <title> [--out <session.json>] [--seconds N]` (default 120s, or Ctrl+C);
  mode-exclusive, key-free, stdout-clean when no `--out`. CRITICAL secret-safety: typed values are
  redacted **at capture** via `SecretRedactor.RedactValue`, keyed by the control's UIA **`IsPassword`**
  flag first (so a masked field with a non-keyword id — e.g. `pin` — is still redacted) then its
  identifier, so a password never lands in `session.json`. Env-bound (needs an interactive desktop +
  the target app) — covered by a gated
  `[InteractiveUiFact]` (RUN_E2E_UI=1); +6 non-gated option-parsing tests. Both `net48` and
  `net8.0-windows` build clean.
- **Distributable Windows release build** (`scripts/publish-release.ps1` + `docs/install.md`). Produces
  a single-file `AgentRunner.exe` (win-x64, framework-dependent by default; `-SelfContained` for no
  prerequisite, `-Zip` to package) so the agent is download-and-run. A cross-platform `dotnet tool` is
  **not** viable — `PackAsTool` rejects `net8.0-windows` + `UseWPF`/`UseWindowsForms`, and the agent
  needs FlaUI/WinForms/WPF (see `.claude/DISCOVERY_LOG.md`); an exe is the right shape for a desktop
  tool. Fixed in passing: `RunJobManager` resolved the runner via `Assembly.Location` (empty under
  single-file) — now falls back to the host process path, so the dashboard's run-launch works in the
  published exe too. Version stamped `0.9.0` (pre-1.0).
- **V9.5 recording mode (increment 2) — UIA capture core**. The pure, testable heart of recording:
  `CapturedUiEvent` (a normalized, framework-agnostic UIA event in Core), `RecordedActionMapper`
  (Invoked/Toggled/SelectionChanged → Click, ValueChanged → EnterText), and `SessionRecorder` which
  accumulates events into a `RecordedSession` and smooths raw noise (consecutive text edits on a field
  collapse to the final value; a doubled click is de-duplicated). Feeds increment 1's
  `RecordingComposer` → YAML draft. The live FlaUI/UIA event subscription + a `--record` CLI (env-bound,
  needs a desktop + target app) are the next increment. +6 tests.
- **Road-to-1.0 release checklist** (`docs/release-checklist.md`). A durable pre-1.0 gate so future
  versions can't silently break users: freeze the public contract (CLI / YAML schema / artifacts /
  MCP), lock it with golden + schema tests, add `schema_version` + a tolerant loader, and adopt a
  SemVer/deprecation policy (1.x additive-only; deprecate-with-WARN before any removal). Surfaced from
  the plan's resume snapshot so `/suite` reminds us before tagging 1.0.
- **V9.5 recording mode (increment 1) — `--compose-recording`**. Turns a recorded manual session
  (a portable `session.json`: window + ordered interactions) into a **validated, goal-based YAML test
  draft** — the biggest authoring on-ramp (record once, edit the draft, run it). `--compose-recording
  <session.json> [--out <draft.yaml>]` prints the YAML to stdout (or writes `--out`); diagnostics +
  policy warnings go to stderr. Pure + key-free: reuses the dashboard's YAML emitter (one source of
  truth) and `TestPlanValidator`; the goal is synthesised in plain language from the steps with secret
  field values redacted; `allowed_actions` = the verbs used (+ `Done`). Live UIA event capture (the
  env-bound half that emits the JSON) is the next increment. +5 tests.
- **V8 inc.2 (partial) — screenshot in selector-healing evidence**. When a target drifts
  (`action_target_not_found`) and `SelectorHealer` proposes a closest-match selector, `summary.md`
  now has a **Selector Healing Suggestions** section listing each `old → new` proposal with its
  confidence, rationale, and a relative link to that step's screenshot — so a human can see the live
  UI before adopting the new selector by hand. Still evidence-only; never auto-applied. +2 tests.
  (The `--heal-apply` YAML-rewrite half is deferred: tests carry no selector field to rewrite until
  recording mode (V9.5) exists — see `.claude/DISCOVERY_LOG.md`.)
- **V7 inc.2b — prompt preview + policy warnings in the static workbench**. The AgentLoop Workbench
  Test Backlog now has a **Notes** column (the same non-fatal `TestPlanValidator` advisories the CLI's
  `--validate-plan` emits, computed at generation time) and a **Prompt** column with an expandable,
  key-free preview of the exact prompt each test would produce (`PromptPreview`, baked at generation —
  the page stays fully static, no view-time server). +2 tests.
- **Dashboard Category field**. The Create form now lets you pick the test **Category**
  (Scenario / Smoke / Monkey / Audit) instead of every dashboard-authored test being hardcoded to
  `Scenario`; the value is whitelisted server-side in `BuildYaml` (unknown → `Scenario`). +1 test.

### Changed
- **Dashboard: guided, fully-explained UI**. Every tab now opens with a plain-language explainer
  banner (what it's for + how to use it). The **Create** tab was rebuilt as a guided form: four
  labelled sections (Identity & triage / Target application / What the agent does / Evidence & demo),
  every field and every dropdown option explained inline, an action-verb legend (which verbs need a
  target), and the previously-missing **Category** and **Risk** fields exposed (Risk now also
  round-trips on Edit). No hidden "advanced" disclosure — everything is visible and described.
- **Dashboard UX clarity**. Network-level fetch failures now surface a clear, recoverable banner
  ("Cannot reach the dashboard server — restart it; this clears automatically") instead of a bare
  *failed to fetch*; it self-clears on the next successful poll. The **Create** tab was decluttered —
  only the essentials (id, framework, title, target window, goal, success condition) stay visible, with
  suite/priority/steps/actions/evidence/tags tucked into a collapsible **Advanced options** section
  (auto-expanded when editing an existing test). No backend/API change.

### Added
- **V7 inc.2 — prompt preview + policy warnings in the dashboard**. The local dashboard now surfaces
  the same V7 signals the CLI exposes: a **⌘ Prompt** button on every catalog card opens a key-free
  preview of the exact prompt the LLM would receive (`GET /api/prompt?planPath=&testId=`, reuses
  `PromptPreview`); each catalog entry carries a `warnings` array (the non-fatal policy advisories from
  `TestPlanValidator`) rendered inline as ⚠ notes; and **Create** echoes any advisories after saving.
  Still a view over the CLI contract — no new data model. +7 tests.
- **V7 prompt preview + policy warnings**. `--show-prompt --test-id <id>` prints the exact prompt
  the LLM would receive for a test (key-free — `PromptPreview` reuses `PromptBuilder`, so it can't
  drift from the real prompt; the live UI snapshot is a labelled placeholder). Text by default,
  `--format json` supported; also an MCP `show_prompt` tool. And `TestPlanValidator` now emits
  **non-fatal policy warnings** (unknown framework, `max_steps` > 100, missing `success_condition`)
  surfaced by `--validate-plan` (as `WARN` on stderr, plus `warnings`/`warningCount` in `--format
  json`) and the MCP `validate_plan` tool — plans stay valid, but authors get a heads-up before a
  run. +6 tests. (Fixed in passing: `--show-prompt` no longer triggers single-plan runtime
  selection, and its stdout stays clean.)
- **MCP adapter (`--mcp`)** — a minimal Model Context Protocol server (JSON-RPC 2.0 over stdio)
  so an agent host (Claude Desktop, Copilot, …) can drive the runner natively. It's an *adapter
  over the same CLI contract* (reuses `TestPlanLoader`/`TestPlanValidator`/`RunArtifactLoader`, no
  new data model) exposing **read-only, key-free** tools: `list_tests`, `validate_plan`,
  `list_runs`, `get_run`. Nothing that spawns a run or needs `.env` is exposed in this increment;
  `get_run` is path-guarded. The line dispatcher (`McpServer.HandleLine`) is pure and unit-tested
  (initialize / tools-list / tools-call / notification / protocol + tool errors); only the stdio
  loop is I/O. Also hardened: the config diagnostic no longer prints to stdout under `--mcp`
  (stdout must carry only JSON-RPC) — same protection `--format json` already had. +10 tests.
  See `docs/mcp.md`.
- **V8 self-healing — selector-drift suggestions (increment 1, evidence-only)**. When an action
  targets an AutomationId/Name that isn't in the live snapshot (`action_target_not_found`), the
  new `SelectorHealer` proposes the closest present element by normalized edit distance over its
  id and name, with a confidence and rationale. It's recorded on the failing step
  (`RunStep.HealingSuggestion`, shown as `heal→<newTarget>` in the summary evidence) but **never
  auto-applied** — CI stays deterministic; a human (or a later local-only `--heal-apply`) decides.
  Pure/key-free (no LLM, no vision) so it runs on every failed target; complements the heavier
  optional `--vision` escalation. +6 tests.
- **V3 Tier-2 vision — real multimodal client + `--vision` (increment 2b)**. `OpenAiVisionClient`
  implements `IVisionClient` against an OpenAI-compatible multimodal endpoint (same
  `LLM_ENDPOINT`/`LLM_API_KEY` as the text path, with an optional vision-capable `VISION_MODEL` /
  `llm.vision_model`, falling back to `LLM_MODEL`): it sends the masked annotated screenshot + the
  identifiers-only overlay index and returns the model's `{box, actionType, …}`. A new `--vision`
  CLI flag wraps the decider in `VisionActionDecider`, so a run uses semantic UIA first and only
  escalates to the VLM when a target can't be resolved. Manual-first/AI-optional unchanged —
  `--vision` is off by default. The network call is the only untested step (like `LlmService`);
  the mapping/escalation/masking are covered by increment 2's tests. +1 test.
- **V3 Tier-2 vision fallback decider (increment 2, the moat)**. `VisionActionDecider`
  (`IActionDecider`) wraps a Tier-1 decider and **escalates to a VLM only when Tier-1's UIA
  target can't be resolved** against the live snapshot — the flat/owner-drawn-UI case where UIA
  alone fails. On escalation it builds the overlay (increment 1), masks secret regions, draws the
  numbered boxes, and sends the image + identifiers-only index to an `IVisionClient`; the model
  replies `{box, actionType, value, …}` and `VisionResponseParser` maps the chosen box back to its
  element. Deterministic + key-free apart from the VLM call itself (a scripted client drives the
  tests, mirroring `MockLlmServer`); vision stays a fallback (cost/latency), never the default,
  and the image is masked before it leaves the machine. +8 tests. The real OpenAI-compatible
  multimodal client + a `--vision` CLI wire-up are the next small step (increment 2b).
- **Dashboard Catalog: filters, batch runs, category visibility, edit/archive**. The Catalog
  now has a filter bar (search + category/framework/priority/suite) and shows each test's
  **category** (was hidden). Multi-select + **Run selected** / **Run filtered** queue many runs
  through a new **bounded run queue** — at most *max parallel* (default 2, clamped [1,16],
  adjustable inline; `GET /api/config` + `POST /api/jobs/concurrency`) execute at once, the rest
  show as `queued` in Live. Per the doctrine (YAML = source of truth, dashboard = view+launcher,
  everything CI-replayable), mutation is bounded: **Edit** re-writes a single-test
  `tests/created/` file through the same validator; **Archive** *moves* a single-test YAML to
  `tests/archived/` (excluded from catalog + CLI `--list-tests`/`--suite` + CI; reversible, shows
  in Git) — **no hard delete**; multi-test files stay edit-on-disk. Archiving is symmetric: an
  **Archived** section in the Catalog lists archived tests with a **Restore** button
  (`GET /api/archived` + `POST /api/tests/unarchive`) that moves the YAML back. Backed by a `BeginProcess`
  seam so the queue is unit-tested without spawning. +8 tests.
- **V3 Tier-2 overlay artifact contract (the vision moat, increment 1 — no key needed)**: at
  `full` evidence each step now also emits `overlay/step_NNN.png` — the (masked) screenshot with
  numbered boxes over every visible, locatable element — plus `overlay/step_NNN.json`, the index
  mapping each box number to its element identifiers. `ScreenshotOverlay` (runner) builds the
  deterministic, image-relative numbered index from the snapshot (same `WindowBounds` mapping as
  secret masking); `ScreenshotAnnotator` (a pure image op in UIAutomation, never throws away a
  shot) draws the boxes. The index is identifiers-only — never a control's `Value` — so it stays
  secret-safe even for password fields. This is the prerequisite a VLM decider consumes ("pick
  box N") for the next increment. `RunStep` gains `OverlayPath`/`OverlayIndexPath`; the summary
  evidence list shows `overlay`. +7 tests. See `docs/competitive-analysis.md` for why V3 is P0.
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

### Changed
- **Symphony→AgentLoop code rename (A5, decision D3)**: the workbench C# types/files
  `SymphonyWorkbench{Options,Result,Generator}` were renamed to `AgentLoopWorkbench*` to drop
  the legacy loop name (collides with the unrelated `openai/symphony`). The generated
  `docs/symphony.html` artifact name and the deliberate "Symphony **ticket**" model name are
  kept on purpose. No behaviour change.
- **Act-stage refactor (post global-audit, A4)**: the action dispatch is extracted out of
  `RunOrchestrator.RunCoreAsync` into a testable `ActionExecutor` (`IActionExecutor` +
  `ActionExecutionResult`), and the action verbs now come from one `ActionVocabulary` source
  of truth instead of being duplicated across the dispatch, `PromptBuilder`'s default
  "Allowed actions" line, `TestPlanValidator`, and `AgentActionValidator`. Behaviour is
  unchanged (same outcomes/failure codes/Done semantics); the dispatch gained 11 direct unit
  tests (`ActionExecutorTests`). Adding a verb is now a one-line `ActionVocabulary.All` edit
  plus an executor branch.

### Fixed
- **Multi-region status resolution (A6)**: success-condition detection scanned only the
  *first* status label, so a flow whose result lands in a different status region (e.g.
  `lblControlsStatus` vs. the login `lblStatus`) could never satisfy `success_condition`. New
  `UiSnapshot.StatusContains` scans every status region; both the early-success check
  (`RunOrchestrator`) and the `Done` gate (`ActionExecutor`) use it. `FindStatusText` still
  returns the first region (now skipping empty labels — logging-only). +5 tests. (MAUI gated E2E wiring remains deferred
  — its packaged/unpackaged launch needs interactive verification.)
- Dashboard CSRF (post global-audit): POST routes (`/api/runs`, `/api/tickets/run`,
  `/api/tests`) now require a same-origin request (Origin == the dashboard URL, or, for
  non-browser clients, a loopback Host) — a random web page the dev visits can no longer
  trigger a run/process spawn cross-origin. + 3 tests.
- Secret masking now also keys off the UIA **password** flag, not just the identifier:
  `UiElement.IsPassword` (populated by the driver) is OR'd into `SecretRedactor` and
  `ScreenshotRedaction`, so a password field with a benign id (`txt1`) is still redacted in
  prompts/logs/snapshots and masked in screenshots. Closes the DISCOVERY_LOG identifier-only gap.
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
- Test suite: 173 tests + 2 gated UI E2E theories = 6 cases across WinForms + WPF +
  Avalonia (skipped unless `RUN_E2E_UI=1`; 179/179 with it). Build clean across
  net48 + net8.0-windows + Avalonia(net8.0) + MAUI.
- Runtime agent execution still needs a local `.env` (OpenRouter) and a launched
  desktop app; validation, listing, Workbench rendering, and the watch loop do not.

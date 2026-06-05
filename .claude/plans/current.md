# Active Plan

This file gives `/suite` and other agents a small executable backlog. The
source of truth remains `docs/roadmap.md`; keep this file focused on near-term
parallel work.

## RESUME SNAPSHOT — 2026-06-05 (read this first)

**`main` HEAD = `248d8e0`** (Merge PR #31, `--heal-apply`). Build clean on net48 + net8 + Avalonia +
MAUI; **338 tests + 3 gated** on `main`; **CI green**. Single contributor identity: `Vince427` (noreply).

**How to try it (interactive):** `--dashboard 8090` (catalog/create/runs/live UI) · `--vision-bridge <dir>`
= Claude Code plays the VLM, no key (`docs/vision-bridge.md`) · `--record`/`--compose-recording` (record→YAML)
· `--heal-apply --run <id> [--yes]` · `--analytics` · `--validate-plan`/`--list-tests`. All key-free except
real LLM runs. Launching a sample + agent works (Start-Process foregrounds the window before capture).

> 🚩 **BEFORE TAGGING v1.0**: complete `docs/release-checklist.md` (freeze the public contract +
> golden tests + `schema_version` + SemVer/deprecation policy). This is how we avoid breaking users
> with future versions. Do **not** cut 1.0 until that gate is green. (memory: `road-to-v1-contract`)
>
> `CONTRACT.md` + `ContractTests` + `ReproducibilityTests` already freeze & lock the public surface
> (CLI/exit-codes/JSON shape, YAML schema, artifacts, MCP) + run-to-run stability + SemVer policy.
> **Still open for the tag:** `schema_version` on YAML + `version` on artifacts + a tolerant loader,
> the CHANGELOG `1.0.0` section, then `v1.0.0`.

**Everything below is MERGED to `main` (all earlier "awaiting-merge" branches are in).** The arc:
- **V3 vision moat** complete — Tier-1 UIA → `VisionActionDecider` (Tier-2 VLM) → `OpenAiVisionClient`
  `--vision`; **plus key-free `--vision-bridge`** (`BridgeVisionDecider`: writes annotated screenshot +
  identifiers-only index, awaits `vision-resp-N.json` from an external agent — Claude Code as the VLM).
- **V7** prompt preview + policy warnings across CLI (`--show-prompt`), dashboard (`/api/prompt`, ⚠),
  static workbench (Notes + Prompt columns), MCP `show_prompt`.
- **V8 self-healing COMPLETE** — `SelectorHealer` evidence → `summary.md` heal section → **`--heal-apply`**
  (rewrites the test's `selectors`, surgical edit **verified by `TestFactGuard`**, `--yes`/dry-run, local-only).
- **V9.5 recording** — `--record` (live UIA capture, env-bound) → `session.json` → `--compose-recording`
  → validated YAML draft. Pure core (`SessionRecorder`/`RecordedActionMapper`/`Core.CapturedUiEvent`).
- **MCP** — read-only (`--mcp`) + opt-in `create_test` (`--mcp-allow-write`).
- **V11** `--analytics` (flaky/selector-drift/duration from `runs/`).
- **Dashboard** — guided fully-explained UI, Category/Risk, connection banner; localhost-only.
- **Distribution** — `scripts/publish-release.ps1` (single-file `AgentRunner.exe`) + `release.yml` (tag
  `v*` → Release). No `dotnet tool` (desktop dep; `DISCOVERY_LOG`). `docs/install.md`.
- **Hardening** — `CONTRACT.md` + `ContractTests` (shape) + `ReproducibilityTests` (run-twice stability)
  + `TestFactGuard` (rewrite fact-gate). CI now builds net48 too. `docs/release-checklist.md` = the 1.0 gate.

**Idioms to keep:** deterministic key-free core is gated in CI / golden-tested; stochastic LLM runs are
recorded, never asserted-equal. Rewrites go through `TestFactGuard`. One emitter (`DashboardApi.BuildYaml`)
for all YAML authoring. Everything multi-targets net48 + net8.

**Next executable (pick up here):**
1. ~~Replay decider~~ **DONE** — `ReplayActionDecider` + `--replay <session.json>` replays a recorded
   session with NO LLM (verified end-to-end on the sample login). record → replay → heal now closes:
   `--record` → `--replay` (drift fails visibly → `SelectorHealer` → `--heal-apply`). *Remaining polish:*
   recorded passwords are redacted, so secret entry isn't reproduced from a capture — supply real values
   (e.g. an env/`.env` substitution at replay) to replay secret fields.
2. **Toward the `v1.0.0` tag** (`docs/release-checklist.md`): `schema_version` on YAML + `version` on
   artifacts + tolerant loader; CHANGELOG `1.0.0` section; tag.
3. **Real net48-NATIVE app proof** (sourcing a buildable one) — the "real third-party app" wedge is
   already proven on a net8 OSS app (`dotnet-winforms-examples`), which surfaced + fixed the occluded-
   window screenshot bug; net48-runtime-specific proof is still open.
4. Live `--record` **"Click not captured"** finding (`DISCOVERY_LOG`); env-bound `--vision`/MAUI gated E2E.

**Pending human action:** GitHub **Pages** still fails at `Configure Pages` — enable
**Settings → Pages → Source: GitHub Actions** (the `enablement:true` auto-enable didn't take).

**Working rhythm:** one feature branch per increment → QA via `code-reviewer` subagent → push →
user merges PR (sequential) → I sync `main` + delete the branch. Commit msgs end with the
`Co-Authored-By: Claude Opus 4.8 (1M context)` trailer. Don't commit/push without being asked.

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
- [~] V2-D: **Avalonia done** — `Sample.AvaloniaApp` (Avalonia 11.3) at login +
  gated-action parity; gated E2E theories now span WinForms + WPF + Avalonia (6 cases,
  all green live). **MAUI**: sample already has login/profile id-parity; gated E2E wiring
  deferred (MSIX/unpackaged launch + win10 RID output path differ — needs an exe-locate
  step and runtime check).
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
- [x] AUDIT-HARDENING (branch `claude/action-plan-a4a6`, PR open): the global-audit
  action plan. **A1** dashboard CSRF same-origin guard on POSTs (+3 tests); **A2**
  `UiElement.IsPassword` OR'd into secret redaction + screenshot masking (benign-id password
  fields now masked); **A3** the two high-value missing tests; **A4** act-dispatch extracted
  into a testable `ActionExecutor` + a single `ActionVocabulary` source of truth (kills the
  4-place verb duplication; +11 tests) — this is the WB-2 follow-up; **A5** `Symphony*`→
  `AgentLoop*` code rename (D3 done for code; generated `symphony.html` + "Symphony ticket"
  name kept on purpose); **A6** multi-region status resolution via `UiSnapshot.StatusContains`
  (success condition found in any status region, not just the first; +5 tests). Suite
  **189 + 2 gated**, build clean net48 + net8 + Avalonia + MAUI. A1-A3 landed on `main`
  (ebf9d20); A4-A6 on the branch.
- [ ] MAUI gated E2E (the remaining half of A6/V2-D): wire the MAUI sample into the gated
  `[InteractiveUiTheory]`. Needs an exe-locate step (win10 RID output path) + interactive
  packaged/unpackaged launch verification — environment-bound, not yet done.

## Strategy Backlog (from `docs/competitive-analysis.md`, 2026-06)

Sequenced by what unlocks the next unit of credibility. The competitive wedge is
*desktop + open + AI-optional + offline + legacy .NET 4.8*; the analysis argues the value
prop hinges on **vision (V3)** + **adoption ergonomics (V9.5/V8)**.

- **[P0] V3 Tier-2 vision fallback** — THE moat. Inc.1 (merged): no-key overlay artifact
  contract (`ScreenshotOverlay` + `ScreenshotAnnotator` → `overlay/step_NNN.{png,json}` at
  `full`). Inc.2 (branch `claude/v3-vision-decider`): `VisionActionDecider` escalates to a VLM
  (`IVisionClient` + `VisionResponseParser`) only when Tier-1's UIA target is unresolvable;
  masks+annotates, maps box→element. Key-free/tested via a scripted client. **Inc.2b (done,
  merged):** `OpenAiVisionClient` (real multimodal) + `--vision` CLI flag. V3 moat now functional
  end-to-end; remaining: a live multimodal demo + a Tier-2 gated E2E (environment-bound).
- **[P0] MAUI gated E2E + runtime cross-framework proof** — close the samples-only gap;
  MAUI/Avalonia custom controls are where vision pays off. (Also the remaining half of A6.)
- **[P1] V9.5 Recording mode** — manual session → first YAML draft (editable, validated).
  Biggest top-of-funnel lever; table-stakes vs Ranorex/TestComplete/testRigor.
- **[P1] V8 Controlled self-healing** — inc.1 (branch `claude/v8-self-healing`): `SelectorHealer`
  records a closest-selector suggestion on `action_target_not_found` (evidence-only, never
  auto-applied; +6 tests). Next: screenshot in evidence, vision-assisted candidate, local-only
  `--heal-apply` rewriting the YAML with confirmation.
- **[P1] MCP adapter over the CLI** — inc.1 done (branch `claude/mcp-adapter`): `--mcp` serves a
  JSON-RPC/stdio MCP server with read-only key-free tools (`list_tests`/`validate_plan`/
  `list_runs`/`get_run`); `McpServer.HandleLine` pure + 10 tests; `docs/mcp.md`. Next: an opt-in
  `run_test` tool + author/edit tools (write YAML via the same validator).
- **[P2] Real legacy-app case study** (ideally .NET Framework 4.8) — one credible proof beats
  ten features. **[P2] One-line install** (`dotnet tool install -g`) + signed release + Pages
  demo. **[P2] V11 analytics** from run history (flaky/selector-drift/cost/duration).
- **[P3] Demand-pulled only:** cross-platform via Appium (MAUI Android/iOS/Mac). Keep
  deferring SaaS cockpit / auth / VM provisioning / multi-agent chat — they'd dilute
  portable-first.

## Human-Orchestrated Items

- [ ] Run real OpenRouter runtime validation against local `.env`.
- [ ] Decide whether runtime desktop tests should run in GitHub Actions,
  Azure Pipelines, or remain local-only for now.

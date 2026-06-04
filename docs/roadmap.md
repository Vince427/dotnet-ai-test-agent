# Roadmap

This roadmap keeps the project portable-first, black-box, and AgentLoop-driven. The agent must test existing applications from the outside, without requiring application teams to add agent-specific classes, packages, or code paths.

> **Strategy & competitive positioning:** see [`competitive-analysis.md`](competitive-analysis.md).
> Short version of the priority order it argues for: **V3 vision fallback** (the moat) → MAUI
> runtime proof → recording (V9.5) + self-healing (V8) → MCP adapter + a real-app case study.
> The version numbers below are the *what*; the analysis is the *why-now*.

## Status (current)

Shipped beyond V1.x (see `CHANGELOG.md` and `docs/architecture-decisions.md` for detail;
`/.claude/plans/current.md` is the live backlog):

- **Testability**: the loop is an injectable `RunOrchestrator` behind an `IActionDecider`
  seam; deterministic loop tests + a key-free `MockLlmServer`; gated full-stack E2E across
  **WinForms + WPF + Avalonia** (samples at parity).
- **V3 (partial)**: secret-field screenshot masking at capture (a Tier-2 overlay primitive);
  full VLM overlay/vision still ahead.
- **V4-A**: `--to-junit` links runs to existing TRX/JUnit tests + source issue/PR + trace id.
- **V6 (partial)**: JUnit XML output via `--to-junit`; documented exit codes.
- **V11 (partial)**: opt-in OpenTelemetry emission (spans + metrics, OTLP/HttpProtobuf),
  `traceId` in `report.json`, viewable in a standalone Aspire dashboard.
- **Local dashboard** (a *view + launcher*, not source of truth): `--dashboard` serves a
  localhost-only UI — catalog, ticket creation, launch, live logs/screenshots, file explorer.

Still open: MAUI E2E wiring, the VLM vision tiers (V3), self-healing (V8), richer
interaction/recording (V9/V9.5), analytics (V11), and MCP/plugin adapters.

## V1.3 - Generic Robo Agent + AgentLoop Foundation - Done

- Generic UI tree discovery.
- Dynamic goals through CLI and `WORKFLOW.md`.
- AgentLoop-style loop: observe -> decide -> act -> guard -> score -> record.
- Loop detection, scoring, structured logs, screenshots, JSON and Markdown run artifacts.
- OpenRouter/local LLM configuration through `.env`.

## V1.4 - YAML Test Backlog - Done

- Versioned test definitions in `tests/*.yaml`.
- Directed execution through `--plan`, `--suite`, and `--test-id`.
- Test metadata: id, title, priority, framework, target window, goal, success condition, max steps, allowed actions, tags, blocked conditions.
- YAML remains the source of truth.

## V1.5 - Static AgentLoop Workbench - Done

- Generate a local static workbench from `tests/*.yaml` and `runs/*/report.json`.
- Existing filenames may still use `symphony` until the rename migration is complete.
- Keep it read-only first: backlog list, latest runs, status, score, guards, screenshots and summaries.
- No database, no auth, no server requirement, no SaaS cockpit.
- UX rule: make the UI simple and obvious, but provide guided choices for suites, test ids, frameworks, result filters, and prompt previews.

## V1.6 - Deterministic Quality Guards - Done

- Add non-LLM checks after actions before accepting success.
- Initial guards: UIA tree empty, UI capture failure, target window disappearance, process crash.
- Later guards: memory spike, application logs, Windows Event Log, unexpected modal dialogs.
- Guard results must be recorded as `Passed`, `ForceReject`, or `Abort`.

## V1.7 - Manual-First Runtime Evidence Contract - Done

- Every critical capability must have a manual path: editable YAML, CLI command, readable artifact, short documentation, and a simple UI view where useful.
- Add manual commands that do not load `.env`: validate YAML plans, list tests, and render the static workbench.
- Treat OpenRouter and other LLM providers as runtime assistance only, not as prerequisites for authoring, validation, listing, or artifact review.
- Keep run evidence stable and portable: JSON report, Markdown summary, screenshots, guards, score, result, and failure reason.
- Add selectable evidence levels: `minimal`, `standard`, and `full` with UI tree snapshots for debug/replay.

## V1.8 - AI Agent Bridge, Not AI Lock-In - Done

- Codex, Claude Code, Copilot, GitHub, Azure, MCP, and plugins should call the runner through stable CLI commands.
- Do not make MCP or a plugin the product core; they are adapters over the local engine.
- Add authoring guidance for agents while preserving a complete human workflow.
- CI should validate specs and publish runtime evidence that both humans and AI agents can read.
- Manual commands should provide both human text output and clean JSON output for agent/tool wrappers.
- Add schema and traceability metadata for AI-authored YAML tests.

## V2 - Multi-Framework TestZoo - In Progress

- Replace the login-only demo with a common TestZoo app set.
- Add equivalent samples for WinForms, WPF, .NET MAUI Windows, and Avalonia Windows Desktop.
- Cover common workflows: login, forms, validation, checkbox/radio/combo, lists, grids, CRUD, tabs, menus, modals, async loading, disabled states, error states, and accessibility edge cases.
- Seed `tests/testzoo.yaml` first, then split into smaller YAML files as scenarios stabilize.
- Prefer one business scenario per YAML file under a suite folder such as `tests/testzoo/`.
- Current seed covers login, profile save, invalid profile validation, checkboxes, and UIA metadata audits across WinForms, WPF, .NET Framework 4.8, .NET 8, and MAUI Windows where samples exist.
- Expand WinForms and WPF first with richer controls and business/E2E flows. Add MAUI Windows and Avalonia parity after the WinForms/WPF flows are stable. **Status:** WinForms, WPF, and Avalonia samples are at login + gated-action parity and run in the gated E2E theories; MAUI sample has id-parity, E2E wiring pending (MSIX/unpackaged launch).
- Add deterministic guard failure demo artifacts for missing targets, crash or
  closed-window capture failures, empty UI trees, and unexpected modals before
  wiring those cases into real UI runtime E2E.

## V2.1 - Public Docs And GitHub Pages

- Publish a simple GitHub Pages site for the quickstart, MVP path, TestZoo status, and a static workbench demo.
- Keep GitHub Pages lightweight first: Markdown docs plus generated static artifacts.
- Add DocFX later when API reference generation and versioned docs become worth the extra setup.
- Make recording mode visible in the roadmap as an adoption feature, but keep it out of the MVP implementation.

## V2.5 - Framework Strategy

- WinForms: FlaUI + Windows UI Automation.
- WPF: FlaUI + Windows UI Automation.
- Avalonia: UIA3 first, then vision fallback when the accessibility tree is flat or incomplete.
- .NET MAUI Windows: FlaUI or Appium Windows depending on packaging and control exposure.
- .NET MAUI Android/iOS/Mac: Appium later; do not force FlaUI outside Windows.

## V3 - Hybrid UIA + Vision Resolution

**P0 — this is the moat** (see `competitive-analysis.md`): without vision fallback the agent
is capped by the target app's UIA quality, and owner-drawn/custom controls (common in
Avalonia/MAUI, Citrix/RDP, legacy GDI) defeat a pure-UIA agent.

- Tier 1: semantic UIA resolution by AutomationId, Name, control type, and patterns.
- Tier 2: UIA + screenshot overlay with numbered bounding boxes for VLM disambiguation.
  - **Tier-2 increment 1 (done / in progress on `claude/v3-vision-overlay`):** the deterministic,
    **no-key** overlay *artifact contract* — `ScreenshotOverlay` numbers each visible element and
    maps it to image pixels (via `WindowBounds`), `ScreenshotAnnotator` draws numbered boxes, and
    the run emits `overlay/step_NNN.png` + `overlay/step_NNN.json` at `full` evidence. This is the
    prerequisite the VLM decider consumes ("pick box N").
  - **Tier-2 increment 2 (done on `claude/v3-vision-decider`):** `VisionActionDecider`
    (`IActionDecider`) escalates to a VLM (`IVisionClient` + `VisionResponseParser`) **only when
    Tier-1's UIA target is unresolvable**; it masks + annotates the screenshot, sends image +
    identifiers-only index, and maps the chosen box back to an element. Key-free/tested via a
    scripted client. **Increment 2b (next):** the real OpenAI-compatible multimodal `IVisionClient`
    + a `--vision` CLI wire-up (the non-deterministic edge, like `LlmService` vs `MockLlmServer`).
- Tier 3: pure vision mode with strict JSON coordinates and physical mouse execution.
- Vision is a fallback, not the default, to control cost and latency.

## V4 - Existing Test Integration

- Do not replace unit tests, integration tests, or existing CI checks.
- Import or link existing test outputs: TRX, JUnit, NUnit/xUnit XML, and CI artifacts.
- YAML tests can reference existing test identities such as `MyApp.Tests.CustomerTests.CreateCustomer` or `ci:smoke-login`.
- Agent tests complement existing tests by validating real UI behavior, focus, navigation, dialogs, visible status, and selector drift.

## V5 - Non-Intrusive Enterprise Adoption

- Support external test packs stored next to the app or in a separate repo.
- No required app code changes.
- No required agent package inside the target app.
- Optional recommendation only: add stable accessibility metadata such as AutomationId when teams are willing to improve testability.
- Support targets by executable path, process name, window title, framework, and environment variables.

## V6 - CI/CD Standard Outputs

- Add CI-friendly output formats: JUnit XML, TRX where useful, JSON summary, Markdown summary, screenshots zip.
- Document exit codes for passed, failed, blocked, aborted, and invalid configuration.
- Add GitHub Actions and Azure Pipelines examples on Windows runners.
- Keep secrets in environment variables or CI secret stores.
- Provide PR/issue templates that guide humans and agents toward YAML-only test changes.

## V7 - Prompt Preview And Policy Validation

- Show exactly what the LLM will receive for a selected test.
- Validate YAML before running: missing success condition, unsafe action, impossible target, unsupported framework, too many max steps.
- Mini UI should help users fix configuration without hiding the YAML.

## V8 - Controlled Self-Healing

- If a selector fails, use semantic and vision fallback to find the likely replacement.
- Produce a healing suggestion with evidence: old selector, new selector, screenshot, confidence, and rationale.
- CI mode never edits tests automatically.
- Local mode may apply a patch only with an explicit flag.

## V9 - Advanced Desktop Interaction

- Add more action types: keyboard shortcuts, menu navigation, right-click, drag/drop, combo selection, tab traversal.
- Improve modal dialog handling, focus recovery, DPI scaling, multi-monitor support, and child windows.
- Keep each action bounded by YAML `allowed_actions`.

## V9.5 - Recording Mode

- Add `da-test record` after the action model and TestZoo controls are stable.
- Let users perform a scenario manually and generate a first YAML draft from observed UI events and snapshots.
- Treat recording as authoring assistance only: generated YAML must remain editable, reviewable, and validated by the normal CLI.
- Redact sensitive fields before storing or sending recorded values to any LLM.

## V10 - Test Planning Like Linear

- Add backlog states: `todo`, `ready`, `running`, `passed`, `failed`, `blocked`, `quarantined`.
- Add dependencies, owners, tags, priority, estimated LLM cost, and flakiness metadata.
- The UI can filter and guide choices, but YAML remains the source of truth.

## V11 - Observability And Analytics

- Analyze historical runs for flaky tests, failing actions, unstable selectors, cost per test, duration, and framework reliability.
- Generate recommendations without making automatic changes.
- Keep reports portable and readable in CI artifacts.

## V12 - Mature Local/CI Product

- Stable CLI and YAML schema.
- Samples for WinForms, WPF, Avalonia, and MAUI Windows.
- Static AgentLoop Workbench with simple UX and useful choices.
- Standard CI outputs.
- Strong non-intrusive adoption story for existing applications and existing test suites.

## Deferred Until Later

- Full ASP.NET dashboard as source of truth.
- SaaS cockpit, auth, multi-tenant storage.
- Hyper-V/Azure VM provisioning.
- Swarm or multi-agent chat.
- Durable external workflow engines unless a concrete need appears.

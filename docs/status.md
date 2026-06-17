# Project Status

Snapshot of where the agent runner stands. Living detail lives elsewhere — this page is the
one-screen "where are we": see [CHANGELOG](../CHANGELOG.md), [roadmap](roadmap.md),
[architecture diagram](architecture-diagram.md), and the live backlog in
`.claude/plans/current.md`.

## Health

- **Build**: clean across net48 + net8.0-windows + Avalonia(net8.0) + MAUI.
- **Tests**: ~167 unit + 2 gated UI E2E theories (= 6 cases across WinForms/WPF/Avalonia,
  skipped unless `RUN_E2E_UI=1`). QA-reviewed (APPROVE) at each major step.
- **Branch**: `claude/runner-orchestrator` (pushed); open the PR from the GitHub link.

## Shipped

- **Testable core** — the loop is an injectable `RunOrchestrator` behind an `IActionDecider`
  seam; deterministic loop tests; key-free `MockLlmServer`.
- **Multi-framework E2E** — gated full-stack runs across **WinForms + WPF + Avalonia**
  (sample apps at parity; MAUI sample present, E2E wiring pending).
- **Run without OpenRouter** — `HeuristicActionDecider` (rule-based, CI) and `--bridge-llm`
  (a human/agent as the decider; demonstrated live driving the real app to "Login successful").
- **Observability** — opt-in OpenTelemetry (spans + metrics, OTLP/HttpProtobuf); `traceId`
  in `report.json`, linked from the dashboard and the static workbench.
- **Local dashboard** (`--dashboard`, localhost-only) — Catalog, Create (→ validated YAML
  **+ AgentLoop ticket**), Tickets (view/run via the CI script), Runs, Live (logs/screenshots),
  Files explorer. Mission-control UI with tooltips.
- **AgentLoop ticket contract** — `tickets/*.md` + `WORKFLOW.md` policy + `run-ticket-proof.ps1`
  adapter; the dashboard creates/runs the same tickets CI consumes.
- **CI integration** — `--to-junit` with existing-test/source/trace links; full pipeline runs
  with **no paid provider**.
- **Security** — secret-field screenshot masking at capture; dashboard path/arg hardening;
  `.env*` refusal.
- **Docs/onboarding** — getting-started, architecture diagram + function inventory, commented
  test template + category taxonomy + schema field descriptions, sample-app tooltips.

## Remaining

- MAUI gated E2E wiring (MSIX/unpackaged launch + win10-RID output path).
- VLM vision tiers (V3) — the screenshot-overlay artifact contract is the prerequisite.
- Self-healing (V8), richer interaction + recording (V9/V9.5), analytics (V11),
  MCP/plugin adapters, RunDiffer migration-parity report.
- Re-run the gated UI E2E on a fresh interactive session (a prior session faulted UIA with
  `RPC_E_SERVERFAULT` — environmental, unrelated to code).

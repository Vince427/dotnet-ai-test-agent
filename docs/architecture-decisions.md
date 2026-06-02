# Architecture Decisions

Durable, distilled record of the load-bearing decisions and the forward roadmap,
so the *why* survives a fresh clone or a new machine. Deliberately lean (a deep
planning exploration happened elsewhere; this is the part worth keeping in-repo).

- **Live backlog**: `.claude/plans/current.md`
- **Open issues / gotchas**: `.claude/DISCOVERY_LOG.md`
- **How a fresh session resumes**: `CLAUDE.md` → "Resuming Work"

## Decisions

### D1 — Test from the outside via FlaUI / UI Automation
Drive existing desktop apps through UIA, never by modifying them (no agent-specific
packages, classes, or test hooks in the target). WinAppDriver is effectively dead
(last release 2021); FlaUI/UIA3 is the maintained, .NET-native choice. **Status:** active.

### D2 — Manual-first / AI-optional
`--validate-plan`, `--list-tests`, `--render-ui`, `--write-guard-demos`, `--to-junit`
must work without `.env`/LLM. Only the runtime agent loop needs an OpenRouter key.
Keeps CI deterministic and the tool useful offline. **Status:** active.

### D3 — Name the loop "AgentLoop", not "Symphony"
Public/doc language uses AgentLoop. `openai/symphony` (Apr 2026) is an unrelated
coding-agent orchestrator — avoid the collision. Code-level `Symphony*` filenames
are renamed only when safe. **Status:** in progress (docs done).

### D4 — Microsoft Agent Framework (MAF) as the LLM SDK
`Microsoft.Agents.AI` (GA Apr 2026, successor to Semantic Kernel + AutoGen) via an
OpenAI-compatible endpoint, so OpenRouter / Anthropic-compat / local Ollama / Hermes
all work. **Status:** active.

### D5 — Central Package Management
NuGet versions centralized in `Directory.Packages.props`; the MAUI sample opts out
(workload-managed `$(MauiVersion)`). `Directory.Build.props` kept conservative (no
forcing `Nullable` that would flip the net48 sample). **Status:** active.

### D6 — Verification-first dev loop, kept lean (ETH discipline)
The highest-ROI thing for the *agent building this repo* is a pass/fail gate.
`.claude/settings.json` hooks: `Stop` runs build+test and blocks finishing on failure
(exit 2); `PreToolUse` blocks dangerous Bash. Scripts are conditional + OS-aware with
an `AGENT_SKIP_VERIFY` escape hatch. Docs stay lean on purpose — the ETH Zürich study
(arXiv:2602.11988) showed over-documentation *reduces* agent success and raises cost.
Generalized into the reusable `setup-verification-loop` skill. **Status:** active.

### D7 — Static Workbench (no SaaS) + CI report format
The AgentLoop Workbench is a single self-contained static HTML (interactive client-side,
no server) — works locally and as a CI artifact. `--render-ui --watch` gives
near-real-time updates with no server. CI consumes **JUnit XML** (the universal report
*format*, distinct from xUnit the test *framework*) via `--to-junit`. No hosted
dashboard as product core. **Status:** active.

### D8 — Security: redact PII, keep both targets healthy
Entered values, logs, snapshots go through `SecretRedactor`; nothing writes `.env`.
Keep net48 **and** net8.0-windows building. **Status:** active.

## Forward roadmap (deferred, with rationale)

These are intentionally not built yet; each note records *why* and the key constraint.

- **Program.Main → `IRunOrchestrator`** (WB-2): the loop is ~700 lines in `Main`, so it
  isn't unit-testable with a fake LLM/driver. Extracting it (behind an injectable
  decider) unlocks deterministic loop tests and clean CLI wiring. Keystone refactor.
- **MCP server**: expose the runner as MCP tools (Claude Desktop/Cursor/Copilot). The
  `shanselman/FlaUI-MCP` "element-by-ref" pattern is the model; no official MS desktop
  MCP exists — a gap to fill.
- **Semantic validator (LLM-as-judge)**: today `Assert` is text/regex; add a
  `SemanticAssert(prompt, snapshot)` second LLM (cheap model + cache) à la Skyvern's
  `page.validate()`. Validated by the SOTA "fresh-context verifier" pattern.
- **Vision fallback**: when the UIA tree is poor (Electron/Qt/games), fall back to a
  VLM (OmniParser-style). UIA-first stays the default (cheaper, more reliable).
- **RunDiffer (migration parity)**: run one YAML on V1 (legacy) and V2 (modern), align
  steps, LLM-judge classifies diffs cosmetic/functional/regression → HTML parity report.
  This is the differentiator for a .NET 4.8 → modern migration; nothing else does it.
- **OpenTelemetry → Aspire dashboard** (observability, opt-in, local): emit spans for
  observe/decide/act/guard/score/record + token/score metrics; view live in the
  standalone Aspire dashboard. **Constraint:** on net48, gRPC OTLP is unsupported since
  exporter 1.12.0 → use `OtlpExportProtocol.HttpProtobuf` (port 4318), never gRPC.
- **Scaling UI tests**: UIA needs an interactive desktop session → 1 test at a time per
  session. Scale = N Windows VMs (Hyper-V linked clones / Azure VM Scale Set), runner
  **interactive** (not a service), VNC not RDP. No real "Windows microVM with desktop"
  exists in 2026; Linux microVMs (Firecracker) don't help. Window Stations + Desktops
  give intra-session parallelism but only for UIA-Invoke actions (no mouse/screenshots).

## Pointers

For runtime LLM config see `README.md`; product spec `docs/spec.md`; roadmap
`docs/roadmap.md`; per-domain contracts `.claude/context/`.

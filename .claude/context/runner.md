# Context: Runner

Owns the executable orchestration loop and manual CLI surface.

## Files

- `src/AgentRunner/Program.cs` (CLI parse + manual commands; wires the runtime loop)
- `src/AgentRunner/IRunOrchestrator.cs` + `src/AgentRunner/RunOrchestrator.cs`
  (observe → decide → act → score → record loop; injectable driver + decider)
- `src/AgentRunner/ReplayActionDecider.cs` (`--replay <session.json>`: key-free deterministic decider
  that replays a `RecordedSession`'s actions in order — no LLM; a drifted target is replayed verbatim so
  the loop detects + heals it. Completes record → replay → heal.)
- `src/AgentRunner/IActionDecider.cs` (the "decide" seam). Implementations: `LlmService`
  (OpenRouter/OpenAI), `HeuristicActionDecider` (rule-based, no LLM), the
  `BridgeLlmServer` HTTP endpoint (`--bridge-llm`) for a human/external-agent decider, and
  `VisionActionDecider` (V3 Tier-2: wraps a Tier-1 decider, escalates to a VLM via `IVisionClient`
  + `VisionResponseParser` only when the Tier-1 UIA target is unresolvable). The VLM gets the
  masked, numbered-box overlay + identifiers-only index; the chosen box maps back to an element.
  Enabled with the `--vision` CLI flag (off by default); `OpenAiVisionClient` is the real
  multimodal client (`VISION_MODEL` / `llm.vision_model`, falls back to `LLM_MODEL`).
  `BridgeVisionDecider` (`--vision-bridge <dir>`) is the **key-free** alternative — it IS the decider
  (not a Tier-2 fallback), writing an annotated screenshot + identifiers-only index per step to a
  folder and awaiting `vision-resp-N.json` from an external agent (the vision peer of `--bridge-llm`;
  no `.env`). Env-bound to run; the file protocol is unit-tested. See `docs/vision-bridge.md`.
- `src/AgentRunner/ActionExecutor.cs` (the "act" seam: `IActionExecutor` +
  `ActionExecutionResult`). Validates the action (allow-list + target existence) then
  dispatches the verb to the driver; returns the outcome the loop records. Extracted out of
  `RunOrchestrator` so the dispatch is unit-testable in isolation (`ActionExecutorTests`).
- `src/Core/ActionVocabulary.cs` (single source of truth for the action verbs). The dispatch,
  `PromptBuilder`'s default "Allowed actions" line, `TestPlanValidator`, and
  `AgentActionValidator` all derive their verb set from it — they cannot drift apart.
- `src/AgentRunner/RunnerTelemetry.cs` (OBS-1: opt-in OpenTelemetry spans + metrics)
- `src/AgentRunner/RunnerOptions.cs`
- `src/AgentRunner/LlmService.cs`
- `src/AgentRunner/WorkflowConfig.cs`
- `src/AgentRunner/ArtifactWriter.cs`
- `src/AgentRunner/RunArtifact.cs`
- `src/AgentRunner/RunAnalytics.cs` (V11: pure `RunAnalytics.Compute(runs)` → `RunAnalyticsResult`;
  the `--analytics` manual command reads `runs/` via `RunArtifactLoader` and renders text or JSON)
- `src/AgentRunner/GuardFailureDemoFactory.cs`
- `src/AgentRunner/ScoringEngine.cs`
- `src/AgentRunner/LoopDetector.cs`
- `src/AgentRunner/QualityGuards.cs`
- `src/AgentRunner/ManualCommandOutput.cs`
- `src/AgentRunner/Mcp/McpServer.cs` (the `--mcp` JSON-RPC/stdio adapter; read-only, key-free
  tools over the same loaders — `docs/mcp.md`). Writes are **opt-in** (`--mcp-allow-write` /
  `AGENTLOOP_MCP_ALLOW_WRITE=1`): the lone write tool `create_test` reuses `DashboardApi.BuildYaml`
  + `TestPlanValidator` and writes `tests/created/<id>.yaml` (`authoring_agent: mcp`); disabled by
  default — then it isn't advertised and returns a tool error. `HandleLine` stays pure/unit-tested.
- `src/AgentRunner/RecordingComposer.cs` (V9.5 recording mode inc.1: `RecordedSession`/`RecordedAction`
  → validated YAML draft via `--compose-recording`; reuses `DashboardApi.BuildYaml` + `TestPlanValidator`)
- `src/AgentRunner/SessionRecorder.cs` (V9.5 inc.2 capture core, pure: `RecordedActionMapper` +
  `SessionRecorder` map `Core.CapturedUiEvent`s → a `RecordedSession`, smoothing event noise). The live
  FlaUI/UIA event source (`UIAutomation/UiaSessionRecorder.cs`) + the `--record` CLI (inc.2b, env-bound)
  feed it; `RecordSession` in `Program` wires the source → `SessionRecorder` → `session.json`.
- `src/AgentRunner/TestFactGuard.cs` (fact-gate: `Verify(before, after, allowedToChange)` reports facts
  a YAML rewrite dropped/changed outside an allow-list). Wired into `--heal-apply`.
- `src/AgentRunner/HealApplier.cs` (V8 inc.2: `--heal-apply --run <id> [--plan] [--yes]` applies a run's
  selector-drift suggestions to the test's `selectors` — pure `Plan` + a surgical YAML rewrite that
  `TestFactGuard` verifies changed only `selectors` before writing; local-only, single-test files).
- `src/AgentRunner.Tests/**`
- `src/Core/AgentGoal.cs`

## Invariants

- Manual modes `--validate-plan`, `--list-tests`, `--render-ui`,
  `--write-guard-demos`, `--mcp`, `--show-prompt`, `--analytics`, and `--compose-recording` must not require
  `.env`, LLM access, FlaUI, or a target app. `--record` is also key-free (no `.env`/LLM) but is the
  one manual mode that DOES need FlaUI + an interactive desktop + the target app (env-bound). `--mcp`,
  `--show-prompt`, `--format json`, `--compose-recording` (without `--out`), and `--record` (without
  `--out`) must keep **stdout** free of diagnostics (the payload — JSON-RPC / JSON / prompt / YAML /
  session JSON — only; logs + policy warnings go to stderr).
- `--analytics [--format json]` (V11) derives run-history insight from `runs/` (loaded via
  `RunArtifactLoader`) through the pure, deterministic `RunAnalytics.Compute`: total runs; per-testId
  pass/fail counts with a **flaky** flag (same id has BOTH a passing — Passed/Succeeded — and a
  non-passing result); **selector-drift** count + groups (steps carrying a `HealingSuggestion`, grouped
  old→new target with a count + max confidence); duration stats (avg/max from `StartedAt`/`EndedAt`,
  runs with no/invalid `EndedAt` excluded) and average step count; and the most-failing tests. Key-free,
  read-only, mode-exclusive. Text summary by default; `--format json` emits the `RunAnalyticsResult`
  (stdout-clean — config diagnostic suppressed, like the other `--format json` modes). Null-safe:
  empty history and partial runs never throw.
- `--compose-recording <session.json> [--out <draft.yaml>]` (V9.5 inc.1) composes a recorded session
  into a validated goal-based YAML draft via `RecordingComposer` — key-free; the goal is synthesised
  from the steps with secret values redacted.
- `--record --window <title> [--out <session.json>] [--seconds N]` (V9.5 inc.2b, **env-bound**) live-
  captures a manual UIA session into the `session.json` that `--compose-recording` consumes. Key-free
  (no `.env`/LLM) but needs a real interactive desktop + the running target app, so it can't be verified
  headless. Attaches via `UiaSessionRecorder`, smooths events through the pure `SessionRecorder`, records
  until `--seconds` (default `RunnerOptions.DefaultRecordSeconds` = 120) elapse or Ctrl+C, then writes the
  `RecordedSession` JSON to `--out` (or stdout when unset — stdout stays clean, diagnostics to stderr).
  Secret values are redacted **at capture** (so a password never lands in `session.json`). `--out` is
  shared with `--compose-recording` and binds to whichever recording mode is active; `--seconds` is
  `--record`-only. `--record` is mode-exclusive with the other manual modes.
- `--show-prompt --test-id <id>` renders the runtime prompt via `PromptPreview` (reuses
  `PromptBuilder`; key-free). `TestPlanValidator` adds non-fatal `Warnings` (unknown framework,
  high `max_steps`, missing `success_condition`) — surfaced by `--validate-plan` (`WARN` on
  stderr) and the MCP `validate_plan`/`show_prompt` tools.
- `TestPlanLoader.DiscoverPlanPaths` excludes `tests/archived/` everywhere (so
  `--list-tests`/`--suite`, the dashboard catalog, and CI all skip archived tests).
  Archiving a test = moving its YAML under `tests/archived/`; it stays in Git, reversible.
- `--format json` must keep stdout parseable JSON.
- `Done` is not success unless the configured success condition is visible or
  the test has no success condition by design. Visibility is tested with
  `UiSnapshot.StatusContains`, which scans **every** status region (not just the first
  label) so multi-region apps work without explicit `Assert`s.
- Unsupported actions and missing targets must fail visibly, not become no-op
  successes. On `action_target_not_found` the loop also records a `SelectorHealer`
  suggestion (`RunStep.HealingSuggestion`, V8) — evidence only, **never auto-applied**.
  `summary.md` surfaces these in a **Selector Healing Suggestions** section (old→new,
  confidence, rationale, + a relative link to the drift step's screenshot) so a human can
  review before adopting. **`--heal-apply`** then applies a confirmed heal: it rewrites the test's
  optional `selectors` field (a surgical YAML edit, `TestFactGuard`-verified to change nothing else),
  local-only + single-test files. (Runtime payoff — a decider that *replays* `selectors` — is the
  remaining piece; today `selectors` is a maintained inventory.)
- The act dispatch lives in `ActionExecutor`, not inline in the loop. New verbs are added
  to `ActionVocabulary.All` (which feeds plan/prompt/target validation) and given a branch
  in `ActionExecutor.ExecuteAsync` — never by hardcoding the verb string in a fourth place.
- Loop detection records real actions, not synthetic pending markers.
- Runtime artifacts stay human-readable and machine-readable.
- At `full` evidence the run also emits `overlay/step_NNN.png` (numbered element boxes) and
  `overlay/step_NNN.json` (box number → element identifiers) — the V3 Tier-2 artifact contract.
  Built on the masked screenshot; index is identifiers-only (secret-safe). `RunStep` carries
  `OverlayPath`/`OverlayIndexPath`; the summary evidence list shows `overlay`.
- `RunArtifact` carries the YAML's `ExistingTests`/`SourceIssue`/`SourcePr` so
  `--to-junit` can emit them (plus `trace_id`) as `<testcase>` `<property>` links.
  `--to-junit` treats both `Passed` and `Succeeded` as passing (not `<error>`).
- Failure steps should expose stable `failureCode` and `failureMessage` values
  in `report.json` and `summary.md` when the runner can name the failure.
- Thread sleeps in async runtime paths should use `Task.Delay`.
- The runtime loop lives in `RunOrchestrator`, not `Program`. Drive it in tests
  with a fake `IAutomationDriver` + a scripted `IActionDecider` (see
  `RunOrchestratorTests`); pass `interStepDelayMs: 0` to keep tests fast. The
  injected driver is owned by the caller (`Program` disposes the FlaUI one).
- Telemetry (OBS-1) is opt-in and manual-first: `RunnerTelemetry.TryStartExport`
  returns null unless `OTEL_EXPORTER_OTLP_ENDPOINT` is set, and `StartActivity`
  is a no-op with no listener — runs stay dependency-free by default. Span/metric
  tags must stay secret-free (use the redacted action value, never raw `Value`).
  On net48 the OTLP exporter must use `HttpProtobuf` (gRPC unsupported).

## Validation

```powershell
dotnet test .\DesktopAiTestAgent.sln --no-restore -v minimal
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --validate-plan --format json
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --list-tests --format json
dotnet run --project .\src\AgentRunner\AgentRunner.csproj -f net8.0-windows -- --analytics --format json
```

## Cross-Domain Notes

- CLI flags that select YAML tests also touch `workflow.md`.
- Artifact shape changes also touch `workbench.md` and docs.
- Provider config or secret logging changes also touch `security.md`.
